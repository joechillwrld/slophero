using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace SlopHero.Mod;

/// <summary>Applies saved offsets to Clone Hero's practice and FPS HUDs.</summary>
internal sealed class NativeHudLayout
{
    private RectTransform? practiceRect, fpsRect;
    private IntPtr practicePointer, fpsPointer;
    private Vector2 practiceOriginal, fpsOriginal;
    private Graphic[] fpsGraphics = Array.Empty<Graphic>();
    private Color[] fpsOriginalColors = Array.Empty<Color>();
    private float lastPracticeX = float.NaN, lastPracticeY = float.NaN;
    private float lastFpsX = float.NaN, lastFpsY = float.NaN;
    private bool lastPracticeEnabled, lastFpsTint;
    private Color lastFpsColor;
    private double nextProbe;

    public void Clear()
    {
        RestorePractice();
        RestoreFps();
        practiceRect = fpsRect = null;
        practicePointer = fpsPointer = IntPtr.Zero;
        fpsGraphics = Array.Empty<Graphic>();
        fpsOriginalColors = Array.Empty<Color>();
        lastPracticeX = lastPracticeY = lastFpsX = lastFpsY = float.NaN;
        lastPracticeEnabled = lastFpsTint = false;
        nextProbe = 0;
    }

    public void Apply(GameManager game, bool practiceEnabled,
        float practiceX, float practiceY, float fpsX, float fpsY,
        bool tintFps, Color fpsColor, double realtime)
    {
        bool changed = practiceEnabled != lastPracticeEnabled
            || practiceX != lastPracticeX || practiceY != lastPracticeY
            || fpsX != lastFpsX || fpsY != lastFpsY
            || tintFps != lastFpsTint || fpsColor != lastFpsColor;
        if (!changed && realtime < nextProbe) return;
        nextProbe = realtime + .25;

        var fpsObject = game.FPSText;
        var nextFps = fpsObject == null ? null : fpsObject.GetComponent<RectTransform>();
        if (nextFps == null && fpsObject != null) nextFps = fpsObject.GetComponentInChildren<RectTransform>(true);
        if (nextFps?.Pointer != fpsPointer)
        {
            RestoreFps();
            fpsRect = nextFps;
            fpsPointer = fpsRect?.Pointer ?? IntPtr.Zero;
            fpsOriginal = fpsRect == null ? Vector2.zero : fpsRect.anchoredPosition;
            fpsGraphics = fpsObject == null ? Array.Empty<Graphic>() : fpsObject.GetComponentsInChildren<Graphic>(true).ToArray();
            fpsOriginalColors = fpsGraphics.Select(g => g == null ? Color.white : g.color).ToArray();
            lastFpsX = lastFpsY = float.NaN;
            lastFpsTint = false;
        }

        if (practiceEnabled)
        {
            var nextPractice = game.practiceUI?.rectTrans;
            if (nextPractice?.Pointer != practicePointer)
            {
                RestorePractice();
                practiceRect = nextPractice;
                practicePointer = practiceRect?.Pointer ?? IntPtr.Zero;
                practiceOriginal = practiceRect == null ? Vector2.zero : practiceRect.anchoredPosition;
                lastPracticeX = lastPracticeY = float.NaN;
            }
            ApplyPosition(practiceRect, practiceOriginal, practiceX, practiceY, ref lastPracticeX, ref lastPracticeY);
        }
        else RestorePractice();

        ApplyPosition(fpsRect, fpsOriginal, fpsX, fpsY, ref lastFpsX, ref lastFpsY);
        if (tintFps != lastFpsTint || fpsColor != lastFpsColor || nextFps?.Pointer != fpsPointer)
        {
            ApplyFpsColor(tintFps, fpsColor);
            lastFpsTint = tintFps;
            lastFpsColor = fpsColor;
        }

        lastPracticeEnabled = practiceEnabled;
        lastPracticeX = practiceX;
        lastPracticeY = practiceY;
        lastFpsX = fpsX;
        lastFpsY = fpsY;
    }

    public bool TryGetScreenRect(int target, out Rect rect)
    {
        var transform = target == 1 ? practiceRect : fpsRect;
        if (transform == null)
        {
            rect = default;
            return false;
        }
        var canvas = transform.GetComponentInParent<Canvas>();
        Camera? camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var screen = RectTransformUtility.WorldToScreenPoint(camera, transform.position);
        float scale = canvas?.scaleFactor ?? 1;
        float width = Math.Max(120, transform.rect.width * scale);
        float height = Math.Max(32, transform.rect.height * scale);
        rect = new Rect(screen.x - width / 2, Screen.height - screen.y - height / 2, width, height);
        return true;
    }

    public Vector2 MoveByScreenDelta(int target, float deltaX, float deltaY)
    {
        var transform = target == 1 ? practiceRect : fpsRect;
        if (transform == null) return Vector2.zero;
        float scale = LocalToScreenScale(transform);
        var delta = new Vector2(deltaX / scale, -deltaY / scale);
        transform.anchoredPosition += delta;
        return delta;
    }

    private static void ApplyPosition(RectTransform? transform, Vector2 original,
        float x, float y, ref float lastX, ref float lastY)
    {
        if (transform == null || (x == lastX && y == lastY)) return;
        var target = original + new Vector2(x, y);
        if (transform.anchoredPosition != target) transform.anchoredPosition = target;
        lastX = x;
        lastY = y;
    }

    private static float LocalToScreenScale(RectTransform transform)
    {
        var canvas = transform.GetComponentInParent<Canvas>();
        float canvasScale = canvas?.scaleFactor ?? 1;
        float parentScale = transform.parent == null ? 1 : Math.Abs(transform.parent.lossyScale.x);
        float scale = canvasScale * parentScale;
        return float.IsFinite(scale) && scale > .001f ? scale : 1;
    }

    private void ApplyFpsColor(bool enabled, Color color)
    {
        for (int i = 0; i < fpsGraphics.Length; i++)
        {
            var graphic = fpsGraphics[i];
            if (graphic == null) continue;
            var current = graphic.color;
            var target = enabled ? color : fpsOriginalColors[i];
            target.a = current.a;
            if (current != target) graphic.color = target;
        }
    }

    private void RestorePractice()
    {
        if (practiceRect != null) practiceRect.anchoredPosition = practiceOriginal;
        practiceRect = null;
        practicePointer = IntPtr.Zero;
        lastPracticeX = lastPracticeY = float.NaN;
    }

    private void RestoreFps()
    {
        if (fpsRect != null) fpsRect.anchoredPosition = fpsOriginal;
        for (int i = 0; i < fpsGraphics.Length && i < fpsOriginalColors.Length; i++)
            if (fpsGraphics[i] != null) fpsGraphics[i].color = fpsOriginalColors[i];
        fpsRect = null;
        fpsPointer = IntPtr.Zero;
        fpsGraphics = Array.Empty<Graphic>();
        fpsOriginalColors = Array.Empty<Color>();
        lastFpsX = lastFpsY = float.NaN;
        lastFpsTint = false;
    }
}
