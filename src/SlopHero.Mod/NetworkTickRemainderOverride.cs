using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using SlopHero.Core;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Experimental opt-in patch that preserves the fractional time left after a
/// connected CHNetManager tick. It leaves the native update and tick rate
/// intact, and discards whole missed intervals to avoid catch-up bursts.
/// </summary>
internal sealed class NetworkTickRemainderOverride
{
    // Current-build CHNetManager object layout from Il2Cpp dump TypeDef 672:
    // elapsedTime 0x30, timePerTick 0x34, ConnectionState 0x38.
    private const int ElapsedTimeOffset = 0x30;
    private const int TimePerTickOffset = 0x34;
    private const int ConnectionStateOffset = 0x38;
    private const int ConnectedState = 2;

    private static NetworkTickRemainderOverride? active;
    private MethodInfo? updateMethod;
    private bool installed;
    private bool failed;
    private bool requested;
    private bool writeFailureLogged;

    internal bool IsInstalled => installed;
    internal bool Failed => failed;

    internal void Update(HarmonyLib.Harmony harmony, bool requested)
    {
        this.requested = requested;
        if (!requested)
        {
            Restore(harmony);
            failed = false;
            writeFailureLogged = false;
            return;
        }

        if (!installed && !failed) Install(harmony);
    }

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        requested = false;
        Restore(harmony);
        failed = false;
        writeFailureLogged = false;
    }

    private void Install(HarmonyLib.Harmony harmony)
    {
        try
        {
            updateMethod = typeof(CHNetManager).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (updateMethod == null)
                throw new MissingMethodException("CHNetManager.Update()");

            active = this;
            var prefix = typeof(NetworkTickRemainderOverride).GetMethod(nameof(BeforeUpdate),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            var postfix = typeof(NetworkTickRemainderOverride).GetMethod(nameof(AfterUpdate),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(updateMethod, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
            installed = true;
            MelonLogger.Msg("Sloptifine experimental network tick remainder hook ready; it is active only for established connections while its default-off setting is enabled.");
        }
        catch (Exception ex)
        {
            active = null;
            if (updateMethod != null)
            {
                try { harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id); }
                catch { /* With no active instance, a retained hook passes through. */ }
            }
            updateMethod = null;
            installed = false;
            failed = true;
            MelonLogger.Warning($"Sloptifine network tick remainder experiment could not install and remains inactive: {ex.Message}");
        }
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        if (!installed && updateMethod == null) return;

        // Disable behavior before unpatching so a retained prefix remains a
        // vanilla pass-through if Harmony cleanup fails.
        active = null;
        try
        {
            if (updateMethod != null) harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Sloptifine network tick remainder hook cleanup failed; network updates remain pass-through: {ex.Message}");
        }
        updateMethod = null;
        installed = false;
    }

    private static void BeforeUpdate(CHNetManager __instance, out TickState __state)
    {
        __state = default;
        var instance = active;
        if (instance == null || !instance.requested || __instance is null) return;

        try
        {
            IntPtr pointer = __instance.Pointer;
            if (pointer == IntPtr.Zero || Marshal.ReadInt32(pointer, ConnectionStateOffset) != ConnectedState)
                return;

            float elapsed = ReadSingle(pointer, ElapsedTimeOffset);
            float interval = ReadSingle(pointer, TimePerTickOffset);
            float total = elapsed + Time.deltaTime;
            if (!float.IsFinite(elapsed) || elapsed < 0f || !float.IsFinite(interval)
                || interval <= 0f || !float.IsFinite(total) || total < interval)
                return;

            __state = new TickState(pointer, total, interval);
        }
        catch
        {
            // Never interfere with the native network update if the exact-build
            // field read fails.
            __state = default;
        }
    }

    private static void AfterUpdate(TickState __state)
    {
        if (!__state.Valid) return;
        var instance = active;
        if (instance == null || !instance.requested) return;

        try
        {
            if (Marshal.ReadInt32(__state.Pointer, ConnectionStateOffset) != ConnectedState)
                return;

            // Only adjust the accumulator if vanilla reached its threshold and
            // performed its normal reset. If the target behavior differs, fail
            // open instead of guessing at its state.
            if (ReadSingle(__state.Pointer, ElapsedTimeOffset) != 0f) return;
            float remainder = NetworkTickRemainder.PreserveFraction(__state.Accumulated, __state.Interval);
            Marshal.WriteInt32(__state.Pointer, ElapsedTimeOffset, BitConverter.SingleToInt32Bits(remainder));
        }
        catch (Exception ex)
        {
            if (!instance.writeFailureLogged)
            {
                instance.writeFailureLogged = true;
                MelonLogger.Warning($"Sloptifine network tick remainder write skipped; vanilla timing is preserved: {ex.Message}");
            }
        }
    }

    private static float ReadSingle(IntPtr pointer, int offset)
        => BitConverter.Int32BitsToSingle(Marshal.ReadInt32(pointer, offset));

    private readonly struct TickState
    {
        internal readonly IntPtr Pointer;
        internal readonly float Accumulated;
        internal readonly float Interval;
        internal bool Valid => Pointer != IntPtr.Zero;

        internal TickState(IntPtr pointer, float accumulated, float interval)
        {
            Pointer = pointer;
            Accumulated = accumulated;
            Interval = interval;
        }
    }
}
