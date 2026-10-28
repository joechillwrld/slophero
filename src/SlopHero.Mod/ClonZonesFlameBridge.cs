using System.Collections;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// ClonZones 2.0 draws its own stacked flame renderers and disables the game's
/// five vanilla flames. Bind to that live pool only while rainbow flames are on.
/// All private-field access is checked before changing the sprite bank.
/// </summary>
internal sealed class ClonZonesFlameBridge
{
    private static ClonZonesFlameBridge? active;
    private FieldInfo? normalFrames, starpowerFrames, stackedPool;
    private FieldInfo? rendererField, activeField, starpowerField, framesField, lastFrameField, templateField;
    private MethodInfo? prepareMethod;
    private HarmonyLib.Harmony? harmony;
    private Sprite[]? originalStarpower;
    private Sprite[]? neutralFrames;
    private Color[]? palette;
    private Dictionary<IntPtr, int>? phases;
    private bool checkedAssembly, failed, installed;

    internal bool IsActive => installed;

    internal bool TryEnable(HarmonyLib.Harmony owner)
    {
        if (installed) return true;
        if (failed) return false;
        if (!checkedAssembly)
        {
            checkedAssembly = true;
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                    string.Equals(a.GetName().Name, "ClonZones", StringComparison.Ordinal));
                if (assembly == null) return false;
                const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
                const BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var bank = assembly.GetType("ClonZones.FlameSpriteBank", throwOnError: true)!;
                var patch = assembly.GetType("ClonZones.GuitarFlamePatch", throwOnError: true)!;
                var stacked = patch.GetNestedType("StackedFlame", BindingFlags.NonPublic)
                    ?? throw new MissingMemberException("ClonZones.StackedFlame");
                normalFrames = bank.GetField("_normal", statics);
                starpowerFrames = bank.GetField("_starpower", statics);
                stackedPool = patch.GetField("_stackedFlames", statics);
                rendererField = stacked.GetField("Renderer", members);
                activeField = stacked.GetField("Active", members);
                starpowerField = stacked.GetField("Starpower", members);
                framesField = stacked.GetField("Frames", members);
                lastFrameField = stacked.GetField("LastFrame", members);
                templateField = stacked.GetField("TemplatePtr", members);
                prepareMethod = patch.GetMethod("PrepareVanillaFlame", statics);
                if (normalFrames == null || starpowerFrames == null || stackedPool == null
                    || rendererField == null || activeField == null || starpowerField == null
                    || framesField == null || lastFrameField == null || templateField == null
                    || prepareMethod == null)
                    throw new MissingMemberException("ClonZones flame pool layout changed");
            }
            catch (Exception ex)
            {
                failed = true;
                MelonLogger.Warning($"ClonZones rainbow flame bridge unavailable: {ex.Message}");
                return false;
            }
        }
        if (prepareMethod == null) return false;
        try
        {
            var normal = normalFrames!.GetValue(null) as Sprite[];
            var star = starpowerFrames!.GetValue(null) as Sprite[];
            if (normal == null || star == null || normal.Length == 0 || normal.Length != star.Length)
                throw new InvalidOperationException("ClonZones flame banks are not ready or have different frame counts");
            neutralFrames = normal;
            originalStarpower = star;
            harmony = owner;
            active = this;
            owner.Patch(prepareMethod, postfix: new HarmonyMethod(typeof(ClonZonesFlameBridge)
                .GetMethod(nameof(AfterPrepare), BindingFlags.Static | BindingFlags.NonPublic)!));
            // The supplied Star Power sheet is blue, so RGB tint cannot make
            // red/green flames from it. Both sheets have the same 16 shapes.
            starpowerFrames.SetValue(null, neutralFrames);
            installed = true;
            MelonLogger.Msg("Rainbow flames now tint ClonZones' active stacked renderers and use neutral Star Power frames.");
            return true;
        }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            MelonLogger.Warning($"ClonZones rainbow flame bridge stopped: {ex.Message}");
            return false;
        }
    }

    internal void Tint(Color[] colors, Dictionary<IntPtr, int> lanePhases)
    {
        if (!installed) return;
        palette = colors;
        phases = lanePhases;
        TintActive();
    }

    private static void AfterPrepare()
    {
        try { active?.TintActive(); }
        catch (Exception ex)
        {
            MelonLogger.Warning($"ClonZones immediate flame tint stopped: {ex.Message}");
            active?.Restore();
        }
    }

    private void TintActive()
    {
        if (!installed || palette == null || stackedPool?.GetValue(null) is not IList pool) return;
        foreach (var entry in pool)
        {
            if (entry == null || activeField!.GetValue(entry) is not true) continue;
            if (rendererField!.GetValue(entry) is not SpriteRenderer renderer || renderer == null) continue;
            if (starpowerField!.GetValue(entry) is true && neutralFrames != null
                && !ReferenceEquals(framesField!.GetValue(entry), neutralFrames))
            {
                framesField.SetValue(entry, neutralFrames);
                if (lastFrameField!.GetValue(entry) is int frame && frame >= 0 && frame < neutralFrames.Length)
                    renderer.sprite = neutralFrames[frame];
            }
            int phase = 0;
            if (templateField!.GetValue(entry) is IntPtr template && phases != null
                && phases.TryGetValue(template, out int lane)) phase = lane;
            SetRgb(renderer, palette[phase % palette.Length]);
        }
    }

    internal void Restore()
    {
        // Turn the postfix into a pass-through before touching pooled objects.
        if (ReferenceEquals(active, this)) active = null;
        try
        {
            if (originalStarpower != null && starpowerFrames != null
                && ReferenceEquals(starpowerFrames.GetValue(null), neutralFrames))
                starpowerFrames.SetValue(null, originalStarpower);
            if (stackedPool?.GetValue(null) is IList pool)
                foreach (var entry in pool)
                {
                    if (entry == null) continue;
                    if (rendererField?.GetValue(entry) is not SpriteRenderer renderer || renderer == null) continue;
                    if (starpowerField?.GetValue(entry) is true && originalStarpower != null
                        && ReferenceEquals(framesField?.GetValue(entry), neutralFrames))
                    {
                        framesField!.SetValue(entry, originalStarpower);
                        if (activeField?.GetValue(entry) is true
                            && lastFrameField?.GetValue(entry) is int frame
                            && frame >= 0 && frame < originalStarpower.Length)
                            renderer.sprite = originalStarpower[frame];
                    }
                    SetRgb(renderer, Color.white);
                }
        }
        catch (Exception ex) { MelonLogger.Warning($"ClonZones flame restore was incomplete: {ex.Message}"); }
        if (prepareMethod != null && harmony != null)
            try { harmony.Unpatch(prepareMethod, HarmonyPatchType.All, harmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"ClonZones flame hook cleanup failed: {ex.Message}"); }
        installed = false;
        palette = null;
        phases = null;
        neutralFrames = originalStarpower = null;
        harmony = null;
    }

    private static void SetRgb(SpriteRenderer renderer, Color color)
    {
        var current = renderer.color;
        if (current.r == color.r && current.g == color.g && current.b == color.b) return;
        color.a = current.a;
        renderer.color = color;
    }
}
