using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Offline solo upper-bound test for the persistent HUD camera render pass.
/// It intentionally hides camera-rendered HUD content and restores the exact
/// camera state when the test scope ends.
/// </summary>
internal sealed class HudCameraBenchmarkOverride
{
    private Camera? camera;
    private bool originalEnabled;
    private bool active, failed;
    private string? failure;

    internal bool IsActive => active;

    internal string? ConsumeFailure()
    {
        string? result = failure;
        failure = null;
        return result;
    }

    internal void Update(bool requested, bool eligible, GameManager? game)
    {
        if (!requested || !eligible || game == null)
        {
            Restore();
            if (!requested) { failed = false; failure = null; }
            return;
        }
        if (active || failed) return;

        try { Activate(game); }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            failure = ex.Message;
        }
    }

    private void Activate(GameManager game)
    {
        var hud = GameObject.Find("HUDCam")?.GetComponent<Camera>();
        var background = GameObject.Find("Background Cam")?.GetComponent<Camera>();
        if (hud == null || !hud.enabled || hud.targetTexture != null
            || hud.clearFlags != CameraClearFlags.Nothing || hud.depth <= 0
            || !Mathf.Approximately(hud.depth, 6) || hud.cullingMask != 4128
            || hud.rect != new Rect(0, 0, 1, 1) || hud.targetDisplay != 0)
            throw new InvalidOperationException("HUDCam did not match the expected full-screen overlay camera.");

        if (background == null || background.targetTexture != null || background.rect != hud.rect
            || background.targetDisplay != hud.targetDisplay
            || (background.enabled && background.clearFlags != CameraClearFlags.SolidColor))
            throw new InvalidOperationException("No validated camera was clearing the gameplay color buffer.");

        Camera? guitarCamera = null;
        foreach (var objectPlayer in game.playerObjects)
        {
            if (objectPlayer == null || !objectPlayer.enabled || !objectPlayer.gameObject.activeInHierarchy
                || objectPlayer.TryCast<GuitarPlayer>() == null) continue;
            foreach (var candidate in objectPlayer.GetComponentsInChildren<Camera>(true))
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy
                    || candidate.gameObject.name != "PlayerCamera") continue;
                if (guitarCamera != null)
                    throw new InvalidOperationException("More than one guitar camera was active.");
                guitarCamera = candidate;
            }
        }
        if (guitarCamera == null || guitarCamera.targetTexture != null || guitarCamera.rect != hud.rect
            || guitarCamera.targetDisplay != hud.targetDisplay
            || (guitarCamera.clearFlags != CameraClearFlags.Depth
                && guitarCamera.clearFlags != CameraClearFlags.Nothing
                && guitarCamera.clearFlags != CameraClearFlags.SolidColor))
            throw new InvalidOperationException("The active guitar camera did not match the solo render setup.");
        if (!background.enabled && guitarCamera.clearFlags != CameraClearFlags.SolidColor)
            throw new InvalidOperationException("Gameplay color clear was not active while the background camera was disabled.");

        camera = hud;
        originalEnabled = hud.enabled;
        hud.enabled = false;
        active = true;
        MelonLogger.Msg("Sloptifine HUD-camera benchmark active; camera-rendered score and gameplay HUD are hidden for offline solo play.");
    }

    internal void Restore()
    {
        if (camera == null) { active = false; return; }
        bool wasActive = active;
        try { camera.enabled = originalEnabled; }
        catch { /* The Gameplay scene may already be unloading. */ }
        camera = null;
        active = false;
        if (wasActive) MelonLogger.Msg("Sloptifine HUD-camera benchmark restored the HUD camera.");
    }

    internal void Clear()
    {
        Restore();
        failed = false;
        failure = null;
    }
}
