using Il2Cpp;
using MelonLoader;
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
    private static readonly string[] FretColors = { "Orange", "Blue", "Yellow", "Red", "Green" };
    private static readonly string[] FretLightningObjects = { "lightning_white", "lightning" };
    private readonly record struct ParticleEffect(ParticleSystem Component, bool IsHit, bool IsSustain);
    private sealed class NeckEffects
    {
        public NeckEffects(ParticleEffect[] particles, Animator[] animators, CustomFadeProperties[] fretFades)
        {
            Particles = particles;
            Animators = animators;
            FretFades = fretFades;
        }

        public ParticleEffect[] Particles { get; }
        public Animator[] Animators { get; }
        public CustomFadeProperties[] FretFades { get; }
    }

    private readonly Dictionary<IntPtr, (ParticleSystem Component, GameObject Object, bool Active)> particles = new();
    private readonly Dictionary<IntPtr, (Animator Component, bool Enabled)> animators = new();
    private readonly Dictionary<IntPtr, (CustomFadeProperties Component, bool Enabled)> fretFades = new();
    private readonly Dictionary<IntPtr, (Renderer Component, bool Enabled)> renderers = new();
    private readonly Dictionary<IntPtr, NeckEffects> neckEffects = new();
    private readonly Dictionary<IntPtr, Renderer[]> animatorRenderers = new();
    private bool hitParticles, sustainParticles, fretEffects;
    private double nextScan, nextDiscovery;

    public bool NeedsUpdate => hitParticles || sustainParticles || fretEffects
        || particles.Count != 0 || animators.Count != 0 || fretFades.Count != 0 || renderers.Count != 0;

    public bool IsApplyDue(bool disableHitParticles, bool disableSustainParticles,
        bool disableFretEffects, double realtime)
    {
        bool changed = hitParticles != disableHitParticles || sustainParticles != disableSustainParticles
            || fretEffects != disableFretEffects;
        if (changed) return true;
        if (!NeedsUpdate) return false;
        if (!disableHitParticles && !disableSustainParticles && !disableFretEffects) return true;
        return realtime >= nextScan;
    }

    public int PausedFretFadeCount
    {
        get
        {
            int count = 0;
            foreach (var state in fretFades.Values)
                try { if (state.Enabled && state.Component != null && !state.Component.enabled) count++; } catch { }
            return count;
        }
    }

    public void Clear()
    {
        Restore();
        hitParticles = sustainParticles = fretEffects = false;
        nextScan = nextDiscovery = 0;
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
        // Apply to cached player-neck effects at most once per second. New
        // necks are discovered on cache miss; rebuild known hierarchies only
        // every 30 seconds to catch a rare late-added child without repeatedly
        // allocating component arrays from stable effect pools.
        if (!changed && realtime < nextScan) return;
        nextScan = realtime + 1.0;
        if (game.playerObjects == null) return;
        bool discover = neckEffects.Count == 0 || realtime >= nextDiscovery;
        if (discover) nextDiscovery = realtime + 30.0;

        foreach (var player in game.playerObjects)
        {
            if (player == null || !player.enabled || player.neckController == null) continue;
            var neck = player.neckController;
            if (discover || !neckEffects.TryGetValue(neck.Pointer, out var effects))
                neckEffects[neck.Pointer] = effects = Discover(player, neck);

            foreach (var effect in effects.Particles)
            {
                var system = effect.Component;
                if (system == null) continue;
                bool suppress = disableSustainParticles && effect.IsSustain
                    || disableHitParticles && effect.IsHit;
                if (suppress) Disable(system); else Restore(system);
            }

            if (disableFretEffects)
            {
                foreach (var effectAnimator in effects.Animators)
                    if (effectAnimator != null) Disable(effectAnimator);
                foreach (var fretFade in effects.FretFades)
                    if (fretFade != null) Disable(fretFade);
            }
            else
            {
                foreach (var animatorToRestore in effects.Animators)
                    if (animatorToRestore != null) Restore(animatorToRestore);
                foreach (var fretFadeToRestore in effects.FretFades)
                    if (fretFadeToRestore != null) Restore(fretFadeToRestore);
            }
        }
    }

    private static NeckEffects Discover(BasePlayer player, BaseNeckController neck)
    {
        var particles = new List<ParticleEffect>();
        foreach (var system in neck.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (system == null) continue;
            string name = system.gameObject.name;
            bool isSustain = name.Contains("Sustain", StringComparison.OrdinalIgnoreCase);
            bool isHit = name.StartsWith("Particles_Note", StringComparison.OrdinalIgnoreCase) && !isSustain;
            particles.Add(new ParticleEffect(system, isHit, isSustain));
        }

        var fretFades = new List<CustomFadeProperties>();
        var guitar = player.TryCast<GuitarPlayer>();
        if (guitar != null)
        {
            // Runtime capture shows these fade scripts attached to the exact
            // fret lightning objects. They have no visible work while the
            // existing fret-effects option has hidden their effect objects.
            foreach (string fretName in FretColors)
            {
                var fret = guitar.transform.Find($"Fret_{fretName}");
                if (fret == null) continue;
                foreach (string lightningName in FretLightningObjects)
                {
                    var lightning = fret.Find(lightningName);
                    if (lightning == null) continue;
                    var fade = lightning.GetComponent<CustomFadeProperties>();
                    if (fade != null) fretFades.Add(fade);
                }
            }
        }

        return new NeckEffects(particles.ToArray(), EffectAnimators(neck).ToArray(), fretFades.ToArray());
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
            // Pools may reactivate an effect after a hit. Suppress it then,
            // without restarting and deactivating an already-idle object on
            // every one-second discovery pass.
            if (system.gameObject.activeSelf)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.gameObject.SetActive(false);
            }
        }
        catch { }
    }

    private void Disable(Animator animator)
    {
        if (!animators.ContainsKey(animator.Pointer)) animators.Add(animator.Pointer, (animator, animator.enabled));
        try { if (animator.enabled) animator.enabled = false; } catch { }
        try
        {
            if (!animatorRenderers.TryGetValue(animator.Pointer, out var children))
                animatorRenderers.Add(animator.Pointer, children = animator.GetComponentsInChildren<Renderer>(true).ToArray());
            foreach (var renderer in children)
            {
                if (renderer == null) continue;
                if (!renderers.ContainsKey(renderer.Pointer)) renderers.Add(renderer.Pointer, (renderer, renderer.enabled));
                if (renderer.enabled) renderer.enabled = false;
            }
        }
        catch { }
    }

    private void Disable(CustomFadeProperties fade)
    {
        if (!fretFades.ContainsKey(fade.Pointer)) fretFades.Add(fade.Pointer, (fade, fade.enabled));
        try { if (fade.enabled) fade.enabled = false; } catch { }
    }

    private void Restore(ParticleSystem system)
    {
        if (!particles.Remove(system.Pointer, out var state)) return;
        try { state.Object.SetActive(state.Active); } catch { }
    }

    private void Restore(Animator animator)
    {
        if (!animators.Remove(animator.Pointer, out var state)) return;
        try { state.Component.enabled = state.Enabled; } catch { }
        if (!animatorRenderers.TryGetValue(animator.Pointer, out var children)) return;
        foreach (var renderer in children)
            if (renderer != null) Restore(renderer);
    }

    private void Restore(CustomFadeProperties fade)
    {
        if (fretFades.Remove(fade.Pointer, out var state))
            try { state.Component.enabled = state.Enabled; } catch { }
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
        foreach (var state in fretFades.Values)
            try { state.Component.enabled = state.Enabled; } catch { }
        foreach (var state in renderers.Values)
            try { state.Component.enabled = state.Enabled; } catch { }
        particles.Clear(); animators.Clear(); fretFades.Clear(); renderers.Clear();
        neckEffects.Clear(); animatorRenderers.Clear();
        nextScan = nextDiscovery = 0;
    }
}

/// <summary>Opt-in MIDI and presence switches; networked game services are untouched.</summary>
internal sealed class OptionalServiceOverrides
{
    private MIDIInputManager? midi;
    private DiscordController? discord;
    private bool midiOriginalEnabled, discordOriginalEnabled;
    private IntPtr midiPointer, discordPointer;
    private IntPtr midiShutdownPointer;
    private bool pauseMidiDiscovery, disableMidiForSession, midiDisabledForSession, disableDiscord;
    private bool discordSuppressedForSession;
    private double nextDiscovery;

    public void Apply(bool stopMidiDiscovery, bool disableDrumMidiForSession, bool disableRichPresence, double realtime)
    {
        bool changed = pauseMidiDiscovery != stopMidiDiscovery
            || disableMidiForSession != disableDrumMidiForSession || disableDiscord != disableRichPresence;
        pauseMidiDiscovery = stopMidiDiscovery;
        disableMidiForSession = disableDrumMidiForSession;
        disableDiscord = disableRichPresence;
        // Service switches default off. Skip service discovery entirely until a
        // user opts in, or a previously suppressed service needs restoring.
        if (!stopMidiDiscovery && !disableDrumMidiForSession && !midiDisabledForSession
            && !disableRichPresence && !discordSuppressedForSession
            && midi == null && discord == null) return;
        if (!changed && realtime < nextDiscovery) return;
        nextDiscovery = realtime + .5;

        var currentMidi = stopMidiDiscovery || disableDrumMidiForSession || midiDisabledForSession
            ? MIDIInputManager.instance : null;
        if (currentMidi != null)
        {
            if (midiPointer != currentMidi.Pointer)
            {
                midi = currentMidi; midiPointer = currentMidi.Pointer; midiOriginalEnabled = currentMidi.enabled;
                midiShutdownPointer = IntPtr.Zero;
            }
        }
        if (midi != null && (disableDrumMidiForSession || midiDisabledForSession)
            && midiShutdownPointer != midiPointer)
        {
            try
            {
                // This is Clone Hero's own teardown path: it closes the
                // active MidiIn devices and unregisters their input callbacks.
                midi.OnApplicationQuit();
                midiShutdownPointer = midiPointer;
                midiDisabledForSession = true;
                MelonLogger.Msg("Sloptifine closed drum MIDI devices for this session; restart after turning the option off to restore them.");
            }
            catch (Exception ex)
            {
                midiShutdownPointer = midiPointer;
                MelonLogger.Warning($"Sloptifine could not close drum MIDI devices; leaving the manager enabled: {ex.Message}");
            }
        }
        if (midi != null)
        {
            bool shouldEnable = midiOriginalEnabled && !stopMidiDiscovery && !midiDisabledForSession;
            if (midi.enabled != shouldEnable) midi.enabled = shouldEnable;
        }
        if (!stopMidiDiscovery && !disableDrumMidiForSession && !midiDisabledForSession)
        { midi = null; midiPointer = midiShutdownPointer = IntPtr.Zero; }

        var currentDiscord = disableRichPresence || discordSuppressedForSession
            ? DiscordController.instance : null;
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
            if (discord.enabled != shouldEnable) discord.enabled = shouldEnable;
        }
    }

    public void Restore()
    {
        if (!midiDisabledForSession)
            try { if (midi != null) midi.enabled = midiOriginalEnabled; } catch { }
        if (!discordSuppressedForSession)
            try { if (discord != null) discord.enabled = discordOriginalEnabled; } catch { }
        midi = null; discord = null; midiPointer = discordPointer = IntPtr.Zero;
        nextDiscovery = 0;
        pauseMidiDiscovery = disableMidiForSession = disableDiscord = false;
    }
}
