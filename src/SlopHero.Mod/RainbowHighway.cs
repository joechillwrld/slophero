using Il2Cpp;
using MelonLoader;
using UnityEngine;
using FlameAnimator = Il2Cpp.Animator;

namespace SlopHero.Mod;

/// <summary>Optional, local guitar colors. Reuses the existing pooled sprites.</summary>
internal sealed class RainbowHighway
{
    private readonly record struct SpriteState(SpriteRenderer Renderer, Color Original, float Phase);
    private readonly record struct FlameState(FlameAnimator Animator, SpriteRenderer Renderer,
        Color Original, Color Default, Color Alternate, float Phase);

    private readonly List<SpriteState> notes = new();
    private readonly List<FlameState> flames = new();
    private double nextPass, nextDiscovery;
    private bool notesFailed, flamesFailed;
    internal bool HasOverrides => notes.Count != 0 || flames.Count != 0;

    internal void Update(bool colorNotes, bool colorFlames, GameManager? game, double now)
    {
        if (!colorNotes) RestoreNotes();
        if (!colorFlames) RestoreFlames();
        if (!colorNotes && !colorFlames) return;
        if (game == null) { Clear(); return; }

        try
        {
            if (now >= nextDiscovery)
            {
                nextDiscovery = now + 1;
                if (colorNotes && !notesFailed && notes.Count == 0) DiscoverNotes(game);
                if (colorFlames && !flamesFailed && flames.Count == 0) DiscoverFlames(game);
            }
            if (now < nextPass) return;
            nextPass = now + .05;
            float cycle = (float)(now * .24 % 1.0);
            if (colorNotes && !notesFailed)
                foreach (var state in notes)
                    if (state.Renderer != null && state.Renderer.enabled && state.Renderer.sprite != null)
                        SetRgb(state.Renderer, Rainbow(cycle + state.Phase));
            if (colorFlames && !flamesFailed)
                foreach (var state in flames)
                {
                    if (state.Animator == null || state.Renderer == null) continue;
                    var color = Rainbow(cycle + state.Phase);
                    // A new hit can reset the sprite from these two fields.
                    // Update both the source colors and the active sprite.
                    color.a = state.Default.a;
                    state.Animator.defaultColor = color;
                    color.a = state.Alternate.a;
                    state.Animator.alternateColor = color;
                    if (state.Renderer.enabled && state.Renderer.sprite != null)
                        SetRgb(state.Renderer, color);
                }
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Rainbow highway colors stopped: {ex.Message}");
            Clear();
            if (colorNotes) notesFailed = true;
            if (colorFlames) flamesFailed = true;
        }
    }

    private void DiscoverNotes(GameManager game)
    {
        if (game.playerObjects == null) return;
        int index = 0;
        // This game stripped several Component.GetComponentInChildren<T>
        // specializations. Object.FindObjectsOfType<T> is already used for
        // Gameplay components elsewhere in this mod and runs just once here.
        foreach (var container in UnityEngine.Object.FindObjectsOfType<GuitarNoteContainer>())
        {
            if (container == null) continue;
            float phase = (index++ % 12) / 12f;
            Remember(notes, container.Head, phase);
            Remember(notes, container.Body, phase);
            Remember(notes, container.Anim, phase);
            Remember(notes, container.Open_HOPO, phase);
        }
        if (notes.Count != 0)
            MelonLogger.Msg($"Rainbow notes found {notes.Count} pooled note sprites.");
    }

    private void DiscoverFlames(GameManager game)
    {
        if (game.playerObjects == null) return;
        int index = 0;
        foreach (var player in game.playerObjects)
        {
            if (player == null || player.TryCast<GuitarPlayer>() == null) continue;
            var animators = player.neckController?.FlameAnimators;
            if (animators == null) continue;
            foreach (var animator in animators)
            {
                if (animator == null) continue;
                var renderer = animator.GetComponent<SpriteRenderer>();
                if (renderer == null) continue;
                flames.Add(new FlameState(animator, renderer, renderer.color,
                    animator.defaultColor, animator.alternateColor, (index++ % 5) / 5f));
            }
        }
        if (flames.Count != 0)
            MelonLogger.Msg($"Rainbow flames found {flames.Count} guitar flame sprites.");
    }

    private static void Remember(List<SpriteState> destination, SpriteRenderer? renderer, float phase)
    {
        if (renderer != null) destination.Add(new SpriteState(renderer, renderer.color, phase));
    }

    private static Color Rainbow(float hue)
    {
        hue -= MathF.Floor(hue);
        return Color.HSVToRGB(hue, .95f, 1f);
    }

    private static void SetRgb(SpriteRenderer renderer, Color color)
    {
        var current = renderer.color;
        if (current.r == color.r && current.g == color.g && current.b == color.b) return;
        color.a = current.a;
        renderer.color = color;
    }

    private void RestoreNotes()
    {
        if (notes.Count == 0) { notesFailed = false; return; }
        foreach (var state in notes)
            try { if (state.Renderer != null) SetRgb(state.Renderer, state.Original); }
            catch { }
        notes.Clear();
        notesFailed = false;
        nextDiscovery = 0;
    }

    private void RestoreFlames()
    {
        if (flames.Count == 0) { flamesFailed = false; return; }
        foreach (var state in flames)
            try
            {
                if (state.Animator != null)
                {
                    state.Animator.defaultColor = state.Default;
                    state.Animator.alternateColor = state.Alternate;
                }
                if (state.Renderer != null) SetRgb(state.Renderer, state.Original);
            }
            catch { }
        flames.Clear();
        flamesFailed = false;
        nextDiscovery = 0;
    }

    internal void Clear()
    {
        RestoreNotes();
        RestoreFlames();
        nextPass = nextDiscovery = 0;
    }
}
