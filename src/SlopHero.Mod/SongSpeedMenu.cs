using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

public sealed partial class Core
{
    private MelonPreferences_Entry<int> songSpeedStep = null!;
    private string songSpeedInput = "100", songSpeedStepInput = "5", songSpeedError = "";
    private bool songSpeedInputDirty;

    private void InitializeSongSpeedMenu()
    {
        songSpeedStep = preferences.CreateEntry("SongSpeedStep", 5);
        songSpeedStep.Value = SongSpeedControl.ClampStep(songSpeedStep.Value);
        songSpeedStepInput = songSpeedStep.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void DrawSongSpeedMenu()
    {
        if (!SongSpeedControl.TryRead(out int liveSpeed))
        {
            GUI.Box(new Rect(20, Screen.height - 118, 340, 88), "SlopHero speed editor\nCurrent speed setting is unavailable.");
            return;
        }

        if (!songSpeedInputDirty) songSpeedInput = liveSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        float x = 20, y = Math.Max(20, Screen.height - 146);
        GUI.Box(new Rect(x, y, 350, 126), "SlopHero song speed");
        GUI.Label(new Rect(x + 12, y + 32, 103, 26), "Speed (%)");
        string editedSpeed = GUI.TextField(new Rect(x + 112, y + 30, 105, 30), songSpeedInput, 8);
        if (!string.Equals(editedSpeed, songSpeedInput, StringComparison.Ordinal))
        {
            songSpeedInput = editedSpeed;
            songSpeedInputDirty = true;
            songSpeedError = "";
        }
        if (GUI.Button(new Rect(x + 225, y + 30, 110, 30), "Apply")) ApplySongSpeedInput();

        GUI.Label(new Rect(x + 12, y + 68, 103, 26), "Step (%)");
        songSpeedStepInput = GUI.TextField(new Rect(x + 112, y + 66, 65, 30), songSpeedStepInput, 4);
        if (GUI.Button(new Rect(x + 184, y + 66, 70, 30), "−")) AdjustSongSpeed(-1);
        if (GUI.Button(new Rect(x + 260, y + 66, 75, 30), "+")) AdjustSongSpeed(1);
        GUI.Label(new Rect(x + 12, y + 100, 323, 22), songSpeedError.Length == 0
            ? $"Current: {liveSpeed}%  ·  Enter 1–1000, then Apply or use ±."
            : songSpeedError);
    }

    private void ApplySongSpeedInput()
    {
        if (!SongSpeedControl.TryParsePercent(songSpeedInput, out int speed))
        {
            songSpeedError = "Enter a whole percent from 1 to 1000.";
            return;
        }
        songSpeedInputDirty = true;
        songSpeedError = SongSpeedControl.TryWrite(speed) ? "" : "The game did not accept that speed value.";
        if (songSpeedError.Length == 0) songSpeedInputDirty = false;
    }

    private void AdjustSongSpeed(int direction)
    {
        if (!int.TryParse(songSpeedStepInput, out int step)) step = songSpeedStep.Value;
        step = SongSpeedControl.ClampStep(step);
        songSpeedStep.Value = step;
        songSpeedStepInput = step.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!SongSpeedControl.TryParsePercent(songSpeedInput, out int speed)
            && !SongSpeedControl.TryRead(out speed)) return;
        int adjusted = Math.Clamp(speed + direction * step, 1, 1000);
        songSpeedInput = adjusted.ToString(System.Globalization.CultureInfo.InvariantCulture);
        songSpeedInputDirty = true;
        songSpeedError = SongSpeedControl.TryWrite(adjusted) ? "" : "The game did not accept that speed value.";
        if (songSpeedError.Length == 0) songSpeedInputDirty = false;
    }
}
