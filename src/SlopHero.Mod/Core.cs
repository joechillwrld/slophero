using System.Diagnostics;
using System.Globalization;
using Il2Cpp;
using MelonLoader;
using SlopHero.Core;
using UnityEngine;

[assembly: MelonInfo(typeof(SlopHero.Mod.Core), "SlopHero", "0.2.1", "joechillwrld")]
[assembly: MelonGame("srylain Inc.", "Clone Hero")]

namespace SlopHero.Mod;

public sealed class Core : MelonMod
{
    private readonly HoldTrigger restartHold = new();
    private readonly HoldTrigger practiceHold = new();
    private readonly ChartStats statsReader = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private MelonPreferences_Category preferences = null!;
    private MelonPreferences_Entry<string> restartButton = null!, practiceButton = null!;
    private MelonPreferences_Entry<float> holdSeconds = null!;
    private MelonPreferences_Entry<float> backgroundOpacity = null!, textRed = null!, textGreen = null!, textBlue = null!;
    private MelonPreferences_Entry<int> hudX = null!, hudY = null!, fontSize = null!;
    private MelonPreferences_Entry<bool> showHud = null!;
    private bool compatible, gameplay, gameplayInitialized, settingsOpen, transition, practiceFailed;
    private bool moveHud, hudDragging, preferencesDirty, cursorOverridden, savedCursorVisible;
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
        compatible = BuildGuard.Matches(out status);
        if (compatible) LoggerInstance.Msg(status);
        else LoggerInstance.Warning(status);
        LoggerInstance.Msg("F8: SlopHero settings. Default: hold R to restart, hold P to practice the current section.");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        gameplayInitialized = false;
        gameplay = string.Equals(sceneName, "Gameplay", StringComparison.OrdinalIgnoreCase);
        statsText = "";
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
            LoggerInstance.Msg("Gameplay scene initialized; checking player and renderer readiness.");
        }
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (sceneName == "Gameplay") { gameplay = false; gameplayInitialized = false; }
        settingsOpen = moveHud = hudDragging = false;
        capture = null;
        SavePreferences();
        UpdateCursor();
        statsText = "";
        statsEngine = IntPtr.Zero;
        restartHold.RequireRelease();
        practiceHold.RequireRelease();
    }

    public override void OnUpdate()
    {
        try
        {
            double now = clock.Elapsed.TotalSeconds;
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (moveHud) { moveHud = false; settingsOpen = true; }
                else settingsOpen = !settingsOpen;
                hudDragging = false;
                SavePreferences();
                UpdateCursor();
                capture = null;
                restartHold.RequireRelease();
                practiceHold.RequireRelease();
            }
            if ((settingsOpen || moveHud) && Input.GetKeyDown(KeyCode.Escape))
            {
                settingsOpen = false;
                moveHud = false;
                hudDragging = false;
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
            var player = game.playerObjects.FirstOrDefault(p => p != null && p.enabled);
            if (player == null || player.TryCast<GuitarPlayer>() == null || player.engine == null)
            {
                statsText = "";
                return;
            }
            if (player.engine.Pointer != statsEngine)
            {
                statsEngine = player.engine.Pointer;
                nextStats = 0;
            }
            if (now >= nextStats)
            {
                var stats = statsReader.Read(player.engine, game);
                string percentage = stats.HitPercentage?.ToString("0.00", CultureInfo.InvariantCulture) + "%";
                if (stats.HitPercentage == null) percentage = "—";
                statsText = $"Misses: {stats.Misses}\nHit: {percentage}\nGhosts: {stats.GhostInputs}";
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
            transition = false;
            nextStats = 0;
            Notice("Practice: starting the section you left.");
            LoggerInstance.Msg($"Selected practice section {section}; ticks {game.practiceDisplayStartTick}–{game.practiceDisplayEndTick}.");
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
        Color previousColor = GUI.color;
        GUI.color = Color.white;
        try
        {
            if ((showHud.Value && gameplay && !string.IsNullOrEmpty(statsText)) || settingsOpen || moveHud)
            {
                string text = string.IsNullOrEmpty(statsText) ? "Misses: 0\nHit: 100.00%\nGhosts: 0" : statsText;
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
            if (!compatible || clock.Elapsed.TotalSeconds < noticeUntil)
                GUI.Box(new Rect(20, Screen.height - 60, Math.Max(300, Screen.width - 40), 44), status);
            if (moveHud)
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
        finally { GUI.color = previousColor; }
    }

    private void DrawHud(string text)
    {
        hudStyle ??= new GUIStyle(GUI.skin.label);
        hudStyle.fontSize = Math.Clamp(fontSize.Value, 14, 48);
        hudStyle.normal.textColor = new Color(HudLayout.Unit(textRed.Value),
            HudLayout.Unit(textGreen.Value), HudLayout.Unit(textBlue.Value), 1);
        int width = Math.Max(240, hudStyle.fontSize * 12 + 24);
        int height = hudStyle.fontSize * 4 + 16;
        var position = HudLayout.ClampPosition(hudX.Value, hudY.Value, width, height, Screen.width, Screen.height);
        Rect rect = new(position.X, position.Y, width, height);
        // DrawTexture's final overload is an unstripping-failed stub in the
        // supplied game assemblies. GUI.Box invokes the available native API.
        if (hudBackgroundStyle == null)
        {
            hudBackgroundStyle = new GUIStyle(GUI.skin.label);
            var texture = Texture2D.whiteTexture;
            hudBackgroundStyle.normal.background = texture;
            hudBackgroundStyle.hover.background = texture;
            hudBackgroundStyle.active.background = texture;
            hudBackgroundStyle.focused.background = texture;
        }
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

    private void DrawBasicHud(string text)
    {
        // The original native-backed label path remains usable independently of
        // background rendering, drag controls, and the settings panel.
        var position = HudLayout.ClampPosition(hudX.Value, hudY.Value, 320, 150, Screen.width, Screen.height);
        GUI.Label(new Rect(position.X + 12, position.Y + 8, 300, 140), text);
    }

    private void CloseEditor()
    {
        settingsOpen = moveHud = hudDragging = false;
        capture = null;
        SavePreferences();
        UpdateCursor();
        restartHold.RequireRelease();
        practiceHold.RequireRelease();
    }

    private void DrawSettings()
    {
        float left = Math.Max(10, (Screen.width - 540) / 2f);
        float top = Math.Max(10, (Screen.height - 590) / 2f);
        GUI.Box(new Rect(left, top, 540, 590), "SlopHero 0.2.1 — F8 / Escape to close");
        GUI.Label(new Rect(left + 20, top + 35, 500, 40), "Pause the game before editing. Song controls apply to solo Quickplay.");
        if (GUI.Button(new Rect(left + 20, top + 78, 500, 32), $"Restart: {restartButton.Value} — click to bind")) capture = "restart";
        if (GUI.Button(new Rect(left + 20, top + 118, 500, 32), $"Practice: {practiceButton.Value} — click to bind")) capture = "practice";
        GUI.Label(new Rect(left + 20, top + 160, 300, 25), $"Hold duration: {Duration:0.00}s");
        if (GUI.Button(new Rect(left + 340, top + 158, 80, 30), "Shorter")) ChangeDuration(-.1f);
        if (GUI.Button(new Rect(left + 430, top + 158, 80, 30), "Longer")) ChangeDuration(.1f);
        if (GUI.Button(new Rect(left + 20, top + 202, 240, 32), showHud.Value ? "HUD: shown" : "HUD: hidden"))
        {
            showHud.Value = !showHud.Value;
            preferencesDirty = true;
        }
        if (GUI.Button(new Rect(left + 280, top + 202, 240, 32), "Move HUD — drag anywhere"))
        {
            capture = null;
            settingsOpen = false;
            moveHud = true;
            restartHold.RequireRelease();
            practiceHold.RequireRelease();
            UpdateCursor();
        }
        GUI.Label(new Rect(left + 20, top + 246, 300, 25), $"Text size: {Math.Clamp(fontSize.Value, 14, 48)}");
        if (GUI.Button(new Rect(left + 340, top + 242, 80, 30), "Smaller"))
        {
            fontSize.Value = Math.Clamp(fontSize.Value - 2, 14, 48);
            preferencesDirty = true;
        }
        if (GUI.Button(new Rect(left + 430, top + 242, 80, 30), "Larger"))
        {
            fontSize.Value = Math.Clamp(fontSize.Value + 2, 14, 48);
            preferencesDirty = true;
        }
        DrawSlider(backgroundOpacity, "Background opacity", left + 20, top + 288, .65f);
        DrawSlider(textRed, "Text red", left + 20, top + 326);
        DrawSlider(textGreen, "Text green", left + 20, top + 364);
        DrawSlider(textBlue, "Text blue", left + 20, top + 402);
        if (GUI.Button(new Rect(left + 20, top + 446, 240, 30), "White text / default background"))
        {
            textRed.Value = textGreen.Value = textBlue.Value = 1;
            backgroundOpacity.Value = .65f;
            preferencesDirty = true;
        }
        if (GUI.Button(new Rect(left + 280, top + 446, 240, 30), "Reset HUD position"))
        {
            hudX.Value = 24;
            hudY.Value = 110;
            preferencesDirty = true;
        }
        GUI.Label(new Rect(left + 20, top + 488, 500, 62), capture == null
            ? "Changes save automatically. Practice starts the current section from its beginning."
            : $"Press a key or controller button to bind {capture}.\nF8 / Escape cancels. Avoid fret and strum buttons.");
        if (restartButton.Value == practiceButton.Value)
            GUI.Label(new Rect(left + 20, top + 550, 500, 25), "Both actions share a button; restart takes priority. Choose different buttons.");
    }

    private void DrawSlider(MelonPreferences_Entry<float> entry, string label, float x, float y, float fallback = 1)
    {
        float current = HudLayout.Unit(entry.Value, fallback);
        GUI.Label(new Rect(x, y, 200, 25), $"{label}: {current * 100:0}%");
        float value = GUI.HorizontalSlider(new Rect(x + 210, y + 7, 280, 20), current, 0, 1);
        if (Math.Abs(entry.Value - value) > .0001f || !float.IsFinite(entry.Value))
        {
            entry.Value = value;
            preferencesDirty = true;
        }
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
