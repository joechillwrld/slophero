using UnityEngine;
using UnityEngine.UI;

namespace SlopHero.Mod;

/// <summary>
/// The three built-in HUD drag handles each own a world-space Canvas even
/// while their sole Image is fully transparent. Pause only those canvases
/// while the opt-in gameplay EventSystem pause is active.
/// </summary>
internal sealed class HiddenHudDragCanvasOverride
{
    private sealed class Handle
    {
        public Canvas Canvas = null!;
        public Image Image = null!;
        public bool PausedByUs;
    }

    private readonly List<Handle> handles = new();
    private bool discovered;

    internal void Update(bool uiEventsSuspended)
    {
        if (!uiEventsSuspended)
        {
            Restore();
            return;
        }
        if (!discovered) Discover();
        foreach (var handle in handles)
        {
            if (handle.Canvas == null || handle.Image == null) continue;
            // A visible handle can still be animated by game code while its
            // EventSystem is paused. Restore it as soon as it becomes visible.
            bool invisible = !handle.Image.enabled || handle.Image.color.a <= .001f;
            if (invisible && !handle.PausedByUs && handle.Canvas.enabled)
            {
                handle.Canvas.enabled = false;
                handle.PausedByUs = true;
            }
            else if (!invisible && handle.PausedByUs)
            {
                handle.Canvas.enabled = true;
                handle.PausedByUs = false;
            }
        }
    }

    internal void Restore()
    {
        foreach (var handle in handles)
        {
            if (!handle.PausedByUs) continue;
            try { if (handle.Canvas != null) handle.Canvas.enabled = true; }
            catch { /* The scene may already be unloading. */ }
            handle.PausedByUs = false;
        }
    }

    internal void Clear()
    {
        Restore();
        handles.Clear();
        discovered = false;
    }

    private void Discover()
    {
        discovered = true;
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace
                || canvas.gameObject.name != "HUD Drag Handle") continue;
            var parent = canvas.transform.parent;
            if (parent == null || parent.parent == null || parent.parent.name != "HUDCam") continue;
            string root = parent.name;
            if (root != "HUD Drag Handle Root" && root != "Input Viewer Drag Handle Root"
                && root != "Leaderboard HUD Drag Handle Root") continue;
            var image = canvas.GetComponent<Image>();
            if (image == null || canvas.GetComponentsInChildren<Graphic>(true).Length != 1) continue;
            handles.Add(new Handle { Canvas = canvas, Image = image });
        }
    }
}
