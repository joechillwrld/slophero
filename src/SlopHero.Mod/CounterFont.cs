using Il2CppTMPro;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>Fonts already loaded by the game can be reused without stripped OS-font APIs.</summary>
internal static class CounterFont
{
    private static readonly Dictionary<string, TMP_FontAsset> available =
        new(StringComparer.OrdinalIgnoreCase);
    private static string requestedFamily = "Default";
    private static bool warned;
    private static float nextScanTime;

    internal static void Configure(string? family)
    {
        requestedFamily = string.IsNullOrWhiteSpace(family) ? "Default" : family.Trim();
        // Keep the font the selector just found. A paused game's labels may
        // disappear from FindObjectsOfType until the song is restarted.
        nextScanTime = 0;
        warned = false;
    }

    internal static string? NextInstalledFamily(string current)
    {
        RefreshAvailable(force: true);
        var names = available.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        if (names.Count == 0) return null;
        names.Insert(0, "Default");
        int index = names.FindIndex(name => string.Equals(name, current, StringComparison.OrdinalIgnoreCase));
        return names[(index + 1) % names.Count];
    }

    internal static Font? UnityFont => TextMeshProFont?.sourceFontFile;

    internal static TMP_FontAsset? TextMeshProFont
    {
        get
        {
            if (string.Equals(requestedFamily, "Default", StringComparison.OrdinalIgnoreCase)) return null;
            if (available.TryGetValue(requestedFamily, out var font) && font != null) return font;
            RefreshAvailable(force: true);
            if (available.TryGetValue(requestedFamily, out font) && font != null) return font;
            if (!warned)
            {
                warned = true;
                MelonLogger.Warning($"Counter font '{requestedFamily}' is not loaded in this scene; using the game's default font.");
            }
            return null;
        }
    }

    private static void RefreshAvailable(bool force = false)
    {
        float now = Time.realtimeSinceStartup;
        if (!force && now < nextScanTime) return;
        nextScanTime = now + 2f;
        try
        {
            available.Clear();
            foreach (var text in UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>())
            {
                if (text == null || text.font == null || string.IsNullOrWhiteSpace(text.font.name)) continue;
                available[text.font.name] = text.font;
            }
            foreach (var text in UnityEngine.Object.FindObjectsOfType<TextMeshPro>())
            {
                if (text == null || text.font == null || string.IsNullOrWhiteSpace(text.font.name)) continue;
                available[text.font.name] = text.font;
            }
        }
        catch (Exception ex)
        {
            if (!warned) { warned = true; MelonLogger.Warning($"Game font discovery unavailable: {ex.Message}"); }
        }
    }
}
