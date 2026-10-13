using System.Diagnostics;
using MelonLoader;

namespace SlopHero.Mod;

internal sealed class RuntimeTimings
{
    private readonly long[] total = new long[6], maximum = new long[6], calls = new long[6];
    // 0.01 ms histogram bins through 19.99 ms, plus an overflow bin. This
    // captures frame pacing without allocating or sorting samples mid-song.
    private readonly int[] frameHistogram = new int[2001];
    private long frameSamples;
    private float maxFrameMs;
    private long start, deadline;
    public bool Active => deadline != 0;
    public void Start()
    {
        Array.Clear(total); Array.Clear(maximum); Array.Clear(calls);
        Array.Clear(frameHistogram); frameSamples = 0; maxFrameMs = 0;
        start = Stopwatch.GetTimestamp(); deadline = start + 30 * Stopwatch.Frequency;
        MelonLogger.Msg("Mod timing capture started for 30 seconds. Keep playing the same chart/settings.");
    }
    public void SampleFrame(float unscaledDeltaMs)
    {
        if (!Active || !float.IsFinite(unscaledDeltaMs) || unscaledDeltaMs <= 0) return;
        int bin = unscaledDeltaMs >= 20 ? 2000 : (int)(unscaledDeltaMs * 100);
        frameHistogram[bin]++;
        frameSamples++;
        if (unscaledDeltaMs > maxFrameMs) maxFrameMs = unscaledDeltaMs;
    }
    public long Begin() => Active ? Stopwatch.GetTimestamp() : 0;
    public void End(int bucket, long began)
    {
        if (!Active || began == 0) return;
        long now = Stopwatch.GetTimestamp(), elapsed = now - began;
        total[bucket] += elapsed; maximum[bucket] = Math.Max(maximum[bucket], elapsed); calls[bucket]++;
        // The main Update scope closes a capture. Nested visual scopes are
        // reported too, but must not finish the report before their parent.
        if (bucket == 0 && now >= deadline) Finish();
    }
    public void Finish()
    {
        if (!Active) return;
        double seconds = (Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency;
        deadline = 0;
        string Describe(int i, string name) => $"{name}: calls={calls[i]}, avg={(calls[i] > 0 ? total[i] * 1000d / Stopwatch.Frequency / calls[i] : 0):0.000}ms, max={maximum[i] * 1000d / Stopwatch.Frequency:0.000}ms";
        string Percentile(double fraction)
        {
            if (frameSamples == 0) return "n/a";
            long target = (long)Math.Ceiling(frameSamples * fraction), seen = 0;
            for (int i = 0; i < frameHistogram.Length; i++)
            {
                seen += frameHistogram[i];
                if (seen < target) continue;
                return i == 2000 ? ">=20ms" : $"<={(i + 1) / 100d:0.00}ms";
            }
            return "n/a";
        }
        MelonLogger.Msg($"Mod timing capture ({seconds:0.0}s), observed update rate={calls[0] / Math.Max(seconds, .001):0.0}/s; frame unscaledDelta samples={frameSamples}, p50={Percentile(.50)}, p95={Percentile(.95)}, p99={Percentile(.99)}, max={maxFrameMs:0.000}ms; {Describe(0, "Update")}; {Describe(1, "FC rings")}; {Describe(2, "Visual overrides")}; {Describe(4, "Player/speed overrides")}; {Describe(5, "Score graphics")}; {Describe(3, "HUD repaint")}. Inner visual timings are subsets of Visual overrides.");
    }
}
