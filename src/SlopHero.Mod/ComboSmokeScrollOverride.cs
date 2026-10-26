using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Optional freeze for the two local combo-smoke UV animations. It leaves
/// their renderers intact and restores each component's original enabled
/// state when switched off or when gameplay ends.
/// </summary>
internal sealed class ComboSmokeScrollOverride
{
    private readonly record struct ScrollState(UVScroll Component, bool Enabled);
    private readonly Dictionary<IntPtr, ScrollState> states = new();
    private readonly HashSet<IntPtr> discoveredPlayers = new();
    private double nextDiscovery;
    private bool warnedDiscovery;

    public bool NeedsRestore => states.Count != 0;
    public int TrackedCount => states.Count;

    public int PausedCount
    {
        get
        {
            int count = 0;
            foreach (var state in states.Values)
            {
                try { if (state.Enabled && state.Component != null && !state.Component.enabled) count++; }
                catch { }
            }
            return count;
        }
    }

    public void Apply(bool freeze, GameManager? game, double realtime)
    {
        if (!freeze)
        {
            Restore();
            nextDiscovery = 0;
            return;
        }

        // Player visuals can be recreated during a practice transition without
        // changing scenes. Recheck slowly while enabled so replacement smoke
        // components are found, while avoiding a hierarchy walk every frame.
        if (realtime >= nextDiscovery) Discover(game, realtime);

        List<IntPtr>? stale = null;
        foreach (var pair in states)
        {
            try
            {
                var component = pair.Value.Component;
                if (component == null)
                {
                    (stale ??= new List<IntPtr>()).Add(pair.Key);
                    continue;
                }
                if (component.enabled) component.enabled = false;
            }
            catch { }
        }
        if (stale != null)
        {
            foreach (var pointer in stale) states.Remove(pointer);
            // A skin/mod may replace only the visual children under the same
            // player object. Let the next pass discover their replacements.
            discoveredPlayers.Clear();
        }
    }

    public void Clear()
    {
        Restore();
        nextDiscovery = 0;
        warnedDiscovery = false;
    }

    private void Discover(GameManager? game, double realtime)
    {
        // Run only while the opt-in switch is enabled, and anchor discovery to
        // active nonremote guitars so online opponents remain untouched. The
        // game can keep inputEnabled false during transitions even while the
        // local guitar's cosmetic hierarchy is already live.
        nextDiscovery = realtime + 2;
        try
        {
            if (game?.playerObjects == null) return;
            foreach (var player in game.playerObjects)
            {
                if (player == null || !player.enabled || player.isRemotePlayerPlaying) continue;
                var guitar = player.TryCast<GuitarPlayer>();
                if (guitar == null) continue;
                // Smoke is a fixed pair in the player prefab. A recreated
                // practice player has a new pointer and still gets scanned.
                if (discoveredPlayers.Contains(guitar.Pointer)) continue;
                var guitarRoot = guitar.transform;

                int found = 0;
                // The current player strips some closed GetComponentsInChildren<T>
                // wrappers. The live Gameplay snapshot already uses this
                // scene-wide UVScroll lookup successfully and finds the pair.
                foreach (var component in UnityEngine.Object.FindObjectsOfType<UVScroll>())
                {
                    if (component == null) continue;
                    var parent = component.transform.parent;
                    var combo = parent == null ? null : parent.parent;
                    if (parent == null || parent.name != "multiplier"
                        || combo == null || combo.name != "Combo"
                        || combo.parent == null || combo.parent.Pointer != guitarRoot.Pointer) continue;
                    string objectName = component.gameObject.name;
                    if (objectName != "smoke_combo_fast" && objectName != "smoke_combo_slow") continue;
                    found++;
                    if (states.ContainsKey(component.Pointer)) continue;
                    states.Add(component.Pointer, new ScrollState(component, component.enabled));
                }
                if (found >= 2) discoveredPlayers.Add(guitar.Pointer);
            }
        }
        catch (Exception ex)
        {
            if (!warnedDiscovery)
            {
                warnedDiscovery = true;
                MelonLogger.Warning($"Combo-smoke discovery failed; its animations remain active: {ex}");
            }
            // A missing wrapper leaves the native animation intact. Retry
            // after the normal two-second discovery interval.
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
        discoveredPlayers.Clear();
    }
}
