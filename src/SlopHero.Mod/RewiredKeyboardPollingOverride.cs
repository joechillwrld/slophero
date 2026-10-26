using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Il2CppRewired;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Default-off experiment that pauses only Rewired's keyboard snapshot during
/// eligible offline solo songs. Joystick/controller polling is not patched.
/// Keyboard-bound game controls stop updating while the option is active.
/// </summary>
internal sealed class RewiredKeyboardPollingOverride
{
    private static RewiredKeyboardPollingOverride? active;
    private MethodInfo? updateMethod;
    private bool installed;
    private bool failed;
    private bool suppressKeyboard;

    internal bool IsInstalled => installed;
    internal bool IsSuppressed => installed && suppressKeyboard;

    internal void Prepare(HarmonyLib.Harmony harmony, bool requested)
    {
        if (requested && !installed && !failed) Install(harmony);
        suppressKeyboard = false;
    }

    internal void Update(HarmonyLib.Harmony harmony, bool requested, bool eligibleSoloSong)
    {
        if (!requested)
        {
            Restore(harmony);
            failed = false;
            return;
        }

        // Install while the opted-in setting is being edited or while the
        // scene is paused, so Harmony setup never lands on the first active
        // gameplay update. The prefix remains pass-through until eligible.
        if (!installed && !failed) Install(harmony);
        suppressKeyboard = installed && eligibleSoloSong;
    }

    internal void Disable() => suppressKeyboard = false;

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        suppressKeyboard = false;
        Restore(harmony);
        failed = false;
    }

    private void Install(HarmonyLib.Harmony harmony)
    {
        try
        {
            long started = Stopwatch.GetTimestamp();
            updateMethod = typeof(ThreadSafeUnityInput.Keyboard).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            if (updateMethod == null)
                throw new MissingMethodException("Rewired.ThreadSafeUnityInput.Keyboard.Update()");

            suppressKeyboard = false;
            active = this;
            var prefix = typeof(RewiredKeyboardPollingOverride).GetMethod(nameof(BeforeKeyboardPolling),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(updateMethod, prefix: new HarmonyMethod(prefix));
            installed = true;
            MelonLogger.Msg($"Sloptifine keyboard-sampler experiment installed in {(Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency:0.00} ms; only Rewired keyboard snapshots pause during eligible offline solo songs.");
        }
        catch (Exception ex)
        {
            active = null;
            suppressKeyboard = false;
            if (updateMethod != null)
            {
                try { harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id); }
                catch { /* Inactive prefix passes through if cleanup fails. */ }
            }
            updateMethod = null;
            installed = false;
            failed = true;
            MelonLogger.Warning($"Sloptifine keyboard-sampler experiment could not install and remains inactive: {ex.Message}");
        }
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        if (!installed && updateMethod == null) return;

        active = null;
        suppressKeyboard = false;
        try
        {
            if (updateMethod != null) harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Sloptifine keyboard-sampler hook cleanup failed; keyboard polling is enabled: {ex.Message}");
        }
        updateMethod = null;
        installed = false;
    }

    private static bool BeforeKeyboardPolling() => active?.suppressKeyboard != true;
}
