namespace SlopHero.Core;

/// <summary>Guards performance experiments that pause optional work in a solo song.</summary>
public static class OfflineSoloSongScope
{
    public static bool IsEligible(
        bool requested,
        bool gameReady,
        bool songPlaying,
        bool paused,
        bool songOver,
        int runningCoroutines,
        int playerCount,
        int gameMode,
        bool playingSetlist,
        bool transitioning,
        bool settingsOpen,
        bool focused,
        bool online)
        => requested && gameReady && songPlaying && !paused && !songOver
            && runningCoroutines == 0 && playerCount == 1 && gameMode == 0
            && !playingSetlist && !transitioning && !settingsOpen && focused && !online;
}
