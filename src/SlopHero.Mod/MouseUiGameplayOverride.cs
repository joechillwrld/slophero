using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Optional experiment that skips Unity's built-in mouse UI path while a
/// chart is actively playing. This patches the int overload called directly
/// by Process(), leaving touch and keyboard/controller navigation intact.
/// </summary>
internal sealed class MouseUiGameplayOverride
{
    private static MouseUiGameplayOverride? active;
    private MethodInfo? target;
    private bool installed;
    private bool failed;
    private bool suppressMouse;
    private long skippedMouseProcessCalls;

    internal bool IsInstalled => installed;
    internal bool IsSuppressed => installed && suppressMouse;

    internal void LogSummary()
    {
        if (!installed && skippedMouseProcessCalls == 0) return;
        MelonLogger.Msg($"Sloptifine mouse-UI experiment: hookInstalled={installed}, currentlySuppressed={IsSuppressed}, ProcessMouseEvent calls skipped={skippedMouseProcessCalls}; counts include only calls bypassed by the gameplay mouse gate.");
    }

    internal void Update(HarmonyLib.Harmony harmony, bool requested, bool gameplayActive)
    {
        if (!requested)
        {
            Restore(harmony);
            failed = false;
            return;
        }

        if (!installed && !failed) Install(harmony);
        suppressMouse = installed && gameplayActive;
    }

    // Keep the hook installed for the current gameplay scene so pause/resume
    // and opening the mod menu do not re-JIT Harmony in the middle of play.
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
            // Unity's Process() calls ProcessMouseEvent(int) directly. The
            // zero-argument overload is only a forwarding wrapper and is not
            // on the hot path measured in the WPA trace.
            var uiAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "UnityEngine.UI", StringComparison.Ordinal));
            var moduleType = uiAssembly?.GetType("UnityEngine.EventSystems.StandaloneInputModule", throwOnError: false);
            // Il2CppInterop exposes some protected Unity members as public
            // reflection methods, so search both visibility flags.
            target = moduleType?.GetMethod("ProcessMouseEvent",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: new[] { typeof(int) }, modifiers: null);
            if (target == null)
                throw new MissingMethodException("UnityEngine.EventSystems.StandaloneInputModule.ProcessMouseEvent(int)");

            suppressMouse = false;
            active = this;
            var prefix = typeof(MouseUiGameplayOverride).GetMethod(nameof(BeforeMouseProcessing),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            installed = true;
            MelonLogger.Msg("Sloptifine experimental mouse-UI hook ready on StandaloneInputModule.ProcessMouseEvent(int); it skips mouse UI only during active gameplay.");
        }
        catch (Exception ex)
        {
            active = null;
            suppressMouse = false;
            if (target != null)
            {
                try { harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id); }
                catch { /* The inactive prefix allows vanilla mouse input even if cleanup fails. */ }
            }
            target = null;
            installed = false;
            failed = true;
            MelonLogger.Warning($"Sloptifine mouse-UI experiment could not install and remains inactive: {ex.Message}");
        }
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        if (!installed && target == null) return;

        // Make an in-flight or partially retained prefix pass through before
        // removing it; this keeps the game behavior safe if Unpatch fails.
        active = null;
        suppressMouse = false;
        try
        {
            if (target != null) harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Sloptifine mouse-UI hook cleanup failed; mouse input is enabled: {ex.Message}");
        }
        target = null;
        installed = false;
    }

    private static bool BeforeMouseProcessing()
    {
        MouseUiGameplayOverride? instance = active;
        if (instance == null || !instance.suppressMouse) return true;
        instance.skippedMouseProcessCalls++;
        return false;
    }
}
