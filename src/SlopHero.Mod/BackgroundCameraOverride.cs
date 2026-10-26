using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Video;

namespace SlopHero.Mod;

/// <summary>
/// Opt-in black-background mode for one offline guitar player. The scene's
/// background camera is also its color clear, so transfer that clear to the
/// player camera before disabling the background render pass.
/// </summary>
internal sealed class BackgroundCameraOverride
{
    private Camera? background;
    private Camera? player;
    private VideoPlayer? backgroundVideo;
    private CameraClearFlags originalPlayerClear;
    private Color originalPlayerColor;
    private bool originalBackgroundEnabled;
    private bool videoPausedByMod;
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
        if (active)
        {
            PauseHiddenVideoIfPlaying();
            return;
        }
        if (failed) return;
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
        var bg = GameObject.Find("Background Cam")?.GetComponent<Camera>();
        if (bg == null || !bg.enabled || bg.targetTexture != null
            || bg.clearFlags != CameraClearFlags.SolidColor || bg.cullingMask != 8192)
            throw new InvalidOperationException("Expected Gameplay background camera was not available.");

        Camera? guitarCamera = null;
        foreach (var objectPlayer in game.playerObjects)
        {
            if (objectPlayer == null || !objectPlayer.enabled || !objectPlayer.gameObject.activeInHierarchy
                || objectPlayer.TryCast<GuitarPlayer>() == null) continue;
            foreach (var camera in objectPlayer.GetComponentsInChildren<Camera>(true))
            {
                if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy
                    || camera.gameObject.name != "PlayerCamera") continue;
                if (guitarCamera != null)
                    throw new InvalidOperationException("More than one guitar camera was active.");
                guitarCamera = camera;
            }
        }
        if (guitarCamera == null || guitarCamera.targetTexture != null
            || guitarCamera.cullingMask != 512 || guitarCamera.depth <= bg.depth
            || guitarCamera.targetDisplay != bg.targetDisplay
            || guitarCamera.rect != bg.rect
            || (guitarCamera.clearFlags != CameraClearFlags.Depth
                && guitarCamera.clearFlags != CameraClearFlags.Nothing))
            throw new InvalidOperationException("Guitar camera did not match the known solo render setup.");

        // The scene's chart-background video is rendered by Background Cam.
        // Disabling that camera removes its render pass, but Unity can keep
        // decoding a VideoPlayer whose output is now invisible. Track only
        // that exact player; Update catches a delayed Play() while hidden.
        var video = GameObject.Find("Background Image")?.GetComponent<VideoPlayer>();
        bool targetsBackgroundCamera = video != null && video.targetCamera == bg;

        background = bg;
        player = guitarCamera;
        backgroundVideo = targetsBackgroundCamera ? video : null;
        originalBackgroundEnabled = bg.enabled;
        originalPlayerClear = guitarCamera.clearFlags;
        originalPlayerColor = guitarCamera.backgroundColor;
        // The player camera runs after Background Cam. Assign the clear first
        // so no rendered frame can inherit the previous frame's color buffer.
        guitarCamera.backgroundColor = bg.backgroundColor;
        guitarCamera.clearFlags = CameraClearFlags.SolidColor;
        bg.enabled = false;
        active = true;
        PauseHiddenVideoIfPlaying();
        MelonLogger.Msg($"Sloptifine black-background mode active for offline solo play; chart video tracked={backgroundVideo != null}, paused={videoPausedByMod}.");
    }

    private void PauseHiddenVideoIfPlaying()
    {
        try
        {
            if (backgroundVideo != null && backgroundVideo.enabled && backgroundVideo.isPlaying
                && background != null && backgroundVideo.targetCamera == background)
            {
                backgroundVideo.Pause();
                videoPausedByMod = true;
            }
        }
        catch { /* A destroyed/replaced player must not affect camera restoration. */ }
    }

    internal void Restore()
    {
        if (background == null && player == null && backgroundVideo == null)
        {
            active = false;
            videoPausedByMod = false;
            return;
        }
        bool wasActive = active;
        // Restore the old color clear before returning the player camera to
        // its depth/nothing clear mode.
        try { if (background != null) background.enabled = originalBackgroundEnabled; }
        catch { /* The scene may already be unloading. */ }
        try
        {
            if (backgroundVideo != null && videoPausedByMod && backgroundVideo.enabled)
                backgroundVideo.Play();
        }
        catch { /* The video player or scene may already be unloading. */ }
        try
        {
            if (player != null)
            {
                player.clearFlags = originalPlayerClear;
                player.backgroundColor = originalPlayerColor;
            }
        }
        catch { /* The player object may already be unloading. */ }
        background = null;
        player = null;
        backgroundVideo = null;
        videoPausedByMod = false;
        active = false;
        if (wasActive) MelonLogger.Msg("Sloptifine background camera restored.");
    }

    internal void Clear()
    {
        Restore();
        failed = false;
        failure = null;
    }
}
