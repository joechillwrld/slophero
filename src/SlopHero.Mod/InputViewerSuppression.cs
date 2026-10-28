using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>Temporarily removes the passive Input Viewer branch from HUD rendering.</summary>
internal sealed class InputViewerSuppression
{
    private GameObject? root;
    private bool originalActive;
    private bool captured, failed;
    private double nextSearch;

    internal bool IsSuppressed => captured && originalActive && root != null && !root.activeSelf;

    internal void Update(bool requested, bool eligible, double now)
    {
        if (!requested)
        {
            Restore();
            failed = false;
            nextSearch = 0;
            return;
        }
        if (!eligible)
        {
            Restore();
            return;
        }
        if (failed || now < nextSearch) return;

        try
        {
            if (!captured || root == null)
            {
                nextSearch = now + 1.0;
                var hud = GameObject.Find("HUDCam");
                var viewer = hud == null ? null : hud.transform.Find("Input Viewer");
                if (viewer == null) return;
                root = viewer.gameObject;
                originalActive = root.activeSelf;
                captured = true;
            }

            if (originalActive && root != null && root.activeSelf)
            {
                root.SetActive(false);
                MelonLogger.Msg("Sloptifine hid the passive guitar input viewer during an active song.");
            }
        }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            MelonLogger.Warning($"Input-viewer suppression stopped; the original HUD branch was restored: {ex.Message}");
        }
    }

    internal void Restore()
    {
        bool wasSuppressed = IsSuppressed;
        try
        {
            if (captured && root != null && root.activeSelf != originalActive)
                root.SetActive(originalActive);
        }
        catch { }
        if (wasSuppressed)
            MelonLogger.Msg("Sloptifine restored the guitar input viewer.");
        root = null;
        captured = false;
    }

    internal void Clear()
    {
        Restore();
        failed = false;
        nextSearch = 0;
    }
}
