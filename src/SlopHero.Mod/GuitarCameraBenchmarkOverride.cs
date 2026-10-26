using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Offline solo upper-bound test for the 3D guitar camera render pass.
/// Note, highway, and effect visuals disappear while scoring and input keep
/// running; the original camera state is restored when the scope ends.
/// </summary>
internal sealed class GuitarCameraBenchmarkOverride
{
    private const float ExpectedVanillaFarClip = 120f;
    private Camera? camera;
    private bool originalEnabled, active, failed;
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
        if (active && camera == null) active = false;
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
            || hud.clearFlags != CameraClearFlags.Nothing || !Mathf.Approximately(hud.depth, 6)
            || hud.cullingMask != 4128 || hud.rect != new Rect(0, 0, 1, 1)
            || hud.targetDisplay != 0)
            throw new InvalidOperationException("HUDCam did not match the expected active overlay setup.");
        if (background == null || !background.enabled || background.targetTexture != null
            || background.clearFlags != CameraClearFlags.SolidColor || background.cullingMask != 8192
            || background.rect != hud.rect || background.targetDisplay != hud.targetDisplay)
            throw new InvalidOperationException("Background Cam was not clearing the solo gameplay image.");

        Camera? guitarCamera = null;
        foreach (var player in game.playerObjects)
        {
            if (player == null || !player.enabled || !player.gameObject.activeInHierarchy
                || player.TryCast<GuitarPlayer>() == null) continue;
            foreach (var candidate in player.GetComponentsInChildren<Camera>(true))
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy
                    || candidate.gameObject.name != "PlayerCamera") continue;
                if (guitarCamera != null)
                    throw new InvalidOperationException("More than one guitar camera was active.");
                guitarCamera = candidate;
            }
        }
        if (guitarCamera == null || guitarCamera.targetTexture != null || guitarCamera.cullingMask != 512
            || guitarCamera.rect != hud.rect || guitarCamera.targetDisplay != hud.targetDisplay
            || !Mathf.Approximately(guitarCamera.farClipPlane, ExpectedVanillaFarClip))
            throw new InvalidOperationException("The guitar camera did not match the known solo render setup.");

        camera = guitarCamera;
        originalEnabled = guitarCamera.enabled;
        guitarCamera.enabled = false;
        active = true;
        MelonLogger.Msg("Sloptifine guitar-camera benchmark active; note, highway, and 3D effect visuals are hidden during offline solo play.");
    }

    internal void Restore()
    {
        if (camera == null) { active = false; return; }
        bool wasActive = active;
        try { camera.enabled = originalEnabled; }
        catch { /* The Gameplay scene may already be unloading. */ }
        camera = null;
        active = false;
        if (wasActive) MelonLogger.Msg("Sloptifine guitar camera restored.");
    }

    internal void Clear()
    {
        Restore();
        failed = false;
        failure = null;
    }
}
