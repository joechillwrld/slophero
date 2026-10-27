using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using FlameAnimator = Il2Cpp.Animator;

namespace SlopHero.Mod;

/// <summary>Optional guitar colors; the extra setter hook exists only while rainbow notes are on.</summary>
internal sealed class RainbowHighway
{
    private sealed class NoteState
    {
        internal readonly SpriteRenderer Renderer;
        internal readonly int Phase;
        internal Color Original;
        internal NoteState(SpriteRenderer renderer, int phase)
        { Renderer = renderer; Phase = phase; Original = renderer.color; }
    }

    private readonly record struct FlameState(FlameAnimator Animator, SpriteRenderer Renderer,
        Color Original, Color Default, Color Alternate, int Phase);

    private static RainbowHighway? active;
    private readonly Dictionary<IntPtr, NoteState> notes = new();
    private readonly List<FlameState> flames = new();
    private readonly Color[] notePalette = new Color[12];
    private MethodInfo? noteColorSetter;
    private HarmonyLib.Harmony? noteHarmony;
    private bool noteHookInstalled, applyingOwnColor, notesDiscovered;
    private bool notesFailed, flamesFailed;
    private double nextPass, nextDiscovery;

    internal bool HasOverrides => notes.Count != 0 || flames.Count != 0 || noteHookInstalled;

    internal void Prepare(HarmonyLib.Harmony harmony, bool requested)
    {
        if (!requested || notesFailed || noteHookInstalled) return;
        try { InstallNoteHook(harmony); }
        catch (Exception ex)
        {
            RestoreNotes();
            notesFailed = true;
            MelonLogger.Warning($"Rainbow notes could not hook the game color output: {ex.Message}");
        }
    }

    internal void Update(HarmonyLib.Harmony harmony, bool colorNotes, bool colorFlames, GameManager? game, double now)
    {
        if (!colorNotes) RestoreNotes();
        if (!colorFlames) RestoreFlames();
        if (!colorNotes && !colorFlames) return;
        if (game == null) { Clear(); return; }

        float cycle = (float)(now * .24 % 1.0);
        if (colorNotes && !notesFailed)
            for (int i = 0; i < notePalette.Length; i++)
                notePalette[i] = Rainbow(cycle + i / (float)notePalette.Length, 1f);

        if (colorNotes) Prepare(harmony, requested: true);
        if (now >= nextDiscovery)
        {
            nextDiscovery = now + .2;
            if (colorNotes && !notesFailed)
                try { DiscoverNotes(game); }
                catch (Exception ex)
                {
                    RestoreNotes();
                    notesFailed = true;
                    MelonLogger.Warning($"Rainbow note discovery stopped: {ex.Message}");
                }
            if (colorFlames && !flamesFailed && flames.Count == 0)
                try { DiscoverFlames(game); }
                catch (Exception ex)
                {
                    RestoreFlames();
                    flamesFailed = true;
                    MelonLogger.Warning($"Rainbow flame discovery stopped: {ex.Message}");
                }
        }
        if (now < nextPass) return;
        nextPass = now + .05;

        if (colorNotes && !notesFailed)
        {
            try
            {
                // Incoming notes fade, so the game can rewrite their RGB
                // before this 20 Hz pass. The setter prefix below changes
                // those existing writes; this pass animates unchanged notes.
                applyingOwnColor = true;
                foreach (var state in notes.Values)
                    if (state.Renderer != null && state.Renderer.enabled && state.Renderer.sprite != null)
                        SetRgb(state.Renderer, notePalette[state.Phase]);
            }
            catch (Exception ex)
            {
                RestoreNotes();
                notesFailed = true;
                MelonLogger.Warning($"Rainbow notes stopped: {ex.Message}");
            }
            finally { applyingOwnColor = false; }
        }

        if (colorFlames && !flamesFailed)
        {
            try
            {
                foreach (var state in flames)
                {
                    if (state.Animator == null || state.Renderer == null) continue;
                    // Flame sprites blend additively. Full brightness washes
                    // to white, so use saturated color at lower intensity.
                    var color = Rainbow(cycle + state.Phase / 5f, .4f);
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
                RestoreFlames();
                flamesFailed = true;
                MelonLogger.Warning($"Rainbow flames stopped: {ex.Message}");
            }
        }
    }

    private void InstallNoteHook(HarmonyLib.Harmony harmony)
    {
        noteColorSetter = typeof(SpriteRenderer).GetProperty("color", BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod()
            ?? throw new MissingMethodException("UnityEngine.SpriteRenderer.color setter");
        noteHarmony = harmony;
        active = this;
        try
        {
            harmony.Patch(noteColorSetter, prefix: new HarmonyMethod(typeof(RainbowHighway)
                .GetMethod(nameof(BeforeNoteColor), BindingFlags.Static | BindingFlags.NonPublic)!));
            noteHookInstalled = true;
        }
        catch
        {
            active = null;
            try { harmony.Unpatch(noteColorSetter, HarmonyPatchType.All, harmony.Id); } catch { }
            noteHarmony = null;
            throw;
        }
    }

    private static void BeforeNoteColor(SpriteRenderer __instance, ref Color value)
    {
        var owner = active;
        if (owner == null || owner.applyingOwnColor
            || !owner.notes.TryGetValue(__instance.Pointer, out var state)) return;
        state.Original = value;
        var tint = owner.notePalette[state.Phase];
        value.r = tint.r;
        value.g = tint.g;
        value.b = tint.b;
    }

    private void DiscoverNotes(GameManager game)
    {
        if (game.playerObjects == null) return;
        int added = 0;
        // FindObjectsOfType returns active slots. Repeat at 5 Hz so notes
        // reused later in the pool are included without a per-frame scan.
        foreach (var container in UnityEngine.Object.FindObjectsOfType<GuitarNoteContainer>())
        {
            if (container == null) continue;
            int phase = (notes.Count / 4) % notePalette.Length;
            added += Remember(container.Head, phase);
            added += Remember(container.Body, phase);
            added += Remember(container.Anim, phase);
            added += Remember(container.Open_HOPO, phase);
        }
        if (!notesDiscovered && added != 0)
        {
            notesDiscovered = true;
            MelonLogger.Msg($"Rainbow notes tracking {notes.Count} active-pool sprites; new slots join during play.");
        }
    }

    private int Remember(SpriteRenderer? renderer, int phase)
    {
        if (renderer == null || notes.ContainsKey(renderer.Pointer)) return 0;
        notes.Add(renderer.Pointer, new NoteState(renderer, phase));
        return 1;
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
                    animator.defaultColor, animator.alternateColor, index++ % 5));
            }
        }
        if (flames.Count != 0)
            MelonLogger.Msg($"Rainbow flames found {flames.Count} guitar flame sprites.");
    }

    private static Color Rainbow(float hue, float brightness)
    {
        hue -= MathF.Floor(hue);
        return Color.HSVToRGB(hue, .95f, brightness);
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
        if (ReferenceEquals(active, this)) active = null;
        if (noteHookInstalled && noteColorSetter != null && noteHarmony != null)
        {
            try { noteHarmony.Unpatch(noteColorSetter, HarmonyPatchType.All, noteHarmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"Rainbow note hook cleanup failed; its inactive prefix passes through: {ex.Message}"); }
        }
        noteHookInstalled = false;
        noteColorSetter = null;
        noteHarmony = null;
        applyingOwnColor = false;
        foreach (var state in notes.Values)
            try { if (state.Renderer != null) SetRgb(state.Renderer, state.Original); }
            catch { }
        notes.Clear();
        notesDiscovered = false;
        notesFailed = false;
        nextDiscovery = 0;
    }

    private void RestoreFlames()
    {
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
    }

    internal void Clear()
    {
        RestoreNotes();
        RestoreFlames();
        nextPass = nextDiscovery = 0;
    }
}
