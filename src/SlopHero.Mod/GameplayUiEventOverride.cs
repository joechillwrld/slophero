using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Experimental option to skip Unity UI event processing during an active
/// offline solo song. Rewired's gameplay input polling is independent of this
/// EventSystem callback and remains enabled.
/// </summary>
internal sealed class GameplayUiEventOverride
{
    private static GameplayUiEventOverride? active;
    private MethodInfo? updateMethod;
    private bool installed;
    private bool failed;
    private bool suspend;

    internal bool IsInstalled => installed;
    internal bool IsSuspended => installed && suspend;

    internal void Update(HarmonyLib.Harmony harmony, bool requested, bool gameplayActive)
    {
        if (!requested)
        {
            suspend = false;
            Restore(harmony);
            failed = false;
            return;
        }

        if (!installed && !failed) Install(harmony);
        suspend = installed && gameplayActive;
    }

    internal void Disable() => suspend = false;

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        suspend = false;
        Restore(harmony);
        failed = false;
    }

    private void Install(HarmonyLib.Harmony harmony)
    {
        try
        {
            var uiAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "UnityEngine.UI", StringComparison.Ordinal));
            var eventSystemType = uiAssembly?.GetType("UnityEngine.EventSystems.EventSystem", throwOnError: false);
            updateMethod = eventSystemType?.GetMethod("Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            if (updateMethod == null)
                throw new MissingMethodException("UnityEngine.EventSystems.EventSystem.Update()");

            suspend = false;
            active = this;
            var prefix = typeof(GameplayUiEventOverride).GetMethod(nameof(BeforeEventSystemUpdate),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(updateMethod, prefix: new HarmonyMethod(prefix));
            installed = true;
            MelonLogger.Msg("Sloptifine experimental UI-event hook ready; event processing pauses only during active solo gameplay.");
        }
        catch (Exception ex)
        {
            active = null;
            suspend = false;
            if (updateMethod != null)
            {
                try { harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id); }
                catch { /* Prefix stays inactive if cleanup fails. */ }
            }
            updateMethod = null;
            installed = false;
            failed = true;
            MelonLogger.Warning($"Sloptifine UI-event experiment could not install and remains inactive: {ex.Message}");
        }
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        if (!installed && updateMethod == null) return;
        suspend = false;
        try
        {
            if (updateMethod != null) harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id);
        }
        catch (Exception ex) { MelonLogger.Warning($"Sloptifine UI-event hook cleanup failed: {ex.Message}"); }
        installed = false;
        updateMethod = null;
        if (ReferenceEquals(active, this)) active = null;
    }

    private static bool BeforeEventSystemUpdate() => active?.suspend != true;
}
