using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Skips only CheatManager.Update during an active solo song. This avoids
/// toggling the component's enabled state, which can invoke lifecycle methods
/// and disturb its registered cheat-code list.
/// </summary>
internal sealed class CheatScannerOverride
{
    private static CheatScannerOverride? active;
    private MethodInfo? updateMethod;
    private bool installed;
    private bool failed;
    private bool suppress;

    internal bool IsInstalled => installed;
    internal bool IsSuppressed => installed && suppress;
    internal string Status => failed ? "unavailable" : !installed ? "inactive" : suppress ? "paused" : "ready";

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

    internal void Disable() => suppress = false;

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
            updateMethod = typeof(CheatManager).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: Type.EmptyTypes, modifiers: null)
                ?? throw new MissingMethodException("CheatManager.Update()");

            suppress = false;
            active = this;
            var prefix = typeof(CheatScannerOverride).GetMethod(nameof(BeforeCheatScannerUpdate),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(updateMethod, prefix: new HarmonyMethod(prefix));
            installed = true;
            MelonLogger.Msg("Sloptifine solo cheat-scanner hook ready; it skips only CheatManager.Update during active solo songs.");
        }
        catch (Exception ex)
        {
            active = null;
            suppress = false;
            if (updateMethod != null)
            {
                try { harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id); }
                catch { /* An inactive prefix passes through if cleanup fails. */ }
            }
            updateMethod = null;
            installed = false;
            failed = true;
            MelonLogger.Warning($"Sloptifine cheat-scanner pause could not install and remains inactive: {ex.Message}");
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
            MelonLogger.Warning($"Sloptifine cheat-scanner hook cleanup failed; vanilla scanner remains enabled: {ex.Message}");
        }
        updateMethod = null;
        installed = false;
    }

    private static bool BeforeCheatScannerUpdate() => active?.suppress != true;
}
