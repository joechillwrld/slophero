using System.Diagnostics;
using MelonLoader;

namespace SlopHero.Mod;

internal sealed class RuntimeTimings
{
    private readonly long[] total = new long[7], maximum = new long[7], calls = new long[7];
    // 0.01 ms bins through 19.99 ms, plus an overflow bin. Clone Hero can
    // simulate several Updates per rendered frame, so keep these cadences
    // separate instead of presenting Update deltas as display frame times.
    private readonly int[] updateHistogram = new int[2001], renderHistogram = new int[2001];
    private long updateSamples, renderSamples, lastRenderTimestamp, observedRenderedFrames;
    private int lastRenderedFrameCount;
    private float maxUpdateMs;
    private double maxRenderMs;
    private long start, deadline;
    public bool Active => deadline != 0;
    public void Start()
    {
        Array.Clear(total); Array.Clear(maximum); Array.Clear(calls);
        Array.Clear(updateHistogram); Array.Clear(renderHistogram);
        updateSamples = renderSamples = lastRenderTimestamp = observedRenderedFrames = 0;
        lastRenderedFrameCount = -1;
        maxUpdateMs = 0; maxRenderMs = 0;
        start = Stopwatch.GetTimestamp(); deadline = start + 30 * Stopwatch.Frequency;
        MelonLogger.Msg("Mod timing capture started for 30 seconds. Keep playing the same chart/settings.");
    }
    public void SampleUpdate(float unscaledDeltaMs, bool willRender, int renderedFrameCount)
    {
        if (!Active) return;
        // Unity can run multiple simulation Updates for one rendered frame.
        // Its completed-frame counter is an independent cross-check on the
        // number of render callbacks inferred from willCurrentFrameRender.
        if (lastRenderedFrameCount >= 0 && renderedFrameCount > lastRenderedFrameCount)
            observedRenderedFrames += renderedFrameCount - lastRenderedFrameCount;
        lastRenderedFrameCount = renderedFrameCount;
        if (float.IsFinite(unscaledDeltaMs) && unscaledDeltaMs > 0)
        {
            int bin = unscaledDeltaMs >= 20 ? 2000 : (int)(unscaledDeltaMs * 100);
            updateHistogram[bin]++;
            updateSamples++;
            if (unscaledDeltaMs > maxUpdateMs) maxUpdateMs = unscaledDeltaMs;
        }
        if (!willRender) return;
        long now = Stopwatch.GetTimestamp();
        if (lastRenderTimestamp != 0)
        {
            double elapsedMs = (now - lastRenderTimestamp) * 1000d / Stopwatch.Frequency;
            if (double.IsFinite(elapsedMs) && elapsedMs > 0)
            {
                int bin = elapsedMs >= 20 ? 2000 : (int)(elapsedMs * 100);
                renderHistogram[bin]++;
                renderSamples++;
                if (elapsedMs > maxRenderMs) maxRenderMs = elapsedMs;
            }
        }
        lastRenderTimestamp = now;
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
        static string Percentile(int[] histogram, long samples, double fraction)
        {
            if (samples == 0) return "n/a";
            long target = (long)Math.Ceiling(samples * fraction), seen = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                seen += histogram[i];
                if (seen < target) continue;
                return i == 2000 ? ">=20ms" : $"<={(i + 1) / 100d:0.00}ms";
            }
            return "n/a";
        }
        MelonLogger.Msg($"Mod timing capture ({seconds:0.0}s), observed update rate={calls[0] / Math.Max(seconds, .001):0.0}/s; simulation Update deltas: samples={updateSamples}, p50={Percentile(updateHistogram, updateSamples, .50)}, p95={Percentile(updateHistogram, updateSamples, .95)}, p99={Percentile(updateHistogram, updateSamples, .99)}, max={maxUpdateMs:0.000}ms; completed render frames={observedRenderedFrames}, rate={observedRenderedFrames / Math.Max(seconds, .001):0.0}/s; scheduled render intervals: samples={renderSamples}, rate={renderSamples / Math.Max(seconds, .001):0.0}/s, p50={Percentile(renderHistogram, renderSamples, .50)}, p95={Percentile(renderHistogram, renderSamples, .95)}, p99={Percentile(renderHistogram, renderSamples, .99)}, max={maxRenderMs:0.000}ms; {Describe(0, "Update")}; {Describe(1, "FC rings")}; {Describe(2, "Visual overrides")}; {Describe(4, "Player/speed overrides")}; {Describe(5, "Score graphics")}; {Describe(6, "Performance effects")}; {Describe(3, "HUD repaint")}. Inner visual timings are subsets of Visual overrides. Scheduled render intervals measure CPU callback spacing, not GPU presentation time.");
    }
}
