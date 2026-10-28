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
        try
        {
            // Segoe UI gives the compact counters a cleaner, more legible face
            // on the supported Windows game build. Arial remains a fallback.
            unityFont = Font.CreateDynamicFontFromOSFont("Segoe UI", 48);
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
        }
        catch (Exception ex)
        {
            unityFont = null;
            textMeshProFont = null;
            MelonLogger.Warning($"Counter font unavailable; keeping Clone Hero's default font: {ex.Message}");
        }
    }
}
