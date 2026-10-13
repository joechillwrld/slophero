using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

public sealed partial class Core
{
    private MelonPreferences_Entry<int> songSpeedStep = null!;

    private void InitializeSongSpeedMenu()
    {
        songSpeedStep = preferences.CreateEntry("SongSpeedStep", 5);
        songSpeedStep.Value = SongSpeedControl.ClampStep(songSpeedStep.Value);
    }
}
