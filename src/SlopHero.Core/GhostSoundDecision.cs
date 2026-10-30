namespace SlopHero.Core;

public static class GhostSoundDecision
{
    // When both counters rise together, keep the sound: a real chart miss
    // must never be silenced by a concurrent ghost input.
    public static bool SuppressLoseMultiplier(long oldGhosts, long newGhosts,
        long oldMisses, long newMisses) =>
        newGhosts > oldGhosts && newMisses == oldMisses;
}
