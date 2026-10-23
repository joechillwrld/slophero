using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Optional experiment that skips Unity's built-in mouse UI path while a
/// chart is actively playing. This deliberately leaves Process() itself
/// running so its touch and keyboard/controller navigation paths still work.
/// </summary>
internal sealed class MouseUiGameplayOverride
{
    private static MouseUiGameplayOverride? active;
    private MethodInfo? target;
    private bool installed;
    private bool failed;

    internal bool IsInstalled => installed;

    internal void Update(HarmonyLib.Harmony harmony, bool requested, bool gameplayActive)
    {
        if (!requested || !gameplayActive)
        {
            Restore(harmony);
            if (!requested) failed = false;
            return;
        }

        if (installed || failed) return;
        Install(harmony);
    }

    internal void Disable(HarmonyLib.Harmony harmony) => Restore(harmony);

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        Restore(harmony);
        failed = false;
    }

    private void Install(HarmonyLib.Harmony harmony)
    {
        try
        {
            // Resolve the zero-argument wrapper: Process() calls this before
            // continuing into the separate keyboard/controller navigation.
            var uiAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "UnityEngine.UI", StringComparison.Ordinal));
            var moduleType = uiAssembly?.GetType("UnityEngine.EventSystems.StandaloneInputModule", throwOnError: false);
            // Il2CppInterop exposes some protected Unity members as public
            // reflection methods, so search both visibility flags.
            target = moduleType?.GetMethod("ProcessMouseEvent",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            if (target == null)
                throw new MissingMethodException("UnityEngine.EventSystems.StandaloneInputModule.ProcessMouseEvent()");

            active = this;
            var prefix = typeof(MouseUiGameplayOverride).GetMethod(nameof(BeforeMouseProcessing),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            installed = true;
            MelonLogger.Msg("Sloptifine experimental gameplay mouse-UI skip enabled; mouse hover/clicks return when paused.");
        }
        catch (Exception ex)
        {
            active = null;
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

    private static bool BeforeMouseProcessing() => active == null;
}
