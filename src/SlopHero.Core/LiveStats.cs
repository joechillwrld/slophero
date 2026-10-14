namespace SlopHero.Core;

/// <summary>Resolved chart notes only: future notes and extra fret inputs never
/// enter the hit-percentage denominator. The game adapter supplies counters.</summary>
public readonly record struct LiveStats
{
    public long Hits { get; }
    public long Misses { get; }
    public long GhostInputs { get; }
    public double? HitPercentage => Hits == 0 && Misses == 0
        ? null : 100d * Hits / ((double)Hits + Misses);

    public static LiveStats FromResolvedHeads(long hits, long resolvedHeads, long ghostInputs)
    {
        if (resolvedHeads < 0) throw new ArgumentOutOfRangeException(nameof(resolvedHeads));
        // Native hits can advance before a bounded chart scan catches up.
        return new LiveStats(hits, Math.Max(0, resolvedHeads - hits), ghostInputs);
    }

    public LiveStats(long hits, long misses, long ghostInputs)
    {
        if (hits < 0) throw new ArgumentOutOfRangeException(nameof(hits));
        if (misses < 0) throw new ArgumentOutOfRangeException(nameof(misses));
        if (ghostInputs < 0) throw new ArgumentOutOfRangeException(nameof(ghostInputs));
        Hits = hits;
        Misses = misses;
        GhostInputs = ghostInputs;
    }
}
