using System.Globalization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using SlopHero.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

namespace SlopHero.Mod;

public sealed partial class Core
{
    private static readonly string[] SettingsPages = { "General", "Counters", "HUD", "Visuals", "Colors", "Profiles", "Performance" };
    private MelonPreferences_Entry<bool> showMisses = null!, showHit = null!, showGhosts = null!, showBest = null!;
    private MelonPreferences_Entry<bool> showSong = null!, showTitle = null!, showArtist = null!, showCharter = null!, showSpeed = null!;
    private MelonPreferences_Entry<int> songX = null!, songY = null!, songFont = null!;
    private MelonPreferences_Entry<float> practiceHudX = null!, practiceHudY = null!, fpsHudX = null!, fpsHudY = null!;
    private MelonPreferences_Entry<float> songOpacity = null!;
    private MelonPreferences_Entry<string> songColor = null!, fcColor = null!, pfcColor = null!, starColor = null!, progressColor = null!, scoreColor = null!, songProgressColor = null!, menuAccentHex = null!;
    private MelonPreferences_Entry<bool> hideSides = null!, hideStrings = null!, hideBeats = null!, tintFc = null!, tintScore = null!, lockSpeed = null!;
    private MelonPreferences_Entry<bool> pauseHiddenBeatlineUpdates = null!;
    private MelonPreferences_Entry<bool> disableAprilFools = null!;
    private MelonPreferences_Entry<bool> customizeFpsColor = null!;
    private MelonPreferences_Entry<bool> disableHitParticles = null!, disableSustainParticles = null!, disableFretEffects = null!;
    private MelonPreferences_Entry<bool> disableStarProgressParticles = null!;
    private MelonPreferences_Entry<bool> stopMidiInput = null!, disableMidiForSession = null!, disableDiscordPresence = null!, suspendHiddenLeaderboard = null!, useCanvasCounterHud = null!, freezeComboSmokeScroll = null!;
    private MelonPreferences_Entry<bool> skipGameplayMouseUi = null!;
    private MelonPreferences_Entry<bool> skipLegacyMouseEvents = null!;
    private MelonPreferences_Entry<bool> throttleWhammyTexture = null!;
    private MelonPreferences_Entry<bool> pauseGameplayCheatScanner = null!;
    private MelonPreferences_Entry<bool> pauseGameplayUiProcessing = null!;
    private MelonPreferences_Entry<bool> pauseHighwayAnimations = null!;
    private MelonPreferences_Entry<bool> reduceUnityJobWorkers = null!;
    private MelonPreferences_Entry<bool> staticTmpScaleDuringSong = null!;
    private MelonPreferences_Entry<bool> pauseRewiredMousePolling = null!;
    private MelonPreferences_Entry<bool> pauseRewiredKeyboardPolling = null!;
    private MelonPreferences_Entry<bool> limitSlopHeroShortcutPolling = null!;
    private MelonPreferences_Entry<bool> skipBackgroundCamera = null!, limitPlayerCameraFarClip = null!;
    private MelonPreferences_Entry<int> farClipDistance = null!;
    private MelonPreferences_Entry<bool> hidePbPopup = null!, hideStarPowerSideGlows = null!;
    private MelonPreferences_Entry<bool> preserveNetworkTickRemainder = null!;
    private MelonPreferences_Entry<bool> disableHudCameraBenchmark = null!;
    private MelonPreferences_Entry<bool> disableGuitarCameraBenchmark = null!;
    private MelonPreferences_Entry<bool> hideHeldSustainGlow = null!;
    private MelonPreferences_Entry<bool> renderEveryUpdate = null!;
    private MelonPreferences_Entry<string> fpsTextColor = null!;
    private MelonPreferences_Entry<float> settingsMenuScale = null!, settingsWindowOffsetX = null!, settingsWindowOffsetY = null!;
    private MelonPreferences_Entry<float> scoreHudScale = null!, inputViewerScale = null!, starPowerHudScale = null!, practiceHudScale = null!, fpsHudScale = null!;
    private readonly VisualOverrides visuals = new();
    private readonly RingColors rings = new();
    private readonly PerformanceOverrides performance = new();
    private readonly AprilFoolsOverrides aprilFools = new();
    private readonly OptionalServiceOverrides optionalServices = new();
    private readonly HiddenLeaderboardOverride hiddenLeaderboard = new();
    private readonly ComboSmokeScrollOverride comboSmokeScroll = new();
    private readonly MouseUiGameplayOverride mouseUiGameplay = new();
    private readonly LegacyMouseEventsDetour legacyMouseEvents = new();
    private readonly WhammyTextureThrottle whammyTextureThrottle = new();
    private readonly StarProgressParticleOverride starProgressParticles = new();
    private readonly CheatScannerOverride cheatScanner = new();
    private readonly GameplayUiEventOverride gameplayUiEvents = new();
    private readonly HighwayAnimationOverride highwayAnimations = new();
    private readonly UnityJobWorkerLimit unityJobWorkerLimit = new();
    private readonly StaticTmpScaleOverride staticTmpScale = new();
    private readonly RewiredMousePollingOverride rewiredMousePolling = new();
    private readonly RewiredKeyboardPollingOverride rewiredKeyboardPolling = new();
    private readonly BackgroundCameraOverride backgroundCamera = new();
    private readonly PlayerCameraFarClipOverride playerCameraFarClip = new();
    private readonly HudCameraBenchmarkOverride hudCameraBenchmark = new();
    private readonly GuitarCameraBenchmarkOverride guitarCameraBenchmark = new();
    private readonly PlainHeldSustainPrototype plainHeldSustains = new();
    private readonly RenderEveryUpdatePrototype renderEveryUpdatePrototype = new();
    private readonly NetworkTickRemainderOverride networkTickRemainder = new();
    private readonly OptionalHudAnimationsOverride optionalHudAnimations = new();
    private readonly RuntimeTimings timings = new();
    private string songText = "", editText = "", editLabel = "", editError = "";
    private Action<string>? editApply;
    private bool replaceEdit, moveSong, editColor;
    private Color editPreview = Color.white;
    private GUIStyle? colorPreviewStyle;
    private GUIStyle? menuNavStyle, menuHeaderStyle, menuPageTitleStyle, menuSubtitleStyle;
    private GUIStyle? menuFlatStyle, menuClickStyle, menuActionStyle, menuToggleLabelStyle, menuToggleCheckStyle, menuArtworkStyle, sectionHeaderStyle;
    private GUIStyle? menuPopupHeaderStyle, menuPopupTextStyle, menuPopupValueStyle, menuPopupButtonStyle;
    private Texture2D? menuArtworkTexture;
    private bool menuArtworkLoadAttempted;
    private int settingsPage;
    private bool settingsWindowDragging;
    private Vector2 settingsWindowDragMouse, settingsWindowDragOrigin;
    private Vector2 settingsScrollPosition;
    private bool profileDropdownOpen;
    private int profileListStart;
    private readonly Dictionary<string, Color> parsedColors = new();
    private static readonly string[] ResizeTargetLabels = { "Counters", "Song HUD", "Score box", "Input viewer", "Star power", "Practice stats", "FPS display" };
    private int resizeSelection;
    private int resizeTarget = -1, resizeHandle = -1;
    private bool resizeMoveDragging;
    private float resizeStartValue, resizeStartRadius;
    private Vector2 resizeDragCenter, resizeStartMouse;
    private Vector2 resizeDragOffset;
    private Rect cachedResizeRect;
    private int cachedResizeRectTarget = -1;
    private double nextResizeRectProbe;
    private bool cachedResizeRectValid;
    private GUIStyle? songStyle;
    private int styledSongFont = -1;
    private Color styledSongColor;
    private string styledSongHex = "";
    private string? measuredSongText;
    private int measuredSongFont = -1, measuredSongWidth, measuredSongHeight;

    private void InitializeFeatures()
    {
        showMisses = preferences.CreateEntry("ShowMisses", true);
        showHit = preferences.CreateEntry("ShowHitPercentage", true);
        showGhosts = preferences.CreateEntry("ShowGhosts", true);
        showBest = preferences.CreateEntry("ShowBestStreak", true);
        showSong = preferences.CreateEntry("ShowSongHud", true);
        showTitle = preferences.CreateEntry("ShowSongTitle", true);
        showArtist = preferences.CreateEntry("ShowArtist", true);
        showCharter = preferences.CreateEntry("ShowCharter", true);
        showSpeed = preferences.CreateEntry("ShowChartSpeed", true);
        songX = preferences.CreateEntry("SongHudX", 400);
        songY = preferences.CreateEntry("SongHudY", 110);
        songFont = preferences.CreateEntry("SongFontSize", 22);
        songOpacity = preferences.CreateEntry("SongBackgroundOpacity", .65f);
        songColor = preferences.CreateEntry("SongTextColor", "FFFFFF");
        practiceHudX = preferences.CreateEntry("PracticeHudOffsetX", 0f);
        practiceHudY = preferences.CreateEntry("PracticeHudOffsetY", 0f);
        fpsHudX = preferences.CreateEntry("FpsHudOffsetX", 0f);
        fpsHudY = preferences.CreateEntry("FpsHudOffsetY", 0f);
        customizeFpsColor = preferences.CreateEntry("CustomizeFpsColor", false);
        fpsTextColor = preferences.CreateEntry("FpsTextColor", "FFFFFF");
        settingsMenuScale = preferences.CreateEntry("SettingsMenuScale", 1.12f);
        settingsWindowOffsetX = preferences.CreateEntry("SettingsWindowOffsetX", 0f);
        settingsWindowOffsetY = preferences.CreateEntry("SettingsWindowOffsetY", 0f);
        scoreHudScale = preferences.CreateEntry("ScoreHudScale", 1f);
        inputViewerScale = preferences.CreateEntry("InputViewerScale", 1f);
        starPowerHudScale = preferences.CreateEntry("StarPowerHudScale", 1f);
        var legacyStarProgressHudScale = preferences.CreateEntry("StarProgressHudScale", 1f);
        var legacySongProgressHudScale = preferences.CreateEntry("SongProgressHudScale", 1f);
        if (scoreHudScale.Value == 1f)
            scoreHudScale.Value = legacyStarProgressHudScale.Value != 1f
                ? legacyStarProgressHudScale.Value : legacySongProgressHudScale.Value;
        if (legacyStarProgressHudScale.Value != 1f || legacySongProgressHudScale.Value != 1f)
        {
            legacyStarProgressHudScale.Value = 1f;
            legacySongProgressHudScale.Value = 1f;
            preferences.SaveToFile();
        }
        practiceHudScale = preferences.CreateEntry("PracticeHudScale", 1f);
        fpsHudScale = preferences.CreateEntry("FpsHudScale", 1f);
        hideSides = preferences.CreateEntry("HideHighwaySides", false);
        hideStrings = preferences.CreateEntry("HideFretStrings", false);
        hideBeats = preferences.CreateEntry("HideBeatlines", false);
        pauseHiddenBeatlineUpdates = preferences.CreateEntry("PerformancePauseHiddenBeatlineUpdates", false);
        disableAprilFools = preferences.CreateEntry("DisableAprilFoolsContent", true);
        tintFc = preferences.CreateEntry("CustomizeFcRings", false);
        tintScore = preferences.CreateEntry("CustomizeScoreColors", false);
        lockSpeed = preferences.CreateEntry("ConstantPracticeNoteSpeed", false);
        disableHitParticles = preferences.CreateEntry("PerformanceDisableHitParticles", false);
        disableSustainParticles = preferences.CreateEntry("PerformanceDisableSustainParticles", false);
        disableFretEffects = preferences.CreateEntry("PerformanceDisableFretEffects", false);
        disableStarProgressParticles = preferences.CreateEntry("PerformanceDisableStarProgressParticles", false);
        stopMidiInput = preferences.CreateEntry("PerformanceStopMidiInput", false);
        disableMidiForSession = preferences.CreateEntry("PerformanceDisableMidiForSession", false);
        disableDiscordPresence = preferences.CreateEntry("PerformanceDisableDiscordPresence", false);
        suspendHiddenLeaderboard = preferences.CreateEntry("PerformanceSuspendHiddenLeaderboard", false);
        useCanvasCounterHud = preferences.CreateEntry("PerformanceCanvasCounterHud", false);
        freezeComboSmokeScroll = preferences.CreateEntry("PerformanceFreezeComboSmokeScroll", false);
        skipGameplayMouseUi = preferences.CreateEntry("PerformanceSkipGameplayMouseUi", false);
        skipLegacyMouseEvents = preferences.CreateEntry("PerformanceSkipLegacyMouseEvents", false);
        throttleWhammyTexture = preferences.CreateEntry("PerformanceThrottleWhammyTexture", false);
        pauseGameplayCheatScanner = preferences.CreateEntry("PerformancePauseGameplayCheatScanner", false);
        pauseGameplayUiProcessing = preferences.CreateEntry("PerformancePauseGameplayUiProcessing", false);
        pauseHighwayAnimations = preferences.CreateEntry("PerformancePauseHighwayAnimations", false);
        reduceUnityJobWorkers = preferences.CreateEntry("PerformanceReduceUnityJobWorkers", false);
        staticTmpScaleDuringSong = preferences.CreateEntry("PerformanceStaticTmpScaleDuringSong", false);
        pauseRewiredMousePolling = preferences.CreateEntry("PerformancePauseRewiredMousePolling", false);
        pauseRewiredKeyboardPolling = preferences.CreateEntry("PerformancePauseRewiredKeyboardPolling", false);
        limitSlopHeroShortcutPolling = preferences.CreateEntry("PerformanceLimitSlopHeroShortcutPolling", false);
        skipBackgroundCamera = preferences.CreateEntry("PerformanceSkipBackgroundCamera", false);
        limitPlayerCameraFarClip = preferences.CreateEntry("PerformanceLimitPlayerCameraFarClip", false);
        farClipDistance = preferences.CreateEntry("PerformanceGuitarCameraFarClip", 80);
        disableHudCameraBenchmark = preferences.CreateEntry("PerformanceDisableHudCameraBenchmark", false);
        disableGuitarCameraBenchmark = preferences.CreateEntry("PerformanceDisableGuitarCameraBenchmark", false);
        // The shader has no instancing variant, and overriding its batching
        // tag can warp the object's per-tail vertex sizing. Clear saved values
        // from old builds; old profile keys are no longer imported.
        var unsafeLegacyBatching = preferences.CreateEntry("PerformanceBatchSustainBodies", false);
        if (unsafeLegacyBatching.Value)
        {
            unsafeLegacyBatching.Value = false;
            preferences.SaveToFile();
        }
        hideHeldSustainGlow = preferences.CreateEntry("PerformanceHideHeldSustainGlow", false);
        renderEveryUpdate = preferences.CreateEntry("PerformanceRenderEveryUpdateSolo", false);
        // Retire experiments that added global setter/camera work, never drew
        // a batch, or found no eligible material in the user's Windows runs.
        // Ignore their old profile keys below so imports cannot revive them.
        var retiredWriteCache = preferences.CreateEntry("PerformanceCacheStaticSustainWrites", false);
        var retiredGeometry = preferences.CreateEntry("PerformanceBatchSustainGeometry", false);
        var retiredAlpha = preferences.CreateEntry("PerformanceSkipUnusedSpriteAlpha", false);
        var retiredHudMerge = preferences.CreateEntry("PerformanceMergeHudCameraNative", false);
        var recovery123Applied = preferences.CreateEntry("PerformanceRecovery123Applied", false);
        if (retiredWriteCache.Value || retiredGeometry.Value || retiredAlpha.Value
            || retiredHudMerge.Value || !recovery123Applied.Value)
        {
            retiredWriteCache.Value = retiredGeometry.Value = retiredAlpha.Value = retiredHudMerge.Value = false;
            if (!recovery123Applied.Value)
            {
                renderEveryUpdate.Value = false;
                reduceUnityJobWorkers.Value = false;
                staticTmpScaleDuringSong.Value = false;
            }
            recovery123Applied.Value = true;
            preferences.SaveToFile();
        }
        hidePbPopup = preferences.CreateEntry("PerformanceHidePbPopup", false);
        hideStarPowerSideGlows = preferences.CreateEntry("PerformanceHideStarPowerSideGlows", false);
        preserveNetworkTickRemainder = preferences.CreateEntry("PerformancePreserveNetworkTickRemainder", false);
        var legacyFreezeLyricsScroll = preferences.CreateEntry("PerformanceFreezeLyricsBackgroundScroll", false);
        if (legacyFreezeLyricsScroll.Value)
        {
            // 0.4.53 exposed this switch but searched the wrong hierarchy. Carry
            // an explicitly enabled setting forward to its corrected target.
            if (!freezeComboSmokeScroll.Value) freezeComboSmokeScroll.Value = true;
            legacyFreezeLyricsScroll.Value = false;
            preferences.SaveToFile();
        }
        fcColor = preferences.CreateEntry("FcColor", "FFD700");
        pfcColor = preferences.CreateEntry("PerfectFcColor", "00FFFF");
        songProgressColor = preferences.CreateEntry("SongProgressColor", "00FFFF");
        starColor = preferences.CreateEntry("StarColor", "FFD700");
        progressColor = preferences.CreateEntry("ProgressColor", "00FFFF");
        scoreColor = preferences.CreateEntry("ScoreTextColor", "FFFFFF");
        InitializeSongSpeedMenu();
        menuAccentHex = preferences.CreateEntry("MenuAccentColor", "A000FF");
    }

    private void DrawSettings()
        => DrawSettingsPage();

    private void DrawSettingsPage()
    {
        if (editApply != null) { DrawValueEditor(); return; }
        float scale = Math.Clamp(settingsMenuScale.Value, .85f, 1.4f);
        Matrix4x4 previousMatrix = GUI.matrix;
        float centerX = Screen.width * .5f, centerY = Screen.height * .5f;
        scale = Math.Min(scale, Math.Min(Math.Max(.5f, (Screen.width - 20f) / 1200f), Math.Max(.5f, (Screen.height - 20f) / 740f)));
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(centerX, centerY));
        try { DrawScaledSettingsPage(scale); }
        finally { GUI.matrix = previousMatrix; }
    }

    private void DrawScaledSettingsPage(float scale)
    {
        // Store the offset in physical pixels, then divide by the GUI scale
        // so the visible window follows the mouse at any menu size.
        const float menuWidth = 1200, menuHeight = 740, sidebarWidth = 184;
        ClampSettingsWindowOffset(scale, menuWidth, menuHeight);
        float frameX = (Screen.width - menuWidth) / 2f + settingsWindowOffsetX.Value / scale;
        float frameY = (Screen.height - menuHeight) / 2f + settingsWindowOffsetY.Value / scale;
        HandleSettingsWindowDrag(scale, menuWidth, menuHeight, ref frameX, ref frameY);
        Color previousColor = GUI.color, previousBackground = GUI.backgroundColor;
        try
        {
            if (menuFlatStyle == null)
            {
                menuFlatStyle = new GUIStyle(GUI.skin.box);
                menuFlatStyle.normal.background = menuFlatStyle.hover.background =
                    menuFlatStyle.active.background = menuFlatStyle.focused.background = Texture2D.whiteTexture;
            }
            GUI.color = Color.white;
            GUI.backgroundColor = new Color(.035f, .04f, .055f, 1);
            GUI.Box(new Rect(frameX, frameY, menuWidth, menuHeight), "", menuFlatStyle);
            GUI.backgroundColor = new Color(.025f, .03f, .043f, 1);
            GUI.Box(new Rect(frameX, frameY, sidebarWidth, menuHeight), "", menuFlatStyle);
            GUI.backgroundColor = new Color(.075f, .08f, .10f, 1);
            GUI.Box(new Rect(frameX + sidebarWidth, frameY, menuWidth - sidebarWidth, menuHeight), "", menuFlatStyle);
            GUI.backgroundColor = new Color(.045f, .05f, .065f, 1);
            GUI.Box(new Rect(frameX, frameY, menuWidth, 58), "", menuFlatStyle);

            menuHeaderStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            menuHeaderStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(frameX + 20, frameY + 8, 250, 28), "SlopHero", menuHeaderStyle);
            menuSubtitleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
            menuSubtitleStyle.normal.textColor = new Color(.54f, .57f, .65f, 1);
            GUI.Label(new Rect(frameX + 20, frameY + 33, 360, 18), "MOD SETTINGS  ·  v0.4.123", menuSubtitleStyle);
            GUI.Label(new Rect(frameX + menuWidth - 184, frameY + 20, 118, 18), "Drag header to move", menuSubtitleStyle);
            menuClickStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            menuClickStyle.normal.textColor = Color.white;
            if (GUI.Button(new Rect(frameX + menuWidth - 48, frameY + 13, 32, 32), "×", menuClickStyle))
            {
                settingsOpen = false;
                SavePreferences();
                UpdateCursor();
            }

            menuNavStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            menuNavStyle.normal.textColor = Color.white;
            menuNavStyle.hover.textColor = Color.white;
            menuNavStyle.normal.background = menuNavStyle.hover.background =
                menuNavStyle.active.background = menuNavStyle.focused.background = Texture2D.whiteTexture;
            menuPageTitleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            menuPageTitleStyle.normal.textColor = Color.white;

            const float navTop = 76, navRowHeight = 40;
            for (int i = 0; i < SettingsPages.Length; i++)
            {
                var rect = new Rect(frameX + 10, frameY + navTop + i * navRowHeight, sidebarWidth - 20, navRowHeight - 4);
                bool selected = settingsPage == i;
                bool hovered = rect.Contains(Event.current.mousePosition);
                Color accent = MenuAccentColor();
                GUI.backgroundColor = selected
                    ? new Color(accent.r * .34f, accent.g * .34f, accent.b * .34f, 1)
                    : hovered ? new Color(.11f, .12f, .16f, 1) : new Color(.045f, .05f, .065f, 1);
                if (GUI.Button(rect, SettingsPages[i], menuNavStyle) && settingsPage != i)
                {
                    settingsPage = i;
                    settingsScrollPosition = Vector2.zero;
                }
                if (selected)
                {
                    GUI.backgroundColor = accent;
                    GUI.Box(new Rect(rect.x, rect.y + 8, 1, rect.height - 16), "", menuFlatStyle);
                }
            }

            GUI.backgroundColor = MenuAccentColor();
            GUI.Box(new Rect(frameX, frameY, menuWidth, 3), "", menuFlatStyle);
            GUI.backgroundColor = new Color(.075f, .08f, .10f, 1);
            GUI.Box(new Rect(frameX + sidebarWidth + 12, frameY + 70, menuWidth - sidebarWidth - 24, menuHeight - 82), "", menuFlatStyle);
            // Use the character as a faint watermark beneath the page controls,
            // rather than a separate sticker in an empty side column.
            const float artworkWidth = 332, artworkHeight = 542, artworkRightMargin = 22, artworkBottomMargin = 10;
            DrawMenuArtwork(new Rect(frameX + menuWidth - artworkWidth - artworkRightMargin,
                frameY + menuHeight - artworkHeight - artworkBottomMargin, artworkWidth, artworkHeight));
            GUI.backgroundColor = Color.white;
            GUI.Label(new Rect(frameX + sidebarWidth + 30, frameY + 76, 632, 30), SettingsPages[settingsPage], menuPageTitleStyle);
            menuSubtitleStyle.normal.textColor = new Color(.57f, .60f, .68f, 1);
            GUI.Label(new Rect(frameX + sidebarWidth + 30, frameY + 106, 632, 20), "Pause to edit · Changes save automatically · Esc closes");
        }
        finally
        {
            GUI.color = previousColor;
            GUI.backgroundColor = previousBackground;
        }
        float contentHeight = settingsPage switch { 1 => 540, 2 => 1480, 3 => 540, 6 => 2100, _ => 500 };
        Rect viewport = new(frameX + sidebarWidth + 12, frameY + 132, 680, menuHeight - 146);
        Rect content = new(0, 0, 656, contentHeight);
        settingsScrollPosition = GUI.BeginScrollView(viewport, settingsScrollPosition, content);
        float x = 14, y = 8;
        Color controlBackground = GUI.backgroundColor, controlColor = GUI.contentColor;
        try
        {
            GUI.backgroundColor = new Color(.14f, .15f, .19f, 1);
            GUI.contentColor = new Color(.92f, .93f, .97f, 1);
            switch (settingsPage)
            {
                case 0: DrawGeneralSettings(x, y); break;
                case 1: DrawCounterSettings(x, y); break;
                case 2: DrawHudSettings(x, y); break;
                case 3: DrawVisualSettings(x, y); break;
                case 4: DrawColorSettings(x, y); break;
                case 5: DrawProfiles(x, y); break;
                case 6: DrawPerformanceSettings(x, y); break;
            }
        }
        finally
        {
            GUI.backgroundColor = controlBackground;
            GUI.contentColor = controlColor;
            GUI.EndScrollView();
        }
        if (editApply != null) DrawValueEditor();
    }

    private void ClampSettingsWindowOffset(float scale, float width, float height)
    {
        float maxX = Math.Max(0, (Screen.width - width * scale) * .5f - 10);
        float maxY = Math.Max(0, (Screen.height - height * scale) * .5f - 10);
        float x = float.IsFinite(settingsWindowOffsetX.Value)
            ? Math.Clamp(settingsWindowOffsetX.Value, -maxX, maxX) : 0;
        float y = float.IsFinite(settingsWindowOffsetY.Value)
            ? Math.Clamp(settingsWindowOffsetY.Value, -maxY, maxY) : 0;
        if (x == settingsWindowOffsetX.Value && y == settingsWindowOffsetY.Value) return;
        settingsWindowOffsetX.Value = x;
        settingsWindowOffsetY.Value = y;
        preferencesDirty = true;
    }

    private void HandleSettingsWindowDrag(float scale, float width, float height, ref float frameX, ref float frameY)
    {
        var e = Event.current;
        if (!Input.GetMouseButton(0) && e.rawType != EventType.MouseUp && e.type != EventType.MouseDown)
            settingsWindowDragging = false;
        // Undo the menu's scale for hit testing. Track movement in physical
        // screen pixels so a dragged header follows the mouse at any scale.
        Vector3 localMouse = GUI.matrix.inverse.MultiplyPoint3x4(
            new Vector3(e.mousePosition.x, e.mousePosition.y, 0));
        var header = new Rect(frameX, frameY, width - 62, 58);
        Vector2 screenMouse = new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        if (e.type == EventType.MouseDown && e.button == 0 && header.Contains(localMouse))
        {
            settingsWindowDragging = true;
            settingsWindowDragMouse = screenMouse;
            settingsWindowDragOrigin = new Vector2(settingsWindowOffsetX.Value, settingsWindowOffsetY.Value);
            e.Use();
        }
        if (settingsWindowDragging && e.type == EventType.MouseDrag)
        {
            Vector2 delta = screenMouse - settingsWindowDragMouse;
            settingsWindowOffsetX.Value = settingsWindowDragOrigin.x + delta.x;
            settingsWindowOffsetY.Value = settingsWindowDragOrigin.y + delta.y;
            ClampSettingsWindowOffset(scale, width, height);
            frameX = (Screen.width - width) * .5f + settingsWindowOffsetX.Value / scale;
            frameY = (Screen.height - height) * .5f + settingsWindowOffsetY.Value / scale;
            preferencesDirty = true;
            e.Use();
        }
        if (settingsWindowDragging && e.rawType == EventType.MouseUp)
        {
            settingsWindowDragging = false;
            SavePreferences();
        }
    }

    private void DrawMenuArtwork(Rect rect)
    {
        if (!menuArtworkLoadAttempted)
        {
            menuArtworkLoadAttempted = true;
            Texture2D? texture = null;
            try
            {
                using var stream = typeof(Core).Assembly.GetManifestResourceStream("SlopHero.Mod.Assets.boymoder.png")
                    ?? throw new InvalidOperationException("Embedded menu illustration is missing.");
                byte[] bytes = new byte[checked((int)stream.Length)];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0) throw new EndOfStreamException("Menu illustration resource ended early.");
                    offset += read;
                }
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, new Il2CppStructArray<byte>(bytes), true))
                    throw new InvalidDataException("Unity could not decode the menu illustration PNG.");
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.hideFlags = HideFlags.HideAndDontSave;
                menuArtworkTexture = texture;
            }
            catch (Exception ex)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                LoggerInstance.Warning($"Settings illustration could not load; the menu remains usable: {ex.Message}");
            }
        }

        if (menuArtworkTexture == null) return;
        Color previous = GUI.color, previousBackground = GUI.backgroundColor;
        try
        {
            menuArtworkStyle ??= new GUIStyle(GUI.skin.label);
            menuArtworkStyle.normal.background = menuArtworkTexture;
            menuArtworkStyle.hover.background = menuArtworkTexture;
            menuArtworkStyle.active.background = menuArtworkTexture;
            menuArtworkStyle.focused.background = menuArtworkTexture;
            // Keep the illustration present without competing with readable
            // controls or the selected accent color.
            GUI.color = new Color(.88f, .90f, .96f, .42f);
            GUI.backgroundColor = Color.white;
            GUI.Box(rect, "", menuArtworkStyle);
        }
        finally { GUI.color = previous; GUI.backgroundColor = previousBackground; }
    }

    private void ClearMenuArtwork()
    {
        if (menuArtworkTexture != null) UnityEngine.Object.Destroy(menuArtworkTexture);
        menuArtworkTexture = null;
        menuArtworkStyle = null;
    }

    private void DrawGeneralSettings(float x, float y)
    {
        SectionHeader("Controls", x, y);
        if (GUI.Button(new Rect(x, y + 30, 632, 36), $"Restart: {restartButton.Value} — bind")) capture = "restart";
        if (GUI.Button(new Rect(x, y + 74, 632, 36), $"Practice: {practiceButton.Value} — bind")) capture = "practice";
        Number("Hold duration (seconds)", (float)Duration, x, y + 120, .25f, 3, v => holdSeconds.Value = v);
        Number("Settings menu size", settingsMenuScale.Value, x, y + 164, .85f, 1.4f, v => { settingsMenuScale.Value = v; preferencesDirty = true; });
        GUI.Label(new Rect(x, y + 208, 632, 54), capture == null
            ? "Song actions apply to solo Quickplay. Practice begins at the start of your current named section."
            : $"Press a key or controller button for {capture}.");
        Number("Song speed step (%)", songSpeedStep.Value, x, y + 270, 1, 500,
            v => songSpeedStep.Value = SongSpeedControl.ClampStep((int)Math.Round(v)));
        bool modTimingClicked, cameraTimingClicked = false;
        if (timings.Active)
            modTimingClicked = GUI.Button(new Rect(x, y + 314, 540, 36), "Finish timing report");
        else
        {
            modTimingClicked = GUI.Button(new Rect(x, y + 314, 265, 36), "Mod timing (30 seconds)");
            cameraTimingClicked = GUI.Button(new Rect(x + 275, y + 314, 265, 36), "Camera timing (30 seconds)");
        }
        if (modTimingClicked || cameraTimingClicked)
        {
            if (timings.Active) timings.Finish();
            else if (gameplayInitialized)
            {
                bool nativeHudTweaks = practiceHudX.Value != 0 || practiceHudY.Value != 0 || fpsHudX.Value != 0 || fpsHudY.Value != 0 || customizeFpsColor.Value
                    || scoreHudScale.Value != 1 || inputViewerScale.Value != 1 || starPowerHudScale.Value != 1
                    || practiceHudScale.Value != 1 || fpsHudScale.Value != 1;
                SongSpeedControl.TryRead(out int speedSetting);
                LoggerInstance.Msg($"Timing settings: counters={showHud.Value}[miss={showMisses.Value},hit={showHit.Value},ghost={showGhosts.Value},streak={showBest.Value}]@{hudX.Value},{hudY.Value}, songHud={showSong.Value}[title={showTitle.Value},artist={showArtist.Value},charter={showCharter.Value},speed={showSpeed.Value}]@{songX.Value},{songY.Value}, canvasCounterHud={useCanvasCounterHud.Value}, nativeOffsets(practice/FPS)={practiceHudX.Value:0.##},{practiceHudY.Value:0.##}/{fpsHudX.Value:0.##},{fpsHudY.Value:0.##}, FCcolors={tintFc.Value}, scoreColors={tintScore.Value}, constantSpeed={lockSpeed.Value}, hideSides={hideSides.Value}, hideStrings={hideStrings.Value}, hideBeatlines={hideBeats.Value}, pauseHiddenBeatlineUpdates={pauseHiddenBeatlineUpdates.Value}, disableAprilFools={disableAprilFools.Value}, perfEffects(hit/sustain/fret/starProgress)={disableHitParticles.Value}/{disableSustainParticles.Value}/{disableFretEffects.Value}/{disableStarProgressParticles.Value}[starSuppressed={starProgressParticles.IsSuppressed}], pausedFretFadeScripts={performance.PausedFretFadeCount}, blackBackground={skipBackgroundCamera.Value}[active={backgroundCamera.IsActive}], hudCameraBenchmark={disableHudCameraBenchmark.Value}[active={hudCameraBenchmark.IsActive}], guitarCameraBenchmark={disableGuitarCameraBenchmark.Value}[active={guitarCameraBenchmark.IsActive}], guitarFarClip={limitPlayerCameraFarClip.Value}[active={playerCameraFarClip.IsActive}], hiddenLeaderboard={suspendHiddenLeaderboard.Value}[canvasSuspended={hiddenLeaderboard.CanvasSuspended}], freezeComboSmokeScroll={freezeComboSmokeScroll.Value}, comboSmokeTracked/paused={comboSmokeScroll.TrackedCount}/{comboSmokeScroll.PausedCount}, MIDI(discovery/session)={stopMidiInput.Value}/{disableMidiForSession.Value}, Discord={disableDiscordPresence.Value}, cheatScanner={pauseGameplayCheatScanner.Value}[state={cheatScanner.Status}], gameplayUiEvents={pauseGameplayUiProcessing.Value}[suspended={gameplayUiEvents.IsSuspended}], highwayPause={pauseHighwayAnimations.Value}[hook={highwayAnimations.IsInstalled},suppressed={highwayAnimations.IsSuppressed}], mouseUiSkip={skipGameplayMouseUi.Value}[hook={mouseUiGameplay.IsInstalled},suppressed={mouseUiGameplay.IsSuppressed}], legacyMouseEvents={skipLegacyMouseEvents.Value}[hook={legacyMouseEvents.IsInstalled},suppressed={legacyMouseEvents.IsSuppressed}], rewiredMouseSampler={pauseRewiredMousePolling.Value}[hook={rewiredMousePolling.IsInstalled},suppressed={rewiredMousePolling.IsSuppressed}], rewiredKeyboardSampler={pauseRewiredKeyboardPolling.Value}[hook={rewiredKeyboardPolling.IsInstalled},suppressed={rewiredKeyboardPolling.IsSuppressed}], hotkeyPoll120={limitSlopHeroShortcutPolling.Value}, whammyDirtyGate={throttleWhammyTexture.Value}[hook={whammyTextureThrottle.IsInstalled},passThrough={whammyTextureThrottle.IsSuppressed}], networkTickRemainder={preserveNetworkTickRemainder.Value}[hook={networkTickRemainder.IsInstalled}], nativeHudTweaks={nativeHudTweaks}, fpsColor={customizeFpsColor.Value}, scales(scorebox/input/SP/practice/FPS)={scoreHudScale.Value:0.##}/{inputViewerScale.Value:0.##}/{starPowerHudScale.Value:0.##}/{practiceHudScale.Value:0.##}/{fpsHudScale.Value:0.##}, renderFrameInterval={OnDemandRendering.renderFrameInterval}, configuredSpeed={(speedSetting > 0 ? speedSetting + "%" : "unknown")}.");
                LoggerInstance.Msg($"Sloptifine 3 visuals: PB={hidePbPopup.Value}[active={optionalHudAnimations.PbSuppressed}], side glows={hideStarPowerSideGlows.Value}[active={optionalHudAnimations.GlowSuppressed}].");
                LogGameplaySnapshot();
                timings.Start(includeCameraTiming: cameraTimingClicked);
            }
            else Notice("Start a song before capturing mod timings.");
        }
        GUI.Label(new Rect(x + 4, y + 356, 610, 44), "Type 10–5000 on the game's speed row; * means pending. Pause or press Enter to apply.");
        GUI.Label(new Rect(x + 4, y + 404, 610, 44), "Timings are written to Latest.log. Close this menu and play.");
    }

    private void DrawCounterSettings(float x, float y)
    {
        SectionHeader("Counter HUD", x, y);
        Toggle(showHud, "Counter HUD", x, y + 30);
        Toggle(showMisses, "Misses", x, y + 72);
        Toggle(showHit, "Hit percentage", x, y + 114);
        Toggle(showGhosts, "Ghost inputs", x, y + 156);
        Toggle(showBest, "Best streak", x, y + 198);
        SectionHeader("Appearance and placement", x, y + 246);
        Appearance(x, y + 274, false);
    }

    private void DrawHudSettings(float x, float y)
    {
        SectionHeader("Song information", x, y);
        Toggle(showSong, "Song HUD", x, y + 30);
        Toggle(showTitle, "Chart title", x, y + 72);
        Toggle(showArtist, "Artist", x, y + 114);
        Toggle(showCharter, "Charter", x, y + 156);
        Toggle(showSpeed, "Speed beside title", x, y + 198);
        Appearance(x, y + 252, true);

        float nativeY = y + 528;
        SectionHeader("Practice and FPS HUD", x, nativeY);
        GUI.Label(new Rect(x + 4, nativeY + 28, 610, 26), "Native game HUD offsets use the game's UI canvas units.");
        if (GUI.Button(new Rect(x, nativeY + 58, 210, 32), "Move practice stats")) BeginNativeHudMove(1);
        if (GUI.Button(new Rect(x + 218, nativeY + 58, 240, 32), "Resize practice stats")) BeginResizeTarget(5);
        if (GUI.Button(new Rect(x + 466, nativeY + 58, 140, 32), "Reset"))
        { practiceHudX.Value = practiceHudY.Value = 0; preferencesDirty = true; }
        Number("Practice stats horizontal offset", practiceHudX.Value, x, nativeY + 96, -1500, 1500, v => practiceHudX.Value = v);
        Number("Practice stats vertical offset", practiceHudY.Value, x, nativeY + 136, -1500, 1500, v => practiceHudY.Value = v);
        if (GUI.Button(new Rect(x, nativeY + 182, 210, 32), "Move FPS display")) BeginNativeHudMove(2);
        if (GUI.Button(new Rect(x + 218, nativeY + 182, 240, 32), "Resize FPS display")) BeginResizeTarget(6);
        if (GUI.Button(new Rect(x + 466, nativeY + 182, 140, 32), "Reset"))
        { fpsHudX.Value = fpsHudY.Value = 0; preferencesDirty = true; }
        Number("FPS horizontal offset", fpsHudX.Value, x, nativeY + 220, -1500, 1500, v => fpsHudX.Value = v);
        Number("FPS vertical offset", fpsHudY.Value, x, nativeY + 260, -1500, 1500, v => fpsHudY.Value = v);
        Toggle(customizeFpsColor, "Customize FPS number color", x, nativeY + 300);
        Hex(fpsTextColor, "FPS number color", x, nativeY + 338);
        Number("FPS readout size", fpsHudScale.Value, x, nativeY + 378, .5f, 2.5f, v => { fpsHudScale.Value = v; preferencesDirty = true; });
        GUI.Label(new Rect(x + 4, nativeY + 418, 610, 54), "Color and size target the numeric FPS readout, not its separator. Position moves the full FPS row. The game's FPS display must be enabled first.");

        float sizingY = y + 1010;
        SectionHeader("Resize HUD elements", x, sizingY);
        GUI.Label(new Rect(x + 4, sizingY + 28, 610, 30), "Score box includes score, combo, star progress, and song progress.");
        Number("Score box", scoreHudScale.Value, x, sizingY + 64, .5f, 2.5f, v => { scoreHudScale.Value = v; preferencesDirty = true; });
        Number("Input viewer", inputViewerScale.Value, x, sizingY + 102, .5f, 2.5f, v => { inputViewerScale.Value = v; preferencesDirty = true; });
        Number("Star power bar", starPowerHudScale.Value, x, sizingY + 140, .5f, 2.5f, v => { starPowerHudScale.Value = v; preferencesDirty = true; });
        Number("Practice stats bar", practiceHudScale.Value, x, sizingY + 178, .5f, 2.5f, v => { practiceHudScale.Value = v; preferencesDirty = true; });
        for (int i = 0; i < ResizeTargetLabels.Length - 1; i++)
        {
            int column = i % 4, row = i / 4;
            float buttonWidth = 632f / 4 - 4;
            int columnsInRow = Math.Min(4, ResizeTargetLabels.Length - 1 - row * 4);
            float rowOffset = (632f - columnsInRow * buttonWidth - (columnsInRow - 1) * 4) * .5f;
            var oldBackground = GUI.backgroundColor;
            try
            {
                if (resizeSelection == i) GUI.backgroundColor = MenuAccentColor();
                if (GUI.Button(new Rect(x + rowOffset + column * (buttonWidth + 4), sizingY + 224 + row * 36, buttonWidth, 32), ResizeTargetLabels[i])) resizeSelection = i;
            }
            finally { GUI.backgroundColor = oldBackground; }
        }
        if (GUI.Button(new Rect(x, sizingY + 308, 400, 34), "Edit selected HUD with corner handles")) BeginResizeTarget(resizeSelection);
        GUI.Label(new Rect(x + 410, sizingY + 308, 220, 54), "F8 returns here; Escape finishes.");
        GUI.Label(new Rect(x + 4, sizingY + 350, 620, 56), "Drag a corner to resize. Drag counters or song info to move; practice and FPS layout controls are above.");
    }

    private void DrawVisualSettings(float x, float y)
    {
        SectionHeader("Highway", x, y);
        Toggle(hideSides, "Hide highway sides", x, y + 30);
        Toggle(hideStrings, "Hide fret strings", x, y + 72);
        Toggle(hideBeats, "Hide beatlines", x, y + 114);
        Toggle(lockSpeed, "Constant practice note speed (experimental)", x, y + 156);
        GUI.Label(new Rect(x + 4, y + 202, 610, 84), "Visual changes preserve gameplay and note timing. Hiding fret strings also pauses their alignment scripts. Practice speed compensation applies only in practice. Turn an option off to restore its normal appearance.");
        SectionHeader("Optional HUD animations", x, y + 300);
        Toggle(hideStarPowerSideGlows, "Hide Star Power side glows during solo songs", x, y + 330);
        Toggle(hidePbPopup, "Hide personal-best popup during solo songs", x, y + 372);
        GUI.Label(new Rect(x + 4, y + 420, 610, 84), "Experimental; default off. Side-glow branches deactivate during active offline solo songs and restore on pause or song exit; their animation may restart. The Star Power controller, score, and notes stay active.");
    }

    private void DrawColorSettings(float x, float y)
    {
        SectionHeader("Score and HUD colors", x, y);
        Toggle(tintFc, "Custom FC / perfect FC rings", x, y + 30);
        Hex(fcColor, "FC ring", x, y + 70); Hex(pfcColor, "Perfect FC ring", x, y + 110);
        Toggle(tintScore, "Custom score colors", x, y + 152);
        Hex(starColor, "Stars", x, y + 192); Hex(progressColor, "Star progress bar", x, y + 232);
        Hex(scoreColor, "Score / accuracy text", x, y + 272);
        Hex(songProgressColor, "Song progress bar", x, y + 312);
        Hex(menuAccentHex, "Menu accent", x, y + 352);
    }

    private void DrawPerformanceSettings(float x, float y)
    {
        SectionHeader("Guitar graphics and effects", x, y);
        Toggle(disableHitParticles, "Skip note-hit particles", x, y + 30);
        Toggle(disableSustainParticles, "Skip sustain particles", x, y + 70);
        Toggle(hideHeldSustainGlow, "Hide held-sustain glow (plain tails)", x, y + 110);
        Toggle(disableFretEffects, "Disable fret flames, sparks, and lightning", x, y + 150);
        Toggle(disableStarProgressParticles, "Skip star-progress burst particles", x, y + 190);
        Toggle(freezeComboSmokeScroll, "Pause combo-smoke animation (smoke stays visible)", x, y + 230);
        Toggle(pauseHiddenBeatlineUpdates, "Skip beatline updates while hidden", x, y + 270);
        Toggle(disableAprilFools, "Disable April Fools / NoteBomber chaos and Pong", x, y + 310);
        GUI.Label(new Rect(x + 4, y + 354, 610, 42), "These visual switches restore on pause, transitions, or online play. Plain tails keep their base sustain body.");

        float servicesY = y + 410;
        SectionHeader("Services and HUD", x, servicesY);
        Toggle(suspendHiddenLeaderboard, "Pause offline leaderboard and spectator UI", x, servicesY + 30);
        Toggle(stopMidiInput, "Pause drum MIDI device discovery", x, servicesY + 70);
        Toggle(disableDiscordPresence, "Disable Discord Rich Presence (restart to restore)", x, servicesY + 110);
        Toggle(useCanvasCounterHud, "Use experimental Canvas renderer for the counter HUD", x, servicesY + 150);
        Toggle(skipBackgroundCamera, "Skip background camera and hidden video in solo songs", x, servicesY + 190);
        GUI.Label(new Rect(x + 4, servicesY + 236, 610, 56), "Experimental; default off. Removes the background render pass and pauses its video decoder while hidden during offline solo songs; both restore on pause or online play.");

        float engineY = y + 730;
        SectionHeader("Engine and scheduler experiments", x, engineY);
        Toggle(skipGameplayMouseUi, "Skip mouse UI raycasts during gameplay", x, engineY + 30);
        Toggle(skipLegacyMouseEvents, "Skip legacy camera mouse events during gameplay", x, engineY + 70);
        Toggle(throttleWhammyTexture, "Skip duplicate whammy texture uploads", x, engineY + 110);
        Toggle(pauseGameplayCheatScanner, "Pause cheat-code scanner during solo songs", x, engineY + 150);
        Toggle(pauseGameplayUiProcessing, "Pause UI events and hidden HUD handles in solo songs", x, engineY + 190);
        Toggle(pauseHighwayAnimations, "Freeze highway, video, and track-fade visuals in solo play", x, engineY + 230);
        Toggle(reduceUnityJobWorkers, "Use one fewer Unity job worker during offline solo songs", x, engineY + 270);
        Toggle(disableMidiForSession, "Disable drum MIDI input (restart to restore)", x, engineY + 310);
        Toggle(staticTmpScaleDuringSong, "Skip TMP scale checks in offline solo play", x, engineY + 350);
        Toggle(pauseRewiredMousePolling, "Pause Rewired mouse polling in solo songs", x, engineY + 390);
        Toggle(pauseRewiredKeyboardPolling, "Pause Rewired keyboard polling in solo songs", x, engineY + 430);
        Toggle(limitSlopHeroShortcutPolling, "Limit SlopHero hotkey checks to 120 Hz", x, engineY + 470);
        Toggle(renderEveryUpdate, "Render every Unity update in solo songs (FPS test)", x, engineY + 510);
        GUI.Label(new Rect(x + 4, engineY + 556, 610, 96), "Experimental; default off. Rendering every update may raise presented FPS but can reduce simulation and instrument-polling rates under CPU load. The original interval returns on pause/exit. Keyboard sampler and hotkey options have separate input effects. UI-event pause suspends built-in navigation. Drum MIDI shutdown needs a restart to restore.");

        float renderingY = y + 1420;
        SectionHeader("3D rendering experiment", x, renderingY);
        Toggle(limitPlayerCameraFarClip, "Limit guitar-camera far clip in solo songs", x, renderingY + 30);
        Number("Guitar-camera far clip (units)", farClipDistance.Value, x, renderingY + 74, 40, 120,
            value => { farClipDistance.Value = Math.Clamp((int)Math.Round(value), 40, 120); preferencesDirty = true; });
        GUI.Label(new Rect(x + 4, renderingY + 118, 610, 52), "Experimental; default off, cap defaults to 80 units. Lower caps cull more distant rendering, but notes or effects can pop out sooner. Applies only during offline solo play and restores the original value. Camera benchmark tests temporarily use the stock clip distance.");

        Toggle(disableHudCameraBenchmark, "Disable HUD camera for solo FPS benchmark", x, renderingY + 190);
        GUI.Label(new Rect(x + 4, renderingY + 234, 610, 62), "Aggressive; default off. Hides score, combo, lyrics, and input HUD to measure this camera's upper-bound cost. The HUD camera restores automatically on pause, focus loss, or leaving solo play.");

        Toggle(disableGuitarCameraBenchmark, "Disable guitar camera for solo FPS benchmark", x, renderingY + 302);
        GUI.Label(new Rect(x + 4, renderingY + 346, 610, 64), "Very aggressive; default off. Hides the notes, highway, and 3D effects while scoring and HUD remain active. Test this OR the HUD-camera benchmark, not both. If black-background mode is on, the HUD camera temporarily performs its screen clear. Restores on pause, focus loss, or leaving solo play.");

        float networkY = renderingY + 426;
        SectionHeader("Online networking experiment", x, networkY);
        Toggle(preserveNetworkTickRemainder, "Preserve fractional network tick time (online only)", x, networkY + 30);
        GUI.Label(new Rect(x + 4, networkY + 74, 610, 72), "Experimental; default off. Keeps the fractional remainder after a connected network update, but skips whole missed intervals to avoid catch-up bursts. Could change packet timing or worsen desync. Instrument polling is unchanged.");

    }

    private void Toggle(MelonPreferences_Entry<bool> entry, string label, float x, float y)
    {
        Rect row = new(x, y, 632, 36);
        Color previousBackground = GUI.backgroundColor;
        bool hovered = row.Contains(Event.current.mousePosition);
        try
        {
            bool changed = GUI.Button(row, "", menuClickStyle);
            if (hovered)
            {
                GUI.backgroundColor = new Color(.10f, .11f, .15f, 1);
                GUI.Box(row, "", menuFlatStyle);
            }
            GUI.backgroundColor = new Color(.10f, .11f, .14f, 1);
            Rect checkbox = new(x + 8, y + 6, 24, 24);
            GUI.Box(checkbox, "", menuFlatStyle);
            GUI.backgroundColor = entry.Value ? MenuAccentColor() : new Color(.055f, .06f, .075f, 1);
            Rect checkboxInner = new(checkbox.x + 2, checkbox.y + 2, checkbox.width - 4, checkbox.height - 4);
            GUI.Box(checkboxInner, "", menuFlatStyle);
            GUI.backgroundColor = previousBackground;
            menuToggleLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft
            };
            menuToggleLabelStyle.normal.textColor = new Color(.91f, .92f, .96f, 1);
            GUI.Label(new Rect(x + 44, y, row.width - 56, row.height), label, menuToggleLabelStyle);
            if (entry.Value)
            {
                menuToggleCheckStyle ??= new GUIStyle(GUI.skin.label)
                    { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                menuToggleCheckStyle.normal.textColor = Color.white;
                GUI.Label(checkboxInner, "✓", menuToggleCheckStyle);
            }
            if (changed)
            {
                entry.Value = !entry.Value;
                preferencesDirty = true;
                nextStats = 0;
                if (ReferenceEquals(entry, showHud) && !entry.Value)
                {
                    counterCanvas.Hide();
                    statsText = "";
                }
            }
        }
        finally { GUI.backgroundColor = previousBackground; }
    }

    private void SectionHeader(string text, float x, float y)
    {
        sectionHeaderStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 13, alignment = TextAnchor.MiddleLeft };
        sectionHeaderStyle.normal.textColor = MenuAccentColor();
        Color previous = GUI.backgroundColor;
        try
        {
            GUI.backgroundColor = MenuAccentColor();
            GUI.Box(new Rect(x, y + 5, 3, 14), "", menuFlatStyle);
            GUI.Label(new Rect(x + 10, y, 622, 24), text, sectionHeaderStyle);
        }
        finally { GUI.backgroundColor = previous; }
    }

    private void BeginNativeHudMove(int target)
    {
        resizeTarget = -1;
        nativeMoveTarget = target;
        nativeHudDragging = false;
        moveHud = true;
        moveSong = false;
        settingsOpen = false;
        capture = null;
        UpdateCursor();
        restartHold.RequireRelease();
        practiceHold.RequireRelease();
    }

    private void BeginResizeTarget(int target)
    {
        resizeTarget = Math.Clamp(target, 0, ResizeTargetLabels.Length - 1);
        resizeHandle = -1;
        resizeMoveDragging = false;
        cachedResizeRectTarget = -1;
        nextResizeRectProbe = 0;
        moveHud = false;
        nativeMoveTarget = 0;
        nativeHudDragging = hudDragging = false;
        settingsOpen = false;
        capture = null;
        UpdateCursor();
        restartHold.RequireRelease();
        practiceHold.RequireRelease();
    }

    private float ResizeValue() => resizeTarget switch
    {
        0 => fontSize.Value,
        1 => songFont.Value,
        2 => scoreHudScale.Value,
        3 => inputViewerScale.Value,
        4 => starPowerHudScale.Value,
        5 => practiceHudScale.Value,
        6 => fpsHudScale.Value,
        _ => 1f
    };

    private void SetResizeValue(float value)
    {
        switch (resizeTarget)
        {
            case 0: fontSize.Value = (int)Math.Clamp(Math.Round(value), 14, 48); break;
            case 1: songFont.Value = (int)Math.Clamp(Math.Round(value), 14, 48); break;
            case 2: scoreHudScale.Value = Math.Clamp(value, .5f, 2.5f); break;
            case 3: inputViewerScale.Value = Math.Clamp(value, .5f, 2.5f); break;
            case 4: starPowerHudScale.Value = Math.Clamp(value, .5f, 2.5f); break;
            case 5: practiceHudScale.Value = Math.Clamp(value, .5f, 2.5f); break;
            case 6: fpsHudScale.Value = Math.Clamp(value, .5f, 2.5f); break;
        }
        preferencesDirty = true;
    }

    private bool TryGetResizeRect(out Rect rect)
    {
        if (resizeTarget == 0)
        {
            int size = Math.Clamp(fontSize.Value, 14, 48);
            string text = string.IsNullOrEmpty(statsText) ? "Misses: 0\nHit: 100.00%\nGhosts: 0" : statsText;
            int lines = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lines++;
            int width = Math.Max(240, size * 12 + 24), height = (int)(size * 1.25f) * lines + 20;
            var p = HudLayout.ClampPosition(hudX.Value, hudY.Value, width, height, Screen.width, Screen.height);
            rect = new Rect(p.X, p.Y, width, height);
            return true;
        }
        if (resizeTarget == 1)
        {
            string text = songText.Length == 0 ? "Chart title (100%)\nArtist: Artist\nCharter: Charter" : songText;
            int size = Math.Clamp(songFont.Value, 14, 48), width = Math.Min(520, Screen.width);
            int charsPerLine = Math.Max(8, (int)((width - 24) / (size * .6f)));
            int lines = 0, lineLength = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i == text.Length || text[i] == '\n')
                {
                    lines += Math.Max(1, (lineLength + charsPerLine - 1) / charsPerLine);
                    lineLength = 0;
                }
                else lineLength++;
            }
            int height = (int)(size * 1.35f) * lines + 20;
            var p = HudLayout.ClampPosition(songX.Value, songY.Value, width, height, Screen.width, Screen.height);
            rect = new Rect(p.X, p.Y, width, height);
            return true;
        }
        rect = default;
        var game = Il2Cpp.GameManager.field_Private_Static_GameManager_0;
        return game != null && nativeHud.TryGetResizeScreenRect(game, resizeTarget, out rect);
    }

    private bool TryGetCachedResizeRect(out Rect rect)
    {
        double now = clock.Elapsed.TotalSeconds;
        if (cachedResizeRectTarget != resizeTarget || now >= nextResizeRectProbe)
        {
            bool targetChanged = cachedResizeRectTarget != resizeTarget;
            cachedResizeRectValid = TryGetResizeRect(out cachedResizeRect);
            cachedResizeRectTarget = resizeTarget;
            nextResizeRectProbe = now + .05;
            if (targetChanged)
            {
                if (cachedResizeRectValid)
                    LoggerInstance.Msg($"Resize editor bounds: {ResizeTargetLabels[resizeTarget]} at {cachedResizeRect.x:0},{cachedResizeRect.y:0} size {cachedResizeRect.width:0}x{cachedResizeRect.height:0}.");
                else
                {
                    string detail = resizeTarget == 5
                        ? $" ({nativeHud.DescribePracticeTarget(Il2Cpp.GameManager.field_Private_Static_GameManager_0)})" : "";
                    LoggerInstance.Warning($"Resize editor could not find visible bounds for {ResizeTargetLabels[resizeTarget]}{detail}.");
                }
            }
        }
        rect = cachedResizeRect;
        return cachedResizeRectValid;
    }

    private static Rect ResizeHandleRect(Rect rect, int corner)
    {
        const float size = 22;
        float x = corner == 0 || corner == 3 ? rect.xMin : rect.xMax;
        float y = corner < 2 ? rect.yMin : rect.yMax;
        return new Rect(x - size * .5f, y - size * .5f, size, size);
    }

    private static Vector2 ResizeCorner(Rect rect, int corner) => corner switch
    {
        0 => new Vector2(rect.xMin, rect.yMin),
        1 => new Vector2(rect.xMax, rect.yMin),
        2 => new Vector2(rect.xMax, rect.yMax),
        _ => new Vector2(rect.xMin, rect.yMax)
    };

    private void DrawResizeHandles()
    {
        if (!TryGetCachedResizeRect(out var rect))
        {
            string hint = resizeTarget == 5
                ? "Practice stats panel was not found in this scene. F8 returns to settings."
                : resizeTarget == 6
                    ? "FPS readout was not found in this scene. F8 returns to settings."
                    : "This HUD is not available in the current scene. F8 returns to settings.";
            GUI.Box(new Rect(20, Screen.height - 60, Math.Max(300, Screen.width - 40), 44), hint);
            return;
        }

        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!ResizeHandleRect(rect, i).Contains(e.mousePosition)) continue;
                resizeHandle = i;
                resizeStartValue = ResizeValue();
                resizeDragCenter = rect.center;
                resizeStartRadius = Vector2.Distance(resizeDragCenter, ResizeCorner(rect, i));
                resizeStartMouse = e.mousePosition;
                e.Use();
                return;
            }
            if (!rect.Contains(e.mousePosition)) return;
            resizeStartValue = ResizeValue();
            if (resizeTarget < 2)
            {
                resizeMoveDragging = true;
                resizeDragOffset = e.mousePosition - new Vector2(rect.x, rect.y);
            }
            else if (resizeTarget >= 5)
            {
                resizeMoveDragging = true;
                resizeStartMouse = e.mousePosition;
            }
            else return; // Clone Hero's own layout controls move these HUDs.
            e.Use();
        }
        if (e.type == EventType.MouseDrag && resizeHandle >= 0)
        {
            // Keep the drag origin fixed for the entire gesture. The
            // displayed bounds refresh at 20 Hz and must not shift the ratio.
            float radius = Vector2.Distance(resizeDragCenter, e.mousePosition);
            if (resizeStartRadius > 1) SetResizeValue(resizeStartValue * radius / resizeStartRadius);
            e.Use();
        }
        if (e.type == EventType.MouseDrag && resizeMoveDragging && resizeTarget < 2)
        {
            var p = HudLayout.ClampPosition((int)(e.mousePosition.x - resizeDragOffset.x),
                (int)(e.mousePosition.y - resizeDragOffset.y), (int)rect.width, (int)rect.height, Screen.width, Screen.height);
            if (resizeTarget == 0) { hudX.Value = p.X; hudY.Value = p.Y; }
            else { songX.Value = p.X; songY.Value = p.Y; }
            preferencesDirty = true;
            e.Use();
        }
        if (e.type == EventType.MouseDrag && resizeMoveDragging && resizeTarget >= 5)
        {
            var delta = nativeHud.MoveByScreenDelta(resizeTarget == 5 ? 1 : 2,
                e.mousePosition.x - resizeStartMouse.x, e.mousePosition.y - resizeStartMouse.y);
            if (resizeTarget == 5) { practiceHudX.Value += delta.x; practiceHudY.Value += delta.y; }
            else { fpsHudX.Value += delta.x; fpsHudY.Value += delta.y; }
            resizeStartMouse = e.mousePosition;
            preferencesDirty = true;
            e.Use();
        }
        if (e.rawType == EventType.MouseUp && (resizeHandle >= 0 || resizeMoveDragging))
        {
            resizeHandle = -1;
            resizeMoveDragging = false;
            SavePreferences();
            LoggerInstance.Msg($"Resize editor saved {ResizeTargetLabels[resizeTarget]} at {ResizeValue():0.##}{(resizeTarget < 2 ? " px" : "×")}.");
        }

        Color previous = GUI.color;
        Color previousBackground = GUI.backgroundColor;
        try
        {
            // GUI.DrawTexture is missing its IL2CPP unstripped wrapper in this
            // Clone Hero build. Calling it throws from OnGUI and closes the
            // editor before resize handles can be used. GUI.Box(Rect, string)
            // is already supported by the game's runtime and gives us the same
            // simple filled rectangles for the outline and handles.
            GUI.color = Color.white;
            GUI.backgroundColor = MenuAccentColor();
            const float line = 5;
            GUI.Box(new Rect(rect.xMin, rect.yMin, rect.width, line), "");
            GUI.Box(new Rect(rect.xMin, rect.yMax - line, rect.width, line), "");
            GUI.Box(new Rect(rect.xMin, rect.yMin, line, rect.height), "");
            GUI.Box(new Rect(rect.xMax - line, rect.yMin, line, rect.height), "");
            for (int i = 0; i < 4; i++) GUI.Box(ResizeHandleRect(rect, i), "+");
            GUI.backgroundColor = previousBackground;
            string label = ResizeTargetLabels[resizeTarget] + (resizeTarget < 2
                ? $" — {ResizeValue():0} px"
                : $" — {ResizeValue():0.00}×");
            GUI.Box(new Rect(rect.xMin, Math.Max(0, rect.yMin - 26), Math.Max(150, label.Length * 8), 22), label);
            GUI.Box(new Rect(20, 16, Math.Max(300, Screen.width - 40), 34),
                $"HUD edit: {label}. Drag a corner to resize.");
            GUI.Box(new Rect(20, Screen.height - 60, Math.Max(300, Screen.width - 40), 44),
                "Drag inside counters, song info, practice stats, or FPS to move. Use Clone Hero's layout controls for other HUDs. F8 returns; Escape finishes.");
        }
        finally
        {
            GUI.color = previous;
            GUI.backgroundColor = previousBackground;
        }

    }

    private void DrawProfiles(float x, float y)
    {
        GUI.Label(new Rect(x, y, 540, 44), "Choose a saved profile or color theme. Selecting it applies the file immediately.");
        string current = profileNames.Length == 0 ? "No profiles found" : $"Selected: {profileNames[profileIndex]} ({profileIndex + 1}/{profileNames.Length})";
        if (GUI.Button(new Rect(x, y + 50, 540, 36), current + (profileDropdownOpen ? "  ▲" : "  ▼")))
        {
            profileDropdownOpen = !profileDropdownOpen;
            profileListStart = Math.Max(0, profileIndex / 4 * 4);
        }

        float actionY = y + 132;
        if (profileDropdownOpen)
        {
            DrawProfileDropdown(x, y + 88);
            actionY = y + 240;
        }
        if (GUI.Button(new Rect(x, actionY, 260, 36), "Save full profile as…"))
            Edit("Save full profile (name)", "", SaveFullProfile);
        if (GUI.Button(new Rect(x + 280, actionY, 260, 36), "Save color theme as…"))
            Edit("Save color theme (name)", "", SaveColorTheme);
        if (GUI.Button(new Rect(x, actionY + 44, 260, 34), "Refresh profile folder")) RefreshProfiles();
        GUI.Label(new Rect(x, actionY + 88, 540, profileDropdownOpen ? 42 : 66), settingsProfiles == null
            ? "Profile folder unavailable; see Latest.log."
            : "Drop or edit .json files in:\n" + settingsProfiles.DirectoryPath);
        if (profileNotice.Length > 0) GUI.Label(new Rect(x, actionY + (profileDropdownOpen ? 132 : 160), 540, 36), profileNotice);
    }

    private void DrawProfileDropdown(float x, float y)
    {
        float height = 32 + Math.Min(4, profileNames.Length) * 28;
        GUI.Box(new Rect(x, y, 540, height), "");
        if (profileNames.Length == 0)
        { GUI.Label(new Rect(x + 10, y + 7, 520, 24), "No profile files found. Save one or add a .json file."); return; }
        if (profileNames.Length > 4)
        {
            if (GUI.Button(new Rect(x + 8, y + 3, 70, 26), "‹ More")) profileListStart = (profileListStart - 4 + profileNames.Length) % profileNames.Length;
            if (GUI.Button(new Rect(x + 462, y + 3, 70, 26), "More ›")) profileListStart = (profileListStart + 4) % profileNames.Length;
        }
        int shown = Math.Min(4, profileNames.Length);
        for (int row = 0; row < shown; row++)
        {
            int index = (profileListStart + row) % profileNames.Length;
            string profileName = profileNames[index];
            float rowY = y + 32 + row * 28;
            if (GUI.Button(new Rect(x + 8, rowY, 452, 26), profileName))
            {
                profileDropdownOpen = false;
                ApplyProfileIndex(index);
            }
            if (GUI.Button(new Rect(x + 466, rowY, 66, 26), "✎"))
            {
                profileDropdownOpen = false;
                Edit("Rename profile", profileName, newName => RenameProfile(profileName, newName));
            }
        }
    }

    private void RefreshProfiles()
    {
        if (settingsProfiles == null) return;
        string selected = profileNames.Length == 0 ? "" : profileNames[Math.Clamp(profileIndex, 0, profileNames.Length - 1)];
        profileNames = settingsProfiles.List();
        int found = Array.FindIndex(profileNames, n => string.Equals(n, selected, StringComparison.OrdinalIgnoreCase));
        profileIndex = found >= 0 ? found : 0;
        profileListStart = Math.Max(0, profileIndex / 4 * 4);
    }

    private void ApplyProfileIndex(int index)
    {
        if (settingsProfiles == null || profileNames.Length == 0)
        { profileNotice = "No profile files found. Save one here or add a .json file to the folder."; return; }
        profileIndex = Math.Clamp(index, 0, profileNames.Length - 1);
        string name = profileNames[profileIndex];
        try
        {
            if (!settingsProfiles.TryLoad(name, out var values))
            { profileNotice = $"Could not read '{name}.json'. Check its JSON format."; return; }
            int invalid = ApplyProfileValues(values);
            nextStats = 0;
            preferencesDirty = true;
            SavePreferences();
            profileNotice = invalid == 0 ? $"Applied '{name}'." : $"Applied '{name}'; skipped {invalid} invalid value(s).";
            LoggerInstance.Msg(profileNotice);
        }
        catch (Exception ex)
        {
            profileNotice = $"Could not apply '{name}': {ex.Message}";
            LoggerInstance.Warning(profileNotice);
        }
    }

    private void SaveFullProfile(string name) => SaveProfile(name, false);
    private void SaveColorTheme(string name) => SaveProfile(name, true);

    private void RenameProfile(string oldName, string newName)
    {
        if (settingsProfiles == null) { profileNotice = "Profile folder is unavailable."; return; }
        try
        {
            string renamed = settingsProfiles.Rename(oldName, newName);
            RefreshProfiles();
            int found = Array.FindIndex(profileNames, n => string.Equals(n, renamed, StringComparison.OrdinalIgnoreCase));
            if (found >= 0) profileIndex = found;
            profileNotice = $"Renamed '{oldName}' to '{renamed}'.";
            LoggerInstance.Msg(profileNotice);
        }
        catch (Exception ex)
        {
            profileNotice = $"Could not rename profile: {ex.Message}";
            LoggerInstance.Warning(profileNotice);
        }
    }

    private Color MenuAccentColor()
    {
        string hex = string.IsNullOrWhiteSpace(menuAccentHex.Value) ? "A000FF" : menuAccentHex.Value;
        if (parsedColors.TryGetValue(hex, out var cached)) return cached;
        Color color;
        try { color = ParseColor(hex); }
        catch (FormatException) { color = new Color(.63f, 0, 1, 1); }
        if (parsedColors.Count > 64) parsedColors.Clear();
        parsedColors[hex] = color;
        return color;
    }

    private void SaveProfile(string name, bool colorsOnly)
    {
        if (settingsProfiles == null) { profileNotice = "Profile folder is unavailable."; return; }
        try
        {
            settingsProfiles.Save(name, CaptureProfileValues(colorsOnly));
            RefreshProfiles();
            int found = Array.FindIndex(profileNames, n => string.Equals(n, name.Trim(), StringComparison.OrdinalIgnoreCase));
            if (found >= 0) profileIndex = found;
            profileNotice = $"Saved {(colorsOnly ? "color theme" : "profile")} '{name.Trim()}'.";
            LoggerInstance.Msg(profileNotice);
        }
        catch (Exception ex)
        {
            profileNotice = $"Could not save profile: {ex.Message}";
            LoggerInstance.Warning(profileNotice);
        }
    }

    private Dictionary<string, string> CaptureProfileValues(bool colorsOnly)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string key, object value) => values[key] = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        void Bool(string key, MelonPreferences_Entry<bool> value) => Add(key, value.Value);
        void Int(string key, MelonPreferences_Entry<int> value) => Add(key, value.Value);
        void Float(string key, MelonPreferences_Entry<float> value) => Add(key, value.Value.ToString("R", CultureInfo.InvariantCulture));
        void Text(string key, MelonPreferences_Entry<string> value) => Add(key, value.Value);

        if (!colorsOnly)
        {
            Text("RestartButton", restartButton); Text("PracticeButton", practiceButton); Float("HoldSeconds", holdSeconds);
            Int("HudX", hudX); Int("HudY", hudY); Int("FontSize", fontSize); Bool("ShowHud", showHud);
            Float("BackgroundOpacity", backgroundOpacity); Float("TextRed", textRed); Float("TextGreen", textGreen); Float("TextBlue", textBlue);
            Bool("ShowMisses", showMisses); Bool("ShowHitPercentage", showHit); Bool("ShowGhosts", showGhosts); Bool("ShowBestStreak", showBest);
            Bool("ShowSongHud", showSong); Bool("ShowSongTitle", showTitle); Bool("ShowArtist", showArtist); Bool("ShowCharter", showCharter); Bool("ShowChartSpeed", showSpeed);
            Int("SongHudX", songX); Int("SongHudY", songY); Int("SongFontSize", songFont); Float("SongBackgroundOpacity", songOpacity);
            Float("PracticeHudOffsetX", practiceHudX); Float("PracticeHudOffsetY", practiceHudY); Float("FpsHudOffsetX", fpsHudX); Float("FpsHudOffsetY", fpsHudY);
            Float("SettingsMenuScale", settingsMenuScale); Float("SettingsWindowOffsetX", settingsWindowOffsetX); Float("SettingsWindowOffsetY", settingsWindowOffsetY);
            Float("ScoreHudScale", scoreHudScale); Float("InputViewerScale", inputViewerScale); Float("StarPowerHudScale", starPowerHudScale); Float("PracticeHudScale", practiceHudScale); Float("FpsHudScale", fpsHudScale);
            Bool("HideHighwaySides", hideSides); Bool("HideFretStrings", hideStrings); Bool("HideBeatlines", hideBeats); Bool("ConstantPracticeNoteSpeed", lockSpeed);
            Bool("PerformancePauseHiddenBeatlineUpdates", pauseHiddenBeatlineUpdates);
            Bool("DisableAprilFoolsContent", disableAprilFools);
            Bool("PerformanceDisableHitParticles", disableHitParticles); Bool("PerformanceDisableSustainParticles", disableSustainParticles);
            Bool("PerformanceDisableFretEffects", disableFretEffects); Bool("PerformanceStopMidiInput", stopMidiInput); Bool("PerformanceDisableMidiForSession", disableMidiForSession);
            Bool("PerformanceDisableStarProgressParticles", disableStarProgressParticles);
            Bool("PerformanceDisableDiscordPresence", disableDiscordPresence); Bool("PerformanceSuspendHiddenLeaderboard", suspendHiddenLeaderboard);
            Bool("PerformanceCanvasCounterHud", useCanvasCounterHud);
            Bool("PerformanceFreezeComboSmokeScroll", freezeComboSmokeScroll);
            Bool("PerformanceSkipGameplayMouseUi", skipGameplayMouseUi);
            Bool("PerformanceSkipLegacyMouseEvents", skipLegacyMouseEvents);
            Bool("PerformanceThrottleWhammyTexture", throttleWhammyTexture);
            Bool("PerformancePauseGameplayCheatScanner", pauseGameplayCheatScanner);
            Bool("PerformancePauseGameplayUiProcessing", pauseGameplayUiProcessing);
            Bool("PerformancePauseHighwayAnimations", pauseHighwayAnimations);
            Bool("PerformanceReduceUnityJobWorkers", reduceUnityJobWorkers);
            Bool("PerformanceStaticTmpScaleDuringSong", staticTmpScaleDuringSong);
            Bool("PerformancePauseRewiredMousePolling", pauseRewiredMousePolling);
            Bool("PerformancePauseRewiredKeyboardPolling", pauseRewiredKeyboardPolling);
            Bool("PerformanceLimitSlopHeroShortcutPolling", limitSlopHeroShortcutPolling);
            Bool("PerformanceSkipBackgroundCamera", skipBackgroundCamera);
            Bool("PerformanceLimitPlayerCameraFarClip", limitPlayerCameraFarClip);
            Int("PerformanceGuitarCameraFarClip", farClipDistance);
            Bool("PerformanceDisableHudCameraBenchmark", disableHudCameraBenchmark);
            Bool("PerformanceDisableGuitarCameraBenchmark", disableGuitarCameraBenchmark);
            Bool("PerformanceHideHeldSustainGlow", hideHeldSustainGlow);
            Bool("PerformanceRenderEveryUpdateSolo", renderEveryUpdate);
            Bool("PerformanceHidePbPopup", hidePbPopup);
            Bool("PerformanceHideStarPowerSideGlows", hideStarPowerSideGlows);
            Bool("PerformancePreserveNetworkTickRemainder", preserveNetworkTickRemainder);
        }
        Bool("CustomizeFcRings", tintFc); Bool("CustomizeScoreColors", tintScore); Bool("CustomizeFpsColor", customizeFpsColor);
        Text("SongTextColor", songColor); Text("FcColor", fcColor); Text("PerfectFcColor", pfcColor); Text("StarColor", starColor);
        Text("ProgressColor", progressColor); Text("ScoreTextColor", scoreColor); Text("SongProgressColor", songProgressColor); Text("FpsTextColor", fpsTextColor);
        Text("MenuAccentColor", menuAccentHex);
        if (colorsOnly)
        {
            Float("TextRed", textRed); Float("TextGreen", textGreen); Float("TextBlue", textBlue); Float("BackgroundOpacity", backgroundOpacity); Float("SongBackgroundOpacity", songOpacity);
        }
        return values;
    }

    private int ApplyProfileValues(Dictionary<string, string> values)
    {
        int invalid = 0;
        void Bool(string key, MelonPreferences_Entry<bool> entry)
        { if (!values.TryGetValue(key, out var s)) return; if (bool.TryParse(s, out bool v)) entry.Value = v; else invalid++; }
        void Int(string key, MelonPreferences_Entry<int> entry, int min, int max)
        { if (!values.TryGetValue(key, out var s)) return; if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) entry.Value = Math.Clamp(v, min, max); else invalid++; }
        void Float(string key, MelonPreferences_Entry<float> entry, float min, float max)
        { if (!values.TryGetValue(key, out var s)) return; if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && float.IsFinite(v)) entry.Value = Math.Clamp(v, min, max); else invalid++; }
        void Hex(string key, MelonPreferences_Entry<string> entry)
        { if (!values.TryGetValue(key, out var s)) return; try { entry.Value = ColorHex(ParseColor(s)); } catch (FormatException) { invalid++; } }
        void Binding(string key, MelonPreferences_Entry<string> entry)
        { if (!values.TryGetValue(key, out var s)) return; if (ValidBinding(s)) entry.Value = s; else invalid++; }

        Binding("RestartButton", restartButton); Binding("PracticeButton", practiceButton); Float("HoldSeconds", holdSeconds, .25f, 3f);
        Int("HudX", hudX, 0, Math.Max(0, Screen.width)); Int("HudY", hudY, 0, Math.Max(0, Screen.height)); Int("FontSize", fontSize, 14, 48); Bool("ShowHud", showHud);
        Float("BackgroundOpacity", backgroundOpacity, 0, 1); Float("TextRed", textRed, 0, 1); Float("TextGreen", textGreen, 0, 1); Float("TextBlue", textBlue, 0, 1);
        Bool("ShowMisses", showMisses); Bool("ShowHitPercentage", showHit); Bool("ShowGhosts", showGhosts); Bool("ShowBestStreak", showBest);
        Bool("ShowSongHud", showSong); Bool("ShowSongTitle", showTitle); Bool("ShowArtist", showArtist); Bool("ShowCharter", showCharter); Bool("ShowChartSpeed", showSpeed);
        Int("SongHudX", songX, 0, Math.Max(0, Screen.width)); Int("SongHudY", songY, 0, Math.Max(0, Screen.height)); Int("SongFontSize", songFont, 14, 48); Float("SongBackgroundOpacity", songOpacity, 0, 1);
        Float("PracticeHudOffsetX", practiceHudX, -1500, 1500); Float("PracticeHudOffsetY", practiceHudY, -1500, 1500); Float("FpsHudOffsetX", fpsHudX, -1500, 1500); Float("FpsHudOffsetY", fpsHudY, -1500, 1500);
        Bool("HideHighwaySides", hideSides); Bool("HideFretStrings", hideStrings); Bool("HideBeatlines", hideBeats); Bool("ConstantPracticeNoteSpeed", lockSpeed);
        Bool("PerformancePauseHiddenBeatlineUpdates", pauseHiddenBeatlineUpdates);
        Bool("DisableAprilFoolsContent", disableAprilFools);
        Bool("PerformanceDisableHitParticles", disableHitParticles); Bool("PerformanceDisableSustainParticles", disableSustainParticles);
        Bool("PerformanceDisableFretEffects", disableFretEffects); Bool("PerformanceStopMidiInput", stopMidiInput); Bool("PerformanceDisableMidiForSession", disableMidiForSession);
        Bool("PerformanceDisableStarProgressParticles", disableStarProgressParticles);
        Bool("PerformanceDisableDiscordPresence", disableDiscordPresence); Bool("PerformanceSuspendHiddenLeaderboard", suspendHiddenLeaderboard);
        Bool("PerformanceCanvasCounterHud", useCanvasCounterHud);
        Bool("PerformanceSkipGameplayMouseUi", skipGameplayMouseUi);
        Bool("PerformanceSkipLegacyMouseEvents", skipLegacyMouseEvents);
        Bool("PerformanceThrottleWhammyTexture", throttleWhammyTexture);
        Bool("PerformancePauseGameplayCheatScanner", pauseGameplayCheatScanner);
        Bool("PerformancePauseGameplayUiProcessing", pauseGameplayUiProcessing);
        Bool("PerformancePauseHighwayAnimations", pauseHighwayAnimations);
        Bool("PerformanceReduceUnityJobWorkers", reduceUnityJobWorkers);
        Bool("PerformanceStaticTmpScaleDuringSong", staticTmpScaleDuringSong);
        Bool("PerformancePauseRewiredMousePolling", pauseRewiredMousePolling);
        Bool("PerformancePauseRewiredKeyboardPolling", pauseRewiredKeyboardPolling);
        Bool("PerformanceLimitSlopHeroShortcutPolling", limitSlopHeroShortcutPolling);
        Bool("PerformanceSkipBackgroundCamera", skipBackgroundCamera);
        Bool("PerformanceLimitPlayerCameraFarClip", limitPlayerCameraFarClip);
        Int("PerformanceGuitarCameraFarClip", farClipDistance, 40, 120);
        Bool("PerformanceDisableHudCameraBenchmark", disableHudCameraBenchmark);
        Bool("PerformanceDisableGuitarCameraBenchmark", disableGuitarCameraBenchmark);
        Bool("PerformanceHideHeldSustainGlow", hideHeldSustainGlow);
        Bool("PerformanceRenderEveryUpdateSolo", renderEveryUpdate);
        Bool("PerformanceHidePbPopup", hidePbPopup);
        Bool("PerformanceHideStarPowerSideGlows", hideStarPowerSideGlows);
        Bool("PerformancePreserveNetworkTickRemainder", preserveNetworkTickRemainder);
        if (values.ContainsKey("PerformanceFreezeComboSmokeScroll"))
            Bool("PerformanceFreezeComboSmokeScroll", freezeComboSmokeScroll);
        else
            Bool("PerformanceFreezeLyricsBackgroundScroll", freezeComboSmokeScroll);
        Bool("CustomizeFcRings", tintFc); Bool("CustomizeScoreColors", tintScore); Bool("CustomizeFpsColor", customizeFpsColor);
        Float("SettingsMenuScale", settingsMenuScale, .85f, 1.4f);
        Float("SettingsWindowOffsetX", settingsWindowOffsetX, -4096, 4096); Float("SettingsWindowOffsetY", settingsWindowOffsetY, -4096, 4096);
        Float("ScoreHudScale", scoreHudScale, .5f, 2.5f); Float("InputViewerScale", inputViewerScale, .5f, 2.5f); Float("StarPowerHudScale", starPowerHudScale, .5f, 2.5f); Float("PracticeHudScale", practiceHudScale, .5f, 2.5f); Float("FpsHudScale", fpsHudScale, .5f, 2.5f);
        if (!values.ContainsKey("ScoreHudScale"))
        {
            if (values.ContainsKey("StarProgressHudScale")) Float("StarProgressHudScale", scoreHudScale, .5f, 2.5f);
            else Float("SongProgressHudScale", scoreHudScale, .5f, 2.5f);
        }
        Hex("SongTextColor", songColor); Hex("FcColor", fcColor); Hex("PerfectFcColor", pfcColor); Hex("StarColor", starColor);
        Hex("ProgressColor", progressColor); Hex("ScoreTextColor", scoreColor); Hex("SongProgressColor", songProgressColor); Hex("FpsTextColor", fpsTextColor);
        Hex("MenuAccentColor", menuAccentHex);
        return invalid;
    }

    private static bool ValidBinding(string value)
    {
        var parts = value.Split(':');
        if (parts.Length == 2 && parts[0] == "Key" && Enum.TryParse(parts[1], out KeyCode key))
            return Enum.IsDefined(typeof(KeyCode), key) && key != KeyCode.None && key != KeyCode.Escape && key != KeyCode.F8;
        return parts.Length == 3 && parts[0] == "Joy"
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int button) && button >= 0;
    }

    private void Appearance(float x, float y, bool song)
    {
        var size = song ? songFont : fontSize;
        var opacity = song ? songOpacity : backgroundOpacity;
        Number("Font size", size.Value, x, y, 14, 48, v => size.Value = (int)Math.Round(v));
        Number("Background opacity (%)", HudLayout.Unit(opacity.Value) * 100, x, y + 40, 0, 100, v => opacity.Value = v / 100);
        if (song) Hex(songColor, "Text color", x, y + 80);
        else
        {
            string hex = ColorHex(new Color(HudLayout.Unit(textRed.Value), HudLayout.Unit(textGreen.Value), HudLayout.Unit(textBlue.Value)));
            if (GUI.Button(new Rect(x, y + 80, 460, 36), "Text color: #" + hex))
                EditColor("Text color (#RRGGBB)", hex, s => { var c = ParseColor(s); textRed.Value = c.r; textGreen.Value = c.g; textBlue.Value = c.b; });
            Preview(new Rect(x + 470, y + 84, 70, 28), ParseColor(hex));
        }
        if (GUI.Button(new Rect(x, y + 126, 265, 36), "Move this HUD"))
        {
            moveSong = song; moveHud = true; nativeMoveTarget = 0; nativeHudDragging = false; settingsOpen = false; capture = null; UpdateCursor();
            restartHold.RequireRelease(); practiceHold.RequireRelease();
        }
        if (GUI.Button(new Rect(x + 275, y + 126, 265, 36), "Reset position"))
        { if (song) { songX.Value = 400; songY.Value = 110; } else { hudX.Value = 24; hudY.Value = 110; } preferencesDirty = true; }
        Number("Position X", song ? songX.Value : hudX.Value, x, y + 166, 0, Screen.width, v => { if (song) songX.Value = (int)v; else hudX.Value = (int)v; });
        Number("Position Y", song ? songY.Value : hudY.Value, x, y + 206, 0, Screen.height, v => { if (song) songY.Value = (int)v; else hudY.Value = (int)v; });
    }

    private void Number(string label, float value, float x, float y, float min, float max, Action<float> apply)
    {
        if (MenuAction(new Rect(x, y, 540, 36), $"{label}    {value.ToString("0.##", CultureInfo.InvariantCulture)}  ›"))
            Edit($"{label} ({min}–{max})", value.ToString("0.##", CultureInfo.InvariantCulture), s => {
                if (!float.TryParse(s.Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out float n) || !float.IsFinite(n)) throw new FormatException("Enter a number.");
                apply(Math.Clamp(n, min, max));
            });
    }

    private bool MenuAction(Rect rect, string label)
    {
        Color previous = GUI.backgroundColor;
        bool hovered = rect.Contains(Event.current.mousePosition);
        try
        {
            if (hovered)
            {
                GUI.backgroundColor = new Color(.10f, .11f, .15f, 1);
                GUI.Box(rect, "", menuFlatStyle);
            }
            menuActionStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft
            };
            menuActionStyle.normal.textColor = new Color(.84f, .86f, .91f, 1);
            menuActionStyle.hover.textColor = Color.white;
            return GUI.Button(rect, label, menuActionStyle);
        }
        finally { GUI.backgroundColor = previous; }
    }

    private void Hex(MelonPreferences_Entry<string> entry, string label, float x, float y)
    {
        if (MenuAction(new Rect(x, y, 520, 36), $"{label}    #{entry.Value}  ›"))
            EditColor(label + " (#RRGGBB)", entry.Value, s => entry.Value = ColorHex(ParseColor(s)));
        Color c; try { c = ParseColor(entry.Value); } catch (FormatException) { c = Color.white; }
        Preview(new Rect(x + 560, y + 4, 56, 28), c);
    }
    internal static Color ParseColor(string s)
    {
        s = s.Trim().TrimStart('#');
        if (s.Length != 6 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint n)) throw new FormatException("Use six hex digits, e.g. FF8800.");
        return new Color(((n >> 16) & 255) / 255f, ((n >> 8) & 255) / 255f, (n & 255) / 255f, 1);
    }
    private static string ColorHex(Color c) => $"{(int)Math.Round(c.r * 255):X2}{(int)Math.Round(c.g * 255):X2}{(int)Math.Round(c.b * 255):X2}";
    private void Edit(string label, string value, Action<string> apply)
    { editColor = false; editLabel = label; editText = value; editApply = apply; replaceEdit = true; editError = ""; capture = null; }

    private void EditColor(string label, string value, Action<string> apply)
    {
        Edit(label, value, apply); editColor = true;
        try { editPreview = ParseColor(value); } catch (FormatException) { editPreview = Color.white; }
    }
    private void Preview(Rect rect, Color color)
    {
        colorPreviewStyle ??= new GUIStyle(GUI.skin.label);
        colorPreviewStyle.normal.background = colorPreviewStyle.hover.background =
            colorPreviewStyle.active.background = colorPreviewStyle.focused.background = Texture2D.whiteTexture;
        var old = GUI.backgroundColor;
        try { GUI.backgroundColor = color; GUI.Box(rect, "", colorPreviewStyle); }
        finally { GUI.backgroundColor = old; }
    }
    private void ColorSliders(float x, float y, float width)
    {
        try { editPreview = ParseColor(editText); } catch (FormatException) { }
        float innerWidth = width - 48;
        Rect previewFrame = new(x + 24, y + 212, innerWidth, 46);
        GUI.backgroundColor = new Color(.10f, .11f, .14f, 1);
        GUI.Box(previewFrame, "", menuFlatStyle);
        Preview(new Rect(previewFrame.x + 3, previewFrame.y + 3, previewFrame.width - 6, previewFrame.height - 6), editPreview);

        float Channel(string label, float value, float row)
        {
            GUI.Label(new Rect(x + 28, row, 86, 30), $"{label}  {Math.Round(value * 255):0}");
            return GUI.HorizontalSlider(new Rect(x + 128, row + 4, innerWidth - 150, 24), value, 0, 1);
        }
        float r = Channel("Red", editPreview.r, y + 274);
        float g = Channel("Green", editPreview.g, y + 316);
        float b = Channel("Blue", editPreview.b, y + 358);
        if (Math.Abs(r - editPreview.r) > .0001f || Math.Abs(g - editPreview.g) > .0001f || Math.Abs(b - editPreview.b) > .0001f)
        { editPreview = new Color(r, g, b); editText = ColorHex(editPreview); replaceEdit = true; editError = ""; }
        menuPopupTextStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        menuPopupTextStyle.normal.textColor = new Color(.66f, .69f, .76f, 1);
        GUI.Label(new Rect(x + 24, y + 410, innerWidth, 48), "Preview updates here. Edit the hex value above or drag the RGB sliders; Apply saves it.", menuPopupTextStyle);
    }

    private bool PopupButton(Rect rect, string label, Color background)
    {
        Color oldBackground = GUI.backgroundColor;
        Color oldContent = GUI.contentColor;
        try
        {
            menuPopupButtonStyle ??= new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            menuPopupButtonStyle.normal.textColor = menuPopupButtonStyle.hover.textColor =
                menuPopupButtonStyle.active.textColor = menuPopupButtonStyle.focused.textColor = Color.white;
            menuPopupButtonStyle.normal.background = menuPopupButtonStyle.hover.background =
                menuPopupButtonStyle.active.background = menuPopupButtonStyle.focused.background = Texture2D.whiteTexture;
            GUI.backgroundColor = background;
            GUI.contentColor = Color.white;
            return GUI.Button(rect, label, menuPopupButtonStyle);
        }
        finally
        {
            GUI.backgroundColor = oldBackground;
            GUI.contentColor = oldContent;
        }
    }

    private void DrawValueEditor()
    {
        // GUI.TextField/DoTextField is a throwing generated stub in this build.
        // Keep the safe IMGUI keyboard editor, but give it the same visual
        // hierarchy and larger targets as the main settings panel.
        float width = Math.Min(640, Screen.width - 32);
        float height = editColor ? 560 : 300;
        float x = Math.Max(16, (Screen.width - width) * .5f);
        float y = Math.Max(12, (Screen.height - height) * .5f);
        Color oldColor = GUI.color, oldBackground = GUI.backgroundColor, oldContent = GUI.contentColor;
        try
        {
            menuFlatStyle ??= new GUIStyle(GUI.skin.box);
            menuFlatStyle.normal.background = menuFlatStyle.hover.background =
                menuFlatStyle.active.background = menuFlatStyle.focused.background = Texture2D.whiteTexture;
            GUI.color = new Color(0, 0, 0, .62f);
            GUI.backgroundColor = Color.black;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", menuFlatStyle);
            GUI.color = Color.white;
            GUI.backgroundColor = new Color(.035f, .04f, .055f, 1);
            GUI.Box(new Rect(x, y, width, height), "", menuFlatStyle);
            GUI.backgroundColor = new Color(.05f, .055f, .075f, 1);
            GUI.Box(new Rect(x, y, width, 68), "", menuFlatStyle);
            GUI.backgroundColor = MenuAccentColor();
            GUI.Box(new Rect(x, y, width, 4), "", menuFlatStyle);
            GUI.backgroundColor = Color.white;
            menuPopupHeaderStyle ??= new GUIStyle(GUI.skin.label)
                { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            menuPopupHeaderStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x + 24, y + 14, width - 48, 34), editLabel, menuPopupHeaderStyle);
            menuPopupTextStyle ??= new GUIStyle(GUI.skin.label)
                { fontSize = 14, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            menuPopupTextStyle.normal.textColor = new Color(.66f, .69f, .76f, 1);
            GUI.Label(new Rect(x + 24, y + 72, width - 48, 32), "Enter a value. Press Enter to apply or Escape to cancel.", menuPopupTextStyle);

            Rect input = new(x + 24, y + 108, width - 48, 48);
            GUI.backgroundColor = new Color(.015f, .02f, .03f, 1);
            GUI.Box(input, "", menuFlatStyle);
            menuPopupValueStyle ??= new GUIStyle(GUI.skin.label)
                { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            menuPopupValueStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(input.x + 14, input.y, input.width - 28, input.height), editText + "|", menuPopupValueStyle);

            menuPopupTextStyle.normal.textColor = editError.Length == 0
                ? new Color(.66f, .69f, .76f, 1) : new Color(1f, .55f, .55f, 1);
            GUI.Label(new Rect(x + 24, y + 164, width - 48, 40), editError.Length == 0
                ? "Type to replace · Ctrl+A selects all · Ctrl+V pastes" : editError, menuPopupTextStyle);
            if (editColor) ColorSliders(x, y, width);

            float buttonY = y + height - 68;
            float buttonWidth = (width - 68) * .5f;
            bool apply = PopupButton(new Rect(x + 24, buttonY, buttonWidth, 48), "Apply",
                MenuAccentColor());
            bool cancel = PopupButton(new Rect(x + 44 + buttonWidth, buttonY, buttonWidth, 48), "Cancel",
                new Color(.14f, .15f, .18f, 1));
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) apply = true;
                else if (e.keyCode == KeyCode.Escape) cancel = true;
                else if (e.control && e.keyCode == KeyCode.A) replaceEdit = true;
                else if (e.control && e.keyCode == KeyCode.V)
                { string paste = GUIUtility.systemCopyBuffer ?? ""; editText = (replaceEdit ? "" : editText) + paste; editText = editText[..Math.Min(editText.Length, 40)]; replaceEdit = false; }
                else if (e.keyCode == KeyCode.Backspace) { editText = replaceEdit ? "" : editText.Length > 0 ? editText[..^1] : ""; replaceEdit = false; }
                else if (!char.IsControl(e.character) && e.character != '\0' && editText.Length < 40)
                { if (replaceEdit) editText = ""; replaceEdit = false; editText += e.character; }
                e.Use();
            }
            if (cancel) editApply = null;
            else if (apply) try { editApply!(editText); editApply = null; preferencesDirty = true; SavePreferences(); }
                catch (FormatException ex) { editError = ex.Message; }
        }
        finally
        {
            GUI.color = oldColor;
            GUI.backgroundColor = oldBackground;
            GUI.contentColor = oldContent;
        }
    }

    private void DrawSongHud(bool moving = false)
    {
        string text = songText.Length == 0 ? "Chart title (100%)\nArtist: Artist\nCharter: Charter" : songText;
        songStyle ??= CreateHudStyle(wordWrap: true);
        int size = Math.Clamp(songFont.Value, 14, 48);
        if (styledSongFont != size) { songStyle.fontSize = size; styledSongFont = size; }
        if (styledSongHex != songColor.Value)
        {
            try { styledSongColor = ParseColor(songColor.Value); } catch (FormatException) { styledSongColor = Color.white; }
            songStyle.normal.textColor = styledSongColor;
            styledSongHex = songColor.Value;
        }
        int width = Math.Min(520, Screen.width);
        if (!ReferenceEquals(measuredSongText, text) || measuredSongFont != size || measuredSongWidth != width)
        {
            int charsPerLine = Math.Max(8, (int)((width - 24) / (size * .6f)));
            int lineCount = 0, currentLineLength = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i == text.Length || text[i] == '\n')
                {
                    lineCount += Math.Max(1, (currentLineLength + charsPerLine - 1) / charsPerLine);
                    currentLineLength = 0;
                }
                else currentLineLength++;
            }
            measuredSongText = text;
            measuredSongFont = size;
            measuredSongWidth = width;
            measuredSongHeight = (int)(size * 1.35f) * lineCount + 20;
        }
        int height = measuredSongHeight;
        var p = HudLayout.ClampPosition(songX.Value, songY.Value, width, height, Screen.width, Screen.height);
        var rect = new Rect(p.X, p.Y, width, height);
        var old = GUI.backgroundColor;
        try { GUI.backgroundColor = new Color(0, 0, 0, HudLayout.Unit(songOpacity.Value, .65f)); GUI.Box(rect, text, songStyle); }
        finally { GUI.backgroundColor = old; }
        if (!moving) return;
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        { hudDragging = true; dragOffset = e.mousePosition - new Vector2(rect.x, rect.y); e.Use(); }
        if (hudDragging && e.type == EventType.MouseDrag)
        {
            var n = HudLayout.ClampPosition((int)(e.mousePosition.x - dragOffset.x), (int)(e.mousePosition.y - dragOffset.y), width, height, Screen.width, Screen.height);
            songX.Value = n.X; songY.Value = n.Y; preferencesDirty = true; e.Use();
        }
    }
    private bool songRenderError;
    private void DrawSongHudSafely()
    {
        if (songRenderError) return;
        try { DrawSongHud(); }
        catch (Exception ex) { songRenderError = true; LoggerInstance.Error($"Song HUD stopped after a rendering error: {ex}"); }
    }

    private bool visualError;
    private bool performanceError;
    private bool starProgressError;
    private bool hiddenLeaderboardError;
    private bool comboSmokeScrollError;
    private bool staticTmpScaleError;
    private bool optionalHudAnimationsError;
    private Il2Cpp.GameManager? optionalHudGame;
    private bool optionalHudWaitingForResume;
    public override void OnLateUpdate()
    {
        if (!compatible || !gameplay || !gameplayInitialized || transition)
        {
            if (staticTmpScale.IsActive || staticTmpScale.NeedsRestore)
                UpdateStaticTmpScaleExperiment(null, clock.Elapsed.TotalSeconds);
            if (optionalHudAnimations.NeedsRestore) optionalHudAnimations.Clear();
            return;
        }
        // Read preference entries and inspect Unity objects at the same 20 Hz
        // cadence as the actual visual work. On an unlocked game, doing these
        // checks first would still repeat them thousands of times per second.
        double now = clock.Elapsed.TotalSeconds;
        HandleOptionalHudResume(now);
        if (now < nextOverridePass) return;
        nextOverridePass = now + .05;
        // This shares the existing 20 Hz override cadence. A separate timer
        // polled every LateUpdate did no extra useful work.
        if (!hiddenLeaderboardError && (suspendHiddenLeaderboard.Value || hiddenLeaderboard.NeedsRestore))
        {
            try { hiddenLeaderboard.Apply(suspendHiddenLeaderboard.Value, now,
                suspendHiddenLeaderboard.Value && IsOnline(now)); }
            catch (Exception ex)
            {
                hiddenLeaderboard.Clear();
                hiddenLeaderboardError = true;
                LoggerInstance.Warning($"Hidden leaderboard optimization stopped; the game's entries were restored: {ex.Message}");
            }
        }
        // FC color postfixes are needed only while the customization is on.
        // Keep their Harmony hooks out of the game's setter path otherwise.
        rings.SetHookState(HarmonyInstance, tintFc.Value);

        if (useCanvasCounterHud.Value || counterCanvas.HasObjects)
        {
            long canvasTiming = timings.Begin();
            try
            {
                string? error = counterCanvas.Refresh(useCanvasCounterHud.Value,
                    showHud.Value && !settingsOpen && !moveHud && resizeTarget < 0,
                    statsText, hudX.Value, hudY.Value, fontSize.Value,
                    new Color(HudLayout.Unit(textRed.Value), HudLayout.Unit(textGreen.Value), HudLayout.Unit(textBlue.Value), 1),
                    backgroundOpacity.Value);
                if (error != null)
                    LoggerInstance.Warning($"Canvas counter HUD failed; using the original renderer: {error}");
            }
            finally { timings.End(7, canvasTiming); }
        }

        bool nativeHudRequested = nativeMoveTarget != 0 || resizeTarget >= 2 || practiceHudX.Value != 0 || practiceHudY.Value != 0
            || fpsHudX.Value != 0 || fpsHudY.Value != 0 || customizeFpsColor.Value
            || scoreHudScale.Value != 1 || inputViewerScale.Value != 1 || starPowerHudScale.Value != 1
            || practiceHudScale.Value != 1 || fpsHudScale.Value != 1;
        bool nativeHudPending = !nativeHudError && (nativeHudRequested || nativeHudActive);
        bool pauseHiddenBeats = hideBeats.Value && pauseHiddenBeatlineUpdates.Value
            && offlineSoloOptimizationActive;
        bool visualRequested = hideSides.Value || hideStrings.Value || hideBeats.Value || pauseHiddenBeats || tintScore.Value;
        bool practiceSpeedRequested = lockSpeed.Value && practiceModeActive;
        // The latest timing capture showed RingColors.Update running at every
        // visual pass even with customization off and no captured rings. Keep
        // the one cleanup pass needed after the user disables colors, then
        // stop dispatching the empty updater while other visual options run.
        bool ringUpdateDue = tintFc.Value || rings.NeedsUpdate;
        bool disableHits = disableHitParticles.Value && offlineSoloOptimizationActive;
        bool disableSustains = disableSustainParticles.Value && offlineSoloOptimizationActive;
        bool disableFrets = disableFretEffects.Value && offlineSoloOptimizationActive;
        bool performanceApplyDue = !performanceError
            && performance.IsApplyDue(disableHits, disableSustains, disableFrets, now);
        bool disableStarProgress = disableStarProgressParticles.Value && offlineSoloOptimizationActive;
        bool starProgressApplyDue = !starProgressError
            && starProgressParticles.IsApplyDue(disableStarProgress, now);
        bool freezeComboSmoke = freezeComboSmokeScroll.Value && offlineSoloOptimizationActive;
        bool comboSmokeRequested = !comboSmokeScrollError && (freezeComboSmoke || comboSmokeScroll.NeedsRestore);
        bool tmpScalePassDue = !staticTmpScaleError && !staticTmpScale.NoTargetsForScene
            && (staticTmpScaleDuringSong.Value || staticTmpScale.IsActive || staticTmpScale.NeedsRestore);
        bool optionalHudPassDue = !optionalHudAnimationsError
            && (hidePbPopup.Value || hideStarPowerSideGlows.Value || optionalHudAnimations.NeedsRestore);
        optionalHudAnimations.UpdateFocusHook(
            hidePbPopup.Value || hideStarPowerSideGlows.Value, gameplay);
        bool visualPending = !visualError && (visualRequested || practiceSpeedRequested || visuals.NeedsUpdate
            || ringUpdateDue);
        if (!nativeHudPending && !visualPending && !comboSmokeRequested && !performanceApplyDue
            && !starProgressApplyDue && !tmpScalePassDue && !optionalHudPassDue) return;

        // Avoid GameManager/IL2CPP property reads at the unlocked update rate.
        // All HUD and visual changes refresh at 5–20 Hz, so resolving the
        // manager at the same cadence is sufficient.
        var game = Il2Cpp.GameManager.field_Private_Static_GameManager_0;
        if (optionalHudPassDue) UpdateOptionalHudAnimations(game, now);
        if (tmpScalePassDue) UpdateStaticTmpScaleExperiment(game, now);
        if (starProgressApplyDue)
        {
            long starParticleTiming = timings.Begin();
            try { starProgressParticles.Apply(game, disableStarProgress, now); }
            catch (Exception ex)
            {
                starProgressParticles.Clear();
                starProgressError = true;
                LoggerInstance.Warning($"Star-progress particle optimization stopped; the original particle state was restored: {ex.Message}");
            }
            finally { timings.End(6, starParticleTiming); }
        }
        if (comboSmokeRequested)
        {
            try { comboSmokeScroll.Apply(freezeComboSmoke, game, now); }
            catch (Exception ex)
            {
                comboSmokeScroll.Clear();
                comboSmokeScrollError = true;
                LoggerInstance.Warning($"Combo smoke optimization stopped; original animation states were restored: {ex.Message}");
            }
        }
        if (game == null || game.globalVariables == null) return;
        bool songReady = game.songLoaded && game.playersInit;
        if (!songReady)
        {
            // Practice's stats panel may exist before the songLoaded/player
            // gates reopen. Keep its saved position and size usable then.
            bool practiceHudRequested = resizeTarget == 5 || nativeMoveTarget == 1
                || practiceHudX.Value != 0 || practiceHudY.Value != 0 || practiceHudScale.Value != 1;
            if (nativeHudPending && practiceHudRequested)
            {
                try { nativeHud.ApplyPracticeBeforeSongReady(game, practiceHudX.Value, practiceHudY.Value, practiceHudScale.Value); }
                catch (Exception ex)
                {
                    nativeHudError = true;
                    LoggerInstance.Error($"Practice HUD customization stopped after an error: {ex}");
                }
            }
            return;
        }

        if (nativeHudPending)
        {
            try
            {
                Color fpsTint;
                try { fpsTint = ParseColor(fpsTextColor.Value); } catch (FormatException) { fpsTint = Color.white; }
                nativeHud.Apply(game,
                    practiceHudX.Value, practiceHudY.Value, fpsHudX.Value, fpsHudY.Value,
                customizeFpsColor.Value, fpsTint, scoreHudScale.Value, inputViewerScale.Value,
                    starPowerHudScale.Value, practiceHudScale.Value,
                    fpsHudScale.Value, now);
                nativeHudActive = nativeHudRequested;
            }
            catch (Exception ex)
            {
                // Restore any transforms already scaled before the failing
                // object, so one bad HUD cannot leave the live layout warped.
                try { nativeHud.Clear(); } catch { }
                nativeHudActive = false;
                nativeHudError = true;
                if (!nativeHudErrorLogged)
                {
                    nativeHudErrorLogged = true;
                    LoggerInstance.Error($"Native HUD customization stopped after an error: {ex}");
                }
            }
        }
        if (performanceApplyDue)
        {
            long performanceTiming = timings.Begin();
            try { performance.Apply(game, disableHits, disableSustains, disableFrets, now); }
            catch (Exception ex)
            {
                performance.Clear();
                performanceError = true;
                LoggerInstance.Warning($"Performance effects optimization stopped; original effect states were restored: {ex.Message}");
            }
            finally { timings.End(6, performanceTiming); }
        }
        if (!visualPending) return;
        try
        {
            Color Read(MelonPreferences_Entry<string> e)
            {
                if (parsedColors.TryGetValue(e.Value, out var cached)) return cached;
                Color color; try { color = ParseColor(e.Value); } catch (FormatException) { color = Color.white; }
                if (parsedColors.Count > 64) parsedColors.Clear();
                parsedColors[e.Value] = color; return color;
            }
            if (ringUpdateDue)
            {
                long ringTiming = timings.Begin();
                rings.Update(game, tintFc.Value,
                    tintFc.Value ? Read(fcColor) : Color.white,
                    tintFc.Value ? Read(pfcColor) : Color.white);
                timings.End(1, ringTiming);
            }
            long visualTiming = timings.Begin();
            visuals.Apply(game, hideSides.Value, hideStrings.Value, hideBeats.Value, pauseHiddenBeats,
                tintScore.Value, Read(starColor), Read(progressColor), Read(scoreColor), Read(songProgressColor), lockSpeed.Value,
                now, timings);
            timings.End(2, visualTiming);
        }
        catch (Exception ex)
        {
            visuals.RestoreBeatRenderers();
            visuals.RestoreStringAttach();
            visualError = true;
            LoggerInstance.Error($"Visual overrides paused after an error; counters and controls remain available: {ex}");
            Notice("Visual overrides stopped after an error. Send Latest.log.");
        }
    }

    private void UpdateOptionalHudAnimations(Il2Cpp.GameManager? game, double now, bool forceReapply = false)
    {
        try
        {
            optionalHudGame = game;
            bool requested = hidePbPopup.Value || hideStarPowerSideGlows.Value;
            bool eligibleExceptPauseOrFocus = game != null && game.songLoaded && game.playersInit
                && game.globalVariables != null && game.isSongPlaying
                && !game.isSongOver && game.coroutinesRunning == 0
                && game.actualPlayerCount == 1 && (int)game.globalVariables.gameMode == 0
                && !game.globalVariables.isPlayingSetlist && !transition && !settingsOpen
                && !moveHud && resizeTarget < 0 && nativeMoveTarget == 0
                && editApply == null && capture == null && !IsOnline(now);
            bool paused = game != null && game.isPaused;
            bool focused = Application.isFocused;
            bool eligible = eligibleExceptPauseOrFocus && !paused && focused;
            optionalHudWaitingForResume = requested && eligibleExceptPauseOrFocus && (paused || !focused);
            optionalHudAnimations.Apply(game, eligible && hidePbPopup.Value,
                eligible && hideStarPowerSideGlows.Value, now, forceReapply);
        }
        catch (Exception ex)
        {
            optionalHudAnimations.Clear();
            optionalHudGame = null;
            optionalHudWaitingForResume = false;
            optionalHudAnimationsError = true;
            LoggerInstance.Warning($"Optional HUD animation optimization stopped; visuals were restored: {ex.Message}");
        }
    }

    private void HandleOptionalHudResume(double now)
    {
        if (!optionalHudAnimations.FocusResumePending && !optionalHudWaitingForResume) return;
        if (!Application.isFocused) return;
        var game = optionalHudGame;
        if (game == null)
        {
            optionalHudAnimations.ClearFocusResumePending();
            optionalHudWaitingForResume = false;
            return;
        }
        try
        {
            if (game.isPaused) return;
            optionalHudAnimations.ClearFocusResumePending();
            optionalHudWaitingForResume = false;
            UpdateOptionalHudAnimations(game, now, forceReapply: true);
        }
        catch (Exception ex)
        {
            optionalHudAnimations.Clear();
            optionalHudGame = null;
            optionalHudWaitingForResume = false;
            optionalHudAnimationsError = true;
            LoggerInstance.Warning($"Optional HUD animation resume fix stopped; visuals were restored: {ex.Message}");
        }
    }

    private void UpdateStaticTmpScaleExperiment(Il2Cpp.GameManager? game, double now)
    {
        if (staticTmpScaleError) return;
        long timing = timings.Begin();
        bool wasActive = staticTmpScale.IsActive;
        int previousCount = staticTmpScale.ModifiedCount;
        try
        {
            bool eligible = staticTmpScaleDuringSong.Value && game != null
                && game.songLoaded && game.playersInit && game.globalVariables != null
                && game.isSongPlaying && !game.isPaused && !game.isSongOver
                && game.actualPlayerCount == 1 && (int)game.globalVariables.gameMode == 0
                && !transition && !settingsOpen && !moveHud && resizeTarget < 0
                && nativeMoveTarget == 0 && editApply == null && capture == null
                && Application.isFocused && !IsOnline(now);
            staticTmpScale.Apply(eligible, now);
            if (!wasActive && staticTmpScale.IsActive)
                LoggerInstance.Msg($"TMP static-scale experiment active; {staticTmpScale.ModifiedCount} stable gameplay labels are opted out of per-frame scale checks.");
            else if (wasActive && !staticTmpScale.IsActive)
                LoggerInstance.Msg($"TMP static-scale experiment restored {previousCount} gameplay labels.");
        }
        catch (Exception ex)
        {
            staticTmpScale.Clear();
            staticTmpScaleError = true;
            LoggerInstance.Warning($"TMP static-scale experiment stopped; original label states were restored: {ex.Message}");
        }
        finally { timings.End(8, timing); }
    }

    private void LogGameplaySnapshot()
    {
        // Read once before the opt-in timer starts. Serialized scene state
        // cannot tell us whether these candidate objects stay live in a song.
        try
        {
            var root = GameObject.Find("HUDCam");
            if (root == null) return;
            var scoreList = root.transform.Find("Leaderboard/Canvas/ScoreList");
            if (scoreList != null)
            {
                var group = scoreList.GetComponent<CanvasGroup>();
                var entries = scoreList.GetComponentsInChildren<Il2Cpp.InGameLeaderboardEntry>(true);
                int running = 0;
                foreach (var entry in entries)
                    if (entry != null && entry.enabled && entry.gameObject.activeInHierarchy) running++;
                LoggerInstance.Msg($"Gameplay snapshot: leaderboard score-list active={scoreList.gameObject.activeInHierarchy}, alpha={(group == null ? "unknown" : group.alpha.ToString("0.###", CultureInfo.InvariantCulture))}, entry Updates eligible={running}/{entries.Length}.");
            }
            var viewer = root.transform.Find("Input Viewer");
            if (viewer != null)
            {
                static string Describe(Transform parent, string name)
                {
                    var branch = parent.Find(name);
                    if (branch == null) return "missing";
                    int visibleSprites = 0;
                    foreach (var renderer in branch.GetComponentsInChildren<SpriteRenderer>(true))
                        if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy) visibleSprites++;
                    return $"{branch.gameObject.activeInHierarchy}:{visibleSprites} sprites";
                }
                LoggerInstance.Msg($"Gameplay snapshot: input-viewer active:sprites guitar/GHL/drums={Describe(viewer, "Guitar Input Viewer")}/{Describe(viewer, "GHL Input Viewer")}/{Describe(viewer, "Drums Input Viewer")}.");
            }
        }
        catch (Exception ex) { LoggerInstance.Warning($"Gameplay snapshot unavailable: {ex.Message}"); }

        // One-time discovery during an explicitly requested timing capture.
        // Counts establish which native visual Update candidates actually
        // exist in a song before any optional suppression is considered.
        try
        {
            static string Count<T>() where T : Behaviour
            {
                try
                {
                    var found = UnityEngine.Object.FindObjectsOfType<T>();
                    int running = 0;
                    foreach (var component in found)
                        if (component != null && component.isActiveAndEnabled) running++;
                    return $"{running}/{found.Length}";
                }
                catch { return "unavailable"; }
            }
            LoggerInstance.Msg($"Gameplay snapshot: visual scripts enabled/found-active-objects: softMask={Count<Il2Cpp.SoftMaskController>()}, blurScaler={Count<Il2Cpp.BlurScaler>()}, customFade={Count<Il2Cpp.CustomFadeProperties>()}, beatPulse={Count<Il2Cpp.BeatPulse>()}, cameraShake={Count<Il2Cpp.CameraShake>()}, uvScroll={Count<Il2Cpp.UVScroll>()}, lyrics={Count<Il2Cpp.LyricDisplay>()}.");
            LoggerInstance.Msg($"Gameplay snapshot: live visual script paths: cameraShake={LivePaths<Il2Cpp.CameraShake>()}; uvScroll={LivePaths<Il2Cpp.UVScroll>()}; lyrics={LivePaths<Il2Cpp.LyricDisplay>()}; customFade={LivePaths<Il2Cpp.CustomFadeProperties>()}; comboSmokeTracked/pausedByMod={comboSmokeScroll.TrackedCount}/{comboSmokeScroll.PausedCount}; fretFadeScriptsPausedByMod={performance.PausedFretFadeCount}.");
            var sprites = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
            int enabledSprites = 0;
            foreach (var sprite in sprites)
                if (sprite != null && sprite.enabled && sprite.gameObject.activeInHierarchy) enabledSprites++;
            var cameras = UnityEngine.Object.FindObjectsOfType<Camera>();
            int renderTextureCameras = 0;
            foreach (var camera in cameras)
                if (camera != null && camera.isActiveAndEnabled && camera.targetTexture != null) renderTextureCameras++;
            var particles = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
            int playingParticles = 0;
            foreach (var particle in particles)
                if (particle != null && particle.gameObject.activeInHierarchy && particle.isPlaying) playingParticles++;
            var videos = UnityEngine.Object.FindObjectsOfType<VideoPlayer>();
            int enabledVideos = 0, playingVideos = 0;
            foreach (var video in videos)
                if (video != null && video.isActiveAndEnabled) { enabledVideos++; if (video.isPlaying) playingVideos++; }
            LoggerInstance.Msg($"Gameplay snapshot: SpriteRenderers enabled/found-active-objects={enabledSprites}/{sprites.Length}; cameras={Count<Camera>()} (render-texture targets={renderTextureCameras}), canvases={Count<Canvas>()}, animators={Count<UnityEngine.Animator>()}, VideoPlayers enabled/playing={enabledVideos}/{playingVideos}, playing particle systems={playingParticles}/{particles.Length}.");
        }
        catch (Exception ex) { LoggerInstance.Warning($"Visual component snapshot unavailable: {ex.Message}"); }
    }

    private static string LivePaths<T>() where T : Component
    {
        try
        {
            var found = UnityEngine.Object.FindObjectsOfType<T>();
            var paths = new List<string>(found.Length);
            foreach (var component in found)
            {
                if (component == null || component is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
                var names = new List<string>(8);
                var current = component.transform;
                while (current != null)
                {
                    names.Add(current.name);
                    current = current.parent;
                }
                names.Reverse();
                paths.Add(string.Join("/", names));
            }
            return paths.Count == 0 ? "none" : string.Join(" | ", paths);
        }
        catch { return "unavailable"; }
    }

}
