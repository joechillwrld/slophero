using System.Diagnostics;
using System.Globalization;
using Il2Cpp;
using MelonLoader;
using SlopHero.Core;
using UnityEngine;

[assembly: MelonInfo(typeof(SlopHero.Mod.Core), "SlopHero", "0.1.0", "joechillwrld")]
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
    private MelonPreferences_Entry<int> hudX = null!, hudY = null!, fontSize = null!;
    private MelonPreferences_Entry<bool> showHud = null!;
    private bool compatible, gameplay, settingsOpen, transition;
    private string? capture;
    private string status = "Starting...", statsText = "";
    private double nextStats, transitionStarted, noticeUntil;
    private int? pendingSection;
    private IntPtr pendingSong;
    private IntPtr statsEngine;
    private GUIStyle? hudStyle;

    public override void OnInitializeMelon()
    {
        preferences = MelonPreferences.CreateCategory("SlopHero");
        restartButton = preferences.CreateEntry("RestartButton", "Key:R");
        practiceButton = preferences.CreateEntry("PracticeButton", "Key:P");
        holdSeconds = preferences.CreateEntry("HoldSeconds", .75f);
        hudX = preferences.CreateEntry("HudX", 24);
        hudY = preferences.CreateEntry("HudY", 24);
        fontSize = preferences.CreateEntry("FontSize", 24);
        showHud = preferences.CreateEntry("ShowHud", true);
        compatible = BuildGuard.Matches(out status);
        if (compatible) LoggerInstance.Msg(status);
        else LoggerInstance.Warning(status);
        LoggerInstance.Msg("F8: SlopHero settings. Default: hold R to restart, hold P to practice the current section.");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        gameplay = string.Equals(sceneName, "Gameplay", StringComparison.OrdinalIgnoreCase);
        statsText = "";
        statsEngine = IntPtr.Zero;
        nextStats = 0;
        restartHold.RequireRelease();
        practiceHold.RequireRelease();
        if (!gameplay && sceneName != "Loading") pendingSection = null;
        if (gameplay && pendingSection == null) transition = false;
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        if (sceneName == "Gameplay") gameplay = false;
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
                settingsOpen = !settingsOpen;
                capture = null;
                restartHold.RequireRelease();
                practiceHold.RequireRelease();
            }
            if (settingsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                settingsOpen = false;
                capture = null;
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
            var game = gameplay ? GameManager.field_Private_Static_GameManager_0 : null;
            bool ready = game != null && game.songLoaded && game.playersInit && game.globalVariables != null;
            if (ready) CompletePracticeTransition(game!);
            bool allow = ready && !transition && !settingsOpen && Application.isFocused && !game!.isPaused
                && !game.isSongOver && game.isSongPlaying && game.coroutinesRunning == 0
                && game.actualPlayerCount == 1 && (int)game.globalVariables!.gameMode == 0
                && !game.globalVariables.isPracticeEnabled && !game.globalVariables.isPlayingSetlist
                && (CHNetManager.instance == null || (int)CHNetManager.instance.field_Private_ConnectionState_0 == 0);
            if (restartHold.Update(InputBinding.Pressed(restartButton.Value), allow, now, Duration))
            {
                BeginRestart(game!);
                return;
            }
            if (practiceHold.Update(InputBinding.Pressed(practiceButton.Value), allow, now, Duration))
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
        if (pendingSection == null || !game.globalVariables.isPracticeEnabled || game.coroutinesRunning != 0
            || game.practicePlayer == null || game.practicePlayer.engine == null || game.song == null) return;
        if (game.globalVariables.currentSongEntry == null || game.globalVariables.currentSongEntry.Pointer != pendingSong)
        {
            pendingSection = null;
            transition = false;
            Notice("Song changed; cancelled the requested practice section.");
            return;
        }
        int section = pendingSection.Value;
        // Clear optional A/B overrides so the native routine derives section bounds.
        game.practiceStartTick = 0;
        game.practiceEndTick = 0;
        game.practiceDisplayStartTick = 0;
        game.practiceDisplayEndTick = 0;
        pendingSection = null;
        game.Method_Public_Void_Int32_Int32_Boolean_Boolean_0(section, 0, true, true);
        transition = false;
        nextStats = 0;
        Notice("Practice: starting the section you left.");
        LoggerInstance.Msg($"Selected practice section {section}; ticks {game.practiceDisplayStartTick}–{game.practiceDisplayEndTick}.");
    }

    private void Notice(string message)
    {
        status = message;
        noticeUntil = clock.Elapsed.TotalSeconds + 8;
        LoggerInstance.Msg(message);
    }

    public override void OnGUI()
    {
        if (showHud.Value && gameplay && !string.IsNullOrEmpty(statsText))
        {
            hudStyle ??= new GUIStyle(GUI.skin.label);
            hudStyle.fontSize = Math.Clamp(fontSize.Value, 14, 48);
            hudStyle.normal.textColor = Color.white;
            float x = Mathf.Clamp(hudX.Value, 0, Math.Max(0, Screen.width - 240));
            float y = Mathf.Clamp(hudY.Value, 0, Math.Max(0, Screen.height - 170));
            float height = hudStyle.fontSize * 4 + 16;
            GUI.Box(new Rect(x, y, 240, height), "");
            GUI.Label(new Rect(x + 12, y + 8, 220, height - 8), statsText, hudStyle);
        }
        if (!compatible || clock.Elapsed.TotalSeconds < noticeUntil)
            GUI.Box(new Rect(20, Screen.height - 60, Math.Max(300, Screen.width - 40), 44), status);
        if (!settingsOpen) return;
        float left = Math.Max(10, (Screen.width - 520) / 2f);
        float top = Math.Max(10, (Screen.height - 370) / 2f);
        GUI.Box(new Rect(left, top, 520, 370), "SlopHero — F8 / Escape to close");
        GUI.Label(new Rect(left + 20, top + 35, 480, 40), "Pause the game before changing settings. Controls apply to solo Quickplay.");
        if (GUI.Button(new Rect(left + 20, top + 85, 480, 32), $"Restart button: {restartButton.Value} — click to bind")) capture = "restart";
        if (GUI.Button(new Rect(left + 20, top + 125, 480, 32), $"Practice button: {practiceButton.Value} — click to bind")) capture = "practice";
        GUI.Label(new Rect(left + 20, top + 167, 300, 25), $"Hold duration: {Duration:0.00}s");
        if (GUI.Button(new Rect(left + 330, top + 165, 75, 30), "Shorter")) ChangeDuration(-.1f);
        if (GUI.Button(new Rect(left + 415, top + 165, 75, 30), "Longer")) ChangeDuration(.1f);
        if (GUI.Button(new Rect(left + 20, top + 210, 230, 30), showHud.Value ? "HUD: shown" : "HUD: hidden"))
        {
            showHud.Value = !showHud.Value;
            preferences.SaveToFile();
        }
        GUI.Label(new Rect(left + 20, top + 252, 480, 70), capture == null
            ? "Practice starts the current section from its beginning.\nHUD position and size: UserData/MelonPreferences.cfg → SlopHero."
            : $"Press a key or a guitar/controller button to bind {capture}.\nF8 or Escape cancels. Avoid buttons used for notes or strumming.");
        if (restartButton.Value == practiceButton.Value)
            GUI.Label(new Rect(left + 20, top + 325, 480, 25), "Both actions share a button; restart takes priority. Bind different buttons.");
    }

    private void ChangeDuration(float delta)
    {
        holdSeconds.Value = Math.Clamp((float)Duration + delta, .25f, 3f);
        preferences.SaveToFile();
    }
}
