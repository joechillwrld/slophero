using System.Diagnostics;
using MelonLoader;

namespace SlopHero.Mod;

internal sealed class RuntimeTimings
{
    private readonly long[] total = new long[6], maximum = new long[6], calls = new long[6];
    private long start, deadline;
    public bool Active => deadline != 0;
    public void Start()
    {
        Array.Clear(total); Array.Clear(maximum); Array.Clear(calls);
        start = Stopwatch.GetTimestamp(); deadline = start + 30 * Stopwatch.Frequency;
        MelonLogger.Msg("Mod timing capture started for 30 seconds. Keep playing the same chart/settings.");
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
        MelonLogger.Msg($"Mod timing capture ({seconds:0.0}s), observed update rate={calls[0] / Math.Max(seconds, .001):0.0}/s; {Describe(0, "Update")}; {Describe(1, "FC rings")}; {Describe(2, "Visual overrides")}; {Describe(4, "Player/speed overrides")}; {Describe(5, "Score graphics")}; {Describe(3, "HUD repaint")}. Inner visual timings are subsets of Visual overrides. Measures mod callbacks, not the whole game's CPU/GPU workload.");
    }
}
