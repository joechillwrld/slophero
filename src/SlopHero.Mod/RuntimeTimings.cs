using System.Diagnostics;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

internal sealed class RuntimeTimings
{
    private readonly long[] total = new long[9], maximum = new long[9], calls = new long[9];
    // 0.01 ms bins through 19.99 ms, plus an overflow bin. Clone Hero can
    // simulate several Updates per rendered frame, so keep these cadences
    // separate instead of presenting Update deltas as display frame times.
    private readonly int[] updateHistogram = new int[2001], renderHistogram = new int[2001], repaintHistogram = new int[2001];
    private long updateSamples, renderSamples, repaintSamples, lastRenderTimestamp, lastRepaintTimestamp;
    private float maxUpdateMs;
    private double maxRenderMs, maxRepaintMs;
    private long start, deadline;
    private readonly Dictionary<int, CameraTiming> cameras = new();
    private Camera.CameraCallback? preCullCallback, preRenderCallback, postRenderCallback;
    private sealed class CameraTiming
    {
        internal string Name = "";
        internal long PreCull, PreRender, CullTicks, RenderTicks;
        internal long CullCalls, RenderCalls;
    }
    public bool Active => deadline != 0;
    public void Start(bool includeCameraTiming = false)
    {
        if (Active) Finish();
        Array.Clear(total); Array.Clear(maximum); Array.Clear(calls);
        Array.Clear(updateHistogram); Array.Clear(renderHistogram); Array.Clear(repaintHistogram);
        updateSamples = renderSamples = repaintSamples = lastRenderTimestamp = lastRepaintTimestamp = 0;
        maxUpdateMs = 0; maxRenderMs = maxRepaintMs = 0;
        cameras.Clear();
        start = Stopwatch.GetTimestamp(); deadline = start + 30 * Stopwatch.Frequency;
        if (includeCameraTiming)
        {
            try
            {
                preCullCallback ??= DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(OnCameraPreCull));
                preRenderCallback ??= DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(OnCameraPreRender));
                postRenderCallback ??= DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(OnCameraPostRender));
                Camera.onPreCull += preCullCallback;
                Camera.onPreRender += preRenderCallback;
                Camera.onPostRender += postRenderCallback;
            }
            catch (Exception ex)
            {
                RemoveCameraCallbacks();
                MelonLogger.Warning($"Camera timing could not start; ordinary mod timing remains available: {ex.Message}");
            }
        }
        MelonLogger.Msg($"Mod timing capture started for 30 seconds (camera callbacks={(includeCameraTiming ? "on" : "off")}). Keep playing the same chart/settings.");
    }
    private void RemoveCameraCallbacks()
    {
        // Unsubscribe each independently, including after a partial Start.
        try { if (preCullCallback != null) Camera.onPreCull -= preCullCallback; }
        catch { /* Unity may already be unloading. */ }
        try { if (preRenderCallback != null) Camera.onPreRender -= preRenderCallback; }
        catch { /* Unity may already be unloading. */ }
        try { if (postRenderCallback != null) Camera.onPostRender -= postRenderCallback; }
        catch { /* Unity may already be unloading. */ }
    }
    private CameraTiming? GetCamera(Camera camera)
    {
        try
        {
            if (camera == null) return null;
            int id = camera.GetInstanceID();
            if (cameras.TryGetValue(id, out var timing)) return timing;
            timing = new CameraTiming { Name = camera.gameObject.name };
            cameras.Add(id, timing);
            return timing;
        }
        catch { return null; /* A camera may be destroyed during a scene transition. */ }
    }
    private void OnCameraPreCull(Camera camera)
    {
        if (!Active) return;
        var timing = GetCamera(camera);
        if (timing != null) timing.PreCull = Stopwatch.GetTimestamp();
    }
    private void OnCameraPreRender(Camera camera)
    {
        if (!Active) return;
        var timing = GetCamera(camera);
        if (timing == null) return;
        long now = Stopwatch.GetTimestamp();
        if (timing.PreCull != 0 && now >= timing.PreCull)
        {
            timing.CullTicks += now - timing.PreCull;
            timing.CullCalls++;
        }
        timing.PreCull = 0;
        timing.PreRender = now;
    }
    private void OnCameraPostRender(Camera camera)
    {
        if (!Active) return;
        var timing = GetCamera(camera);
        if (timing == null) return;
        long now = Stopwatch.GetTimestamp();
        if (timing.PreRender != 0 && now >= timing.PreRender)
        {
            timing.RenderTicks += now - timing.PreRender;
            timing.RenderCalls++;
        }
        timing.PreRender = 0;
    }
    public void SampleUpdate(float unscaledDeltaMs, bool willRender)
    {
        if (!Active) return;
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
    public void SampleRepaint()
    {
        if (!Active) return;
        long now = Stopwatch.GetTimestamp();
        if (lastRepaintTimestamp != 0)
        {
            double elapsedMs = (now - lastRepaintTimestamp) * 1000d / Stopwatch.Frequency;
            if (double.IsFinite(elapsedMs) && elapsedMs > 0)
            {
                int bin = elapsedMs >= 20 ? 2000 : (int)(elapsedMs * 100);
                repaintHistogram[bin]++;
                repaintSamples++;
                if (elapsedMs > maxRepaintMs) maxRepaintMs = elapsedMs;
            }
        }
        lastRepaintTimestamp = now;
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
        RemoveCameraCallbacks();
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
        MelonLogger.Msg($"Mod timing capture ({seconds:0.0}s), observed update rate={calls[0] / Math.Max(seconds, .001):0.0}/s; simulation Update deltas: samples={updateSamples}, p50={Percentile(updateHistogram, updateSamples, .50)}, p95={Percentile(updateHistogram, updateSamples, .95)}, p99={Percentile(updateHistogram, updateSamples, .99)}, max={maxUpdateMs:0.000}ms; scheduled render callbacks: samples={renderSamples}, rate={renderSamples / Math.Max(seconds, .001):0.0}/s, p50={Percentile(renderHistogram, renderSamples, .50)}, p95={Percentile(renderHistogram, renderSamples, .95)}, p99={Percentile(renderHistogram, renderSamples, .99)}, max={maxRenderMs:0.000}ms; Unity GUI repaint callbacks: samples={repaintSamples}, rate={repaintSamples / Math.Max(seconds, .001):0.0}/s, p50={Percentile(repaintHistogram, repaintSamples, .50)}, p95={Percentile(repaintHistogram, repaintSamples, .95)}, p99={Percentile(repaintHistogram, repaintSamples, .99)}, max={maxRepaintMs:0.000}ms; {Describe(0, "Update")}; {Describe(1, "FC rings")}; {Describe(2, "Visual overrides")}; {Describe(4, "Player/speed overrides")}; {Describe(5, "Score graphics")}; {Describe(6, "Performance effects")}; {Describe(3, "HUD repaint")}; {Describe(7, "Canvas HUD sync")}; {Describe(8, "TMP scale experiment")}. Inner visual timings are subsets of Visual overrides. Canvas rendering and GPU work are not measured by the sync bucket. Scheduled render and GUI repaint intervals measure CPU callback spacing, not GPU presentation time.");
        foreach (var camera in cameras.Values)
            MelonLogger.Msg($"Camera timing {camera.Name}: pre-cull to pre-render calls={camera.CullCalls}, avg={(camera.CullCalls > 0 ? camera.CullTicks * 1000d / Stopwatch.Frequency / camera.CullCalls : 0):0.000}ms; pre-render to post-render calls={camera.RenderCalls}, avg={(camera.RenderCalls > 0 ? camera.RenderTicks * 1000d / Stopwatch.Frequency / camera.RenderCalls : 0):0.000}ms. CPU callback intervals include other work and do not measure GPU presentation.");
        cameras.Clear();
    }
}
