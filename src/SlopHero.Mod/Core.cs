using System.Diagnostics;
using System.Globalization;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using SlopHero.Core;
using UnityEngine;
using UnityEngine.Rendering;

[assembly: MelonInfo(typeof(SlopHero.Mod.Core), "SlopHero", "0.4.139", "joechillwrld")]
[assembly: MelonGame("srylain Inc.", "Clone Hero")]

namespace SlopHero.Mod;

public sealed partial class Core : MelonMod
{
    private readonly HoldTrigger restartHold = new();
    private readonly HoldTrigger practiceHold = new();
    private readonly ShortcutPollingGate shortcutPolling = new();
    private bool lastRestartShortcutPressed, lastPracticeShortcutPressed;
    private readonly ChartStats statsReader = new();
    private readonly NativeHudLayout nativeHud = new();
    private readonly CounterCanvasHud counterCanvas = new();
    private readonly CounterCanvasHud songCanvas = new(songMode: true);
    private readonly ClonZonesFlameTickGate clonZonesFlameTickGate = new();
    private readonly ClonZonesFlameSuppressor clonZonesFlameSuppressor = new();
    private readonly ClonZonesOriginalFretRenderers clonZonesOriginalFretRenderers = new();
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
    private double nextAprilFoolsPass;
    private double nextOptionalHookSettingsPass;
    private double nextUnfocusedRestorePass;
    private double nextRainbowPreferenceCheck, nextRainbowAnimationCheck;
    private bool rainbowAnimationPending;
    private double nextOnlineStateCheck;
    private bool onlineStateKnown, onlineState, offlineSoloOptimizationActive;
    private bool activeGameplayVisualOptimizationActive;
    private bool? lastWhammyCapSetting;
    private bool? lastHighwayPauseSetting;
    private bool aprilFoolsError;
    private bool gameplayUiEventError;
    private bool? lastCheatScannerSetting;
    private bool? lastCheatScannerSuppressed;
    private bool? lastGameplayUiSuspendState;
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
            // Prepare the exact-build native trampoline at mod startup, well
            // before Gameplay scene loading. Preparation immediately undoes
            // the hook; eligible solo songs still control when it is active.
            legacyMouseEvents.Prepare(skipLegacyMouseEvents.Value);
        }
        else LoggerInstance.Warning(status);
        LoggerInstance.Msg("F8: SlopHero settings. Default: hold R to restart, hold P to practice the current section.");
    }

    private void ResetNativeBridgesForScene()
    {
        if (!compatible || !retainNativeBridgesBetweenScenes.Value)
        {
            nativeSustainCache.Clear();
            fretSortingOrderCache.Clear();
            plainHeldSustains.Clear(HarmonyInstance);
            return;
        }
        // Keep only bridges whose own feature remains selected. Their native
        // flags route calls through to the original game methods while idle;
        // the tables are cleared on reactivation and glow scene references
        // are discarded before the outgoing objects are destroyed.
        try
        {
            if (cacheRegularSustainWrites.Value) nativeSustainCache.SuspendForScene();
            else nativeSustainCache.Clear();
            if (cacheFretSortingWrites.Value) fretSortingOrderCache.SuspendForScene();
            else fretSortingOrderCache.Clear();
            if (hideHeldSustainGlow.Value) plainHeldSustains.SuspendForScene();
            else plainHeldSustains.Clear(HarmonyInstance);
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"Native bridge retention stopped; restoring original callsites: {ex.Message}");
            nativeSustainCache.Clear();
            fretSortingOrderCache.Clear();
            plainHeldSustains.Clear(HarmonyInstance);
        }
    }

    private void ResetRewiredHooksForScene()
    {
        if (!compatible || !retainRewiredHooksBetweenScenes.Value)
        {
            rewiredMousePolling.Clear(HarmonyInstance);
            rewiredKeyboardPolling.Clear(HarmonyInstance);
            return;
        }
        if (pauseRewiredMousePolling.Value) rewiredMousePolling.Disable();
        else rewiredMousePolling.Clear(HarmonyInstance);
        if (pauseRewiredKeyboardPolling.Value) rewiredKeyboardPolling.Disable();
        else rewiredKeyboardPolling.Clear(HarmonyInstance);
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        clonZonesOriginalFretRenderers.SelectForScene(useClonZonesOriginalFretRenderers.Value,
            string.Equals(sceneName, "Gameplay", StringComparison.OrdinalIgnoreCase));
        unityJobWorkerLimit.Clear();
        staticTmpScale.Clear();
        ResetRewiredHooksForScene();
        backgroundCamera.Clear();
        hudCameraBenchmark.Clear();
        guitarCameraBenchmark.Clear();
        playerCameraFarClip.Clear();
        rainbowHighway.Clear();
        staticMeterAttach.Clear();
        clonZonesFlameTickGate.Clear();
        clonZonesFlameSuppressor.Clear();
        inputViewerSuppression.Clear();
        ResetNativeBridgesForScene();
        optionalHudAnimations.Clear();
        optionalHudGame = null;
        optionalHudWaitingForResume = false;
        optionalHudAnimationsError = false;
        staticTmpScaleError = false;
        SongSpeedControl.ResetMenuCache();
        gameplayInitialized = false;
        onlineStateKnown = false;
        nextOnlineStateCheck = 0;
        offlineSoloOptimizationActive = false;
        activeGameplayVisualOptimizationActive = false;
        practiceModeActive = false;
        gameplay = string.Equals(sceneName, "Gameplay", StringComparison.OrdinalIgnoreCase);
        statsText = songText = "";
        counterCanvas.Clear();
        songCanvas.Clear();
        cheatScanner.Clear(HarmonyInstance);
        gameplayUiEventError = false;
        lastCheatScannerSetting = null;
        lastCheatScannerSuppressed = null;
        lastGameplayUiSuspendState = null;
        gameplayUiEvents.Clear(HarmonyInstance);
        mouseUiGameplay.Clear(HarmonyInstance);
        // Retain the already prepared, inactive trampoline across scenes so
        // its one-time setup does not recur during Gameplay loading.
        legacyMouseEvents.Disable();
        highwayAnimations.Clear(HarmonyInstance);
        lastHighwayPauseSetting = null;
        nextOptionalHookSettingsPass = 0;
        if (compatible && gameplay && skipGameplayMouseUi.Value)
            mouseUiGameplay.Update(HarmonyInstance, requested: true, gameplayActive: false);
        if (compatible && gameplay && pauseGameplayUiProcessing.Value)
            gameplayUiEvents.Update(HarmonyInstance, requested: true, gameplayActive: false);
        if (compatible)
        {
            bool pauseCheats = pauseGameplayCheatScanner.Value;
            cheatScanner.Update(HarmonyInstance, pauseCheats, gameplay);
            lastCheatScannerSetting = pauseCheats;
            bool capRequested = throttleWhammyTexture.Value;
            whammyTextureThrottle.Update(HarmonyInstance, capRequested, gameplay);
            whammyTextureThrottle.SetSuppressed(true);
            lastWhammyCapSetting = capRequested;
            bool pauseHighway = pauseHighwayAnimations.Value;
            highwayAnimations.Update(HarmonyInstance, pauseHighway, gameplay);
            lastHighwayPauseSetting = pauseHighway;
        }
        visuals.Clear();
        performance.Clear();
        starProgressParticles.Clear();
        hiddenLeaderboard.Clear();
        comboSmokeScroll.Clear();
        hiddenLeaderboardError = false;
        comboSmokeScrollError = false;
        nativeHud.Clear();
        rings.Clear(HarmonyInstance);
        if (compatible && gameplay) rings.SetHookState(HarmonyInstance, tintFc.Value);
        resizeTarget = -1;
        resizeHandle = -1;
        resizeMoveDragging = false;
        nativeMoveTarget = 0;
        nativeHudDragging = false;
        nativeHudError = false;
        nativeHudActive = false;
        visualError = false;
        performanceError = false;
        starProgressError = false;
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
            if (compatible) legacyMouseEvents.Prepare(skipLegacyMouseEvents.Value);
            if (compatible) rewiredMousePolling.Prepare(HarmonyInstance, pauseRewiredMousePolling.Value);
            if (compatible) rewiredKeyboardPolling.Prepare(HarmonyInstance, pauseRewiredKeyboardPolling.Value);
            // Patch exact native callsites while the Gameplay scene is
            // initializing, before the first active-song Update.
            if (compatible) plainHeldSustains.Prepare(hideHeldSustainGlow.Value);
            if (compatible) nativeSustainCache.Prepare(cacheRegularSustainWrites.Value);
            if (compatible) fretSortingOrderCache.Prepare(cacheFretSortingWrites.Value);
            if (compatible && limitClonZonesFlameTicks.Value
                && (!disableFretEffects.Value || rainbowFlames.Value))
                clonZonesFlameTickGate.Update(HarmonyInstance, requested: true, activeSong: false);
            if (compatible && staticTmpScaleDuringSong.Value)
            {
                try { staticTmpScale.Prepare(); }
                catch (Exception ex)
                {
                    staticTmpScale.Clear();
                    staticTmpScaleError = true;
                    LoggerInstance.Warning($"TMP scene preparation stopped; vanilla text updates remain enabled: {ex.Message}");
                }
            }
        }
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (string.Equals(sceneName, "Gameplay", StringComparison.OrdinalIgnoreCase))
            clonZonesOriginalFretRenderers.Restore();
        staticTmpScale.Clear();
        ResetRewiredHooksForScene();
        backgroundCamera.Clear();
        hudCameraBenchmark.Clear();
        guitarCameraBenchmark.Clear();
        playerCameraFarClip.Clear();
        rainbowHighway.Clear();
        staticMeterAttach.Clear();
        clonZonesFlameTickGate.Clear();
        clonZonesFlameSuppressor.Clear();
        ResetNativeBridgesForScene();
        optionalHudAnimations.Clear();
        optionalHudGame = null;
        optionalHudWaitingForResume = false;
        optionalHudAnimationsError = false;
        staticTmpScaleError = false;
        SongSpeedControl.ResetMenuCache();
        counterCanvas.Clear();
        songCanvas.Clear();
        cheatScanner.Clear(HarmonyInstance);
        gameplayUiEventError = false;
        lastCheatScannerSetting = null;
        lastCheatScannerSuppressed = null;
        lastGameplayUiSuspendState = null;
        gameplayUiEvents.Clear(HarmonyInstance);
        mouseUiGameplay.Clear(HarmonyInstance);
        legacyMouseEvents.Disable();
        highwayAnimations.Clear(HarmonyInstance);
        lastHighwayPauseSetting = null;
        nextOptionalHookSettingsPass = 0;
        if (sceneName == "Gameplay")
        {
            gameplay = false;
            gameplayInitialized = false;
            practiceModeActive = false;
            offlineSoloOptimizationActive = false;
            activeGameplayVisualOptimizationActive = false;
        }
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
        starProgressParticles.Clear();
        hiddenLeaderboard.Clear();
        comboSmokeScroll.Clear();
        hiddenLeaderboardError = false;
        comboSmokeScrollError = false;
        nativeHud.Clear();
        nativeHudActive = false;
        rings.Clear(HarmonyInstance);
        visualError = false;
        performanceError = false;
        starProgressError = false;
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
            if (noticeUntil > 0 && now >= noticeUntil) noticeUntil = 0;
            if (compatible && !aprilFoolsError && now >= nextAprilFoolsPass)
            {
                nextAprilFoolsPass = now + .1;
                try { aprilFools.Apply(disableAprilFools.Value, now, scanPong: !gameplay); }
                catch (Exception ex)
                {
                    aprilFools.Restore();
                    aprilFoolsError = true;
                    LoggerInstance.Warning($"April Fools suppression stopped safely after a game binding error: {ex.Message}");
                }
            }
            // MIDI and Discord service toggles do not participate in guitar
            // input or note timing. Poll them at 10 Hz instead of once for
            // every simulation tick on high-refresh/unlocked configurations.
            if (now >= nextOptionalServicePass)
            {
                nextOptionalServicePass = now + .1;
                optionalServices.Apply(stopMidiInput.Value, disableMidiForSession.Value, disableDiscordPresence.Value, now);
            }
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (moveHud || resizeTarget >= 0) { moveHud = false; resizeTarget = -1; resizeHandle = -1; resizeMoveDragging = false; settingsOpen = true; }
                else settingsOpen = !settingsOpen;
                if (settingsOpen || moveHud || resizeTarget >= 0)
                {
                    inputViewerSuppression.Restore();
                    mouseUiGameplay.Disable();
                    legacyMouseEvents.Disable();
                    rewiredMousePolling.Disable();
                    rewiredKeyboardPolling.Disable();
                    backgroundCamera.Restore();
                    hudCameraBenchmark.Restore();
                    guitarCameraBenchmark.Restore();
                    nativeSustainCache.Update(cacheRegularSustainWrites.Value, eligible: false);
                    fretSortingOrderCache.Update(cacheFretSortingWrites.Value, eligibleGameplaySong: false);
                    plainHeldSustains.Update(HarmonyInstance, hideHeldSustainGlow.Value, eligible: false, game: null);
                    cheatScanner.Disable();
                    gameplayUiEvents.Disable();
                    highwayAnimations.SetSuppressed(false);
                }
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
            // Unity's focus getter crosses the IL2CPP boundary. The game can
            // run thousands of simulation ticks per second, so read it once
            // for all of this tick's restore and eligibility checks.
            bool focused = Application.isFocused;
            if (!focused && (settingsOpen || moveHud || resizeTarget >= 0))
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
            // The first blur restores immediately. While unfocused, reconcile
            // at 20 Hz so a scene load cannot leave a new hook active, without
            // running a dozen idempotent restore routines on every tick.
            if (focused) nextUnfocusedRestorePass = 0;
            else if (now >= nextUnfocusedRestorePass)
            {
                nextUnfocusedRestorePass = now + .05;
                if (mouseUiGameplay.IsInstalled) mouseUiGameplay.Disable();
                inputViewerSuppression.Restore();
                if (legacyMouseEvents.IsInstalled) legacyMouseEvents.Disable();
                if (rewiredMousePolling.IsInstalled) rewiredMousePolling.Disable();
                rewiredKeyboardPolling.Disable();
                backgroundCamera.Restore();
                hudCameraBenchmark.Restore();
                guitarCameraBenchmark.Restore();
                // Suspend selected native hooks on blur. Their inactive stubs
                // pass through; uninstalling here forced a costly re-prepare
                // on the first song tick after Alt+Tab.
                nativeSustainCache.Update(cacheRegularSustainWrites.Value, eligible: false);
                fretSortingOrderCache.Update(cacheFretSortingWrites.Value, eligibleGameplaySong: false);
                plainHeldSustains.Update(HarmonyInstance, hideHeldSustainGlow.Value, eligible: false, game: null);
                playerCameraFarClip.Restore();
                highwayAnimations.SetSuppressed(false);
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
            // Scene transitions already reconcile the hook. While a scene is
            // stable, only a changed preference needs patch work; this path
            // otherwise ran on every unlocked simulation tick.
            // These two opt-in Harmony switches only need to react to menu or
            // profile edits. Poll their preferences at 20 Hz instead of
            // crossing this check on every unlocked simulation update.
            if (now >= nextOptionalHookSettingsPass)
            {
                nextOptionalHookSettingsPass = now + .05;
                bool capRequested = throttleWhammyTexture.Value;
                if (lastWhammyCapSetting != capRequested)
                {
                    whammyTextureThrottle.Update(HarmonyInstance, capRequested, gameplay);
                    lastWhammyCapSetting = capRequested;
                }
                bool pauseHighway = pauseHighwayAnimations.Value;
                if (lastHighwayPauseSetting != pauseHighway)
                {
                    highwayAnimations.Update(HarmonyInstance, pauseHighway, gameplay);
                    lastHighwayPauseSetting = pauseHighway;
                }
                bool pauseCheatsRequested = pauseGameplayCheatScanner.Value;
                if (lastCheatScannerSetting != pauseCheatsRequested)
                {
                    cheatScanner.Update(HarmonyInstance, pauseCheatsRequested, gameplay);
                    lastCheatScannerSetting = pauseCheatsRequested;
                    lastCheatScannerSuppressed = null;
                }
                networkTickRemainder.Update(HarmonyInstance, preserveNetworkTickRemainder.Value);
            }
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
                && !settingsOpen && !moveHud && resizeTarget < 0 && focused;
            if (shortcutPolling.ShouldPoll(limitSlopHeroShortcutPolling.Value, shortcutContext, now,
                    restartButton.Value, practiceButton.Value))
            {
                lastRestartShortcutPressed = InputBinding.Pressed(restartButton.Value, ref restartBinding, now);
                lastPracticeShortcutPressed = InputBinding.Pressed(practiceButton.Value, ref practiceBinding, now);
            }
            else if (!shortcutContext)
            {
                lastRestartShortcutPressed = lastPracticeShortcutPressed = false;
            }
            bool restartPressed = shortcutContext && lastRestartShortcutPressed;
            bool practicePressed = shortcutContext && lastPracticeShortcutPressed;
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
            if (ready && focused && (!songPlaying || game!.isPaused || settingsOpen) && !transition)
            {
                plainHeldSustains.Warm(hideHeldSustainGlow.Value, game, now);
            }
            practiceModeActive = ready && game!.globalVariables!.isPracticeEnabled;
            bool shortcutCandidate = ready && !transition && !settingsOpen && !moveHud
                && resizeTarget < 0 && focused && !game!.isPaused
                && !game.isSongOver && game.isSongPlaying && game.coroutinesRunning == 0
                && game.actualPlayerCount == 1 && (int)game.globalVariables!.gameMode == 0
                && !practiceModeActive && !game.globalVariables.isPlayingSetlist;
            bool pauseHighwayRequested = pauseHighwayAnimations.Value && highwayAnimations.IsInstalled;
            bool pauseCheatsActive = pauseGameplayCheatScanner.Value && cheatScanner.IsInstalled;
            bool pauseUiRequested = pauseGameplayUiProcessing.Value && gameplayUiEvents.IsInstalled;
            bool workerLimitRequested = reduceUnityJobWorkers.Value;
            bool mouseSamplerRequested = pauseRewiredMousePolling.Value;
            bool keyboardSamplerRequested = pauseRewiredKeyboardPolling.Value;
            bool backgroundCameraRequested = skipBackgroundCamera.Value;
            bool farClipRequested = limitPlayerCameraFarClip.Value;
            bool hudCameraBenchmarkRequested = disableHudCameraBenchmark.Value;
            bool guitarCameraBenchmarkRequested = disableGuitarCameraBenchmark.Value;
            bool plainHeldSustainRequested = hideHeldSustainGlow.Value;
            bool nativeSustainRequested = cacheRegularSustainWrites.Value;
            bool fretSortingCacheRequested = cacheFretSortingWrites.Value;
            // When fret effects have already stopped ClonZones flames, its
            // high-frequency Tick prefix would only add an empty-pool check.
            bool clonZonesTickRequested = limitClonZonesFlameTicks.Value
                && (!disableFretEffects.Value || rainbowFlames.Value);
            bool workerLimitCandidate = workerLimitRequested && ready && songPlaying
                && !game!.isPaused && !game.isSongOver && game.actualPlayerCount == 1
                && !transition && !settingsOpen && !moveHud && resizeTarget < 0;
            bool skipMouseUiRequested = skipGameplayMouseUi.Value;
            bool skipLegacyMouseRequested = skipLegacyMouseEvents.Value;
            bool needsOfflineSoloGate = pauseHighwayRequested || pauseCheatsActive || pauseUiRequested
                || workerLimitRequested || mouseSamplerRequested || keyboardSamplerRequested
                || backgroundCameraRequested || farClipRequested || hudCameraBenchmarkRequested
                || guitarCameraBenchmarkRequested
                || plainHeldSustainRequested || nativeSustainRequested || fretSortingCacheRequested
                || pauseStaticMeterAttach.Value || freezeComboSmokeScroll.Value
                || skipMouseUiRequested || skipLegacyMouseRequested
                || throttleWhammyTexture.Value || disableHitParticles.Value || disableSustainParticles.Value
                || disableFretEffects.Value || disableStarProgressParticles.Value || freezeComboSmokeScroll.Value
                || hideInputViewerDuringSong.Value
                || (hideBeats.Value && pauseHiddenBeatlineUpdates.Value);
            bool soloOptimizationCandidate = needsOfflineSoloGate && ready && songPlaying
                && !game!.isPaused && !game.isSongOver && game.actualPlayerCount == 1
                && game.coroutinesRunning == 0 && (int)game.globalVariables!.gameMode == 0
                && !game.globalVariables.isPlayingSetlist
                && !transition && !settingsOpen && !moveHud && resizeTarget < 0
                && focused;
            bool activeSoloContext = soloOptimizationCandidate;
            bool online = IsOnline(now);
            bool isOfflineSolo = (activeSoloContext || shortcutCandidate || workerLimitCandidate) && !online;
            offlineSoloOptimizationActive = activeSoloContext && !online;
            // These paths are local UI/visual work. They can be skipped during
            // any active chart, including online play, without touching note,
            // controller, or network simulation updates.
            bool activeGameplaySong = ready && songPlaying && !game!.isPaused && !game.isSongOver
                && game.coroutinesRunning == 0 && !transition && !settingsOpen && !moveHud
                && resizeTarget < 0 && focused;
            activeGameplayVisualOptimizationActive = activeGameplaySong;
            clonZonesFlameSuppressor.Update(disableFretEffects.Value && !rainbowFlames.Value
                && activeGameplaySong, gameplay);
            inputViewerSuppression.Update(hideInputViewerDuringSong.Value,
                hideInputViewerDuringSong.Value && activeGameplaySong, now);
            // Unlike UI/input hooks, the worker-count limit is independent of
            // foreground input. Keep it stable while the user alt-tabs during
            // a solo song instead of restoring and reapplying the native pool.
            // Keep the pool stable through a momentary offline solo pause.
            // Leaving solo, going online, or switching the setting off must
            // restore the game's worker count without a grace period.
            bool briefWorkerPause = workerLimitRequested && unityJobWorkerLimit.IsApplied
                && ready && game != null && game.actualPlayerCount == 1
                && !game.isSongOver && !transition && !settingsOpen
                && !moveHud && resizeTarget < 0 && !IsOnline(now);
            unityJobWorkerLimit.Update(workerLimitCandidate && isOfflineSolo, now,
                briefSoloPause: briefWorkerPause);
            highwayAnimations.SetSuppressed(pauseHighwayRequested && activeSoloContext && isOfflineSolo);
            if (cheatScanner.IsInstalled)
            {
                bool pauseCheats = pauseCheatsActive && activeSoloContext && isOfflineSolo;
                cheatScanner.SetSuppressed(pauseCheats);
                if (lastCheatScannerSuppressed != cheatScanner.IsSuppressed)
                {
                    if (cheatScanner.IsSuppressed || lastCheatScannerSuppressed == true)
                        LoggerInstance.Msg($"Sloptifine solo cheat scanner {(cheatScanner.IsSuppressed ? "paused" : "restored")}.");
                    lastCheatScannerSuppressed = cheatScanner.IsSuppressed;
                }
            }
            if (!gameplayUiEventError)
            {
                try
                {
                    bool pauseUiEvents = pauseUiRequested && activeSoloContext && isOfflineSolo;
                    gameplayUiEvents.Update(HarmonyInstance, pauseGameplayUiProcessing.Value, pauseUiEvents);
                    if (lastGameplayUiSuspendState != gameplayUiEvents.IsSuspended)
                    {
                        if (gameplayUiEvents.IsSuspended || lastGameplayUiSuspendState == true)
                            LoggerInstance.Msg($"Sloptifine gameplay UI events {(gameplayUiEvents.IsSuspended ? "suspended" : "restored")}.");
                        lastGameplayUiSuspendState = gameplayUiEvents.IsSuspended;
                    }
                }
                catch (Exception ex)
                {
                    gameplayUiEvents.Clear(HarmonyInstance);
                    gameplayUiEventError = true;
                    LoggerInstance.Warning($"Sloptifine UI-event pause stopped; Unity UI events restored: {ex.Message}");
                }
            }
            if (lastStatsSongPlaying && !songPlaying)
            {
                // The native practice panel can receive its final counters
                // just after our last five-Hz sample. Recheck briefly at the
                // end, then return to the normal low-cost refresh interval.
                nextStats = 0;
                finalStatsRefreshUntil = now + 1;
            }
            lastStatsSongPlaying = songPlaying;
            whammyTextureThrottle.SetSuppressed(!activeGameplaySong);
            mouseUiGameplay.Update(HarmonyInstance, skipMouseUiRequested, skipMouseUiRequested && activeGameplaySong);
            legacyMouseEvents.Update(skipLegacyMouseRequested, activeGameplaySong);
            // These scoped experiments share the same offline solo gate. Read
            // the IL2CPP game properties once, and only when one is requested.
            bool scopedExperimentRequested = mouseSamplerRequested || keyboardSamplerRequested
                || backgroundCameraRequested || farClipRequested || hudCameraBenchmarkRequested
                || guitarCameraBenchmarkRequested || plainHeldSustainRequested || nativeSustainRequested
                || fretSortingCacheRequested;
            bool eligibleScopedExperiment = scopedExperimentRequested && OfflineSoloSongScope.IsEligible(
                requested: true, gameReady: ready, songPlaying: songPlaying,
                paused: ready && game!.isPaused,
                songOver: ready && game!.isSongOver,
                runningCoroutines: ready ? game!.coroutinesRunning : -1,
                playerCount: ready ? game!.actualPlayerCount : 0,
                gameMode: ready ? (int)game!.globalVariables!.gameMode : -1,
                playingSetlist: ready && game!.globalVariables!.isPlayingSetlist,
                transitioning: transition,
                settingsOpen: settingsOpen || moveHud || resizeTarget >= 0,
                focused: focused,
                online: !isOfflineSolo);
            bool pauseMouseSampler = mouseSamplerRequested && activeGameplaySong;
            rewiredMousePolling.Update(HarmonyInstance, mouseSamplerRequested, pauseMouseSampler);
            bool pauseKeyboardSampler = keyboardSamplerRequested && eligibleScopedExperiment;
            if (keyboardSamplerRequested || rewiredKeyboardPolling.IsInstalled)
                rewiredKeyboardPolling.Update(HarmonyInstance, keyboardSamplerRequested, pauseKeyboardSampler);
            bool skipBackground = backgroundCameraRequested && eligibleScopedExperiment;
            backgroundCamera.Update(backgroundCameraRequested, skipBackground, game);
            if (backgroundCamera.ConsumeFailure() is string backgroundFailure)
                LoggerInstance.Warning($"Background camera experiment stopped; original cameras restored: {backgroundFailure}");
            hudCameraBenchmark.Update(hudCameraBenchmarkRequested, eligibleScopedExperiment, game);
            if (hudCameraBenchmark.ConsumeFailure() is string hudCameraFailure)
                LoggerInstance.Warning($"HUD-camera benchmark stopped; original camera restored: {hudCameraFailure}");
            guitarCameraBenchmark.Update(guitarCameraBenchmarkRequested,
                eligibleScopedExperiment && !hudCameraBenchmark.IsActive, game);
            if (guitarCameraBenchmark.ConsumeFailure() is string guitarCameraFailure)
                LoggerInstance.Warning($"Guitar-camera benchmark stopped; original camera restored: {guitarCameraFailure}");
            // Camera-isolation benchmarks compare render paths, so temporarily
            // restore the guitar camera's stock clip plane for both tests.
            // Passing requested=false also clears a stale mismatch failure;
            // the far-clip experiment is reapplied as soon as the benchmark ends.
            bool cameraBenchmarkActive = hudCameraBenchmark.IsActive || guitarCameraBenchmark.IsActive;
            bool farClipRequestedForRuntime = farClipRequested && !cameraBenchmarkActive;
            bool limitPlayerFarClip = farClipRequestedForRuntime && eligibleScopedExperiment;
            playerCameraFarClip.Update(farClipRequestedForRuntime, limitPlayerFarClip, game,
                farClipDistance.Value);
            if (playerCameraFarClip.ConsumeFailure() is string farClipFailure)
                LoggerInstance.Warning($"Guitar-camera far-clip experiment stopped; original camera restored: {farClipFailure}");
            bool eligibleRenderPrototype = activeGameplaySong
                && !hudCameraBenchmarkRequested && !guitarCameraBenchmarkRequested
                && !hudCameraBenchmark.IsActive && !guitarCameraBenchmark.IsActive;
            plainHeldSustains.Update(HarmonyInstance, plainHeldSustainRequested, eligibleRenderPrototype, game);
            nativeSustainCache.Update(nativeSustainRequested, eligibleRenderPrototype);
            fretSortingOrderCache.Update(fretSortingCacheRequested, eligibleRenderPrototype);
            if (clonZonesTickRequested || clonZonesFlameTickGate.IsInstalled)
                clonZonesFlameTickGate.Update(HarmonyInstance, clonZonesTickRequested,
                    clonZonesTickRequested && activeGameplaySong);
            if (ready) CompletePracticeTransition(game!);
            bool allow = shortcutCandidate && isOfflineSolo;
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
                    // isRemotePlayerPlaying describes remote combo activity,
                    // not whether this is our player slot. The CHPlayer's
                    // Rewired Player reference is present for local controllers
                    // and absent for network-owned remote slots.
                    var player = game.playerObjects.FirstOrDefault(p => p != null && p.enabled
                        && p.TryCast<GuitarPlayer>() != null && p.engine != null
                        && p.player != null && p.player.field_Public_Player_0 != null);
                    if (player != null)
                    {
                        if (localStatsPlayer != player.Pointer)
                            LoggerInstance.Msg("Counter HUD selected the local controller-backed guitar player.");
                        localStatsPlayer = player.Pointer;
                    }
                    else
                    {
                        player = game.playerObjects.FirstOrDefault(p => p != null && p.enabled
                            && p.Pointer == localStatsPlayer
                            && p.TryCast<GuitarPlayer>() != null && p.engine != null
                            && p.player != null && p.player.field_Public_Player_0 != null);
                        if (player == null && !online)
                            player = game.playerObjects.FirstOrDefault(p => p != null && p.enabled
                                && p.TryCast<GuitarPlayer>() != null && p.engine != null);
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
            mouseUiGameplay.Clear(HarmonyInstance);
            legacyMouseEvents.Clear();
            rewiredMousePolling.Clear(HarmonyInstance);
            rewiredKeyboardPolling.Clear(HarmonyInstance);
            backgroundCamera.Clear();
            hudCameraBenchmark.Clear();
            guitarCameraBenchmark.Clear();
            playerCameraFarClip.Clear();
            rainbowHighway.Clear();
            staticMeterAttach.Clear();
            clonZonesFlameTickGate.Clear();
            clonZonesFlameSuppressor.Update(false, gameplay);
            nativeSustainCache.Clear();
            fretSortingOrderCache.Clear();
            plainHeldSustains.Clear(HarmonyInstance);
            whammyTextureThrottle.Clear(HarmonyInstance);
            unityJobWorkerLimit.Clear();
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
        mouseUiGameplay.Disable();
        legacyMouseEvents.Disable();
        rewiredMousePolling.Disable();
        rewiredKeyboardPolling.Disable();
        backgroundCamera.Restore();
        hudCameraBenchmark.Restore();
        guitarCameraBenchmark.Restore();
        playerCameraFarClip.Restore();
        rainbowHighway.Clear();
        staticMeterAttach.Clear();
        clonZonesFlameTickGate.Clear();
        clonZonesFlameSuppressor.Update(false, gameplay);
        ResetNativeBridgesForScene();
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
        mouseUiGameplay.Disable();
        legacyMouseEvents.Disable();
        rewiredMousePolling.Disable();
        rewiredKeyboardPolling.Disable();
        backgroundCamera.Restore();
        hudCameraBenchmark.Restore();
        guitarCameraBenchmark.Restore();
        playerCameraFarClip.Restore();
        rainbowHighway.Clear();
        staticMeterAttach.Clear();
        clonZonesFlameTickGate.Clear();
        clonZonesFlameSuppressor.Update(false, gameplay);
        ResetNativeBridgesForScene();
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

    // Core and the hidden-leaderboard option both need the same connection
    // state. Cache it at their shared 20 Hz cadence instead of repeatedly
    // crossing the IL2CPP boundary from each independent feature.
    private bool IsOnline(double realtime)
    {
        if (onlineStateKnown && realtime < nextOnlineStateCheck) return onlineState;
        nextOnlineStateCheck = realtime + .05;
        try
        {
            var network = CHNetManager.instance;
            onlineState = network != null && (int)network.field_Private_ConnectionState_0 != 0;
        }
        catch
        {
            // Keep all offline-only behavior disabled when connection state
            // cannot be resolved.
            onlineState = true;
        }
        onlineStateKnown = true;
        return onlineState;
    }

    public override void OnGUI()
    {
        // With both visible HUDs retained, normal solo play has no IMGUI
        // content. MelonLoader still calls OnGUI for every event, so
        // avoid entering Unity's GUI event API on this common path. Editing,
        // song info, notices, and opt-in timing captures keep the full path.
        if (!timings.Active && compatible && gameplay
            && (!showHud.Value || string.IsNullOrEmpty(statsText) || counterCanvas.Visible)
            && (!showSong.Value || string.IsNullOrEmpty(songText) || songCanvas.Visible)
            && !settingsOpen && !moveHud && resizeTarget < 0 && nativeMoveTarget == 0
            && noticeUntil <= 0) return;
        if (timings.Active && Event.current.type == EventType.Repaint)
            timings.SampleRepaint();
        // Passive HUDs need only the repaint event. Keep full event processing
        // while editing so sliders, typing, and dragging remain responsive.
        bool resizeEditing = resizeTarget >= 0;
        if (!settingsOpen && !moveHud && !resizeEditing && Event.current.type != EventType.Repaint) return;
        if (counterCanvas.Visible && (!showHud.Value || settingsOpen || moveHud || resizeEditing
            || !gameplay || string.IsNullOrEmpty(statsText))) counterCanvas.Hide();
        if (songCanvas.Visible && (!showSong.Value || settingsOpen || moveHud || resizeEditing
            || !gameplay || string.IsNullOrEmpty(songText))) songCanvas.Hide();
        bool noticeVisible = !compatible || (noticeUntil > 0 && clock.Elapsed.TotalSeconds < noticeUntil);
        bool counterHudVisible = !counterCanvas.Visible && showHud.Value && gameplay && !string.IsNullOrEmpty(statsText);
        bool songHudVisible = !songCanvas.Visible && showSong.Value && gameplay && !string.IsNullOrEmpty(songText);
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
            if (!(moveHud && (moveSong || nativeMoveTarget != 0)) && ((!songCanvas.Visible && showSong.Value && gameplay && !string.IsNullOrEmpty(songText)) || resizeTarget == 1)) DrawSongHudSafely();
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
        if (CounterFont.UnityFont is { } counterFont && hudStyle.font != counterFont)
            hudStyle.font = counterFont;
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
        ClearMenuArtwork();
        clonZonesOriginalFretRenderers.Restore();
        mouseUiGameplay.Clear(HarmonyInstance);
        legacyMouseEvents.Clear();
        rewiredMousePolling.Clear(HarmonyInstance);
        rewiredKeyboardPolling.Clear(HarmonyInstance);
        backgroundCamera.Clear();
        hudCameraBenchmark.Clear();
        guitarCameraBenchmark.Clear();
        playerCameraFarClip.Clear();
        rainbowHighway.Clear();
        staticMeterAttach.Clear();
        clonZonesFlameTickGate.Clear();
        clonZonesFlameSuppressor.Clear();
        nativeSustainCache.Clear();
        fretSortingOrderCache.Clear();
        plainHeldSustains.Clear(HarmonyInstance);
        networkTickRemainder.Clear(HarmonyInstance);
        optionalHudAnimations.Clear();
        gameplayUiEvents.Clear(HarmonyInstance);
        highwayAnimations.Clear(HarmonyInstance);
        rings.Clear(HarmonyInstance);
        whammyTextureThrottle.Clear(HarmonyInstance);
        unityJobWorkerLimit.Clear();
        staticTmpScale.Clear();
        timings.Finish();
        aprilFools.Restore();
        visuals.Clear();
        performance.Clear();
        starProgressParticles.Clear();
        comboSmokeScroll.Clear();
        counterCanvas.Clear();
        songCanvas.Clear();
        cheatScanner.Clear(HarmonyInstance);
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
