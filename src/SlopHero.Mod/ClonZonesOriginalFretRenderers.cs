using System.Reflection;
using System.Security.Cryptography;
using MelonLoader;
using MelonLoader.Utils;

namespace SlopHero.Mod;

/// <summary>
/// Opt-in scene-load selection of ClonZones' own original-renderer path.
/// Its config is read at Melon initialization, so a settings-menu choice
/// must set the same exact-build fields before the next guitar Start callback.
/// No renderers or fret sprites are changed by SlopHero itself.
/// </summary>
internal sealed class ClonZonesOriginalFretRenderers
{
    private const string KnownAssemblySha256 = "2C87CFA737AA84CF247283431F4571170338AA85B7180DED9626359BB6E665D1";
    private static readonly string[] FieldNames =
    {
        "_createOverlayRenderers", "_useOverlayRenderers",
        "_useHeadOverlayRenderers", "_useLiftOverlayRenderers"
    };

    private FieldInfo[]? fields;
    private bool[]? configuredValues;
    private bool failed, selectedForScene;

    internal bool IsSelectedForScene => selectedForScene;

    internal void SelectForScene(bool requested, bool gameplay)
    {
        if (!gameplay || !requested)
        {
            Restore();
            return;
        }
        if (failed || !Resolve()) return;
        try
        {
            // Mark before writing so a partial reflection failure restores
            // every field captured from ClonZones' own configuration.
            selectedForScene = true;
            foreach (var field in fields!) field.SetValue(null, false);
            MelonLogger.Msg("Sloptifine selected ClonZones' original fret renderers for this Gameplay scene (experimental).");
        }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            MelonLogger.Warning($"ClonZones original fret-renderer experiment unavailable: {ex.Message}");
        }
    }

    private bool Resolve()
    {
        if (fields != null) return true;
        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                string.Equals(a.GetName().Name, "ClonZones", StringComparison.Ordinal));
            if (assembly == null) return false;
            string path = string.IsNullOrWhiteSpace(assembly.Location)
                ? Path.Combine(MelonEnvironment.ModsDirectory, "ClonZones.dll") : assembly.Location;
            string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if (!string.Equals(actual, KnownAssemblySha256, StringComparison.Ordinal))
                throw new InvalidOperationException("ClonZones.dll differs from the supplied assembly; renderer layout was not verified");
            var type = assembly.GetType("ClonZones.GuitarFretPatch", throwOnError: true)!;
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            var resolved = new FieldInfo[FieldNames.Length];
            var original = new bool[FieldNames.Length];
            for (int i = 0; i < resolved.Length; i++)
            {
                resolved[i] = type.GetField(FieldNames[i], flags)
                    ?? throw new MissingFieldException(type.FullName, FieldNames[i]);
                if (resolved[i].FieldType != typeof(bool))
                    throw new InvalidOperationException($"ClonZones field {FieldNames[i]} changed type");
                original[i] = resolved[i].GetValue(null) is true;
            }
            configuredValues = original;
            fields = resolved;
            return true;
        }
        catch (Exception ex)
        {
            failed = true;
            MelonLogger.Warning($"ClonZones original fret-renderer experiment unavailable: {ex.Message}");
            return false;
        }
    }

    internal void Restore()
    {
        if (!selectedForScene || fields == null || configuredValues == null) return;
        selectedForScene = false;
        try
        {
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, configuredValues[i]);
        }
        catch (Exception ex)
        {
            failed = true;
            MelonLogger.Warning($"ClonZones fret-renderer setting restore was incomplete: {ex.Message}");
        }
    }
}
