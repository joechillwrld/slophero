using Il2Cpp;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Optional freeze for the two animated lyric-background UVs. It leaves the
/// lyric display and its background sprites intact, and restores each
/// component's original enabled state when switched off or when gameplay ends.
/// </summary>
internal sealed class LyricsScrollOverride
{
    private readonly record struct ScrollState(UVScroll Component, bool Enabled);
    private readonly Dictionary<IntPtr, ScrollState> states = new();
    private double nextDiscovery;

    public bool NeedsRestore => states.Count != 0;

    public void Apply(bool freeze, double realtime)
    {
        if (!freeze)
        {
            Restore();
            nextDiscovery = 0;
            return;
        }

        if (states.Count == 0)
        {
            if (realtime < nextDiscovery) return;
            Discover(realtime);
        }

        foreach (var state in states.Values)
        {
            try
            {
                var component = state.Component;
                if (component != null && component.enabled) component.enabled = false;
            }
            catch { }
        }
    }

    public void Clear()
    {
        Restore();
        nextDiscovery = 0;
    }

    private void Discover(double realtime)
    {
        // This is called only while the opt-in switch is enabled. Restrict the
        // search to Clone Hero's two known lyric-background objects rather
        // than scanning the whole scene or touching other lyric effects.
        nextDiscovery = realtime + 1;
        try
        {
            var hud = GameObject.Find("HUDCam");
            var lyrics = hud == null ? null : hud.transform.Find("Menu Canvas/Lyrics");
            if (lyrics == null) return;

            foreach (var component in lyrics.GetComponentsInChildren<UVScroll>(true))
            {
                if (component == null) continue;
                string objectName = component.gameObject.name;
                if ((objectName != "CurrentLyricBackground" && objectName != "nextLyricBackground")
                    || component.transform.parent != lyrics) continue;
                if (states.ContainsKey(component.Pointer)) continue;
                states.Add(component.Pointer, new ScrollState(component, component.enabled));
            }
            if (states.Count != 0) nextDiscovery = double.MaxValue;
        }
        catch
        {
            // Missing or stripped wrappers leave vanilla rendering untouched;
            // retry on a later 20 Hz feature pass.
        }
    }

    private void Restore()
    {
        foreach (var state in states.Values)
        {
            try
            {
                if (state.Component != null && state.Component.enabled != state.Enabled)
                    state.Component.enabled = state.Enabled;
            }
            catch { }
        }
        states.Clear();
    }
}
