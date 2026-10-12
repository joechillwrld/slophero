using System.Diagnostics;
using System.Globalization;
using Il2Cpp;
using MelonLoader;
using SlopHero.Core;
using UnityEngine;

[assembly: MelonInfo(typeof(SlopHero.Mod.Core), "SlopHero", "0.4.15", "joechillwrld")]
[assembly: MelonGame("srylain Inc.", "Clone Hero")]

namespace SlopHero.Mod;

public sealed partial class Core : MelonMod
{
    private readonly HoldTrigger restartHold = new();
    private readonly HoldTrigger practiceHold = new();
    private readonly ChartStats statsReader = new();
    private readonly NativeHudLayout nativeHud = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private MelonPreferences_Category preferences = null!;
    private MelonPreferences_Entry<string> restartButton = null!, practiceButton = null!;
    private MelonPreferences_Entry<float> holdSeconds = null!;
    private MelonPreferences_Entry<float> backgroundOpacity = null!, textRed = null!, textGreen = null!, textBlue = null!;
    private MelonPreferences_Entry<int> hudX = null!, hudY = null!, fontSize = null!;
    private MelonPreferences_Entry<bool> showHud = null!;
    private bool compatible, gameplay, gameplayInitialized, settingsOpen, transition, practiceFailed;
    private bool moveHud, hudDragging, preferencesDirty, cursorOverridden, savedCursorVisible;
    private int nativeMoveTarget;
    private bool nativeHudDragging, nativeHudError, nativeHudErrorLogged, nativeHudActive;
    private Vector2 nativeHudLastMouse;
    private CursorLockMode savedCursorLock;
    private Vector2 dragOffset;
    private string? capture;
    private string status = "Starting...", statsText = "";
    private string practiceWaitState = "";
    private double nextStats, transitionStarted, noticeUntil;
    private int? pendingSection;
    private IntPtr pendingSong;
    private IntPtr sourceManager;
    private IntPtr statsEngine;
    private GUIStyle? hudStyle;
    private int styledHudFont = -1;
    private Color styledHudColor;
    private GUIStyle? hudBackgroundStyle;
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
        gameplayInitialized = false;
        gameplay = string.Equals(sceneName, "Gameplay", StringComparison.OrdinalIgnoreCase);
        statsText = songText = "";
        visuals.Clear();
        nativeHud.Clear();
        nativeMoveTarget = 0;
        nativeHudDragging = false;
        nativeHudError = false;
        nativeHudActive = false;
        rings.Clear();
        visualError = false;
        statsEngine = IntPtr.Zero;
        nextStats = 0;
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
        if (sceneName == "Gameplay") { gameplay = false; gameplayInitialized = false; }
        settingsOpen = moveHud = hudDragging = false;
        nativeMoveTarget = 0;
        nativeHudDragging = false;
        capture = null;
        editApply = null;
        SavePreferences();
        UpdateCursor();
        statsText = songText = "";
        visuals.Clear();
        nativeHud.Clear();
        nativeHudActive = false;
        rings.Clear();
        visualError = false;
        statsEngine = IntPtr.Zero;
        restartHold.RequireRelease();
        practiceHold.RequireRelease();
    }

    public override void OnUpdate()
    {
        long timingStart = timings.Begin();
        try
        {
            double now = clock.Elapsed.TotalSeconds;
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (moveHud) { moveHud = false; settingsOpen = true; }
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
            if ((settingsOpen || moveHud) && editApply == null && Input.GetKeyDown(KeyCode.Escape))
            {
                settingsOpen = false;
                moveHud = false;
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
            if (!Application.isFocused && (settingsOpen || moveHud))
            {
                settingsOpen = moveHud = hudDragging = false;
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
            if (!compatible) return;
            if (transition && now - transitionStarted > 30)
            {
                pendingSection = null;
                transition = false;
                Notice("Transition did not finish; use the normal game menus and send Latest.log.");
            }
            var game = gameplay && gameplayInitialized ? GameManager.field_Private_Static_GameManager_0 : null;
            bool ready = game != null && game.songLoaded && game.playersInit && game.globalVariables != null;
            if (ready) CompletePracticeTransition(game!);
            bool allow = ready && !transition && !settingsOpen && !moveHud && Application.isFocused && !game!.isPaused
                && !game.isSongOver && game.isSongPlaying && game.coroutinesRunning == 0
                && game.actualPlayerCount == 1 && (int)game.globalVariables!.gameMode == 0
                && !game.globalVariables.isPracticeEnabled && !game.globalVariables.isPlayingSetlist
                && (CHNetManager.instance == null || (int)CHNetManager.instance.field_Private_ConnectionState_0 == 0);
            if (restartHold.Update(InputBinding.Pressed(restartButton.Value), allow, now, Duration))
            {
                BeginRestart(game!);
                return;
            }
            if (practiceHold.Update(InputBinding.Pressed(practiceButton.Value), allow && !practiceFailed, now, Duration))
            {
                BeginPractice(game!);
                return;
            }
            if (!ready || transition || game == null || game.playerObjects == null)
            {
                statsText = "";
                return;
            }
            if (now >= nextStats)
            {
                if (!showHud.Value && !showSong.Value && !moveHud)
                {
                    // With both mod HUDs hidden, there is nothing to sample or
                    // format. The song controls above still stay responsive.
                    statsText = songText = "";
                    nextStats = now + .2;
                    return;
                }
                // The HUD refreshes five times per second. Avoid walking the
                // IL2CPP player array and casting its entries on every frame.
                var player = game.playerObjects.FirstOrDefault(p => p != null && p.enabled);
                if (player == null || player.TryCast<GuitarPlayer>() == null || player.engine == null)
                {
                    statsText = "";
                    nextStats = now + .2;
                    return;
                }
                if (player.engine.Pointer != statsEngine) statsEngine = player.engine.Pointer;
                var stats = statsReader.Read(player.engine, game);
                string percentage = stats.HitPercentage?.ToString("0.00", CultureInfo.InvariantCulture) + "%";
                if (stats.HitPercentage == null) percentage = "—";
                var lines = new List<string>();
                if (showMisses.Value) lines.Add($"Misses: {stats.Misses}");
                if (showHit.Value) lines.Add($"Hit: {percentage}");
                if (showGhosts.Value) lines.Add($"Ghosts: {stats.GhostInputs}");
                if (showBest.Value) lines.Add($"Best streak: {player.engine.field_Public_Int32_5}");
                statsText = string.Join("\n", lines);
                var song = game.globalVariables!.currentSongEntry;
                lines.Clear();
                if (song != null)
                {
                    if (showTitle.Value) lines.Add(SongTitleLabel.Format(song.Name_StrippedTags ?? "Unknown title", game.practiceSpeed, showSpeed.Value));
                    if (showArtist.Value) lines.Add("Artist: " + (song.Artist_StrippedTags ?? "Unknown"));
                    if (showCharter.Value) lines.Add("Charter: " + (song.Charter_StrippedTags ?? "Unknown"));
                }
                songText = string.Join("\n", lines);
                nextStats = now + .2;
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
        // Passive HUDs need only the repaint event. Keep full event processing
        // while editing so sliders, typing, and dragging remain responsive.
        if (!settingsOpen && !moveHud && Event.current.type != EventType.Repaint) return;
        long timingStart = timings.Begin();
        Color previousColor = GUI.color;
        GUI.color = Color.white;
        try
        {
            if ((showHud.Value && gameplay && !string.IsNullOrEmpty(statsText)) || settingsOpen || moveHud)
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
            if (!(moveHud && (moveSong || nativeMoveTarget != 0)) && showSong.Value && gameplay && !string.IsNullOrEmpty(songText)) DrawSongHudSafely();
            if (!compatible || clock.Elapsed.TotalSeconds < noticeUntil)
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
        hudStyle ??= new GUIStyle(GUI.skin.label);
        int size = Math.Clamp(fontSize.Value, 14, 48);
        Color color = new(HudLayout.Unit(textRed.Value), HudLayout.Unit(textGreen.Value), HudLayout.Unit(textBlue.Value), 1);
        if (styledHudFont != size) { hudStyle.fontSize = size; styledHudFont = size; }
        if (styledHudColor != color) { hudStyle.normal.textColor = color; styledHudColor = color; }
        int width = Math.Max(240, size * 12 + 24);
        int height = (int)(size * 1.25f) * (1 + text.Count(c => c == '\n')) + 20;
        var position = HudLayout.ClampPosition(hudX.Value, hudY.Value, width, height, Screen.width, Screen.height);
        Rect rect = new(position.X, position.Y, width, height);
        // DrawTexture's final overload is an unstripping-failed stub in the
        // supplied game assemblies. GUI.Box invokes the available native API.
        EnsureHudBackground();
        Color previousBackground = GUI.backgroundColor;
        try
        {
            GUI.backgroundColor = new Color(0, 0, 0, HudLayout.Unit(backgroundOpacity.Value, .65f));
            GUI.Box(rect, "", hudBackgroundStyle);
        }
        finally { GUI.backgroundColor = previousBackground; }
        GUI.Label(new Rect(rect.x + 12, rect.y + 8, width - 24, height - 8), text, hudStyle);
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

    private void EnsureHudBackground()
    {
        if (hudBackgroundStyle != null) return;
        hudBackgroundStyle = new GUIStyle(GUI.skin.label);
        var texture = Texture2D.whiteTexture;
        hudBackgroundStyle.normal.background = texture;
        hudBackgroundStyle.hover.background = texture;
        hudBackgroundStyle.active.background = texture;
        hudBackgroundStyle.focused.background = texture;
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
        if (!nativeHud.TryGetScreenRect(nativeMoveTarget, out var rect))
        {
            string hint = nativeMoveTarget == 1
                ? "Practice stats UI not found. Enter Practice mode, then reopen F8 → Game HUD."
                : "FPS display not found. Enable the game's FPS display, then reopen F8 → Game HUD.";
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
        if (settingsOpen || moveHud)
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
        settingsOpen = moveHud = hudDragging = false;
        SavePreferences();
        UpdateCursor();
    }

    private void ChangeDuration(float delta)
    {
        holdSeconds.Value = Math.Clamp((float)Duration + delta, .25f, 3f);
        preferences.SaveToFile();
    }
}
