namespace SlopHero.Core;

/// <summary>Measures the game's actual playback-clock rate against real time.</summary>
public sealed class PracticeClockRate
{
    private double previousSongTime, previousRealtime;
    private double windowSongTime, windowRealtime;
    private bool sampled;
    private float rate = float.NaN;
    public float CurrentRate => rate;

    public float Update(double songTime, double realtime, bool active)
    {
        if (!active || !double.IsFinite(songTime) || !double.IsFinite(realtime))
        {
            Reset();
            return float.NaN;
        }
        if (!sampled)
        {
            previousSongTime = songTime;
            previousRealtime = realtime;
            sampled = true;
            return rate;
        }

        double elapsed = realtime - previousRealtime;
        double songElapsed = songTime - previousSongTime;
        previousSongTime = songTime;
        previousRealtime = realtime;
        if (!double.IsFinite(elapsed) || elapsed <= 0 || elapsed > 1 ||
            !double.IsFinite(songElapsed) || songElapsed <= 0)
        {
            windowSongTime = windowRealtime = 0;
            return rate;
        }

        windowSongTime += songElapsed;
        windowRealtime += elapsed;
        if (windowRealtime >= .2)
        {
            double measured = windowSongTime / windowRealtime;
            windowSongTime = windowRealtime = 0;
            if (double.IsFinite(measured) && measured >= .1 && measured <= 4)
                rate = (float)measured;
        }
        return rate;
    }

    public void Reset()
    {
        sampled = false;
        previousSongTime = previousRealtime = 0;
        windowSongTime = windowRealtime = 0;
        rate = float.NaN;
    }
}
