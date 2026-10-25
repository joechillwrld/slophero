using System.Globalization;
using MelonLoader;
using SlopHero.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

namespace SlopHero.Mod;

public sealed partial class Core
{
    private static readonly string[] SettingsPages = { "General", "Counters", "Song HUD", "Highway", "Colors", "Game HUD", "Sizing", "Profiles", "Sloptifine", "Sloptifine 2" };
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
    private MelonPreferences_Entry<string> fpsTextColor = null!;
    private MelonPreferences_Entry<float> settingsMenuScale = null!, scoreHudScale = null!, inputViewerScale = null!, starPowerHudScale = null!, practiceHudScale = null!, fpsHudScale = null!;
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
    private readonly RuntimeTimings timings = new();
    private string songText = "", editText = "", editLabel = "", editError = "";
    private Action<string>? editApply;
    private bool replaceEdit, moveSong, editColor;
    private Color editPreview = Color.white;
    private GUIStyle? colorPreviewStyle;
    private GUIStyle? menuNavStyle, menuHeaderStyle, menuPageTitleStyle;
    private int settingsPage;
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
        scale = Math.Min(scale, Math.Min(Math.Max(.5f, (Screen.width - 20f) / 900f), Math.Max(.5f, (Screen.height - 20f) / 660f)));
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(centerX, centerY));
        try { DrawScaledSettingsPage(scale); }
        finally { GUI.matrix = previousMatrix; }
    }

    private void DrawScaledSettingsPage(float scale)
    {
        // Keep the centered window, with a left category rail and a separate
        // options surface modeled on the user's mockup.
        const float menuWidth = 900, menuHeight = 660, sidebarWidth = 220;
        float frameX = (Screen.width - menuWidth) / 2f, frameY = (Screen.height - menuHeight) / 2f;
        float x = frameX + sidebarWidth + 24, y = frameY + 102;
        Color previousColor = GUI.color, previousBackground = GUI.backgroundColor;
        try
        {
            GUI.color = Color.white;
            GUI.backgroundColor = new Color(.12f, .12f, .12f, 1);
            GUI.Box(new Rect(frameX, frameY, menuWidth, menuHeight), "");
            GUI.backgroundColor = new Color(.09f, .09f, .09f, 1);
            GUI.Box(new Rect(frameX, frameY, sidebarWidth, menuHeight), "");
            GUI.backgroundColor = new Color(.34f, .34f, .34f, 1);
            GUI.Box(new Rect(frameX + sidebarWidth, frameY, menuWidth - sidebarWidth, menuHeight), "");

            menuHeaderStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            menuHeaderStyle.normal.textColor = Color.white;
            GUI.backgroundColor = MenuAccentColor();
            GUI.Box(new Rect(frameX, frameY, sidebarWidth, 54), "Mod Settings", menuHeaderStyle);

            menuNavStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            var navPadding = menuNavStyle.padding;
            navPadding.left = 14;
            menuNavStyle.padding = navPadding;
            menuNavStyle.normal.textColor = Color.white;
            menuNavStyle.hover.textColor = Color.white;
            menuPageTitleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            menuPageTitleStyle.normal.textColor = Color.white;

            const float navTop = 66, navRowHeight = 44;
            for (int i = 0; i < SettingsPages.Length; i++)
            {
                var rect = new Rect(frameX + 8, frameY + navTop + i * navRowHeight, sidebarWidth - 16, navRowHeight - 2);
                bool selected = settingsPage == i;
                bool hovered = rect.Contains(Event.current.mousePosition);
                GUI.backgroundColor = selected ? MenuAccentColor()
                    : hovered ? Color.Lerp(new Color(.14f, .14f, .14f, 1), MenuAccentColor(), .22f)
                    : (i % 2 == 0 ? new Color(.14f, .14f, .14f, 1) : new Color(.22f, .22f, .22f, 1));
                if (GUI.Button(rect, SettingsPages[i], menuNavStyle)) settingsPage = i;
            }

            GUI.backgroundColor = MenuAccentColor();
            GUI.Box(new Rect(frameX + sidebarWidth, frameY, menuWidth - sidebarWidth, 5), "");
            GUI.backgroundColor = new Color(.34f, .34f, .34f, 1);
            GUI.Label(new Rect(frameX + sidebarWidth + 24, frameY + 16, 632, 32), SettingsPages[settingsPage], menuPageTitleStyle);
            GUI.Label(new Rect(frameX + sidebarWidth + 24, frameY + 52, 632, 22), "Pause to edit. Values save automatically; Escape closes.");
        }
        finally
        {
            GUI.color = previousColor;
            GUI.backgroundColor = previousBackground;
        }
        switch (settingsPage)
        {
            case 0:
                if (GUI.Button(new Rect(x, y, 632, 34), $"Restart: {restartButton.Value} — bind")) capture = "restart";
                if (GUI.Button(new Rect(x, y + 44, 632, 34), $"Practice: {practiceButton.Value} — bind")) capture = "practice";
                Number("Hold duration (seconds)", (float)Duration, x, y + 94, .25f, 3, v => holdSeconds.Value = v);
                Number("Settings menu size", settingsMenuScale.Value, x, y + 146, .85f, 1.4f, v => { settingsMenuScale.Value = v; preferencesDirty = true; });
                GUI.Label(new Rect(x, y + 194, 632, 70), capture == null ? "Song actions apply to solo Quickplay.\nPractice begins at the start of your current named section." : $"Press a key or controller button for {capture}.");
                Number("Song speed step (%)", songSpeedStep.Value, x, y + 266, 1, 500, v => songSpeedStep.Value = SongSpeedControl.ClampStep((int)Math.Round(v)));
                if (GUI.Button(new Rect(x, y + 310, 540, 32), timings.Active ? "Finish timing report" : "Capture mod timings for 30 seconds"))
                {
                    if (timings.Active) timings.Finish();
                    else if (gameplayInitialized)
                    {
                        bool nativeHudTweaks = practiceHudX.Value != 0 || practiceHudY.Value != 0 || fpsHudX.Value != 0 || fpsHudY.Value != 0 || customizeFpsColor.Value
                            || scoreHudScale.Value != 1 || inputViewerScale.Value != 1 || starPowerHudScale.Value != 1
                            || practiceHudScale.Value != 1 || fpsHudScale.Value != 1;
                        SongSpeedControl.TryRead(out int speedSetting);
                        LoggerInstance.Msg($"Timing settings: counters={showHud.Value}[miss={showMisses.Value},hit={showHit.Value},ghost={showGhosts.Value},streak={showBest.Value}]@{hudX.Value},{hudY.Value}, songHud={showSong.Value}[title={showTitle.Value},artist={showArtist.Value},charter={showCharter.Value},speed={showSpeed.Value}]@{songX.Value},{songY.Value}, canvasCounterHud={useCanvasCounterHud.Value}, nativeOffsets(practice/FPS)={practiceHudX.Value:0.##},{practiceHudY.Value:0.##}/{fpsHudX.Value:0.##},{fpsHudY.Value:0.##}, FCcolors={tintFc.Value}, scoreColors={tintScore.Value}, constantSpeed={lockSpeed.Value}, hideSides={hideSides.Value}, hideStrings={hideStrings.Value}, hideBeatlines={hideBeats.Value}, pauseHiddenBeatlineUpdates={pauseHiddenBeatlineUpdates.Value}, disableAprilFools={disableAprilFools.Value}, perfEffects(hit/sustain/fret/starProgress)={disableHitParticles.Value}/{disableSustainParticles.Value}/{disableFretEffects.Value}/{disableStarProgressParticles.Value}[starSuppressed={starProgressParticles.IsSuppressed}], pausedFretFadeScripts={performance.PausedFretFadeCount}, hiddenLeaderboard={suspendHiddenLeaderboard.Value}, freezeComboSmokeScroll={freezeComboSmokeScroll.Value}, comboSmokeTracked/paused={comboSmokeScroll.TrackedCount}/{comboSmokeScroll.PausedCount}, MIDI(discovery/session)={stopMidiInput.Value}/{disableMidiForSession.Value}, Discord={disableDiscordPresence.Value}, cheatScanner={pauseGameplayCheatScanner.Value}[state={cheatScanner.Status}], gameplayUiEvents={pauseGameplayUiProcessing.Value}[suspended={gameplayUiEvents.IsSuspended}], highwayPause={pauseHighwayAnimations.Value}[hook={highwayAnimations.IsInstalled},suppressed={highwayAnimations.IsSuppressed}], mouseUiSkip={skipGameplayMouseUi.Value}[hook={mouseUiGameplay.IsInstalled},suppressed={mouseUiGameplay.IsSuppressed}], legacyMouseEvents={skipLegacyMouseEvents.Value}[hook={legacyMouseEvents.IsInstalled},suppressed={legacyMouseEvents.IsSuppressed}], whammyDirtyGate={throttleWhammyTexture.Value}[hooks={whammyTextureThrottle.IsInstalled}], nativeHudTweaks={nativeHudTweaks}, fpsColor={customizeFpsColor.Value}, scales(scorebox/input/SP/practice/FPS)={scoreHudScale.Value:0.##}/{inputViewerScale.Value:0.##}/{starPowerHudScale.Value:0.##}/{practiceHudScale.Value:0.##}/{fpsHudScale.Value:0.##}, renderFrameInterval={OnDemandRendering.renderFrameInterval}, configuredSpeed={(speedSetting > 0 ? speedSetting + "%" : "unknown")}.");
                        LogGameplaySnapshot();
                        timings.Start();
                    }
                    else Notice("Start a song before capturing mod timings.");
                }
                GUI.Label(new Rect(x, y + 348, 540, 42), "Type 10–5000 on the game's speed row; * means pending. Pause or press Enter to apply.");
                GUI.Label(new Rect(x, y + 394, 540, 50), "Timings are written to Latest.log. Close this menu and play.");
                break;
            case 1:
                Toggle(showHud, "Counter HUD", x, y); Toggle(showMisses, "Misses", x, y + 40);
                Toggle(showHit, "Hit percentage", x, y + 80); Toggle(showGhosts, "Ghost inputs", x, y + 120);
                Toggle(showBest, "Best streak", x, y + 160);
                Appearance(x, y + 208, false);
                break;
            case 2:
                Toggle(showSong, "Song HUD", x, y); Toggle(showTitle, "Chart title", x, y + 40);
                Toggle(showArtist, "Artist", x, y + 80); Toggle(showCharter, "Charter", x, y + 120);
                Toggle(showSpeed, "Speed beside title", x, y + 160);
                Appearance(x, y + 210, true);
                break;
            case 3:
                Toggle(hideSides, "Hide highway sides", x, y); Toggle(hideStrings, "Hide fret strings", x, y + 40);
                Toggle(hideBeats, "Hide beatlines", x, y + 80);
                Toggle(lockSpeed, "Constant practice note speed (experimental)", x, y + 130);
                GUI.Label(new Rect(x, y + 180, 540, 100), "Visual changes preserve gameplay and note timing.\nPractice speed compensation applies only in practice.\nTurn an option off to restore its normal appearance.");
                break;
            case 4:
                Toggle(tintFc, "Custom FC / perfect FC rings", x, y);
                Hex(fcColor, "FC ring", x, y + 44); Hex(pfcColor, "Perfect FC ring", x, y + 86);
                Toggle(tintScore, "Custom score colors", x, y + 132);
                Hex(starColor, "Stars", x, y + 176); Hex(progressColor, "Star progress bar", x, y + 218);
                Hex(scoreColor, "Score / accuracy text", x, y + 260);
                Hex(songProgressColor, "Song progress bar", x, y + 302);
                Hex(menuAccentHex, "Menu accent", x, y + 344);
                break;
            case 5:
                GUI.Label(new Rect(x, y, 540, 28), "Native game HUD. Offsets use the UI canvas's units.");
                if (GUI.Button(new Rect(x, y + 36, 218, 32), "Move practice stats")) BeginNativeHudMove(1);
                if (GUI.Button(new Rect(x + 226, y + 36, 250, 32), "Resize practice stats")) BeginResizeTarget(5);
                if (GUI.Button(new Rect(x + 484, y + 36, 140, 32), "Reset"))
                { practiceHudX.Value = practiceHudY.Value = 0; preferencesDirty = true; }
                Number("Practice stats horizontal offset", practiceHudX.Value, x, y + 74, -1500, 1500, v => practiceHudX.Value = v);
                Number("Practice stats vertical offset", practiceHudY.Value, x, y + 112, -1500, 1500, v => practiceHudY.Value = v);
                if (GUI.Button(new Rect(x, y + 158, 218, 32), "Move FPS display")) BeginNativeHudMove(2);
                if (GUI.Button(new Rect(x + 226, y + 158, 250, 32), "Resize FPS display")) BeginResizeTarget(6);
                if (GUI.Button(new Rect(x + 484, y + 158, 140, 32), "Reset"))
                { fpsHudX.Value = fpsHudY.Value = 0; preferencesDirty = true; }
                Number("FPS horizontal offset", fpsHudX.Value, x, y + 196, -1500, 1500, v => fpsHudX.Value = v);
                Number("FPS vertical offset", fpsHudY.Value, x, y + 234, -1500, 1500, v => fpsHudY.Value = v);
                Toggle(customizeFpsColor, "Customize FPS number color", x, y + 276);
                Hex(fpsTextColor, "FPS number color", x, y + 314);
                Number("FPS readout size", fpsHudScale.Value, x, y + 356, .5f, 2.5f, v => { fpsHudScale.Value = v; preferencesDirty = true; });
                GUI.Label(new Rect(x, y + 400, 600, 60), "Color and size target the numeric FPS readout, not its separator. Position moves the full FPS row. The game's FPS display must be enabled first.");
                break;
            case 6:
                GUI.Label(new Rect(x, y, 632, 32), "Score box includes score, combo, star progress, and song progress.");
                Number("Score box", scoreHudScale.Value, x, y + 42, .5f, 2.5f, v => { scoreHudScale.Value = v; preferencesDirty = true; });
                Number("Input viewer", inputViewerScale.Value, x, y + 82, .5f, 2.5f, v => { inputViewerScale.Value = v; preferencesDirty = true; });
                Number("Star power bar", starPowerHudScale.Value, x, y + 122, .5f, 2.5f, v => { starPowerHudScale.Value = v; preferencesDirty = true; });
                Number("Practice stats bar", practiceHudScale.Value, x, y + 162, .5f, 2.5f, v => { practiceHudScale.Value = v; preferencesDirty = true; });
                int sizingTargetCount = ResizeTargetLabels.Length - 1;
                for (int i = 0; i < sizingTargetCount; i++)
                {
                    int column = i % 4, row = i / 4;
                    float buttonWidth = 632f / 4 - 4;
                    int columnsInRow = Math.Min(4, sizingTargetCount - row * 4);
                    float rowOffset = (632f - columnsInRow * buttonWidth - (columnsInRow - 1) * 4) * .5f;
                    var oldBackground = GUI.backgroundColor;
                    try
                    {
                        if (resizeSelection == i) GUI.backgroundColor = MenuAccentColor();
                        if (GUI.Button(new Rect(x + rowOffset + column * (buttonWidth + 4), y + 332 + row * 36, buttonWidth, 32), ResizeTargetLabels[i])) resizeSelection = i;
                    }
                    finally { GUI.backgroundColor = oldBackground; }
                }
                if (GUI.Button(new Rect(x, y + 416, 400, 34), "Edit selected HUD with corner handles")) BeginResizeTarget(resizeSelection);
                GUI.Label(new Rect(x + 410, y + 416, 220, 56), "F8 returns here; Escape finishes.");
                GUI.Label(new Rect(x, y + 462, 632, 48), "Drag a corner to resize. Drag counters or song info to move; practice and FPS layout controls are also on Game HUD.");
                break;
            case 7:
                DrawProfiles(x, y);
                break;
            case 8:
                SectionHeader("Visual effects", x, y);
                Toggle(disableHitParticles, "Skip note-hit particles", x, y + 26);
                Toggle(disableSustainParticles, "Skip sustain particles", x, y + 56);
                Toggle(disableFretEffects, "Disable fret flames, sparks, and lightning", x, y + 86);
                Toggle(disableStarProgressParticles, "Skip star-progress burst particles", x, y + 116);
                Toggle(freezeComboSmokeScroll, "Pause combo-smoke animation (smoke stays visible)", x, y + 146);
                Toggle(pauseHiddenBeatlineUpdates, "Skip beatline updates while hidden", x, y + 176);
                Toggle(disableAprilFools, "Disable April Fools / NoteBomber chaos and Pong", x, y + 206);
                GUI.Label(new Rect(x + 22, y + 236, 610, 28),
                    "Live toggle; turn off here to restore the game's joke modes.");

                SectionHeader("Services and HUD", x, y + 270);
                Toggle(suspendHiddenLeaderboard, "Pause offline leaderboard and spectator UI", x, y + 296);
                Toggle(stopMidiInput, "Pause drum MIDI device discovery", x, y + 324);
                Toggle(disableDiscordPresence, "Disable Discord Rich Presence (restart to restore)", x, y + 352);
                Toggle(useCanvasCounterHud, "Use experimental Canvas renderer for the counter HUD", x, y + 380);
                break;
            case 9:
                SectionHeader("Engine and scheduler experiments", x, y);
                Toggle(skipGameplayMouseUi, "Skip mouse UI raycasts during gameplay", x, y + 26);
                Toggle(skipLegacyMouseEvents, "Skip legacy camera mouse events during gameplay", x, y + 56);
                Toggle(throttleWhammyTexture, "Skip duplicate whammy texture uploads", x, y + 86);
                Toggle(pauseGameplayCheatScanner, "Pause cheat-code scanner during solo songs", x, y + 116);
                Toggle(pauseGameplayUiProcessing, "Pause UI events during solo songs (also pauses UI navigation)", x, y + 146);
                Toggle(pauseHighwayAnimations, "Freeze highway, video, and track-fade visuals in solo play", x, y + 176);
                Toggle(reduceUnityJobWorkers, "Use one fewer Unity job worker during offline solo songs", x, y + 206);
                Toggle(disableMidiForSession, "Disable drum MIDI input (restart to restore)", x, y + 236);
                Toggle(staticTmpScaleDuringSong, "Skip TMP scale checks in offline solo play", x, y + 266);
                GUI.Label(new Rect(x + 22, y + 300, 610, 118), "Experimental; default off. Legacy mouse callbacks from Clone Hero or other mods are skipped only during active gameplay; vanilla callbacks return on pause, menus, or focus loss. Mouse UI raycasts are a separate switch. TMP layout and graphic rebuilds stay enabled; labels under animation are checked at 20 Hz and restored if their scale changes, though text may briefly soften. Settings restore on pause, menus, scene changes, or unload. Drum MIDI shutdown requires a restart to restore.");
                break;
        }
        if (editApply != null) DrawValueEditor();
    }

    private void Toggle(MelonPreferences_Entry<bool> entry, string label, float x, float y)
    {
        bool value = GUI.Toggle(new Rect(x, y, 632, 30), entry.Value, label);
        if (value != entry.Value)
        {
            entry.Value = value;
            preferencesDirty = true;
            nextStats = 0;
            if (ReferenceEquals(entry, showHud) && !value)
            {
                counterCanvas.Hide();
                statsText = "";
            }
        }
    }

    private GUIStyle? sectionHeaderStyle;
    private void SectionHeader(string text, float x, float y)
    {
        sectionHeaderStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(x, y, 632, 24), text, sectionHeaderStyle);
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
            Float("SettingsMenuScale", settingsMenuScale); Float("ScoreHudScale", scoreHudScale); Float("InputViewerScale", inputViewerScale); Float("StarPowerHudScale", starPowerHudScale); Float("PracticeHudScale", practiceHudScale); Float("FpsHudScale", fpsHudScale);
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
        if (values.ContainsKey("PerformanceFreezeComboSmokeScroll"))
            Bool("PerformanceFreezeComboSmokeScroll", freezeComboSmokeScroll);
        else
            Bool("PerformanceFreezeLyricsBackgroundScroll", freezeComboSmokeScroll);
        Bool("CustomizeFcRings", tintFc); Bool("CustomizeScoreColors", tintScore); Bool("CustomizeFpsColor", customizeFpsColor);
        Float("SettingsMenuScale", settingsMenuScale, .85f, 1.4f); Float("ScoreHudScale", scoreHudScale, .5f, 2.5f); Float("InputViewerScale", inputViewerScale, .5f, 2.5f); Float("StarPowerHudScale", starPowerHudScale, .5f, 2.5f); Float("PracticeHudScale", practiceHudScale, .5f, 2.5f); Float("FpsHudScale", fpsHudScale, .5f, 2.5f);
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
            if (GUI.Button(new Rect(x, y + 80, 460, 32), "Text color: #" + hex))
                EditColor("Text color (#RRGGBB)", hex, s => { var c = ParseColor(s); textRed.Value = c.r; textGreen.Value = c.g; textBlue.Value = c.b; });
            Preview(new Rect(x + 470, y + 80, 70, 32), ParseColor(hex));
        }
        if (GUI.Button(new Rect(x, y + 126, 265, 32), "Move this HUD"))
        {
            moveSong = song; moveHud = true; nativeMoveTarget = 0; nativeHudDragging = false; settingsOpen = false; capture = null; UpdateCursor();
            restartHold.RequireRelease(); practiceHold.RequireRelease();
        }
        if (GUI.Button(new Rect(x + 275, y + 126, 265, 32), "Reset position"))
        { if (song) { songX.Value = 400; songY.Value = 110; } else { hudX.Value = 24; hudY.Value = 110; } preferencesDirty = true; }
        Number("Position X", song ? songX.Value : hudX.Value, x, y + 166, 0, Screen.width, v => { if (song) songX.Value = (int)v; else hudX.Value = (int)v; });
        Number("Position Y", song ? songY.Value : hudY.Value, x, y + 206, 0, Screen.height, v => { if (song) songY.Value = (int)v; else hudY.Value = (int)v; });
    }

    private void Number(string label, float value, float x, float y, float min, float max, Action<float> apply)
    {
        if (GUI.Button(new Rect(x, y, 540, 32), label + ": " + value.ToString("0.##", CultureInfo.InvariantCulture)))
            Edit($"{label} ({min}–{max})", value.ToString("0.##", CultureInfo.InvariantCulture), s => {
                if (!float.TryParse(s.Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out float n) || !float.IsFinite(n)) throw new FormatException("Enter a number.");
                apply(Math.Clamp(n, min, max));
            });
    }

    private void Hex(MelonPreferences_Entry<string> entry, string label, float x, float y)
    {
        if (GUI.Button(new Rect(x, y, 460, 32), label + ": #" + entry.Value))
            EditColor(label + " (#RRGGBB)", entry.Value, s => entry.Value = ColorHex(ParseColor(s)));
        Color c; try { c = ParseColor(entry.Value); } catch (FormatException) { c = Color.white; }
        Preview(new Rect(x + 470, y, 70, 32), c);
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
    private void ColorSliders(float x, float y)
    {
        try { editPreview = ParseColor(editText); } catch (FormatException) { }
        Preview(new Rect(x + 20, y + 118, 460, 40), editPreview);
        float Channel(string label, float value, float row)
        {
            GUI.Label(new Rect(x + 20, row, 100, 25), $"{label}: {Math.Round(value * 255)}");
            return GUI.HorizontalSlider(new Rect(x + 125, row + 7, 350, 20), value, 0, 1);
        }
        float r = Channel("Red", editPreview.r, y + 180), g = Channel("Green", editPreview.g, y + 218), b = Channel("Blue", editPreview.b, y + 256);
        if (Math.Abs(r - editPreview.r) > .0001f || Math.Abs(g - editPreview.g) > .0001f || Math.Abs(b - editPreview.b) > .0001f)
        { editPreview = new Color(r, g, b); editText = ColorHex(editPreview); replaceEdit = true; editError = ""; }
        GUI.Label(new Rect(x + 20, y + 296, 460, 28), "Preview only until Apply. Type hex or use the RGB sliders.");
    }

    private void DrawValueEditor()
    {
        // GUI.TextField/DoTextField is a throwing generated stub in this build.
        // Use native string controls and IMGUI keyboard events instead.
        int height = editColor ? 390 : 190;
        float x = Math.Max(10, (Screen.width - 500) / 2f), y = Math.Max(10, (Screen.height - height) / 2f);
        GUI.Box(new Rect(x, y, 500, height), editLabel);
        GUI.Box(new Rect(x + 20, y + 40, 460, 34), editText + "|");
        GUI.Label(new Rect(x + 20, y + 82, 460, 28), editError.Length == 0 ? "Type to replace. Ctrl+A selects all; Ctrl+V pastes." : editError);
        if (editColor) ColorSliders(x, y);
        float buttonY = y + height - 64;
        bool apply = GUI.Button(new Rect(x + 20, buttonY, 220, 32), "Apply"), cancel = GUI.Button(new Rect(x + 260, buttonY, 220, 32), "Cancel");
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
    public override void OnLateUpdate()
    {
        if (!compatible || !gameplay || !gameplayInitialized || transition)
        {
            if (staticTmpScale.IsActive || staticTmpScale.NeedsRestore)
                UpdateStaticTmpScaleExperiment(null, clock.Elapsed.TotalSeconds);
            return;
        }
        // Read preference entries and inspect Unity objects at the same 20 Hz
        // cadence as the actual visual work. On an unlocked game, doing these
        // checks first would still repeat them thousands of times per second.
        double now = clock.Elapsed.TotalSeconds;
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
        bool pauseHiddenBeats = hideBeats.Value && pauseHiddenBeatlineUpdates.Value;
        bool visualRequested = hideSides.Value || hideStrings.Value || hideBeats.Value || pauseHiddenBeats || tintScore.Value;
        bool practiceSpeedRequested = lockSpeed.Value && practiceModeActive;
        // The latest timing capture showed RingColors.Update running at every
        // visual pass even with customization off and no captured rings. Keep
        // the one cleanup pass needed after the user disables colors, then
        // stop dispatching the empty updater while other visual options run.
        bool ringUpdateDue = tintFc.Value || rings.NeedsUpdate;
        bool disableHits = disableHitParticles.Value;
        bool disableSustains = disableSustainParticles.Value;
        bool disableFrets = disableFretEffects.Value;
        bool performanceApplyDue = !performanceError
            && performance.IsApplyDue(disableHits, disableSustains, disableFrets, now);
        bool starProgressApplyDue = !starProgressError
            && starProgressParticles.IsApplyDue(disableStarProgressParticles.Value, now);
        bool comboSmokeRequested = !comboSmokeScrollError && (freezeComboSmokeScroll.Value || comboSmokeScroll.NeedsRestore);
        bool tmpScalePassDue = !staticTmpScaleError
            && (staticTmpScaleDuringSong.Value || staticTmpScale.IsActive || staticTmpScale.NeedsRestore);
        bool visualPending = !visualError && (visualRequested || practiceSpeedRequested || visuals.NeedsUpdate
            || ringUpdateDue);
        if (!nativeHudPending && !visualPending && !comboSmokeRequested && !performanceApplyDue
            && !starProgressApplyDue && !tmpScalePassDue) return;

        // Avoid GameManager/IL2CPP property reads at the unlocked update rate.
        // All HUD and visual changes refresh at 5–20 Hz, so resolving the
        // manager at the same cadence is sufficient.
        var game = Il2Cpp.GameManager.field_Private_Static_GameManager_0;
        if (tmpScalePassDue) UpdateStaticTmpScaleExperiment(game, now);
        if (starProgressApplyDue)
        {
            long starParticleTiming = timings.Begin();
            try { starProgressParticles.Apply(game, disableStarProgressParticles.Value, now); }
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
            try { comboSmokeScroll.Apply(freezeComboSmokeScroll.Value, game, now); }
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
            visualError = true;
            LoggerInstance.Error($"Visual overrides paused after an error; counters and controls remain available: {ex}");
            Notice("Visual overrides stopped after an error. Send Latest.log.");
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
