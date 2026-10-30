namespace SlopHero.Core;

public enum AutoRestartReason { None, MissLimit, GhostLimit, EarlyMiss }

/// <summary>Pure decision policy; the game transition is handled separately.</summary>
public readonly record struct AutoRestartRule(int MissLimit, int GhostLimit, int? NoMissBeforeSection)
{
    public AutoRestartReason Evaluate(long misses, long ghosts, int currentSection)
    {
        if (NoMissBeforeSection is int section && section >= 0
            && currentSection < section && misses > 0) return AutoRestartReason.EarlyMiss;
        if (MissLimit > 0 && misses >= MissLimit) return AutoRestartReason.MissLimit;
        if (GhostLimit > 0 && ghosts >= GhostLimit) return AutoRestartReason.GhostLimit;
        return AutoRestartReason.None;
    }
}
