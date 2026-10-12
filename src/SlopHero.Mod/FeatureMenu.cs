using System.Globalization;
using MelonLoader;
using SlopHero.Core;
using UnityEngine;

namespace SlopHero.Mod;

public sealed partial class Core
{
    private MelonPreferences_Entry<bool> showMisses = null!, showHit = null!, showGhosts = null!, showBest = null!;
    private MelonPreferences_Entry<bool> showSong = null!, showTitle = null!, showArtist = null!, showCharter = null!, showSpeed = null!;
    private MelonPreferences_Entry<int> songX = null!, songY = null!, songFont = null!;
    private MelonPreferences_Entry<float> songOpacity = null!;
    private MelonPreferences_Entry<string> songColor = null!, fcColor = null!, pfcColor = null!, starColor = null!, progressColor = null!, scoreColor = null!, songProgressColor = null!;
    private MelonPreferences_Entry<bool> hideSides = null!, hideStrings = null!, hideBeats = null!, tintFc = null!, tintScore = null!, lockSpeed = null!;
    private readonly VisualOverrides visuals = new();
    private readonly RingColors rings = new();
    private readonly RuntimeTimings timings = new();
    private string songText = "", editText = "", editLabel = "", editError = "";
    private Action<string>? editApply;
    private bool replaceEdit, moveSong, editColor;
    private Color editPreview = Color.white;
    private GUIStyle? colorPreviewStyle;
    private int settingsPage;
    private readonly Dictionary<string, Color> parsedColors = new();
    private GUIStyle? songStyle;
    private int styledSongFont = -1;
    private Color styledSongColor;
    private string styledSongHex = "";

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
        hideSides = preferences.CreateEntry("HideHighwaySides", false);
        hideStrings = preferences.CreateEntry("HideFretStrings", false);
        hideBeats = preferences.CreateEntry("HideBeatlines", false);
        tintFc = preferences.CreateEntry("CustomizeFcRings", false);
        tintScore = preferences.CreateEntry("CustomizeScoreColors", false);
        lockSpeed = preferences.CreateEntry("ConstantPracticeNoteSpeed", false);
        fcColor = preferences.CreateEntry("FcColor", "FFD700");
        pfcColor = preferences.CreateEntry("PerfectFcColor", "00FFFF");
        songProgressColor = preferences.CreateEntry("SongProgressColor", "00FFFF");
        starColor = preferences.CreateEntry("StarColor", "FFD700");
        progressColor = preferences.CreateEntry("ProgressColor", "00FFFF");
        scoreColor = preferences.CreateEntry("ScoreTextColor", "FFFFFF");
    }

    private void DrawSettings()
    {
        if (editApply != null) { DrawValueEditor(); return; }
        float x = Math.Max(10, (Screen.width - 580) / 2f), y = Math.Max(10, (Screen.height - 600) / 2f);
        GUI.Box(new Rect(x, y, 580, 600), "SlopHero 0.4.9 — F8 / Escape to close");
        string[] pages = { "Controls", "Counters", "Song HUD", "Highway", "Colors" };
        float tabWidth = 556f / pages.Length;
        for (int i = 0; i < pages.Length; i++)
            if (GUI.Button(new Rect(x + 12 + i * tabWidth, y + 32, tabWidth - 4, 30), pages[i])) settingsPage = i;
        x += 20; y += 80;
        GUI.Label(new Rect(x, y, 540, 25), "Pause before editing. Click a value to type; Enter applies, Escape cancels.");
        y += 34;
        switch (settingsPage)
        {
            case 0:
                if (GUI.Button(new Rect(x, y, 540, 34), $"Restart: {restartButton.Value} — bind")) capture = "restart";
                if (GUI.Button(new Rect(x, y + 44, 540, 34), $"Practice: {practiceButton.Value} — bind")) capture = "practice";
                Number("Hold duration (seconds)", (float)Duration, x, y + 94, .25f, 3, v => holdSeconds.Value = v);
                GUI.Label(new Rect(x, y + 150, 540, 70), capture == null ? "Song actions apply to solo Quickplay.\nPractice begins at the start of your current named section." : $"Press a key or controller button for {capture}.");
                if (GUI.Button(new Rect(x, y + 290, 540, 32), timings.Active ? "Finish timing report" : "Capture mod timings for 30 seconds"))
                {
                    if (timings.Active) timings.Finish();
                    else if (gameplayInitialized)
                    {
                        LoggerInstance.Msg($"Timing settings: FC colors={tintFc.Value}, score colors={tintScore.Value}, constantSpeed={lockSpeed.Value}, hideSides={hideSides.Value}, hideStrings={hideStrings.Value}, hideBeatlines={hideBeats.Value}.");
                        timings.Start();
                    }
                    else Notice("Start a song before capturing mod timings.");
                }
                GUI.Label(new Rect(x, y + 338, 540, 70), "Timings are written to Latest.log. Close this menu and play.");
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
                break;
        }
        if (editApply != null) DrawValueEditor();
    }

    private void Toggle(MelonPreferences_Entry<bool> entry, string label, float x, float y)
    {
        if (GUI.Button(new Rect(x, y, 540, 32), $"{label}: {(entry.Value ? "On" : "Off")}"))
        { entry.Value = !entry.Value; preferencesDirty = true; nextStats = 0; }
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
            moveSong = song; moveHud = true; settingsOpen = false; capture = null; UpdateCursor();
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
        songStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
        int size = Math.Clamp(songFont.Value, 14, 48);
        if (styledSongFont != size) { songStyle.fontSize = size; styledSongFont = size; }
        if (styledSongHex != songColor.Value)
        {
            try { styledSongColor = ParseColor(songColor.Value); } catch (FormatException) { styledSongColor = Color.white; }
            songStyle.normal.textColor = styledSongColor;
            styledSongHex = songColor.Value;
        }
        int width = Math.Min(520, Screen.width);
        int charsPerLine = Math.Max(8, (int)((width - 24) / (size * .6f)));
        int lineCount = text.Split('\n').Sum(line => Math.Max(1, (line.Length + charsPerLine - 1) / charsPerLine));
        int height = (int)(size * 1.35f) * lineCount + 20;
        var p = HudLayout.ClampPosition(songX.Value, songY.Value, width, height, Screen.width, Screen.height);
        var rect = new Rect(p.X, p.Y, width, height);
        EnsureHudBackground();
        var old = GUI.backgroundColor;
        try { GUI.backgroundColor = new Color(0, 0, 0, HudLayout.Unit(songOpacity.Value, .65f)); GUI.Box(rect, "", hudBackgroundStyle); }
        finally { GUI.backgroundColor = old; }
        GUI.Label(new Rect(rect.x + 12, rect.y + 8, width - 24, height - 16), text, songStyle);
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
    public override void OnLateUpdate()
    {
        if (!compatible || !gameplay || !gameplayInitialized || transition || visualError) return;
        var game = Il2Cpp.GameManager.field_Private_Static_GameManager_0;
        if (game == null || !game.songLoaded || !game.playersInit || game.globalVariables == null) return;
        // Once the one-time full-song speed reconciliation is complete, skip
        // the IL2CPP callback entirely when every optional override is idle.
        bool visualRequested = hideSides.Value || hideStrings.Value || hideBeats.Value || tintScore.Value || lockSpeed.Value;
        if (!visualRequested && !visuals.NeedsUpdate && !tintFc.Value && !rings.NeedsUpdate) return;
        try
        {
            Color Read(MelonPreferences_Entry<string> e)
            {
                if (parsedColors.TryGetValue(e.Value, out var cached)) return cached;
                Color color; try { color = ParseColor(e.Value); } catch (FormatException) { color = Color.white; }
                if (parsedColors.Count > 64) parsedColors.Clear();
                parsedColors[e.Value] = color; return color;
            }
            long ringTiming = timings.Begin();
            rings.Update(game, tintFc.Value,
                tintFc.Value ? Read(fcColor) : Color.white,
                tintFc.Value ? Read(pfcColor) : Color.white);
            timings.End(1, ringTiming);
            long visualTiming = timings.Begin();
            visuals.Apply(game, hideSides.Value, hideStrings.Value, hideBeats.Value,
                tintScore.Value, Read(starColor), Read(progressColor), Read(scoreColor), Read(songProgressColor), lockSpeed.Value,
                clock.Elapsed.TotalSeconds);
            timings.End(2, visualTiming);
        }
        catch (Exception ex)
        {
            visualError = true;
            LoggerInstance.Error($"Visual overrides paused after an error; counters and controls remain available: {ex}");
            Notice("Visual overrides stopped after an error. Send Latest.log.");
        }
    }

}
