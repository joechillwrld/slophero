using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlopHero.Mod;

/// <summary>Applies saved offsets to Clone Hero's practice and FPS HUDs.</summary>
internal sealed class NativeHudLayout
{
    private readonly Dictionary<IntPtr, (Transform Transform, Vector3 Original, int Kind)> resized = new();
    private readonly Dictionary<IntPtr, Graphic[]> resizeGraphics = new();
    private readonly Dictionary<IntPtr, Renderer[]> resizeRenderers = new();
    private readonly Vector3[] resizeGraphicCorners = new Vector3[4];
    private readonly Vector3[] resizeRendererCorners = new Vector3[8];
    private InputViewer[]? inputViewers;
    private SPBar[]? spBars;
    private StarProgress[]? starProgresses;
    private SongProgress[]? songProgress;
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
    private double nextScaleProbe;
    private float oldScoreScale = float.NaN, oldInputScale = float.NaN, oldSpScale = float.NaN, oldStarProgressScale = float.NaN, oldSongProgressScale = float.NaN, oldPracticeScale = float.NaN, oldFpsScale = float.NaN;

    public void Clear()
    {
        RestoreScales();
        RestorePractice();
        RestoreFps();
        practiceRect = fpsRect = null;
        practicePointer = fpsPointer = IntPtr.Zero;
        inputViewers = null;
        spBars = null;
        starProgresses = null;
        songProgress = null;
        resizeGraphics.Clear();
        resizeRenderers.Clear();
        fpsGraphics = Array.Empty<Graphic>();
        fpsOriginalColors = Array.Empty<Color>();
        lastPracticeX = lastPracticeY = lastFpsX = lastFpsY = float.NaN;
        lastPracticeEnabled = lastFpsTint = false;
        nextProbe = 0;
        nextScaleProbe = 0;
    }

    public void Apply(GameManager game, bool practiceEnabled,
        float practiceX, float practiceY, float fpsX, float fpsY,
        bool tintFps, Color fpsColor, float scoreScale, float inputScale, float spScale,
        float starProgressScale, float songProgressScale, float practiceScale, float fpsScale, double realtime)
    {
        bool changed = practiceEnabled != lastPracticeEnabled
            || practiceX != lastPracticeX || practiceY != lastPracticeY
            || fpsX != lastFpsX || fpsY != lastFpsY
            || tintFps != lastFpsTint || fpsColor != lastFpsColor;
        bool scaleChanged = scoreScale != oldScoreScale || inputScale != oldInputScale || spScale != oldSpScale || starProgressScale != oldStarProgressScale
            || songProgressScale != oldSongProgressScale || practiceScale != oldPracticeScale || fpsScale != oldFpsScale;
        if (!changed && !scaleChanged && realtime < nextProbe && realtime < nextScaleProbe) return;
        nextProbe = realtime + .25;

        var fpsObject = game.FPSText;
        var (nextFps, nextFpsGraphics) = FindFpsHud(fpsObject);
        if (nextFps?.Pointer != fpsPointer)
        {
            RestoreFps();
            fpsRect = nextFps;
            fpsPointer = fpsRect?.Pointer ?? IntPtr.Zero;
            fpsOriginal = fpsRect == null ? Vector2.zero : fpsRect.anchoredPosition;
            fpsGraphics = nextFpsGraphics;
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
        ApplyHudScales(game, scoreScale, inputScale, spScale, starProgressScale, songProgressScale, practiceScale, fpsScale, realtime, scaleChanged);
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

    private void ApplyHudScales(GameManager game, float scoreScale, float inputScale, float spScale,
        float starProgressScale, float songProgressScale, float practiceScale, float fpsScale, double realtime, bool changed)
    {
        bool enabled = scoreScale != 1 || inputScale != 1 || spScale != 1 || starProgressScale != 1
            || songProgressScale != 1 || practiceScale != 1 || fpsScale != 1;
        if (!changed && (!enabled || realtime < nextScaleProbe)) return;
        nextScaleProbe = realtime + .5;
        if (scoreScale != 1) ApplyScoreHudScale(game, scoreScale);
        else RestoreKind(1);
        if (inputScale != 1)
        {
            if (inputViewers == null || inputViewers.Length == 0)
                inputViewers = UnityEngine.Object.FindObjectsOfType<InputViewer>();
            foreach (var viewer in inputViewers) if (viewer != null) Scale(viewer.transform, inputScale, 2);
        }
        else RestoreKind(typeof(InputViewer));
        if (spScale != 1)
        {
            if (spBars == null || spBars.Length == 0)
                spBars = UnityEngine.Object.FindObjectsOfType<SPBar>();
            foreach (var bar in spBars) if (bar != null) Scale(bar.transform, spScale, 3);
        }
        else RestoreKind(typeof(SPBar));
        if (starProgressScale != 1)
        {
            if (starProgresses == null || starProgresses.Length == 0)
                starProgresses = UnityEngine.Object.FindObjectsOfType<StarProgress>();
            foreach (var progress in starProgresses) if (progress != null) Scale(progress.transform, starProgressScale, 7);
        }
        else RestoreKind(typeof(StarProgress));
        if (songProgressScale != 1)
        {
            if (songProgress == null || songProgress.Length == 0)
                songProgress = UnityEngine.Object.FindObjectsOfType<SongProgress>();
            foreach (var bar in songProgress) if (bar != null) Scale(bar.transform, songProgressScale, 4);
        }
        else RestoreKind(typeof(SongProgress));
        if (practiceScale != 1) Scale(practiceRect, practiceScale, 5); else RestoreKind(practiceRect);
        if (fpsScale != 1) Scale(fpsRect, fpsScale, 6); else RestoreKind(fpsRect);
        oldScoreScale = scoreScale; oldInputScale = inputScale; oldSpScale = spScale;
        oldStarProgressScale = starProgressScale; oldSongProgressScale = songProgressScale;
        oldPracticeScale = practiceScale; oldFpsScale = fpsScale;
    }

    private void Scale(Transform? transform, float factor, int kind)
    {
        if (transform == null) return;
        if (!resized.TryGetValue(transform.Pointer, out var state))
            resized[transform.Pointer] = state = (transform, transform.localScale, kind);
        var target = state.Original * Math.Clamp(factor, .5f, 2.5f);
        if (transform.localScale != target) transform.localScale = target;
    }

    private void ApplyScoreHudScale(GameManager game, float factor)
    {
        var manager = game.scoreManager;
        var root = manager?.transform;
        if (root != null) Scale(root, factor, 1);
        if (manager != null)
        {
            ScaleIfOutside(manager.scoreFont?.transform, root, factor);
            ScaleIfOutside(manager.comboFont?.transform, root, factor);
        }
        if (game.playerObjects == null) return;
        foreach (var player in game.playerObjects)
            if (player != null) ScaleIfOutside(player.AccuracyText?.transform, root, factor);
    }

    private void ScaleIfOutside(Transform? candidate, Transform? groupRoot, float factor)
    {
        if (candidate == null || (groupRoot != null && IsWithin(candidate, groupRoot))) return;
        Scale(candidate, factor, 1);
    }

    private static bool IsWithin(Transform candidate, Transform root)
    {
        for (Transform? current = candidate; current != null; current = current.parent)
            if (current.Pointer == root.Pointer) return true;
        return false;
    }

    private void RestoreKind(Transform? transform)
    {
        if (transform == null) return;
        if (resized.Remove(transform.Pointer, out var state) && state.Transform != null) state.Transform.localScale = state.Original;
    }

    private void RestoreKind(int kind)
    {
        foreach (var pair in resized.ToArray())
        {
            if (pair.Value.Transform == null) { resized.Remove(pair.Key); continue; }
            if (pair.Value.Kind != kind) continue;
            pair.Value.Transform.localScale = pair.Value.Original;
            resized.Remove(pair.Key);
        }
    }

    private void RestoreKind(Type componentType)
    {
        foreach (var pair in resized.ToArray())
        {
            if (pair.Value.Transform == null) { resized.Remove(pair.Key); continue; }
            int kind = componentType == typeof(InputViewer) ? 2 : componentType == typeof(SPBar) ? 3
                : componentType == typeof(StarProgress) ? 7 : componentType == typeof(SongProgress) ? 4 : 0;
            if (pair.Value.Kind != kind) continue;
            pair.Value.Transform.localScale = pair.Value.Original;
            resized.Remove(pair.Key);
        }
    }

    private void RestoreScales()
    {
        foreach (var pair in resized.Values) if (pair.Transform != null) pair.Transform.localScale = pair.Original;
        resized.Clear();
        oldScoreScale = oldInputScale = oldSpScale = oldStarProgressScale = oldSongProgressScale = oldPracticeScale = oldFpsScale = float.NaN;
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

    /// <summary>Finds screen bounds for the HUD groups edited by corner handles.</summary>
    public bool TryGetResizeScreenRect(GameManager game, int target, out Rect rect)
    {
        if (target == 7) return TryGetScreenRect(1, out rect);
        if (target == 8) return TryGetScreenRect(2, out rect);

        bool found = false;
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        Camera? worldCamera = Camera.main;

        switch (target)
        {
            case 2:
                var scoreManager = game.scoreManager;
                AddResizeBounds(scoreManager?.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                if (scoreManager != null)
                {
                    AddResizeBounds(scoreManager.scoreFont?.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                    AddResizeBounds(scoreManager.comboFont?.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                }
                if (game.playerObjects != null)
                    foreach (var player in game.playerObjects)
                        if (player != null) AddResizeBounds(player.AccuracyText?.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case 3:
                if (inputViewers == null || inputViewers.Length == 0)
                    inputViewers = UnityEngine.Object.FindObjectsOfType<InputViewer>();
                foreach (var viewer in inputViewers) if (viewer != null) AddResizeBounds(viewer.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case 4:
                if (spBars == null || spBars.Length == 0)
                    spBars = UnityEngine.Object.FindObjectsOfType<SPBar>();
                foreach (var bar in spBars) if (bar != null) AddResizeBounds(bar.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case 5:
                if (starProgresses == null || starProgresses.Length == 0)
                    starProgresses = UnityEngine.Object.FindObjectsOfType<StarProgress>();
                foreach (var progress in starProgresses) if (progress != null) AddResizeBounds(progress.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                break;
            case 6:
                if (songProgress == null || songProgress.Length == 0)
                    songProgress = UnityEngine.Object.FindObjectsOfType<SongProgress>();
                foreach (var bar in songProgress) if (bar != null) AddResizeBounds(bar.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                break;
            default: rect = default; return false;
        }

        rect = found ? Rect.MinMaxRect(minX, minY, maxX, maxY) : default;
        return found && rect.width > 1 && rect.height > 1;
    }

    private void AddResizeBounds(Transform? transform, Camera? worldCamera,
        ref bool found, ref float minX, ref float minY, ref float maxX, ref float maxY)
    {
        if (transform == null || !transform.gameObject.activeInHierarchy) return;
        if (!resizeGraphics.TryGetValue(transform.Pointer, out var graphics) || graphics.Length == 0)
            resizeGraphics[transform.Pointer] = graphics = transform.GetComponentsInChildren<Graphic>(true);
        if (!resizeRenderers.TryGetValue(transform.Pointer, out var renderers) || renderers.Length == 0)
            resizeRenderers[transform.Pointer] = renderers = transform.GetComponentsInChildren<Renderer>(true);

        foreach (var graphic in graphics)
        {
            if (graphic == null || graphic.rectTransform == null || !graphic.gameObject.activeInHierarchy) continue;
            var canvas = graphic.GetComponentInParent<Canvas>();
            Camera? camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : (canvas.worldCamera ?? worldCamera);
            graphic.rectTransform.GetWorldCorners(resizeGraphicCorners);
            for (int i = 0; i < resizeGraphicCorners.Length; i++)
                IncludeResizePoint(resizeGraphicCorners[i], camera, ref found, ref minX, ref minY, ref maxX, ref maxY);
        }

        // Clone Hero's gameplay HUD is built from SpriteRenderers, not UI
        // Graphics. Project all eight bounds corners through the game camera
        // so score, star, and song-progress controls get a selectable outline.
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            var bounds = renderer.bounds;
            var center = bounds.center;
            var extents = bounds.extents;
            resizeRendererCorners[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
            resizeRendererCorners[1] = center + new Vector3(-extents.x, -extents.y, extents.z);
            resizeRendererCorners[2] = center + new Vector3(-extents.x, extents.y, -extents.z);
            resizeRendererCorners[3] = center + new Vector3(-extents.x, extents.y, extents.z);
            resizeRendererCorners[4] = center + new Vector3(extents.x, -extents.y, -extents.z);
            resizeRendererCorners[5] = center + new Vector3(extents.x, -extents.y, extents.z);
            resizeRendererCorners[6] = center + new Vector3(extents.x, extents.y, -extents.z);
            resizeRendererCorners[7] = center + new Vector3(extents.x, extents.y, extents.z);
            for (int i = 0; i < resizeRendererCorners.Length; i++)
                IncludeResizePoint(resizeRendererCorners[i], worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
        }
    }

    private static void IncludeResizePoint(Vector3 world, Camera? camera,
        ref bool found, ref float minX, ref float minY, ref float maxX, ref float maxY)
    {
        Vector3 point = camera != null ? camera.WorldToScreenPoint(world) : world;
        minX = Math.Min(minX, point.x); maxX = Math.Max(maxX, point.x);
        minY = Math.Min(minY, Screen.height - point.y); maxY = Math.Max(maxY, Screen.height - point.y);
        found = true;
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

    private static (RectTransform? Transform, Graphic[] Texts) FindFpsHud(GameObject? fpsObject)
    {
        if (fpsObject == null) return (null, Array.Empty<Graphic>());
        var parent = fpsObject.transform.parent;
        var parentRect = parent == null ? null : parent.GetComponent<RectTransform>();
        if (parentRect != null)
        {
            var siblings = parent!.gameObject.GetComponentsInChildren<Graphic>(true)
                .Where(IsTextGraphic).ToArray();
            // FPSText is the slash separator on this game build; the numeric
            // values are sibling text objects, so move/tint their shared row.
            if (siblings.Length > 1) return (parentRect, siblings);
        }

        var ownRect = fpsObject.GetComponent<RectTransform>()
            ?? fpsObject.GetComponentInChildren<RectTransform>(true);
        var ownTexts = fpsObject.GetComponentsInChildren<Graphic>(true)
            .Where(IsTextGraphic).ToArray();
        return (ownRect, ownTexts);
    }

    private static bool IsTextGraphic(Graphic graphic) =>
        graphic != null && (graphic.TryCast<TextMeshProUGUI>() != null || graphic.TryCast<Text>() != null);

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
