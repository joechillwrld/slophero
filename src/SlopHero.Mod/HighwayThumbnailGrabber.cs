using Il2Cpp;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>Save only the live guitar-camera view, including its highway and notes.</summary>
internal static class HighwayThumbnailGrabber
{
    internal static string Capture(GameManager game, string userDataDirectory)
    {
        if (game.playerObjects == null) throw new InvalidOperationException("Start a song first.");
        Camera? camera = null;
        foreach (var player in game.playerObjects)
        {
            if (player == null || !player.enabled || player.TryCast<GuitarPlayer>() == null) continue;
            foreach (var candidate in player.GetComponentsInChildren<Camera>(true))
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy
                    || candidate.gameObject.name != "PlayerCamera" || candidate.cullingMask != 512) continue;
                if (camera != null) throw new InvalidOperationException("Multiple player cameras are active.");
                camera = candidate;
            }
        }
        if (camera == null || camera.targetTexture != null)
            throw new InvalidOperationException("The guitar camera is not ready for capture.");

        int width = Math.Clamp(Screen.width, 320, 1920);
        int height = Math.Clamp(Screen.height, 180, 1080);
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var oldClear = camera.clearFlags;
        var oldBackground = camera.backgroundColor;
        Texture2D? image = null;
        try
        {
            target.Create();
            camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0, 0, 0, 0);
            camera.Render();
            RenderTexture.active = target;
            // The player camera fills the screen even though the playable
            // highway occupies only its center. Keep the notes and fretboard
            // while cropping the broad empty margins from the PNG.
            int cropWidth = Math.Max(1, (int)(width * .5f));
            int cropHeight = Math.Max(1, (int)(height * .8f));
            int cropX = (width - cropWidth) / 2;
            image = new Texture2D(cropWidth, cropHeight, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(cropX, 0, cropWidth, cropHeight), 0, 0);
            image.Apply();
            var png = image.EncodeToPNG();
            if (png == null || png.Length == 0) throw new InvalidOperationException("PNG encoding returned no image.");
            string folder = Path.Combine(userDataDirectory, "SlopHero", "Thumbnails");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "highway-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".png");
            File.WriteAllBytes(path, png.ToArray());
            return path;
        }
        finally
        {
            camera.targetTexture = oldTarget;
            camera.clearFlags = oldClear;
            camera.backgroundColor = oldBackground;
            RenderTexture.active = oldActive;
            if (image != null) UnityEngine.Object.Destroy(image);
            UnityEngine.Object.Destroy(target);
        }
    }
}
