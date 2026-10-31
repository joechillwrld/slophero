namespace SlopHero.Core;

public static class ChartNote
{
    public static bool CountsTowardAccuracy(uint flags) => (flags & 6) != 6;
    public static bool IsResolved(ushort state) => (state & 3) != 0;
    public static bool IsHit(ushort state) => (state & 1) != 0;
    public static bool IsMissed(ushort state) => (state & 2) != 0;
    public static bool IsMissedWithoutHit(ushort state) => IsMissed(state) && !IsHit(state);
}
