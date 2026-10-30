namespace SlopHero.Core;

public static class PaletteRange
{
    public static float Sample(float minimum, float maximum, float lowerBound, float upperBound, float unit)
    {
        float low = float.IsFinite(minimum) ? Math.Clamp(minimum, lowerBound, upperBound) : lowerBound;
        float high = float.IsFinite(maximum) ? Math.Clamp(maximum, lowerBound, upperBound) : upperBound;
        if (high < low) (low, high) = (high, low);
        return low + Math.Clamp(unit, 0f, 1f) * (high - low);
    }
}
