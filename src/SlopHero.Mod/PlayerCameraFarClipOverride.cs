using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Experimental solo-only reduction of the guitar camera's far clip plane.
/// This may cull distant notes or effects, so it stays opt-in and restores the
/// exact runtime value whenever the solo-song scope ends.
/// </summary>
internal sealed class PlayerCameraFarClipOverride
{
    private const float ExpectedVanillaFarClip = 120f;
    private const float ExperimentalFarClip = 80f;

    private Camera? camera;
    private float originalFarClip;
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
            || !Mathf.Approximately(guitarCamera.farClipPlane, ExpectedVanillaFarClip))
            throw new InvalidOperationException("The active guitar camera did not match the known 120-unit solo setup.");

        camera = guitarCamera;
        originalFarClip = guitarCamera.farClipPlane;
        guitarCamera.farClipPlane = ExperimentalFarClip;
        active = true;
        MelonLogger.Msg($"Sloptifine guitar-camera far clip reduced from {originalFarClip:0} to {ExperimentalFarClip:0} units for offline solo play.");
    }

    internal void Restore()
    {
        if (camera == null) { active = false; return; }
        bool wasActive = active;
        try
        {
            // Preserve a later change made by the game or another mod.
            if (Mathf.Approximately(camera.farClipPlane, ExperimentalFarClip))
                camera.farClipPlane = originalFarClip;
        }
        catch { /* The camera may already be unloading. */ }
        camera = null;
        active = false;
        if (wasActive) MelonLogger.Msg("Sloptifine guitar-camera far clip restored.");
    }

    internal void Clear()
    {
        Restore();
        failed = false;
        failure = null;
    }
}
