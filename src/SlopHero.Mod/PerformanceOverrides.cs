using Il2Cpp;
using UnityEngine;

namespace SlopHero.Mod;

// Reversibly disables optional gameplay effects selected by the user.
// It never deactivates player, note, scoring, or input objects.
internal sealed class PerformanceOverrides
{
    private sealed class NeckEffects
    {
        public Behaviour[] FretAnimators = Array.Empty<Behaviour>();
        public ParticleSystem[] Particles = Array.Empty<ParticleSystem>();
    }

    private readonly Dictionary<IntPtr, (Behaviour Component, bool Original)> disabled = new();
    private readonly Dictionary<IntPtr, (GameObject Object, bool Original)> disabledParticleObjects = new();
    private readonly Dictionary<IntPtr, NeckEffects> necks = new();
    private readonly Dictionary<IntPtr, ParticleSystem[]> starEffects = new();
    private StarProgress[]? starProgressObjects;
    private bool starInventoryLogged;

    public void Clear()
    {
        RestoreAll();
        necks.Clear();
        starEffects.Clear();
        starProgressObjects = null;
        starInventoryLogged = false;
    }

    public void Apply(GameManager game, bool enabled, bool fretEffects, bool neckParticles, bool starParticles)
    {
        starProgressObjects ??= UnityEngine.Object.FindObjectsOfType<StarProgress>().ToArray();
        LogStarInventory();
        if (!enabled)
        {
            RestoreAll();
            return;
        }

        if (game.playerObjects != null)
        foreach (var player in game.playerObjects)
        {
            if (player == null || !player.enabled || player.TryCast<GuitarPlayer>() == null || player.neckController == null)
                continue;

            var neck = player.neckController.TryCast<GuitarNeckController>();
            if (neck == null) continue;
            if (!necks.TryGetValue(neck.Pointer, out var effects))
            {
                var animators = new List<Behaviour>();
                Add(animators, neck.sparks);
                Add(animators, neck.sparksOverlay);
                Add(animators, neck.SPSparks);
                effects = new NeckEffects
                {
                    FretAnimators = animators.ToArray(),
                    Particles = neck.gameObject.GetComponentsInChildren<ParticleSystem>(true).ToArray()
                };
                necks.Add(neck.Pointer, effects);
            }

            foreach (var animator in effects.FretAnimators) SetDisabled(animator, fretEffects);
            foreach (var particle in effects.Particles) SetParticleObjectDisabled(particle, neckParticles);
        }

        foreach (var star in starProgressObjects)
        {
            if (star == null) continue;
            if (!starEffects.TryGetValue(star.Pointer, out var particles))
            {
                particles = star.gameObject.GetComponentsInChildren<ParticleSystem>(true).ToArray();
                starEffects.Add(star.Pointer, particles);
            }
            foreach (var particle in particles) SetParticleObjectDisabled(particle, starParticles);
        }
    }

    private void LogStarInventory()
    {
        if (starInventoryLogged || starProgressObjects == null) return;
        starInventoryLogged = true;
        try
        {
            var details = starProgressObjects.Where(star => star != null).SelectMany(star =>
                star.gameObject.GetComponentsInChildren<SpriteRenderer>(true).Select(renderer =>
                {
                    string sprite = renderer.sprite == null ? "none" : renderer.sprite.name;
                    return $"{renderer.name}[{sprite}]";
                }));
            MelonLoader.MelonLogger.Msg("Star HUD renderer inventory: " + string.Join("; ", details));
        }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning($"Star HUD renderer inventory unavailable: {ex.Message}"); }
    }

    private static void Add(List<Behaviour> target, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2Cpp.Animator>? source)
    {
        if (source == null) return;
        foreach (var animator in source) if (animator != null) target.Add(animator);
    }

    private void SetDisabled(Behaviour? component, bool shouldDisable)
    {
        if (component == null) return;
        if (shouldDisable)
        {
            if (!disabled.ContainsKey(component.Pointer)) disabled.Add(component.Pointer, (component, component.enabled));
            if (component.enabled) component.enabled = false;
        }
        else if (disabled.Remove(component.Pointer, out var state) && state.Component != null)
            state.Component.enabled = state.Original;
    }

    private void SetParticleObjectDisabled(ParticleSystem? particle, bool shouldDisable)
    {
        if (particle == null) return;
        var gameObject = particle.gameObject;
        if (gameObject == null) return;
        if (shouldDisable)
        {
            if (!disabledParticleObjects.ContainsKey(gameObject.Pointer))
                disabledParticleObjects.Add(gameObject.Pointer, (gameObject, gameObject.activeSelf));
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
        else if (disabledParticleObjects.Remove(gameObject.Pointer, out var state) && state.Object != null)
            state.Object.SetActive(state.Original);
    }

    private void RestoreAll()
    {
        foreach (var state in disabled.Values)
            if (state.Component != null) state.Component.enabled = state.Original;
        disabled.Clear();
        foreach (var state in disabledParticleObjects.Values)
            if (state.Object != null) state.Object.SetActive(state.Original);
        disabledParticleObjects.Clear();
    }
}
