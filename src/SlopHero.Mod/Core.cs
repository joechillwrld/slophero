using System.Diagnostics;
using System.Globalization;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using SlopHero.Core;
using UnityEngine;
using UnityEngine.Rendering;

[assembly: MelonInfo(typeof(SlopHero.Mod.Core), "SlopHero", "0.4.54", "joechillwrld")]
[assembly: MelonGame("srylain Inc.", "Clone Hero")]

namespace SlopHero.Mod;

public sealed partial class Core : MelonMod
{
    private readonly HoldTrigger restartHold = new();
    private readonly HoldTrigger practiceHold = new();
    private readonly ChartStats statsReader = new();
    private readonly NativeHudLayout nativeHud = new();
    private readonly CounterCanvasHud counterCanvas = new();
    private SettingsProfiles? settingsProfiles;
    private string[] profileNames = Array.Empty<string>();
    private int profileIndex;
    private string profileNotice = "";
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private MelonPreferences_Category preferences = null!;
    private MelonPreferences_Entry<string> restartButton = null!, practiceButton = null!;
    private InputBinding.Cache restartBinding, practiceBinding;
    private MelonPreferences_Entry<float> holdSeconds = null!;
    private MelonPreferences_Entry<float> backgroundOpacity = null!, textRed = null!, textGreen = null!, textBlue = null!;
    private MelonPreferences_Entry<int> hudX = null!, hudY = null!, fontSize = null!;
    private MelonPreferences_Entry<bool> showHud = null!;
    private bool compatible, gameplay, gameplayInitialized, practiceModeActive, settingsOpen, transition, practiceFailed;
    private bool moveHud, hudDragging, preferencesDirty, cursorOverridden, savedCursorVisible;
    private int nativeMoveTarget;
    private bool nativeHudDragging, nativeHudError, nativeHudErrorLogged, nativeHudActive;
    private Vector2 nativeHudLastMouse;
    private CursorLockMode savedCursorLock;
    private Vector2 dragOffset;
    private string? capture;
    private string status = "Starting...", statsText = "";
    private string practiceWaitState = "";
    private double nextStats, nextGameStateRefresh, nextOverridePass, nextOptionalServicePass, transitionStarted, noticeUntil, finalStatsRefreshUntil;
    private bool lastStatsSongPlaying;
    private IntPtr localStatsPlayer;
    private int? pendingSection;
    private IntPtr pendingSong;
    private IntPtr sourceManager;
    private GUIStyle? hudStyle;
    private int styledHudFont = -1;
    private Color styledHudColor;
    private string? measuredHudText;
    private int measuredHudFont = -1, measuredHudHeight;
    private bool hudRenderFailed, hudRenderErrorLogged, settingsRenderErrorLogged, guiErrorLogged;

    public override void OnInitializeMelon()
    {
        preferences = MelonPreferences.CreateCategory("SlopHero");
        restartButton = preferences.CreateEntry("RestartButton", "Key:R");
        practiceButton = preferences.CreateEntry("PracticeButton", "Key:P");
        holdSeconds = preferences.CreateEntry("HoldSeconds", .75f);
        hudX = preferences.CreateEntry("HudX", 24);
        hudY = preferences.CreateEntry("HudY", 110);
        fontSize = preferences.CreateEntry("FontSize", 24);
        showHud = preferences.CreateEntry("ShowHud", true);
        backgroundOpacity = preferences.CreateEntry("BackgroundOpacity", .65f);
        textRed = preferences.CreateEntry("TextRed", 1f);
        textGreen = preferences.CreateEntry("TextGreen", 1f);
        textBlue = preferences.CreateEntry("TextBlue", 1f);
        var layoutVersion = preferences.CreateEntry("HudLayoutVersion", 0);
        if (layoutVersion.Value < 1)
        {
            if (hudX.Value == 24 && hudY.Value == 24) hudY.Value = 110;
            layoutVersion.Value = 1;
            preferences.SaveToFile();
        }
        InitializeFeatures();
        try
        {
            settingsProfiles = new SettingsProfiles(MelonEnvironment.UserDataDirectory);
            RefreshProfiles();
        }
        catch (Exception ex) { LoggerInstance.Warning($"Settings profiles unavailable: {ex.Message}"); }
        compatible = BuildGuard.Matches(out status);
        if (compatible)
        {
            LoggerInstance.Msg(status);
            try { rings.Install(HarmonyInstance); } catch (Exception ex) { LoggerInstance.Error($"FC hooks unavailable; frame overrides remain active: {ex}"); }
        }
        else LoggerInstance.Warning(status);
        LoggerInstance.Msg("F8: SlopHero settings. Default: hold R to restart, hold P to practice the current section.");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        SongSpeedControl.ResetMenuCache();
        gameplayInitialized = false;
        practiceModeActive = false;
        gameplay = string.Equals(sceneName, "Gameplay", StringComparison.OrdinalIgnoreCase);
        statsText = songText = "";
        counterCanvas.Clear();
        visuals.Clear();
        performance.Clear();
        hiddenLeaderboard.Clear();
        comboSmokeScroll.Clear();
        hiddenLeaderboardError = false;
        comboSmokeScrollError = false;
        nextHiddenLeaderboardPass = 0;
        nativeHud.Clear();
        resizeTarget = -1;
        resizeHandle = -1;
        resizeMoveDragging = false;
        nativeMoveTarget = 0;
        nativeHudDragging = false;
        nativeHudError = false;
        nativeHudActive = false;
        rings.Clear();
        visualError = false;
        nextStats = 0;
        nextGameStateRefresh = 0;
        finalStatsRefreshUntil = 0;
        lastStatsSongPlaying = false;
        localStatsPlayer = IntPtr.Zero;
        nextOverridePass = 0;
        restartHold.RequireRelease();
        practiceHold.RequireRelease();
        if (!gameplay && sceneName != "Loading") pendingSection = null;
        if (gameplay && pendingSection == null) transition = false;
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        if (string.Equals(sceneName, "Gameplay", StringComparison.OrdinalIgnoreCase))
        {
            gameplayInitialized = true;
        }
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        SongSpeedControl.ResetMenuCache();
        counterCanvas.Clear();
        if (sceneName == "Gameplay") { gameplay = false; gameplayInitialized = false; practiceModeActive = false; }
        settingsOpen = moveHud = hudDragging = false;
        resizeTarget = -1;
        resizeHandle = -1;
        resizeMoveDragging = false;
        nativeMoveTarget = 0;
        nativeHudDragging = false;
        capture = null;
        editApply = null;
        SavePreferences();
        UpdateCursor();
        statsText = songText = "";
        visuals.Clear();
        performance.Clear();
        hiddenLeaderboard.Clear();
        comboSmokeScroll.Clear();
        hiddenLeaderboardError = false;
        comboSmokeScrollError = false;
        nextHiddenLeaderboardPass = 0;
        nativeHud.Clear();
        nativeHudActive = false;
        rings.Clear();
        visualError = false;
        restartHold.RequireRelease();
        practiceHold.RequireRelease();
    }

    public override void OnUpdate()
    {
        // Diagnostics are opt-in. A Clone Hero simulation tick may skip
        // rendering, so sample Unity's render decision separately.
        if (timings.Active) timings.SampleUpdate(Time.unscaledDeltaTime * 1000f,
            OnDemandRendering.willCurrentFrameRender);
        long timingStart = timings.Begin();
        try
        {
            double now = clock.Elapsed.TotalSeconds;
            // MIDI and Discord service toggles do not participate in guitar
            // input or note timing. Poll them at 10 Hz instead of once for
            // every simulation tick on high-refresh/unlocked configurations.
            if (now >= nextOptionalServicePass)
            {
                nextOptionalServicePass = now + .1;
                optionalServices.Apply(stopMidiInput.Value, disableDiscordPresence.Value, now);
            }
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (moveHud || resizeTarget >= 0) { moveHud = false; resizeTarget = -1; resizeHandle = -1; resizeMoveDragging = false; settingsOpen = true; }
                else settingsOpen = !settingsOpen;
                hudDragging = false;
                nativeMoveTarget = 0;
                nativeHudDragging = false;
                SavePreferences();
                UpdateCursor();
                capture = null;
                editApply = null;
                restartHold.RequireRelease();
                practiceHold.RequireRelease();
            }
            if ((settingsOpen || moveHud || resizeTarget >= 0) && editApply == null && Input.GetKeyDown(KeyCode.Escape))
            {
                settingsOpen = false;
                moveHud = false;
                resizeTarget = -1;
                resizeHandle = -1;
                resizeMoveDragging = false;
                hudDragging = false;
                nativeMoveTarget = 0;
                nativeHudDragging = false;
                capture = null;
                SavePreferences();
                UpdateCursor();
            }
            if (hudDragging && !Input.GetMouseButton(0))
            {
                hudDragging = false;
                SavePreferences();
            }
            if (!Application.isFocused && (settingsOpen || moveHud || resizeTarget >= 0))
            {
                settingsOpen = moveHud = hudDragging = false;
                resizeTarget = -1;
                resizeHandle = -1;
                resizeMoveDragging = false;
                nativeMoveTarget = 0;
                nativeHudDragging = false;
                capture = null;
                SavePreferences();
                UpdateCursor();
            }
            if (capture != null)
            {
                var binding = InputBinding.Capture();
                if (binding != null)
                {
                    if (capture == "restart") restartButton.Value = binding;
                    else practiceButton.Value = binding;
                    capture = null;
                    preferences.SaveToFile();
                    restartHold.RequireRelease();
                    practiceHold.RequireRelease();
                }
            }
            if (compatible && !settingsOpen && !moveHud && !gameplay)
                SongSpeedControl.ProcessSelectedSpeedInput(songSpeedStep.Value, now);
            if (!compatible) return;
            if (transition && now - transitionStarted > 30)
            {
                pendingSection = null;
                transition = false;
                Notice("Transition did not finish; use the normal game menus and send Latest.log.");
            }

            // Poll action bindings at the simulation cadence, but avoid
            // repeatedly crossing the IL2CPP boundary for game state when no
            // action is held. HUD data still refreshes at 5 Hz; practice and
            // transition readiness refresh at 20 Hz.
            bool shortcutContext = gameplay && gameplayInitialized && !transition
                && !settingsOpen && !moveHud && resizeTarget < 0 && Application.isFocused;
            bool restartPressed = shortcutContext && InputBinding.Pressed(restartButton.Value, ref restartBinding);
            bool practicePressed = shortcutContext && InputBinding.Pressed(practiceButton.Value, ref practiceBinding);
            bool statsRefreshDue = gameplay && gameplayInitialized && now >= nextStats;
            bool gameStateRefreshDue = now >= nextGameStateRefresh;
            if (!shortcutContext && !gameplay)
            {
                practiceModeActive = false;
                restartHold.Update(false, false, now, Duration);
                practiceHold.Update(false, false, now, Duration);
                return;
            }
            if (!restartPressed && !practicePressed && !statsRefreshDue && !gameStateRefreshDue)
            {
                restartHold.Update(false, false, now, Duration);
                practiceHold.Update(false, false, now, Duration);
                return;
            }
            nextGameStateRefresh = now + .05;
            var game = gameplay && gameplayInitialized ? GameManager.field_Private_Static_GameManager_0 : null;
            bool ready = game != null && game.songLoaded && game.playersInit && game.globalVariables != null;
            bool songPlaying = ready && game!.isSongPlaying;
            if (lastStatsSongPlaying && !songPlaying)
            {
                // The native practice panel can receive its final counters
                // just after our last five-Hz sample. Recheck briefly at the
                // end, then return to the normal low-cost refresh interval.
                nextStats = 0;
                finalStatsRefreshUntil = now + 1;
            }
            lastStatsSongPlaying = songPlaying;
            practiceModeActive = ready && game!.globalVariables!.isPracticeEnabled;
            if (ready) CompletePracticeTransition(game!);
            bool allow = ready && !transition && !settingsOpen && !moveHud && resizeTarget < 0 && Application.isFocused && !game!.isPaused
                && !game.isSongOver && game.isSongPlaying && game.coroutinesRunning == 0
                && game.actualPlayerCount == 1 && (int)game.globalVariables!.gameMode == 0
                && !practiceModeActive && !game.globalVariables.isPlayingSetlist
                && (CHNetManager.instance == null || (int)CHNetManager.instance.field_Private_ConnectionState_0 == 0);
            if (restartHold.Update(allow && restartPressed, allow, now, Duration))
            {
                BeginRestart(game!);
                return;
            }
            bool practiceAllowed = allow && !practiceFailed;
            if (practiceHold.Update(practiceAllowed && practicePressed, practiceAllowed, now, Duration))
            {
                BeginPractice(game!);
                return;
            }
            if (!ready || transition || game == null || game.playerObjects == null)
            {
                statsText = "";
                if (statsRefreshDue) nextStats = now + .2;
                return;
            }
            if (now >= nextStats)
            {
                bool countersVisible = showHud.Value || (moveHud && !moveSong);
                bool songVisible = showSong.Value || (moveHud && moveSong);
                if (!countersVisible && !songVisible)
                {
                    // With both mod HUDs hidden, there is nothing to sample or
                    // format. The song controls above still stay responsive.
                    statsText = songText = "";
                    nextStats = now + .2;
                    return;
                }
                bool detailedCounters = countersVisible && (showMisses.Value || showHit.Value || showGhosts.Value);
                bool streakVisible = countersVisible && showBest.Value;
                if (detailedCounters || streakVisible)
                {
                    // Scan chart notes only for counter data that is actually
                    // displayed. The song-info HUD doesn't depend on a player.
                    // Online player arrays can start with a remote guitar.
                    // Its resolved chart heads and local engine hit counter do
                    // not describe our run, so use a non-remote input guitar.
                    var player = game.playerObjects.FirstOrDefault(p => p != null && p.enabled
                        && !p.isRemotePlayerPlaying && p.inputEnabled
                        && p.TryCast<GuitarPlayer>() != null && p.engine != null);
                    if (player != null) localStatsPlayer = player.Pointer;
                    else
                    {
                        player = game.playerObjects.FirstOrDefault(p => p != null && p.enabled
                            && !p.isRemotePlayerPlaying && p.Pointer == localStatsPlayer
                            && p.TryCast<GuitarPlayer>() != null && p.engine != null);
                        var net = CHNetManager.instance;
                        bool online = net != null && (int)net.field_Private_ConnectionState_0 != 0;
                        if (player == null && !online)
                            player = game.playerObjects.FirstOrDefault(p => p != null && p.enabled
                                && !p.isRemotePlayerPlaying && p.TryCast<GuitarPlayer>() != null && p.engine != null);
                    }
                    if (player == null)
                        statsText = "";
                    else
                    {
                        LiveStats stats = detailedCounters ? statsReader.Read(player.engine, game) : default;
                        var lines = new List<string>();
                        if (showMisses.Value) lines.Add($"Misses: {stats.Misses}");
                        if (showHit.Value)
                        {
                            string percentage = stats.HitPercentage?.ToString("0.00", CultureInfo.InvariantCulture) + "%";
                            if (stats.HitPercentage == null) percentage = "—";
                            lines.Add($"Hit: {percentage}");
                        }
                        if (showGhosts.Value) lines.Add($"Ghosts: {stats.GhostInputs}");
                        if (showBest.Value) lines.Add($"Best streak: {player.engine.field_Public_Int32_5}");
                        statsText = string.Join("\n", lines);
                    }
                }
                else statsText = "";
                if (songVisible)
                {
                    var song = game.globalVariables!.currentSongEntry;
                    var lines = new List<string>();
                    if (song != null)
                    {
                        if (showTitle.Value) lines.Add(SongTitleLabel.Format(song.Name_StrippedTags ?? "Unknown title", game.practiceSpeed, showSpeed.Value));
                        if (showArtist.Value) lines.Add("Artist: " + (song.Artist_StrippedTags ?? "Unknown"));
                        if (showCharter.Value) lines.Add("Charter: " + (song.Charter_StrippedTags ?? "Unknown"));
                    }
                    songText = string.Join("\n", lines);
                }
                else songText = "";
                nextStats = now + (now < finalStatsRefreshUntil ? .05 : .2);
            }
        }
        catch (Exception ex)
        {
            // Disable on a binding failure instead of retrying bad game calls every frame.
            compatible = false;
            statsText = "";
            pendingSection = null;
            Notice("SlopHero disabled after an error. Send MelonLoader/Latest.log.");
            LoggerInstance.Error(ex.ToString());
        }
        finally { timings.End(0, timingStart); }
    }

    private double Duration => float.IsFinite(holdSeconds.Value) ? Math.Clamp(holdSeconds.Value, .25f, 3f) : .75;

    private void BeginRestart(GameManager game)
    {
        var pause = game.pauseMenu?.GetComponent<PauseMenu>();
        if (pause == null) { Notice("Could not find the game's restart handler."); return; }
        transition = true;
        transitionStarted = clock.Elapsed.TotalSeconds;
        restartHold.RequireRelease();
        practiceHold.RequireRelease();
        Time.timeScale = 1;
        // Native 0x23C6B0: game's restart handler; avoids opening the menu.
        pause.Method_Private_Void_PDM_11();
        LoggerInstance.Msg("Requested full-song restart through the game's handler.");
    }

    private void BeginPractice(GameManager game)
    {
        if (game.currentSection < 0) { Notice("No named section here; try after the first section starts."); return; }
        var pause = game.pauseMenu?.GetComponent<PauseMenu>();
        if (pause == null) { Notice("Could not find the game's practice handler."); return; }
        pendingSection = game.currentSection;
        pendingSong = game.globalVariables.currentSongEntry.Pointer;
        sourceManager = game.Pointer;
        practiceWaitState = "";
        transition = true;
        transitionStarted = clock.Elapsed.TotalSeconds;
        restartHold.RequireRelease();
        practiceHold.RequireRelease();
        Time.timeScale = 1;
        // Native 0x23DAF0: enable practice and reload Gameplay through FadeBehaviour.
        pause.Method_Private_Void_PDM_3();
        LoggerInstance.Msg($"Requested practice at section index {pendingSection}.");
    }

    private void CompletePracticeTransition(GameManager game)
    {
        if (pendingSection == null || !gameplayInitialized || game.Pointer == sourceManager
            || !game.globalVariables.isPracticeEnabled) return;
        var player = game.practicePlayer;
        var beats = player == null ? null : player.beatRenderer;
        bool rendererReady = beats != null && beats.field_Private_ObjectPublicLi1DoObLi1InDoInLiUnique_0 != null
            && beats.field_Protected_Il2CppReferenceArray_1_Transform_0 != null
            && beats.field_Protected_Il2CppReferenceArray_1_SpriteRenderer_0 != null
            && beats.field_Protected_Il2CppReferenceArray_1_GameObject_0 != null;
        bool menuReady = game.practiceSectionMenu != null && game.practiceSectionMenu.gameObject.activeInHierarchy
            && game.practiceSectionMenu.field_Private_List_1_Object1PublicStObInStVoObInStObInUnique_0 != null;
        bool playerReady = game.songLoaded && game.playersInit && game.song != null && game.sync != null
            && game.practiceUI != null && player != null && player.engine != null;
        if (!PracticeReadiness.CanSelect(gameplayInitialized, game.Pointer != sourceManager,
            playerReady, menuReady, rendererReady, game.coroutinesRunning))
        {
            string waiting = $"player={playerReady}, sectionMenu={menuReady}, beatRenderer={rendererReady}, coroutines={game.coroutinesRunning}";
            if (waiting != practiceWaitState)
            {
                practiceWaitState = waiting;
                LoggerInstance.Msg($"Waiting to select practice section {pendingSection}: {waiting}");
            }
            return;
        }
        if (game.globalVariables.currentSongEntry == null || game.globalVariables.currentSongEntry.Pointer != pendingSong)
        {
            pendingSection = null;
            transition = false;
            Notice("Song changed; cancelled the requested practice section.");
            return;
        }
        int section = pendingSection.Value;
        pendingSection = null;
        try
        {
            // Clear optional A/B overrides so the native routine derives section bounds.
            game.practiceStartTick = 0;
            game.practiceEndTick = 0;
            game.practiceDisplayStartTick = 0;
            game.practiceDisplayEndTick = 0;
            game.Method_Public_Void_Int32_Int32_Boolean_Boolean_0(section, 0, true, true);
            // Complete the vanilla PracticeSectionMenu confirmation sequence:
            // reset the attempt, enable local player input, clear the pause flag.
            // Native RVAs 0xBF510 and 0xB6570, following 0x10B7E0.
            game.Method_Public_Void_Boolean_0(true);
            game.Method_Public_Void_2();
            game.RestartAfterPause = false;
            transition = false;
            nextStats = 0;
            Notice("Practice: starting the section you left.");
            LoggerInstance.Msg($"Selected practice section {section}.");
        }
        catch (Exception ex)
        {
            transition = false;
            practiceFailed = true;
            Notice("Automatic practice selection failed. Use the section menu; HUD remains enabled. Send Latest.log.");
            LoggerInstance.Error($"Practice selection failed: {ex}");
        }
    }

    private void Notice(string message)
    {
        status = message;
        noticeUntil = clock.Elapsed.TotalSeconds + 8;
        LoggerInstance.Msg(message);
    }

    public override void OnGUI()
    {
        if (timings.Active && Event.current.type == EventType.Repaint)
            timings.SampleRepaint();
        // Passive HUDs need only the repaint event. Keep full event processing
        // while editing so sliders, typing, and dragging remain responsive.
        bool resizeEditing = resizeTarget >= 0;
        if (!settingsOpen && !moveHud && !resizeEditing && Event.current.type != EventType.Repaint) return;
        if (counterCanvas.Visible && (!showHud.Value || settingsOpen || moveHud || resizeEditing
            || !gameplay || string.IsNullOrEmpty(statsText))) counterCanvas.Hide();
        bool noticeVisible = !compatible || (noticeUntil > 0 && clock.Elapsed.TotalSeconds < noticeUntil);
        bool counterHudVisible = !counterCanvas.Visible && showHud.Value && gameplay && !string.IsNullOrEmpty(statsText);
        bool songHudVisible = showSong.Value && gameplay && !string.IsNullOrEmpty(songText);
        if (!settingsOpen && !moveHud && !resizeEditing && !noticeVisible && !counterHudVisible && !songHudVisible) return;
        long timingStart = timings.Begin();
        Color previousColor = GUI.color;
        GUI.color = Color.white;
        try
        {
            // The settings page must not force a counter preview after the
            // master switch is turned off. Only explicit move/resize modes
            // show a placeholder for a hidden HUD.
            if (counterHudVisible || nativeMoveTarget != 0 || (moveHud && !moveSong) || resizeTarget == 0)
            {
                if (nativeMoveTarget != 0) DrawNativeHudMove();
                else
                {
                    string text = string.IsNullOrEmpty(statsText) ? "Misses: 0\nHit: 100.00%\nGhosts: 0" : statsText;
                    if (moveHud && moveSong) text = string.IsNullOrEmpty(songText) ? "Chart title (100%)\nArtist: Artist\nCharter: Charter" : songText;
                    if (hudRenderFailed) DrawBasicHud(text);
                    else
                    {
                        try { DrawHud(text); }
                        catch (Exception ex)
                        {
                            hudRenderFailed = true;
                            hudDragging = false;
                            if (!hudRenderErrorLogged)
                            {
                                hudRenderErrorLogged = true;
                                LoggerInstance.Error($"HUD appearance renderer failed; using basic counters: {ex}");
                            }
                            DrawBasicHud(text);
                        }
                    }
                }
            }
            if (!(moveHud && (moveSong || nativeMoveTarget != 0)) && ((showSong.Value && gameplay && !string.IsNullOrEmpty(songText)) || resizeTarget == 1)) DrawSongHudSafely();
            if (resizeEditing) DrawResizeHandles();
            if (noticeVisible)
                GUI.Box(new Rect(20, Screen.height - 60, Math.Max(300, Screen.width - 40), 44), status);
            if (moveHud && nativeMoveTarget == 0)
                GUI.Box(new Rect(20, Screen.height - 110, Math.Max(300, Screen.width - 40), 44),
                    "Drag the HUD anywhere. F8 returns to settings; Escape finishes. Position saves when released.");
            if (settingsOpen)
            {
                try { DrawSettings(); }
                catch (Exception ex)
                {
                    CloseEditor();
                    if (!settingsRenderErrorLogged)
                    {
                        settingsRenderErrorLogged = true;
                        LoggerInstance.Error($"Settings renderer failed; editor closed so song controls stay available: {ex}");
                    }
                }
            }
            // Sliders and drag events may be consumed; rawType retains the release.
            if (Event.current.rawType == EventType.MouseUp) SavePreferences();
        }
        catch (Exception ex)
        {
            CloseEditor();
            if (!guiErrorLogged)
            {
                guiErrorLogged = true;
                LoggerInstance.Error($"Overlay rendering failed; editor closed. Send Latest.log: {ex}");
            }
        }
        finally { GUI.color = previousColor; timings.End(3, timingStart); }
    }

    private void DrawHud(string text)
    {
        if (moveHud && moveSong) { DrawSongHud(true); return; }
        hudStyle ??= CreateHudStyle();
        int size = Math.Clamp(fontSize.Value, 14, 48);
        Color color = new(HudLayout.Unit(textRed.Value), HudLayout.Unit(textGreen.Value), HudLayout.Unit(textBlue.Value), 1);
        if (styledHudFont != size) { hudStyle.fontSize = size; styledHudFont = size; }
        if (styledHudColor != color) { hudStyle.normal.textColor = color; styledHudColor = color; }
        int width = Math.Max(240, size * 12 + 24);
        if (!ReferenceEquals(measuredHudText, text) || measuredHudFont != size)
        {
            int lines = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lines++;
            measuredHudText = text;
            measuredHudFont = size;
            measuredHudHeight = (int)(size * 1.25f) * lines + 20;
        }
        int height = measuredHudHeight;
        var position = HudLayout.ClampPosition(hudX.Value, hudY.Value, width, height, Screen.width, Screen.height);
        Rect rect = new(position.X, position.Y, width, height);
        Color previousBackground = GUI.backgroundColor;
        try
        {
            GUI.backgroundColor = new Color(0, 0, 0, HudLayout.Unit(backgroundOpacity.Value, .65f));
            GUI.Box(rect, text, hudStyle);
        }
        finally { GUI.backgroundColor = previousBackground; }
        if (!moveHud) return;
        var evt = Event.current;
        if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
        {
            hudDragging = true;
            dragOffset = evt.mousePosition - new Vector2(rect.x, rect.y);
            evt.Use();
        }
        if (hudDragging && evt.type == EventType.MouseDrag)
        {
            var newPosition = HudLayout.ClampPosition(
                Mathf.RoundToInt(evt.mousePosition.x - dragOffset.x),
                Mathf.RoundToInt(evt.mousePosition.y - dragOffset.y), width, height, Screen.width, Screen.height);
            hudX.Value = newPosition.X;
            hudY.Value = newPosition.Y;
            preferencesDirty = true;
            evt.Use();
        }
        if (hudDragging && evt.rawType == EventType.MouseUp)
        {
            hudDragging = false;
            SavePreferences();
        }
    }

    private static GUIStyle CreateHudStyle(bool wordWrap = false)
    {
        var style = new GUIStyle(GUI.skin.label) { wordWrap = wordWrap };
        var padding = style.padding;
        padding.left = 12; padding.right = 12; padding.top = 8; padding.bottom = 8;
        style.padding = padding;
        var texture = Texture2D.whiteTexture;
        style.normal.background = texture;
        style.hover.background = texture;
        style.active.background = texture;
        style.focused.background = texture;
        return style;
    }

    private void DrawBasicHud(string text)
    {
        // The original native-backed label path remains usable independently of
        // background rendering, drag controls, and the settings panel.
        var position = HudLayout.ClampPosition(hudX.Value, hudY.Value, 320, 150, Screen.width, Screen.height);
        GUI.Label(new Rect(position.X + 12, position.Y + 8, 300, 140), text);
    }

    private void DrawNativeHudMove()
    {
        var game = GameManager.field_Private_Static_GameManager_0;
        Rect rect;
        bool hasRect = nativeMoveTarget == 1 && game != null
            ? nativeHud.TryGetResizeScreenRect(game, 5, out rect)
            : nativeHud.TryGetScreenRect(nativeMoveTarget, out rect);
        if (!hasRect)
        {
            string hint = nativeMoveTarget == 1
                ? "Practice stats panel was not found in this scene. Reopen F8 → Game HUD after it appears."
                : "FPS readout was not found in this scene. Reopen F8 → Game HUD after it appears.";
            GUI.Box(new Rect(20, Screen.height - 110, Math.Max(300, Screen.width - 40), 44), hint);
            return;
        }
        string label = nativeMoveTarget == 1 ? "Practice stats — drag to move" : "FPS display — drag to move";
        GUI.Box(rect, label);
        GUI.Box(new Rect(20, Screen.height - 60, Math.Max(300, Screen.width - 40), 44),
            "Drag the highlighted native HUD. F8 returns to settings; Escape finishes.");
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        {
            nativeHudDragging = true;
            nativeHudLastMouse = e.mousePosition;
            e.Use();
        }
        if (nativeHudDragging && e.type == EventType.MouseDrag)
        {
            var delta = nativeHud.MoveByScreenDelta(nativeMoveTarget,
                e.mousePosition.x - nativeHudLastMouse.x,
                e.mousePosition.y - nativeHudLastMouse.y);
            if (nativeMoveTarget == 1)
            {
                practiceHudX.Value += delta.x;
                practiceHudY.Value += delta.y;
            }
            else
            {
                fpsHudX.Value += delta.x;
                fpsHudY.Value += delta.y;
            }
            nativeHudLastMouse = e.mousePosition;
            preferencesDirty = true;
            e.Use();
        }
        if (nativeHudDragging && e.rawType == EventType.MouseUp)
        {
            nativeHudDragging = false;
            SavePreferences();
        }
    }

    private void CloseEditor()
    {
        settingsOpen = moveHud = hudDragging = false;
        resizeTarget = -1;
        resizeHandle = -1;
        resizeMoveDragging = false;
        nativeMoveTarget = 0;
        nativeHudDragging = false;
        capture = null;
        editApply = null;
        SavePreferences();
        UpdateCursor();
        restartHold.RequireRelease();
        practiceHold.RequireRelease();
    }

    private void SavePreferences()
    {
        if (!preferencesDirty) return;
        preferences.SaveToFile();
        preferencesDirty = false;
    }

    private void UpdateCursor()
    {
        if (settingsOpen || moveHud || resizeTarget >= 0)
        {
            if (!cursorOverridden)
            {
                savedCursorVisible = Cursor.visible;
                savedCursorLock = Cursor.lockState;
                cursorOverridden = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (cursorOverridden)
        {
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
            cursorOverridden = false;
        }
    }

    public override void OnDeinitializeMelon()
    {
        timings.Finish();
        performance.Clear();
        comboSmokeScroll.Clear();
        counterCanvas.Clear();
        optionalServices.Restore();
        settingsOpen = moveHud = hudDragging = false;
        resizeTarget = -1;
        resizeHandle = -1;
        resizeMoveDragging = false;
        SavePreferences();
        UpdateCursor();
    }

    private void ChangeDuration(float delta)
    {
        holdSeconds.Value = Math.Clamp((float)Duration + delta, .25f, 3f);
        preferences.SaveToFile();
    }
}
