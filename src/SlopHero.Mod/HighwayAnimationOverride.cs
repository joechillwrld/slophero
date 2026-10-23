using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Experimental solo-only suspension of the visual highway/background update.
/// The switch is opt-in because the game's update also advances background
/// video and fade presentation along with the MaterialPropertyBlock scroll.
/// </summary>
internal sealed class HighwayAnimationOverride
{
    private static HighwayAnimationOverride? active;
    private MethodInfo? updateMethod;
    private bool installed;
    private bool failed;
    private bool suppress;

    internal bool IsInstalled => installed;
    internal bool IsSuppressed => installed && suppress;

    internal void Update(HarmonyLib.Harmony harmony, bool requested, bool gameplayScene)
    {
        if (!requested || !gameplayScene)
        {
            suppress = false;
            Restore(harmony);
            failed = false;
            return;
        }

        if (!installed && !failed) Install(harmony);
    }

    internal void SetSuppressed(bool value) => suppress = installed && value;

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        suppress = false;
        Restore(harmony);
        failed = false;
    }

    private void Install(HarmonyLib.Harmony harmony)
    {
        try
        {
            updateMethod = typeof(HighwayScroll).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: Type.EmptyTypes, modifiers: null)
                ?? throw new MissingMethodException("HighwayScroll.Update()");

            suppress = false;
            active = this;
            var prefix = typeof(HighwayAnimationOverride).GetMethod(nameof(BeforeHighwayUpdate),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(updateMethod, prefix: new HarmonyMethod(prefix));
            installed = true;
            MelonLogger.Msg("Sloptifine experimental highway-animation hook ready; solo-play suspension is opt-in.");
        }
        catch (Exception ex)
        {
            active = null;
            suppress = false;
            if (updateMethod != null)
            {
                try { harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id); }
                catch { /* The inactive prefix passes through if cleanup fails. */ }
            }
            updateMethod = null;
            installed = false;
            failed = true;
            MelonLogger.Warning($"Sloptifine highway-animation experiment could not install and remains inactive: {ex.Message}");
        }
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        if (!installed && updateMethod == null) return;
        suppress = false;
        if (ReferenceEquals(active, this)) active = null;
        try
        {
            if (updateMethod != null) harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Sloptifine highway-animation hook cleanup failed; the game update is enabled: {ex.Message}");
        }
        updateMethod = null;
        installed = false;
    }

    private static bool BeforeHighwayUpdate() => active?.suppress != true;
}
