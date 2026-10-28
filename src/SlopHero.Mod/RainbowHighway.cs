using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using FlameAnimator = Il2Cpp.Animator;

namespace SlopHero.Mod;

/// <summary>Optional guitar colors; the setter hook exists only while a rainbow option is on.</summary>
internal sealed class RainbowHighway
{
    private sealed class SpriteState
    {
        internal readonly SpriteRenderer Renderer;
        internal readonly int Phase;
        internal Color Original;
        internal SpriteState(SpriteRenderer renderer, int phase)
        { Renderer = renderer; Phase = phase; Original = renderer.color; }
    }

    private sealed class FlameState
    {
        internal readonly FlameAnimator Animator;
        internal readonly SpriteRenderer Renderer;
        internal readonly Color Default, Alternate, OriginalRenderer;
        internal readonly int Phase;
        internal FlameState(FlameAnimator animator, SpriteRenderer renderer, Color defaultColor,
            Color alternateColor, Color originalRenderer, int phase)
        { Animator = animator; Renderer = renderer; Default = defaultColor; Alternate = alternateColor; OriginalRenderer = originalRenderer; Phase = phase; }
    }

    private static RainbowHighway? active;
    private readonly Dictionary<IntPtr, SpriteState> notes = new();
    private readonly Dictionary<IntPtr, FlameState> flamesByAnimator = new();
    private readonly List<FlameState> flames = new();
    private readonly Color[] notePalette = new Color[12];
    private readonly Color[] flamePalette = new Color[5];
    private MethodInfo? noteColorSetter, flameAnimatorUpdate;
    private HarmonyLib.Harmony? noteHarmony, flameHarmony;
    private bool noteHookInstalled, flameUpdateHookInstalled;
    private bool noteHookFailed, flameHookFailed, applyingOwnColor, notesDiscovered;
    private bool notesFailed, flamesFailed;
    private double nextPass, nextNotesDiscovery, nextFlamesDiscovery;

    internal bool HasOverrides => notes.Count != 0 || flames.Count != 0
        || noteHookInstalled || flameUpdateHookInstalled;

    internal void Prepare(HarmonyLib.Harmony harmony, bool colorNotes, bool colorFlames)
    {
        if (colorNotes && notes.Count != 0 && !noteHookFailed && !noteHookInstalled)
            try { InstallNoteColorHook(harmony); }
        catch (Exception ex)
        {
            noteHookFailed = true;
            MelonLogger.Warning($"Rainbow note color hook could not install; periodic tinting will remain as fallback: {ex.Message}");
        }
        if (colorFlames && flames.Count != 0 && !flameHookFailed && !flameUpdateHookInstalled)
            try { InstallFlameUpdateHook(harmony); }
            catch (Exception ex)
            {
                flameHookFailed = true;
                MelonLogger.Warning($"Rainbow flame update hook could not install; periodic tinting will remain as fallback: {ex.Message}");
            }
    }

    internal void Update(HarmonyLib.Harmony harmony, bool colorNotes, bool colorFlames, GameManager? game, double now)
    {
        if (!colorNotes) RestoreNotes();
        if (!colorFlames) RestoreFlames();
        if (!colorNotes && !colorFlames) return;
        if (game == null) { Clear(); return; }

        bool animate = now >= nextPass;
        float cycle = 0;
        if (animate)
        {
            nextPass = now + .05;
            // Slower hue drift makes the note colors less frantic while
            // retaining a smooth 20 Hz palette update.
            cycle = (float)(now * .12 % 1.0);
            if (colorNotes && !notesFailed)
                for (int i = 0; i < notePalette.Length; i++)
                    notePalette[i] = Rainbow(cycle + i / (float)notePalette.Length, 1f);
            if (colorFlames && !flamesFailed)
                for (int i = 0; i < flamePalette.Length; i++)
                    flamePalette[i] = Rainbow(cycle + i / (float)flamePalette.Length, .4f);
        }

        if (colorNotes && !notesFailed && now >= nextNotesDiscovery)
        {
            // Search the local guitar pool at 1 Hz, including inactive note
            // containers. Note renderers can be created after the first scan;
            // keep admitting new pooled slots without the old 5 Hz scene-wide
            // active-object search.
            nextNotesDiscovery = now + 1.0;
            try { DiscoverNotes(game); }
            catch (Exception ex)
            {
                RestoreNotes();
                notesFailed = true;
                MelonLogger.Warning($"Rainbow note discovery stopped: {ex.Message}");
            }
        }
        if (colorFlames && !flamesFailed && flames.Count == 0 && now >= nextFlamesDiscovery)
        {
            nextFlamesDiscovery = now + 1.0;
            try { DiscoverFlames(game); }
            catch (Exception ex)
            {
                RestoreFlames();
                flamesFailed = true;
                MelonLogger.Warning($"Rainbow flame discovery stopped: {ex.Message}");
            }
        }
        // Avoid global setter/update hooks until the actual target objects are
        // known. The flame postfix reapplies the tint after the game's own
        // Animator update, which was overwriting the previous periodic color.
        Prepare(harmony, colorNotes && notes.Count != 0, colorFlames && flames.Count != 0);
        if (!animate) return;

        if (colorNotes && !notesFailed)
        {
            try
            {
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
                    var color = flamePalette[state.Phase];
                    color.a = state.Default.a;
                    state.Animator.defaultColor = color;
                    color.a = state.Alternate.a;
                    state.Animator.alternateColor = color;
                    if (state.Renderer.enabled && state.Renderer.sprite != null)
                    {
                        applyingOwnColor = true;
                        SetRgb(state.Renderer, color);
                        applyingOwnColor = false;
                    }
                }
            }
            catch (Exception ex)
            {
                RestoreFlames();
                flamesFailed = true;
                MelonLogger.Warning($"Rainbow flames stopped: {ex.Message}");
            }
            finally { applyingOwnColor = false; }
        }
    }

    private void InstallNoteColorHook(HarmonyLib.Harmony harmony)
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
            try { harmony.Unpatch(noteColorSetter, HarmonyPatchType.All, harmony.Id); } catch { }
            DeactivateIfUnused();
            throw;
        }
    }

    private void InstallFlameUpdateHook(HarmonyLib.Harmony harmony)
    {
        flameAnimatorUpdate = typeof(FlameAnimator).GetMethod("Update",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException("Il2Cpp.Animator.Update");
        flameHarmony = harmony;
        active = this;
        try
        {
            harmony.Patch(flameAnimatorUpdate, postfix: new HarmonyMethod(typeof(RainbowHighway)
                .GetMethod(nameof(AfterFlameAnimatorUpdate), BindingFlags.Static | BindingFlags.NonPublic)!));
            flameUpdateHookInstalled = true;
        }
        catch
        {
            try { harmony.Unpatch(flameAnimatorUpdate, HarmonyPatchType.All, harmony.Id); } catch { }
            DeactivateIfUnused();
            throw;
        }
    }

    private static void BeforeNoteColor(SpriteRenderer __instance, ref Color value)
    {
        var owner = active;
        if (owner == null || owner.applyingOwnColor) return;
        var pointer = __instance.Pointer;
        if (!owner.notes.TryGetValue(pointer, out var note)) return;
        note.Original = value;
        var tint = owner.notePalette[note.Phase];
        value.r = tint.r; value.g = tint.g; value.b = tint.b;
    }

    private static void AfterFlameAnimatorUpdate(FlameAnimator __instance)
    {
        var owner = active;
        if (owner == null || !owner.flamesByAnimator.TryGetValue(__instance.Pointer, out var flame)) return;
        var renderer = flame.Renderer;
        if (renderer == null || !renderer.enabled || renderer.sprite == null) return;
        var color = owner.flamePalette[flame.Phase];
        owner.applyingOwnColor = true;
        try { SetRgb(renderer, color); }
        finally { owner.applyingOwnColor = false; }
    }

    private void DiscoverNotes(GameManager game)
    {
        if (game.playerObjects == null) return;
        int added = 0, containerIndex = 0;
        foreach (var player in game.playerObjects)
        {
            if (player == null || player.TryCast<GuitarPlayer>() == null) continue;
            try
            {
                // Search only the guitar's hierarchy; this includes inactive
                // pooled notes without allocating a scene-wide object list.
                foreach (var container in player.GetComponentsInChildren<GuitarNoteContainer>(true))
                {
                    if (container == null) continue;
                    int phase = containerIndex++ % notePalette.Length;
                    added += Remember(container.Head, phase);
                    added += Remember(container.Body, phase);
                    added += Remember(container.Anim, phase);
                    added += Remember(container.Open_HOPO, phase);
                }
            }
            catch { /* A stripped generic specialization falls back below. */ }
        }

        if (added == 0)
        {
            var gameplayScene = SceneManager.GetActiveScene();
            foreach (var container in Resources.FindObjectsOfTypeAll<GuitarNoteContainer>())
            {
                if (container == null || !container.gameObject.scene.IsValid()
                    || container.gameObject.scene != gameplayScene) continue;
                int phase = containerIndex++ % notePalette.Length;
                added += Remember(container.Head, phase);
                added += Remember(container.Body, phase);
                added += Remember(container.Anim, phase);
                added += Remember(container.Open_HOPO, phase);
            }
        }
        if (added != 0)
        {
            notesDiscovered = true;
            MelonLogger.Msg($"Rainbow notes tracking {notes.Count} pooled sprites, including inactive slots.");
        }
    }

    private int Remember(SpriteRenderer? renderer, int phase)
    {
        if (renderer == null || notes.ContainsKey(renderer.Pointer)) return 0;
        notes.Add(renderer.Pointer, new SpriteState(renderer, phase));
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
                // Animator stores the renderer it actually updates. This also
                // handles skins that move the sprite away from the component.
                var renderer = animator.field_Private_SpriteRenderer_0;
                if (renderer == null) continue;
                int phase = index++ % flamePalette.Length;
                var state = new FlameState(animator, renderer, animator.defaultColor,
                    animator.alternateColor, renderer.color, phase);
                flames.Add(state);
                flamesByAnimator[animator.Pointer] = state;
            }
        }
        if (flames.Count != 0)
            MelonLogger.Msg($"Rainbow flames tracking {flamesByAnimator.Count} animator-owned sprite renderer(s).");
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
        if (notes.Count == 0 && !notesDiscovered && !notesFailed && !noteHookInstalled)
        { noteHookFailed = false; return; }
        applyingOwnColor = true;
        foreach (var state in notes.Values)
            try { if (state.Renderer != null) SetRgb(state.Renderer, state.Original); }
            catch { }
        applyingOwnColor = false;
        notes.Clear();
        notesDiscovered = false;
        notesFailed = false;
        noteHookFailed = false;
        nextNotesDiscovery = 0;
        RemoveColorHookIfUnused();
    }

    private void RestoreFlames()
    {
        if (flames.Count == 0 && !flamesFailed && !flameUpdateHookInstalled)
        { flameHookFailed = false; return; }
        applyingOwnColor = true;
        foreach (var state in flames)
            try
            {
                if (state.Animator != null)
                {
                    state.Animator.defaultColor = state.Default;
                    state.Animator.alternateColor = state.Alternate;
                }
                if (state.Renderer != null) SetRgb(state.Renderer, state.OriginalRenderer);
            }
            catch { }
        applyingOwnColor = false;
        flames.Clear();
        flamesByAnimator.Clear();
        flamesFailed = false;
        flameHookFailed = false;
        nextFlamesDiscovery = 0;
        RemoveFlameUpdateHookIfUnused();
    }

    private void RemoveColorHookIfUnused()
    {
        if (notes.Count != 0 || !noteHookInstalled) return;
        if (noteColorSetter != null && noteHarmony != null)
        {
            try { noteHarmony.Unpatch(noteColorSetter, HarmonyPatchType.All, noteHarmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"Rainbow color hook cleanup failed; its inactive prefix passes through: {ex.Message}"); }
        }
        noteHookInstalled = false;
        noteColorSetter = null;
        noteHarmony = null;
        noteHookFailed = false;
        DeactivateIfUnused();
    }

    private void RemoveFlameUpdateHookIfUnused()
    {
        if (flames.Count != 0 || !flameUpdateHookInstalled) return;
        if (flameAnimatorUpdate != null && flameHarmony != null)
        {
            try { flameHarmony.Unpatch(flameAnimatorUpdate, HarmonyPatchType.All, flameHarmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"Rainbow flame update hook cleanup failed; its inactive postfix passes through: {ex.Message}"); }
        }
        flameUpdateHookInstalled = false;
        flameAnimatorUpdate = null;
        flameHarmony = null;
        flameHookFailed = false;
        DeactivateIfUnused();
    }

    private void DeactivateIfUnused()
    {
        if (!noteHookInstalled && !flameUpdateHookInstalled && ReferenceEquals(active, this)) active = null;
    }

    internal void Clear()
    {
        RestoreNotes();
        RestoreFlames();
        noteHookFailed = flameHookFailed = false;
        nextPass = nextNotesDiscovery = nextFlamesDiscovery = 0;
    }
}
