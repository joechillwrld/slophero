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
    private readonly Vector3[] nativeHudRectCorners = new Vector3[4];
    private readonly List<Transform> scoreGroupScratch = new(48);
    private InputViewer[]? inputViewers;
    private SPBar[]? spBars;
    private StarProgress[]? starProgresses;
    private SongProgress[]? songProgress;
    private RectTransform? practiceRect, fpsRect;
    private PracticeUI? cachedPracticeUi;
    private FrameRate? fpsFrameRate;
    private IntPtr practicePointer, fpsPointer;
    private Vector2 practiceOriginal, fpsOriginal;
    private TMP_Text[] fpsTmpTexts = Array.Empty<TMP_Text>();
    private Text[] fpsLegacyTexts = Array.Empty<Text>();
    private Color[] fpsTmpOriginalColors = Array.Empty<Color>();
    private Color[] fpsLegacyOriginalColors = Array.Empty<Color>();
    private string fpsPlainText = "", fpsStyledText = "";
    private float lastPracticeX = float.NaN, lastPracticeY = float.NaN;
    private float lastFpsX = float.NaN, lastFpsY = float.NaN;
    private bool lastFpsTint;
    private Color lastFpsColor;
    private double nextProbe;
    private double nextScaleProbe;
    private double nextFpsProbe;
    private double nextFpsTintProbe;
    private long nextPracticeLookupTick;
    private float oldScoreScale = float.NaN, oldInputScale = float.NaN, oldSpScale = float.NaN, oldPracticeScale = float.NaN, oldFpsScale = float.NaN;

    public void Clear()
    {
        RestoreScales();
        RestorePractice();
        RestoreFps();
        practiceRect = fpsRect = null;
        cachedPracticeUi = null;
        fpsFrameRate = null;
        practicePointer = fpsPointer = IntPtr.Zero;
        inputViewers = null;
        spBars = null;
        starProgresses = null;
        songProgress = null;
        resizeGraphics.Clear();
        resizeRenderers.Clear();
        fpsTmpTexts = Array.Empty<TMP_Text>();
        fpsLegacyTexts = Array.Empty<Text>();
        fpsTmpOriginalColors = Array.Empty<Color>();
        fpsLegacyOriginalColors = Array.Empty<Color>();
        lastPracticeX = lastPracticeY = lastFpsX = lastFpsY = float.NaN;
        lastFpsTint = false;
        nextProbe = 0;
        nextScaleProbe = 0;
        nextFpsProbe = 0;
        nextFpsTintProbe = 0;
        nextPracticeLookupTick = 0;
    }

    public void Apply(GameManager game,
        float practiceX, float practiceY, float fpsX, float fpsY,
        bool tintFps, Color fpsColor, float scoreScale, float inputScale, float spScale,
        float practiceScale, float fpsScale, double realtime)
    {
        bool changed = practiceX != lastPracticeX || practiceY != lastPracticeY
            || fpsX != lastFpsX || fpsY != lastFpsY
            || tintFps != lastFpsTint || fpsColor != lastFpsColor;
        bool scaleChanged = scoreScale != oldScoreScale || inputScale != oldInputScale || spScale != oldSpScale
            || practiceScale != oldPracticeScale || fpsScale != oldFpsScale;
        if (!changed && !scaleChanged && realtime < nextProbe && realtime < nextScaleProbe)
        {
            if (tintFps && realtime >= nextFpsTintProbe)
            {
                // FrameRate rewrites the text periodically. Restyle only when
                // needed, at the existing 20 Hz HUD cadence, without scanning
                // scene objects or rebuilding any GUI every game frame.
                ApplyFpsColor(true, fpsColor);
                nextFpsTintProbe = realtime + .05;
            }
            return;
        }
        nextProbe = realtime + .25;

        // The gameplay scene has a dedicated FPS object with FrameRate and
        // TextMeshProUGUI on the same GameObject. GameManager.FPSText is not a
        // reliable reference to that object in this build.
        if (fpsFrameRate == null && realtime >= nextFpsProbe)
        {
            fpsFrameRate = UnityEngine.Object.FindObjectOfType<FrameRate>();
            nextFpsProbe = realtime + 1;
        }
        var fpsObject = fpsFrameRate != null && fpsFrameRate.gameObject.activeInHierarchy
            ? fpsFrameRate.gameObject : null;
        var (nextFps, nextFpsTmpTexts, nextFpsLegacyTexts) = FindFpsHud(fpsObject);
        if (nextFps?.Pointer != fpsPointer || !SameTextTargets(fpsTmpTexts, nextFpsTmpTexts)
            || !SameTextTargets(fpsLegacyTexts, nextFpsLegacyTexts))
        {
            RestoreFps();
            fpsRect = nextFps;
            fpsPointer = fpsRect?.Pointer ?? IntPtr.Zero;
            fpsOriginal = fpsRect == null ? Vector2.zero : fpsRect.anchoredPosition;
            fpsTmpTexts = nextFpsTmpTexts;
            fpsLegacyTexts = nextFpsLegacyTexts;
            fpsTmpOriginalColors = fpsTmpTexts.Select(t => t == null ? Color.white : t.color).ToArray();
            fpsLegacyOriginalColors = fpsLegacyTexts.Select(t => t == null ? Color.white : t.color).ToArray();
            lastFpsX = lastFpsY = float.NaN;
            lastFpsTint = false;
        }

        RefreshPracticeTarget(game);
        ApplyPosition(practiceRect, practiceOriginal, practiceX, practiceY, ref lastPracticeX, ref lastPracticeY);

        ApplyPosition(fpsRect, fpsOriginal, fpsX, fpsY, ref lastFpsX, ref lastFpsY);
        ApplyHudScales(game, scoreScale, inputScale, spScale, practiceScale, fpsScale, realtime, scaleChanged);
        if (tintFps || tintFps != lastFpsTint || fpsColor != lastFpsColor || nextFps?.Pointer != fpsPointer)
        {
            ApplyFpsColor(tintFps, fpsColor);
            nextFpsTintProbe = realtime + .05;
            lastFpsTint = tintFps;
            lastFpsColor = fpsColor;
        }

        lastPracticeX = practiceX;
        lastPracticeY = practiceY;
        lastFpsX = fpsX;
        lastFpsY = fpsY;
    }

    // PracticeUI is serialized on GameManager even when the game's practice
    // flag or songLoaded state is temporarily false. The editable stats panel
    // is PracticeUI's own RectTransform, so resolve it directly.
    private void RefreshPracticeTarget(GameManager game)
    {
        var ui = GetPracticeUi(game);
        var next = ui?.rectTrans ?? ui?.GetComponent<RectTransform>();
        if ((next?.Pointer ?? IntPtr.Zero) == practicePointer) return;
        RestorePractice();
        practiceRect = next;
        practicePointer = next?.Pointer ?? IntPtr.Zero;
        practiceOriginal = next == null ? Vector2.zero : next.anchoredPosition;
        lastPracticeX = lastPracticeY = float.NaN;
    }

    private PracticeUI? GetPracticeUi(GameManager game)
    {
        var ui = game.practiceUI;
        if (ui != null) return cachedPracticeUi = ui;
        if (cachedPracticeUi != null) return cachedPracticeUi;
        long now = Environment.TickCount64;
        if (now < nextPracticeLookupTick) return null;
        nextPracticeLookupTick = now + 1000;
        return cachedPracticeUi = UnityEngine.Object.FindObjectOfType<PracticeUI>();
    }

    public void ApplyPracticeBeforeSongReady(GameManager game, float x, float y, float scale)
    {
        RefreshPracticeTarget(game);
        ApplyPosition(practiceRect, practiceOriginal, x, y, ref lastPracticeX, ref lastPracticeY);
        if (scale != 1) Scale(practiceRect, scale, 5); else RestoreKind(practiceRect);
    }

    public string DescribePracticeTarget(GameManager? game)
    {
        if (game == null) return "GameManager missing";
        var ui = GetPracticeUi(game);
        if (ui == null) return $"PracticeUI missing; songLoaded={game.songLoaded}, playersInit={game.playersInit}";
        var panel = ui.rectTrans;
        if (panel == null) return "PracticeUI exists but rectTrans is null";
        return $"panelActive={panel.gameObject.activeInHierarchy}, panelSize={panel.rect.width:0}x{panel.rect.height:0}, "
            + $"songLoaded={game.songLoaded}, playersInit={game.playersInit}, practiceFlag={game.globalVariables?.isPracticeEnabled}";
    }

    private void ApplyHudScales(GameManager game, float scoreScale, float inputScale, float spScale,
        float practiceScale, float fpsScale, double realtime, bool changed)
    {
        bool enabled = scoreScale != 1 || inputScale != 1 || spScale != 1 || practiceScale != 1 || fpsScale != 1;
        if (!changed && (!enabled || realtime < nextScaleProbe)) return;
        nextScaleProbe = realtime + .5;
        if (scoreScale != 1) ApplyScoreHudScale(game, scoreScale);
        else RestoreKind(1);
        if (inputScale != 1)
        {
            if (inputViewers == null || inputViewers.Length == 0)
                inputViewers = UnityEngine.Object.FindObjectsOfType<InputViewer>();
            foreach (var viewer in inputViewers) if (viewer != null) ScaleInputViewer(viewer, inputScale);
        }
        else RestoreKind(typeof(InputViewer));
        if (spScale != 1)
        {
            if (spBars == null || spBars.Length == 0)
                spBars = UnityEngine.Object.FindObjectsOfType<SPBar>();
            foreach (var bar in spBars) if (bar != null) Scale(bar.transform, spScale, 3);
        }
        else RestoreKind(typeof(SPBar));
        if (practiceScale != 1) Scale(practiceRect, practiceScale, 5); else RestoreKind(practiceRect);
        if (fpsScale != 1) Scale(fpsRect, fpsScale, 6); else RestoreKind(fpsRect);
        oldScoreScale = scoreScale; oldInputScale = inputScale; oldSpScale = spScale;
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
        var hudRoot = manager?.scoreFont?.transform.parent;
        // In the supplied gameplay scene Score, Combo, Song Progress, and
        // Star are all children of HUD. Scale that shared parent so the whole
        // score box changes size as one piece instead of drifting apart.
        if (hudRoot != null && manager?.comboFont?.transform.parent?.Pointer == hudRoot.Pointer)
        {
            Scale(hudRoot, factor, 1);
            return;
        }

        var candidates = scoreGroupScratch;
        candidates.Clear();
        if (manager != null)
        {
            AddScoreTarget(manager.comboTransform);
            AddScoreTarget(manager.scoreFont?.transform);
            AddScoreTarget(manager.comboFont?.transform);
            AddSpriteFontTargets(manager.scoreFont);
            AddSpriteFontTargets(manager.comboFont);
        }
        if (starProgresses == null || starProgresses.Length == 0)
            starProgresses = UnityEngine.Object.FindObjectsOfType<StarProgress>();
        foreach (var progress in starProgresses)
            if (progress != null) AddScoreTarget(progress.transform);
        if (songProgress == null || songProgress.Length == 0)
            songProgress = UnityEngine.Object.FindObjectsOfType<SongProgress>();
        foreach (var progress in songProgress)
            if (progress != null) AddScoreTarget(progress.transform);
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            bool nested = false;
            for (int j = 0; j < candidates.Count; j++)
            {
                var parent = candidates[j];
                if (parent.Pointer == candidate.Pointer || !IsWithin(candidate, parent)) continue;
                nested = true;
                break;
            }
            if (!nested) Scale(candidate, factor, 1);
        }
    }

    private void AddSpriteFontTargets(SpriteFont? font)
    {
        var sprites = font?.Sprites;
        if (sprites == null) return;
        foreach (var sprite in sprites)
            if (sprite != null) AddScoreTarget(sprite.transform);
    }

    private void AddScoreTarget(Transform? transform)
    {
        if (transform == null) return;
        foreach (var existing in scoreGroupScratch) if (existing.Pointer == transform.Pointer) return;
        scoreGroupScratch.Add(transform);
    }

    private void ScaleInputViewer(InputViewer viewer, float factor)
    {
        // All instrument-specific displays are children of Input Viewer in
        // the gameplay scene. Scaling the root also covers the active mode.
        Scale(viewer.transform, factor, 2);
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
                : componentType == typeof(SongProgress) ? 4 : 0;
            if (pair.Value.Kind != kind) continue;
            pair.Value.Transform.localScale = pair.Value.Original;
            resized.Remove(pair.Key);
        }
    }

    private void RestoreScales()
    {
        foreach (var pair in resized.Values) if (pair.Transform != null) pair.Transform.localScale = pair.Original;
        resized.Clear();
        oldScoreScale = oldInputScale = oldSpScale = oldPracticeScale = oldFpsScale = float.NaN;
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
        transform.GetWorldCorners(nativeHudRectCorners);
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        foreach (var corner in nativeHudRectCorners)
        {
            var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y)) continue;
            minX = Math.Min(minX, point.x); maxX = Math.Max(maxX, point.x);
            minY = Math.Min(minY, Screen.height - point.y); maxY = Math.Max(maxY, Screen.height - point.y);
        }
        rect = float.IsFinite(minX) && float.IsFinite(minY) && maxX - minX > 1 && maxY - minY > 1
            ? Rect.MinMaxRect(minX, minY, maxX, maxY) : default;
        if (target == 1 && (rect.width <= 1 || rect.height <= 1))
        {
            // PracticeUI's ContentSizeFitter can leave Panel at zero height
            // while its header and statistic rows are already visible.
            bool childFound = false;
            minX = minY = float.PositiveInfinity;
            maxX = maxY = float.NegativeInfinity;
            // Rows are added dynamically in practice. This fallback runs only
            // while editing the practice HUD, so refresh that small child list.
            resizeGraphics.Remove(transform.Pointer);
            AddResizeBounds(transform, camera, ref childFound, ref minX, ref minY, ref maxX, ref maxY);
            if (childFound) rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
        return rect.width > 1 && rect.height > 1;
    }

    /// <summary>Finds screen bounds for the HUD groups edited by corner handles.</summary>
    public bool TryGetResizeScreenRect(GameManager game, int target, out Rect rect)
    {
        if (target == 5)
        {
            RefreshPracticeTarget(game);
            return TryGetScreenRect(1, out rect);
        }
        if (target == 6) return TryGetScreenRect(2, out rect);

        bool found = false;
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        // Gameplay has a HUDCam for layers 5/12 and no MainCamera tag. Using
        // Camera.main here made every sprite outline disappear.
        if (target == 3 && (inputViewers == null || inputViewers.Length == 0))
            inputViewers = UnityEngine.Object.FindObjectsOfType<InputViewer>();
        Transform? cameraTarget = target == 3 ? inputViewers?.FirstOrDefault(v => v != null)?.transform
            : game.scoreManager?.scoreFont?.transform;
        Camera? worldCamera = FindHudCamera(cameraTarget);

        switch (target)
        {
            case 2:
                var scoreManager = game.scoreManager;
                bool scoreAnchorsFound = false;
                if (scoreManager != null)
                {
                    scoreAnchorsFound |= AddResizeBounds(scoreManager.scoreFont?.transform.Find("Score_BG"), worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                    scoreAnchorsFound |= AddResizeBounds(scoreManager.comboFont?.transform.Find("combo_bg"), worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                }
                if (starProgresses == null || starProgresses.Length == 0)
                    starProgresses = UnityEngine.Object.FindObjectsOfType<StarProgress>();
                foreach (var progress in starProgresses)
                    if (progress != null)
                    {
                        scoreAnchorsFound |= AddResizeBounds(progress.transform.Find("StarProgressBG"), worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                        scoreAnchorsFound |= AddResizeBounds(progress.transform.Find("star_outer"), worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                    }
                if (songProgress == null || songProgress.Length == 0)
                    songProgress = UnityEngine.Object.FindObjectsOfType<SongProgress>();
                foreach (var progress in songProgress)
                    if (progress != null) scoreAnchorsFound |= AddResizeBounds(progress.transform.Find("SongProgressBG"), worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                // Fall back for a different HUD prefab, but keep the supplied
                // build's outline anchored to the graphics users actually see.
                if (!scoreAnchorsFound && scoreManager != null)
                {
                    AddResizeBounds(scoreManager.scoreFont?.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                    AddResizeBounds(scoreManager.comboFont?.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                    AddSpriteFontResizeBounds(scoreManager.scoreFont, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                    AddSpriteFontResizeBounds(scoreManager.comboFont, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                }
                break;
            case 3:
                if (inputViewers == null || inputViewers.Length == 0)
                    inputViewers = UnityEngine.Object.FindObjectsOfType<InputViewer>();
                foreach (var viewer in inputViewers)
                {
                    if (viewer == null) continue;
                    AddResizeBounds(viewer.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                }
                break;
            case 4:
                if (spBars == null || spBars.Length == 0)
                    spBars = UnityEngine.Object.FindObjectsOfType<SPBar>();
                foreach (var bar in spBars) if (bar != null) AddResizeBounds(bar.transform, worldCamera, ref found, ref minX, ref minY, ref maxX, ref maxY);
                break;
            default: rect = default; return false;
        }

        rect = found ? Rect.MinMaxRect(minX, minY, maxX, maxY) : default;
        return found && rect.width > 1 && rect.height > 1;
    }

    private static Camera? FindHudCamera(Transform? target)
    {
        for (var current = target; current != null; current = current.parent)
        {
            var camera = current.GetComponent<Camera>();
            if (camera != null && camera.enabled) return camera;
        }
        return Camera.main;
    }

    private bool AddResizeBounds(Transform? transform, Camera? worldCamera,
        ref bool found, ref float minX, ref float minY, ref float maxX, ref float maxY)
    {
        if (transform == null || !transform.gameObject.activeInHierarchy) return false;
        bool objectFound = false;
        float objectMinX = float.PositiveInfinity, objectMinY = float.PositiveInfinity;
        float objectMaxX = float.NegativeInfinity, objectMaxY = float.NegativeInfinity;
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
                IncludeResizePoint(resizeGraphicCorners[i], camera, ref objectFound, ref objectMinX, ref objectMinY, ref objectMaxX, ref objectMaxY);
        }

        // Clone Hero's gameplay HUD is built from SpriteRenderers, not UI
        // Graphics. Project all eight bounds corners through the game camera
        // so score, star, and song-progress controls get a selectable outline.
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || worldCamera == null) continue;
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
                IncludeResizePoint(resizeRendererCorners[i], worldCamera, ref objectFound, ref objectMinX, ref objectMinY, ref objectMaxX, ref objectMaxY);
        }
        if (!objectFound) return false;
        minX = Math.Min(minX, objectMinX); minY = Math.Min(minY, objectMinY);
        maxX = Math.Max(maxX, objectMaxX); maxY = Math.Max(maxY, objectMaxY);
        found = true;
        return true;
    }

    private void AddSpriteFontResizeBounds(SpriteFont? font, Camera? camera,
        ref bool found, ref float minX, ref float minY, ref float maxX, ref float maxY)
    {
        var sprites = font?.Sprites;
        if (sprites == null) return;
        foreach (var sprite in sprites)
            if (sprite != null) AddResizeBounds(sprite.transform, camera, ref found, ref minX, ref minY, ref maxX, ref maxY);
    }

    private static void IncludeResizePoint(Vector3 world, Camera? camera,
        ref bool found, ref float minX, ref float minY, ref float maxX, ref float maxY)
    {
        Vector3 point = camera != null ? camera.WorldToScreenPoint(world) : world;
        if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z)) return;
        if (camera != null && point.z < camera.nearClipPlane) return;
        float x = point.x, y = Screen.height - point.y;
        float marginX = Math.Max(64, Screen.width * .15f), marginY = Math.Max(64, Screen.height * .15f);
        if (x < -marginX || x > Screen.width + marginX || y < -marginY || y > Screen.height + marginY) return;
        x = Math.Clamp(x, 0, Screen.width);
        y = Math.Clamp(y, 0, Screen.height);
        minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
        minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
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

    private static (RectTransform? Transform, TMP_Text[] TmpTexts, Text[] LegacyTexts) FindFpsHud(GameObject? fpsObject)
    {
        if (fpsObject == null) return (null, Array.Empty<TMP_Text>(), Array.Empty<Text>());
        var rect = fpsObject.GetComponent<RectTransform>();
        var tmp = fpsObject.GetComponent<TextMeshProUGUI>();
        if (tmp != null) return (rect, new[] { tmp }, Array.Empty<Text>());
        var legacy = fpsObject.GetComponent<Text>();
        return (rect, Array.Empty<TMP_Text>(), legacy == null ? Array.Empty<Text>() : new[] { legacy });
    }

    private static bool SameTextTargets<T>(T[] current, T[] next) where T : UnityEngine.Object
    {
        if (current.Length != next.Length) return false;
        for (int i = 0; i < current.Length; i++)
            if (current[i] == null || next[i] == null || current[i].Pointer != next[i].Pointer) return false;
        return true;
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
        for (int i = 0; i < fpsTmpTexts.Length; i++)
        {
            var text = fpsTmpTexts[i];
            if (text == null) continue;
            // FrameRate writes the FPS and simulated FPS into one TMP label.
            // Tinting TMP_Text.color affects the slash and both numbers. Rich
            // text scopes the color to the first number instead.
            var current = text.text ?? "";
            var plain = current == fpsStyledText ? fpsPlainText : current;
            if (enabled)
            {
                var styled = TintFirstNumber(plain, color);
                if (current != styled) text.text = styled;
                fpsPlainText = plain;
                fpsStyledText = styled;
            }
            else
            {
                if (current != plain) text.text = plain;
                fpsPlainText = fpsStyledText = "";
            }
        }
        for (int i = 0; i < fpsLegacyTexts.Length; i++)
        {
            var text = fpsLegacyTexts[i];
            if (text == null) continue;
            var current = text.color;
            var target = enabled ? color : fpsLegacyOriginalColors[i];
            target.a = current.a;
            if (current != target) text.color = target;
        }
    }

    private static string TintFirstNumber(string value, Color color)
    {
        int start = 0;
        while (start < value.Length)
        {
            // The game's FPS label may already contain TMP color tags; hex
            // digits in a tag are not part of the visible FPS number.
            if (value[start] == '<')
            {
                int tagEnd = value.IndexOf('>', start + 1);
                if (tagEnd >= 0) { start = tagEnd + 1; continue; }
            }
            if (char.IsDigit(value[start])) break;
            start++;
        }
        if (start == value.Length) return value;
        int end = start;
        while (end < value.Length && (char.IsDigit(value[end]) || value[end] == '.' || value[end] == ',')) end++;
        int r = Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255);
        int g = Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255);
        int b = Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255);
        return value[..start] + $"<color=#{r:X2}{g:X2}{b:X2}>" + value[start..end] + "</color>" + value[end..];
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
        for (int i = 0; i < fpsTmpTexts.Length && i < fpsTmpOriginalColors.Length; i++)
            if (fpsTmpTexts[i] != null)
            {
                if (fpsTmpTexts[i].text == fpsStyledText) fpsTmpTexts[i].text = fpsPlainText;
                fpsTmpTexts[i].color = fpsTmpOriginalColors[i];
            }
        fpsPlainText = fpsStyledText = "";
        for (int i = 0; i < fpsLegacyTexts.Length && i < fpsLegacyOriginalColors.Length; i++)
            if (fpsLegacyTexts[i] != null) fpsLegacyTexts[i].color = fpsLegacyOriginalColors[i];
        fpsRect = null;
        fpsPointer = IntPtr.Zero;
        fpsTmpTexts = Array.Empty<TMP_Text>();
        fpsLegacyTexts = Array.Empty<Text>();
        fpsTmpOriginalColors = Array.Empty<Color>();
        fpsLegacyOriginalColors = Array.Empty<Color>();
        lastFpsX = lastFpsY = float.NaN;
        lastFpsTint = false;
    }
}
