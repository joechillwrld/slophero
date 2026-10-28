using System.Reflection;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Makes the existing opt-in fret-effects switch cover ClonZones' replacement
/// flame system, which renders independently of Clone Hero's flame animators.
/// ClonZones owns scene setup/teardown; only resume it inside Gameplay.
/// </summary>
internal sealed class ClonZonesFlameSuppressor
{
    private MethodInfo? setActive;
    private FieldInfo? activeField;
    private bool checkedAssembly, failed, suppressed, wasActive;

    internal bool IsSuppressed => suppressed;

    internal void Update(bool suppress, bool inGameplay)
    {
        if (suppress && !inGameplay) suppress = false;
        if (!suppress)
        {
            Restore(inGameplay);
            return;
        }
        if (suppressed || failed) return;
        if (!checkedAssembly)
        {
            checkedAssembly = true;
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                    string.Equals(a.GetName().Name, "ClonZones", StringComparison.Ordinal));
                if (assembly == null) return;
                var patch = assembly.GetType("ClonZones.GuitarFlamePatch", throwOnError: true)!;
                setActive = patch.GetMethod("SetActive", BindingFlags.Public | BindingFlags.Static,
                    binder: null, types: new[] { typeof(bool) }, modifiers: null);
                activeField = patch.GetField("_active", BindingFlags.NonPublic | BindingFlags.Static);
                if (setActive == null || activeField?.FieldType != typeof(bool))
                    throw new MissingMemberException("ClonZones flame activation API changed");
            }
            catch (Exception ex)
            {
                failed = true;
                MelonLogger.Warning($"ClonZones flame suppression unavailable: {ex.Message}");
                return;
            }
        }
        if (setActive == null || activeField == null) return;
        try
        {
            wasActive = activeField.GetValue(null) is true;
            if (!wasActive) return;
            setActive.Invoke(null, new object[] { false });
            suppressed = true;
            MelonLogger.Msg("Sloptifine suspended ClonZones replacement flames with fret effects.");
        }
        catch (Exception ex)
        {
            failed = true;
            MelonLogger.Warning($"ClonZones flame suppression stopped: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private void Restore(bool inGameplay)
    {
        if (!suppressed) return;
        suppressed = false;
        try
        {
            if (inGameplay && wasActive) setActive?.Invoke(null, new object[] { true });
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"ClonZones flame restore was incomplete: {ex.InnerException?.Message ?? ex.Message}");
        }
        wasActive = false;
    }

    internal void Clear()
    {
        // Scene callbacks belong to ClonZones; re-enabling it here could make
        // its Gameplay-only renderer active after entering the main menu.
        suppressed = wasActive = false;
    }
}
