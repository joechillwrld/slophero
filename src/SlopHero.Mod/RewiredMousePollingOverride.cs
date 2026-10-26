using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Il2CppRewired;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Default-off experiment that skips Rewired's mouse-only snapshot during
/// eligible offline solo songs. Keyboard, joystick, and controller updates
/// remain on their normal paths.
/// </summary>
internal sealed class RewiredMousePollingOverride
{
    private static RewiredMousePollingOverride? active;
    private MethodInfo? updateMethod;
    private bool installed;
    private bool failed;
    private bool suppressMouse;

    internal bool IsInstalled => installed;
    internal bool IsSuppressed => installed && suppressMouse;

    // Patch while Gameplay initializes, before the first song frame. The
    // prefix passes through until the strict offline-solo gate opens.
    internal void Prepare(HarmonyLib.Harmony harmony, bool requested)
    {
        if (requested && !installed && !failed) Install(harmony);
        suppressMouse = false;
    }

    internal void Update(HarmonyLib.Harmony harmony, bool requested, bool eligibleSoloSong)
    {
        if (!requested)
        {
            Restore(harmony);
            failed = false;
            return;
        }

        // Keep the already-prepared prefix installed for this Gameplay scene;
        // it passes through when play is paused, online, or otherwise ineligible.
        if (!eligibleSoloSong)
        {
            suppressMouse = false;
            return;
        }

        // Install only when the user opts in; do not leave a Harmony prefix
        // on Rewired's hot path when the setting is disabled.
        if (eligibleSoloSong && !installed && !failed) Install(harmony);
        suppressMouse = installed && eligibleSoloSong;
    }

    // Opening SlopHero's menu or losing focus must immediately restore mouse
    // polling, even before the next 20 Hz game-state refresh.
    internal void Disable() => suppressMouse = false;

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        suppressMouse = false;
        Restore(harmony);
        failed = false;
    }

    private void Install(HarmonyLib.Harmony harmony)
    {
        try
        {
            long started = Stopwatch.GetTimestamp();
            updateMethod = typeof(ThreadSafeUnityInput.Mouse).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            if (updateMethod == null)
                throw new MissingMethodException("Rewired.ThreadSafeUnityInput.Mouse.Update()");

            suppressMouse = false;
            active = this;
            var prefix = typeof(RewiredMousePollingOverride).GetMethod(nameof(BeforeMousePolling),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(updateMethod, prefix: new HarmonyMethod(prefix));
            installed = true;
            MelonLogger.Msg($"Sloptifine mouse-sampler experiment installed in {(Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency:0.00} ms; only Rewired mouse snapshots pause during eligible offline solo songs.");
        }
        catch (Exception ex)
        {
            active = null;
            suppressMouse = false;
            if (updateMethod != null)
            {
                try { harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id); }
                catch { /* Inactive prefix passes through if cleanup fails. */ }
            }
            updateMethod = null;
            installed = false;
            failed = true;
            MelonLogger.Warning($"Sloptifine mouse-sampler experiment could not install and remains inactive: {ex.Message}");
        }
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        if (!installed && updateMethod == null) return;

        active = null;
        suppressMouse = false;
        try
        {
            if (updateMethod != null) harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Sloptifine mouse-sampler hook cleanup failed; mouse polling is enabled: {ex.Message}");
        }
        updateMethod = null;
        installed = false;
    }

    private static bool BeforeMousePolling() => active?.suppressMouse != true;
}
