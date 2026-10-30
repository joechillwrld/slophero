using Il2CppTMPro;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>One cached, process-lifetime font for SlopHero's small counter HUD.</summary>
internal static class CounterFont
{
    private static bool attempted;
    private static Font? unityFont;
    private static TMP_FontAsset? textMeshProFont;
    private static string requestedFamily = "Segoe UI";
    private static readonly Dictionary<string, (Font Font, TMP_FontAsset? Tmp)> cache =
        new(StringComparer.OrdinalIgnoreCase);
    private static string[]? installedFamilies;

    internal static string? NextInstalledFamily(string current)
    {
        if (installedFamilies == null)
        {
            try
            {
                installedFamilies = Font.GetOSInstalledFontNames()
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex)
            {
                installedFamilies = Array.Empty<string>();
                MelonLogger.Warning($"Installed font enumeration unavailable: {ex.Message}");
            }
        }
        if (installedFamilies.Length == 0) return null;
        int index = Array.FindIndex(installedFamilies,
            name => string.Equals(name, current, StringComparison.OrdinalIgnoreCase));
        return installedFamilies[(index + 1) % installedFamilies.Length];
    }

    internal static void Configure(string? family)
    {
        string next = string.IsNullOrWhiteSpace(family) ? "Segoe UI" : family.Trim();
        if (string.Equals(next, requestedFamily, StringComparison.OrdinalIgnoreCase)) return;
        requestedFamily = next;
        attempted = false;
    }

    internal static Font? UnityFont
    {
        get { Prepare(); return unityFont; }
    }

    internal static TMP_FontAsset? TextMeshProFont
    {
        get { Prepare(); return textMeshProFont; }
    }

    private static void Prepare()
    {
        if (attempted) return;
        attempted = true;
        if (cache.TryGetValue(requestedFamily, out var cached))
        {
            unityFont = cached.Font;
            textMeshProFont = cached.Tmp;
            return;
        }
        try
        {
            unityFont = Font.CreateDynamicFontFromOSFont(requestedFamily, 48);
            if (unityFont == null) unityFont = Font.CreateDynamicFontFromOSFont("Arial", 48);
            if (unityFont == null) return;

            unityFont.name = "SlopHero Counter Font";
            unityFont.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                textMeshProFont = TMP_FontAsset.CreateFontAsset(unityFont);
                if (textMeshProFont != null)
                {
                    textMeshProFont.name = "SlopHero Counter Font SDF";
                    textMeshProFont.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            catch { /* The IMGUI counters can still use the OS font directly. */ }
            cache[requestedFamily] = (unityFont, textMeshProFont);
        }
        catch (Exception ex)
        {
            unityFont = null;
            textMeshProFont = null;
            MelonLogger.Warning($"Counter font unavailable; keeping Clone Hero's default font: {ex.Message}");
        }
    }
}
