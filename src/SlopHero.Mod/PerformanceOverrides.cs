using Il2Cpp;
using UnityEngine;
using Animator = Il2Cpp.Animator;
using ParticleSystem = UnityEngine.ParticleSystem;

namespace SlopHero.Mod;

/// <summary>
/// Optional, reversible suppression of note effects. This targets only the
/// particle systems and animation/renderers owned by a player's neck; scoring,
/// notes, input, and timing are left untouched.
/// </summary>
internal sealed class PerformanceOverrides
{
    private readonly Dictionary<IntPtr, (ParticleSystem Component, GameObject Object, bool Active)> particles = new();
    private readonly Dictionary<IntPtr, (Animator Component, bool Enabled)> animators = new();
    private readonly Dictionary<IntPtr, (Renderer Component, bool Enabled)> renderers = new();
    private bool hitParticles, sustainParticles, fretEffects;
    private double nextScan;

    public bool NeedsUpdate => hitParticles || sustainParticles || fretEffects
        || particles.Count != 0 || animators.Count != 0 || renderers.Count != 0;

    public void Clear()
    {
        Restore();
        hitParticles = sustainParticles = fretEffects = false;
        nextScan = 0;
    }

    public void Apply(GameManager game, bool disableHitParticles, bool disableSustainParticles,
        bool disableFretEffects, double realtime)
    {
        bool changed = hitParticles != disableHitParticles || sustainParticles != disableSustainParticles
            || fretEffects != disableFretEffects;
        hitParticles = disableHitParticles;
        sustainParticles = disableSustainParticles;
        fretEffects = disableFretEffects;

        if (!NeedsUpdate) return;
        if (!disableHitParticles && !disableSustainParticles && !disableFretEffects)
        {
            Restore();
            return;
        }
        // Scan only the player neck hierarchy, and at most once per second.
        // Player effect pools are children of this hierarchy; no global scene
        // search or per-frame component discovery is needed.
        if (!changed && realtime < nextScan) return;
        nextScan = realtime + 1.0;
        if (game.playerObjects == null) return;

        foreach (var player in game.playerObjects)
        {
            if (player == null || !player.enabled || player.neckController == null) continue;
            var neck = player.neckController;
            foreach (var system in neck.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system == null) continue;
                string name = system.gameObject.name;
                bool suppress = disableSustainParticles
                    ? name.Contains("Sustain", StringComparison.OrdinalIgnoreCase)
                    : false;
                bool noteParticle = name.StartsWith("Particles_Note", StringComparison.OrdinalIgnoreCase);
                bool hitParticle = noteParticle && !name.Contains("Sustain", StringComparison.OrdinalIgnoreCase);
                suppress |= disableHitParticles && hitParticle;
                if (suppress) Disable(system); else Restore(system);
            }

            if (disableFretEffects)
                foreach (var animator in EffectAnimators(neck)) Disable(animator);
            else
                foreach (var animator in EffectAnimators(neck)) Restore(animator);
        }
    }

    private static IEnumerable<Animator> EffectAnimators(BaseNeckController neck)
    {
        if (neck.FlameAnimators != null)
            foreach (var animator in neck.FlameAnimators) if (animator != null) yield return animator;
        if (neck.LightningAnimators != null)
            foreach (var animator in neck.LightningAnimators) if (animator != null) yield return animator;
        if (neck.LightningAnimators2 != null)
            foreach (var animator in neck.LightningAnimators2) if (animator != null) yield return animator;

        var guitar = neck.TryCast<GuitarNeckController>();
        if (guitar != null)
        {
            if (guitar.sparks != null) foreach (var animator in guitar.sparks) if (animator != null) yield return animator;
            if (guitar.sparksOverlay != null) foreach (var animator in guitar.sparksOverlay) if (animator != null) yield return animator;
            if (guitar.SPSparks != null) foreach (var animator in guitar.SPSparks) if (animator != null) yield return animator;
        }

        var drums = neck.TryCast<DrumsNeckController>();
        if (drums != null)
        {
            if (drums.SPSparks != null) foreach (var animator in drums.SPSparks) if (animator != null) yield return animator;
            if (drums.GhostFlameAnimators != null) foreach (var animator in drums.GhostFlameAnimators) if (animator != null) yield return animator;
            if (drums.AccentFlameAnimators != null) foreach (var animator in drums.AccentFlameAnimators) if (animator != null) yield return animator;
        }
    }

    private void Disable(ParticleSystem system)
    {
        if (!particles.ContainsKey(system.Pointer))
            particles.Add(system.Pointer, (system, system.gameObject, system.gameObject.activeSelf));
        try
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.gameObject.SetActive(false);
        }
        catch { }
    }

    private void Disable(Animator animator)
    {
        if (!animators.ContainsKey(animator.Pointer)) animators.Add(animator.Pointer, (animator, animator.enabled));
        try { animator.enabled = false; } catch { }
        try
        {
            foreach (var renderer in animator.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                if (!renderers.ContainsKey(renderer.Pointer)) renderers.Add(renderer.Pointer, (renderer, renderer.enabled));
                renderer.enabled = false;
            }
        }
        catch { }
    }

    private void Restore(ParticleSystem system)
    {
        if (!particles.Remove(system.Pointer, out var state)) return;
        try { state.Object.SetActive(state.Active); } catch { }
    }

    private void Restore(Animator animator)
    {
        if (animators.Remove(animator.Pointer, out var state))
            try { state.Component.enabled = state.Enabled; } catch { }
        try
        {
            foreach (var renderer in animator.GetComponentsInChildren<Renderer>(true))
                if (renderer != null) Restore(renderer);
        }
        catch { }
    }

    private void Restore(Renderer renderer)
    {
        if (renderers.Remove(renderer.Pointer, out var state))
            try { state.Component.enabled = state.Enabled; } catch { }
    }

    private void Restore()
    {
        foreach (var state in particles.Values)
            try { state.Object.SetActive(state.Active); } catch { }
        foreach (var state in animators.Values)
            try { state.Component.enabled = state.Enabled; } catch { }
        foreach (var state in renderers.Values)
            try { state.Component.enabled = state.Enabled; } catch { }
        particles.Clear(); animators.Clear(); renderers.Clear();
    }
}

/// <summary>Opt-in switches for global input/presence services; online game services are untouched.</summary>
internal sealed class OptionalServiceOverrides
{
    private MIDIInputManager? midi;
    private DiscordController? discord;
    private bool midiOriginalEnabled, discordOriginalEnabled;
    private IntPtr midiPointer, discordPointer;
    private bool disableMidi, disableDiscord;
    private bool discordSuppressedForSession;
    private double nextDiscovery;

    public void Apply(bool stopMidi, bool disableRichPresence, double realtime)
    {
        bool changed = disableMidi != stopMidi || disableDiscord != disableRichPresence;
        disableMidi = stopMidi;
        disableDiscord = disableRichPresence;
        if (!changed && realtime < nextDiscovery) return;
        nextDiscovery = realtime + .5;

        var currentMidi = MIDIInputManager.instance;
        if (currentMidi != null)
        {
            if (midiPointer != currentMidi.Pointer)
            {
                midi = currentMidi; midiPointer = currentMidi.Pointer; midiOriginalEnabled = currentMidi.enabled;
            }
        }
        if (midi != null) midi.enabled = midiOriginalEnabled && !stopMidi;

        var currentDiscord = DiscordController.instance;
        if (currentDiscord != null)
        {
            if (discordPointer != currentDiscord.Pointer)
            {
                discord = currentDiscord; discordPointer = currentDiscord.Pointer; discordOriginalEnabled = currentDiscord.enabled;
            }
        }
        if (disableRichPresence) discordSuppressedForSession = true;
        if (discord != null)
        {
            // DiscordController shuts its client down from OnDisable and has no
            // restart path for an already-created instance. Keep it suppressed
            // for this process; turning the option off takes effect next launch.
            bool shouldEnable = discordOriginalEnabled && !disableRichPresence && !discordSuppressedForSession;
            discord.enabled = shouldEnable;
        }
    }

    public void Restore()
    {
        try { if (midi != null) midi.enabled = midiOriginalEnabled; } catch { }
        if (!discordSuppressedForSession)
            try { if (discord != null) discord.enabled = discordOriginalEnabled; } catch { }
        midi = null; discord = null; midiPointer = discordPointer = IntPtr.Zero;
        nextDiscovery = 0;
        disableMidi = disableDiscord = false;
    }
}
