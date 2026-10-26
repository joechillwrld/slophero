namespace SlopHero.Core;

/// <summary>
/// Keeps the fractional phase left after one network update while discarding
/// whole missed periods. This avoids turning a long frame into catch-up bursts.
/// </summary>
public static class NetworkTickRemainder
{
    public static float PreserveFraction(float accumulatedTime, float tickInterval)
    {
        if (!float.IsFinite(accumulatedTime) || accumulatedTime < 0f
            || !float.IsFinite(tickInterval) || tickInterval <= 0f)
            return 0f;

        float remainder = accumulatedTime % tickInterval;
        return float.IsFinite(remainder) && remainder >= 0f ? remainder : 0f;
    }
}
