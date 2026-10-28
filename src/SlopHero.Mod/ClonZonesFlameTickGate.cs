using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Default-off ClonZones 2.0 experiment. Its 60/80 Hz flame sprite animations
/// are polled at every uncapped simulation Update. While flames are active,
/// allow at most 240 Tick calls per second; the native game and input clocks
/// are untouched. Empty-pool calls follow the original fast return.
/// </summary>
internal sealed class ClonZonesFlameTickGate
{
    private static ClonZonesFlameTickGate? current;
    private MethodInfo? tickMethod;
    private MethodInfo? deactivateStacked;
    private FieldInfo? stackedPool, stackedActive, stackedLastFrame;
    private HarmonyLib.Harmony? harmony;
    private Func<int>? activeCount;
    private bool installed, enabled, failed, trimBlankFrames;
    private float nextTick, lastTick;
    private long skipped;

    internal bool IsInstalled => installed;
    internal long Skipped => skipped;

    internal void Update(HarmonyLib.Harmony owner, bool requested, bool activeSong)
    {
        if (!requested) { Clear(); return; }
        if (failed) return;
        if (!installed) Prepare(owner);
        if (!installed) return;
        if (enabled && !activeSong && skipped != 0)
        {
            MelonLogger.Msg($"ClonZones flame tick gate skipped {skipped:N0} duplicate polls this song.");
            skipped = 0;
        }
        if (!enabled && activeSong) skipped = 0;
        enabled = activeSong;
        if (!enabled) nextTick = lastTick = 0;
    }

    private void Prepare(HarmonyLib.Harmony owner)
    {
        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                string.Equals(a.GetName().Name, "ClonZones", StringComparison.Ordinal));
            if (assembly == null) { failed = true; return; }
            var type = assembly.GetType("ClonZones.GuitarFlamePatch", throwOnError: true)!;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            tickMethod = type.GetMethod("Tick", flags)
                ?? throw new MissingMethodException("ClonZones.GuitarFlamePatch.Tick");
            var stacked = type.GetField("_activeStackedCount", flags)
                ?? throw new MissingFieldException("ClonZones._activeStackedCount");
            var pooled = type.GetField("_activePoolCount", flags)
                ?? throw new MissingFieldException("ClonZones._activePoolCount");
            if (stacked.FieldType != typeof(int) || pooled.FieldType != typeof(int))
                throw new InvalidOperationException("ClonZones flame counters changed type");
            // Compile field reads once. Reflection on each ~3 kHz Tick would
            // cost more than the small loop this switch is meant to avoid.
            activeCount = Expression.Lambda<Func<int>>(Expression.Add(
                Expression.Field(null, stacked), Expression.Field(null, pooled))).Compile();
            var stackedType = type.GetNestedType("StackedFlame", BindingFlags.NonPublic)
                ?? throw new MissingMemberException("ClonZones.StackedFlame");
            stackedPool = type.GetField("_stackedFlames", flags);
            stackedActive = stackedType.GetField("Active", BindingFlags.Instance | BindingFlags.Public);
            stackedLastFrame = stackedType.GetField("LastFrame", BindingFlags.Instance | BindingFlags.Public);
            deactivateStacked = type.GetMethod("Deactivate", BindingFlags.Static | BindingFlags.NonPublic,
                binder: null, types: new[] { stackedType }, modifiers: null);
            trimBlankFrames = stackedPool != null && stackedActive != null && stackedLastFrame != null
                && deactivateStacked != null && HasKnownBlankTail(assembly);
            harmony = owner;
            current = this;
            owner.Patch(tickMethod, prefix: new HarmonyMethod(typeof(ClonZonesFlameTickGate)
                .GetMethod(nameof(BeforeTick), BindingFlags.Static | BindingFlags.NonPublic)!));
            installed = true;
            MelonLogger.Msg($"ClonZones flame-tick experiment prepared; 240 Hz cap ready, empty-frame trim={trimBlankFrames}.");
        }
        catch (Exception ex)
        {
            Clear();
            failed = true;
            MelonLogger.Warning($"ClonZones flame-tick experiment unavailable: {ex.Message}");
        }
    }

    private static bool BeforeTick()
    {
        var gate = current;
        if (gate == null || !gate.enabled || gate.activeCount == null || gate.activeCount() <= 0)
            return true;
        float now = Time.time;
        if (now >= gate.lastTick && now < gate.nextTick)
        {
            gate.skipped++;
            return false;
        }
        gate.lastTick = now;
        gate.nextTick = now + 1f / 240f;
        if (gate.trimBlankFrames)
            try { gate.DeactivateInvisibleFlames(); }
            catch (Exception ex)
            {
                gate.trimBlankFrames = false;
                MelonLogger.Warning($"ClonZones blank-flame trim stopped; original flame lifetime remains: {ex.InnerException?.Message ?? ex.Message}");
            }
        return true;
    }

    private void DeactivateInvisibleFlames()
    {
        if (stackedPool?.GetValue(null) is not IList pool) return;
        foreach (var entry in pool)
        {
            if (entry == null || stackedActive?.GetValue(entry) is not true) continue;
            if (stackedLastFrame?.GetValue(entry) is int frame && frame >= 8)
                deactivateStacked?.Invoke(null, new[] { entry });
        }
    }

    private static bool HasKnownBlankTail(Assembly assembly)
    {
        try
        {
            var directory = Path.GetDirectoryName(assembly.Location);
            if (directory == null) return false;
            var root = Path.Combine(directory, "ClonZones");
            var selected = Path.Combine(root, "Assets");
            var current = Path.Combine(root, "current.txt");
            if (File.Exists(current))
            {
                string? folder = File.ReadLines(current).Select(x => x.Trim()).FirstOrDefault(x =>
                    x.Length != 0 && !x.StartsWith(';') && !x.StartsWith('#'));
                if (folder != null && Directory.Exists(Path.Combine(root, folder)))
                    selected = Path.Combine(root, folder);
            }
            // Match ClonZones.Core.LocateAssetRoot's two legacy fallbacks.
            if (!Directory.Exists(selected))
            {
                var legacy = Path.Combine(directory, "GH3Zones", "Assets");
                selected = Directory.Exists(legacy) ? legacy : Path.Combine(directory, "Assets");
            }
            string normal = Path.Combine(selected, "FX", "note_hit.png");
            string star = Path.Combine(selected, "FX", "note_hit_blue.png");
            if (!File.Exists(normal) || !File.Exists(star)) return false;
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(normal))) ==
                    "EB3DA0EBEC67BEE57DD271ED03AB1A06F71E4DA8A7BBC3AE0245E513A4D278AA"
                && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(star))) ==
                    "9D885E74E08F52805E15F607CA91E0EF70A815B3CD74D9D8DEB402578B3826C9";
        }
        catch { return false; }
    }

    internal void Clear()
    {
        enabled = false;
        if (skipped != 0)
            MelonLogger.Msg($"ClonZones flame tick gate skipped {skipped:N0} duplicate polls this song.");
        skipped = 0;
        if (ReferenceEquals(current, this)) current = null;
        if (tickMethod != null && harmony != null)
            try { harmony.Unpatch(tickMethod, HarmonyPatchType.All, harmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"ClonZones flame-tick cleanup failed: {ex.Message}"); }
        installed = false;
        harmony = null;
        activeCount = null;
        trimBlankFrames = false;
        nextTick = lastTick = 0;
    }
}
