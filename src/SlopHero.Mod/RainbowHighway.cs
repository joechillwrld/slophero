using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using FlameAnimator = Il2Cpp.Animator;

namespace SlopHero.Mod;

/// <summary>Optional guitar colors; the setter hook exists only while a rainbow option is on.</summary>
internal sealed class RainbowHighway
{
    private sealed class NoteGroup
    {
        internal readonly GameObject Object;
        internal readonly List<SpriteState> Sprites = new(4);
        internal int Phase;
        internal NoteGroup(GameObject gameObject, int phase) { Object = gameObject; Phase = phase; }
    }

    private sealed class SpriteState
    {
        internal readonly SpriteRenderer Renderer;
        internal readonly NoteGroup Group;
        internal readonly bool IsBody;
        internal Color Original, Displayed;
        internal SpriteState(SpriteRenderer renderer, NoteGroup group, bool isBody)
        { Renderer = renderer; Group = group; IsBody = isBody; Original = Displayed = renderer.color; }
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
    private readonly List<NoteGroup> noteGroups = new();
    private readonly Dictionary<IntPtr, FlameState> flamesByAnimator = new();
    private readonly Dictionary<IntPtr, int> flamePhases = new();
    private readonly ClonZonesFlameBridge clonZonesFlames = new();
    private readonly List<FlameState> flames = new();
    private readonly Color[] notePalette = new Color[12];
    private readonly Color[] flamePalette = new Color[5];
    private MethodInfo? noteColorSetter, flameAnimatorUpdate;
    private HarmonyLib.Harmony? noteHarmony, flameHarmony;
    private bool noteHookInstalled, flameUpdateHookInstalled;
    private bool noteHookFailed, flameHookFailed, applyingOwnColor, notesDiscovered, notesActive;
    private bool notesFailed, flamesFailed;
    private double nextPass, nextNotesDiscovery, nextFlamesDiscovery;

    internal bool HasOverrides => notesActive || flames.Count != 0 || clonZonesFlames.IsActive
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
        if (colorFlames && flames.Count != 0 && clonZonesFlames.TryEnable(harmony)) return;
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
        else if (!notesActive)
        {
            // Game animation may have changed pooled note colors while this
            // option was off; capture those current vanilla values on re-entry.
            try
            {
                foreach (var state in notes.Values)
                    if (state.Renderer != null)
                        state.Original = state.Displayed = state.Renderer.color;
                notesActive = true;
            }
            catch (Exception ex)
            {
                notesFailed = true;
                MelonLogger.Warning($"Rainbow notes could not refresh saved colors: {ex.Message}");
            }
        }
        if (!colorFlames) RestoreFlames();
        if (!colorNotes && !colorFlames) return;
        if (game == null)
        {
            // A manager gap during an in-place restart is not a scene change.
            // Restore colors/hooks but keep the successful pooled-note scan;
            // actual scene load/unload callbacks call Clear() and forget it.
            RestoreNotes();
            RestoreFlames();
            return;
        }

        bool noteDiscoveryDue = colorNotes && !notesFailed && !notesDiscovered && now >= nextNotesDiscovery;
        bool flameDiscoveryDue = colorFlames && !flamesFailed && flames.Count == 0 && now >= nextFlamesDiscovery;
        // Initialize a usable palette on the same pass that binds the new
        // ClonZones pool, so the first hit cannot inherit an all-zero tint.
        bool animate = now >= nextPass || flameDiscoveryDue;
        // Palette updates run at 20 Hz. Skip the list, discovery, and hook
        // checks on all intervening simulation Updates.
        if (!animate && !noteDiscoveryDue && !flameDiscoveryDue) return;
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
                    flamePalette[i] = Rainbow(cycle + i / (float)flamePalette.Length,
                        clonZonesFlames.IsActive ? .7f : .4f);
        }

        if (colorNotes && !notesFailed && !notesDiscovered && now >= nextNotesDiscovery)
        {
            // Scan once per scene. All 2,000 pooled note renderers, including
            // inactive slots, are in this guitar hierarchy; calling this
            // stripped IL2CPP generic a second time can fail at runtime.
            nextNotesDiscovery = now + 1.0;
            try
            {
                DiscoverNotes(game);
                if (notesDiscovered) nextNotesDiscovery = now + 5.0;
            }
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
                foreach (var group in noteGroups)
                {
                    // The 2,000 sprites belong to 500 pooled containers. One
                    // parent activity check avoids three redundant hierarchy
                    // walks for each inactive note on every tint pass.
                    if (!group.Object.activeInHierarchy) continue;
                    foreach (var state in group.Sprites)
                        if (state.Renderer != null && state.Renderer.enabled && state.Renderer.sprite != null)
                            SetNoteRgb(state, notePalette[group.Phase]);
                }
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
                if (clonZonesFlames.IsActive)
                    clonZonesFlames.Tint(flamePalette, flamePhases);
                else foreach (var state in flames)
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
        if (owner == null || !owner.notesActive || owner.applyingOwnColor) return;
        var pointer = __instance.Pointer;
        if (!owner.notes.TryGetValue(pointer, out var note)) return;
        var tint = owner.notePalette[note.Group.Phase];
        // The game's SetState reads the already-tinted RGB back just to
        // change alpha. Do not mistake that write for a new vanilla color.
        var displayed = note.Displayed;
        if (MathF.Abs(value.r - displayed.r) < .001f && MathF.Abs(value.g - displayed.g) < .001f
            && MathF.Abs(value.b - displayed.b) < .001f)
            note.Original.a = value.a;
        else
        {
            note.Original = value;
            if (note.IsBody && TryResolveLane(value, out int phase)) note.Group.Phase = phase;
            tint = owner.notePalette[note.Group.Phase];
        }
        value.r = tint.r; value.g = tint.g; value.b = tint.b;
        note.Displayed = value;
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
            // Search only the guitar's hierarchy; this includes inactive
            // pooled notes without allocating a scene-wide object list.
            foreach (var container in player.GetComponentsInChildren<GuitarNoteContainer>(true))
            {
                if (container == null) continue;
                // One shared phase follows the lane color written by the
                // game. Pool order is unrelated to lane and caused recycled
                // notes to jump unpredictably around the rainbow.
                var group = new NoteGroup(container.gameObject, (containerIndex++ % 5) * 2);
                if (container.Body != null && TryResolveLane(container.Body.color, out int lanePhase))
                    group.Phase = lanePhase;
                added += Remember(container.Head, group);
                added += Remember(container.Body, group, isBody: true);
                added += Remember(container.Anim, group);
                added += Remember(container.Open_HOPO, group);
                noteGroups.Add(group);
            }
        }

        // Do not fall back to Resources.FindObjectsOfTypeAll<T>. That generic
        // Unity entry point is stripped in this game build; on the second
        // one-second pass it throws even though the local guitar scan worked.
        if (added != 0)
        {
            notesDiscovered = true;
            MelonLogger.Msg($"Rainbow notes tracked {notes.Count} pooled sprites once for this scene, including inactive slots.");
        }
    }

    private int Remember(SpriteRenderer? renderer, NoteGroup group, bool isBody = false)
    {
        if (renderer == null || notes.ContainsKey(renderer.Pointer)) return 0;
        var state = new SpriteState(renderer, group, isBody);
        notes.Add(renderer.Pointer, state);
        group.Sprites.Add(state);
        return 1;
    }

    private static bool TryResolveLane(Color color, out int phase)
    {
        phase = 0;
        // The current game's five normal Body colors match the older game's
        // green/red/yellow/blue/orange palette. Ignore cyan Star Power and
        // white alpha-only updates; those retain the container's last lane.
        if (color.g > .75f && color.r < .25f && color.b < .3f) { phase = 0; return true; }
        if (color.r > .75f && color.g < .25f && color.b < .3f) { phase = 2; return true; }
        if (color.r > .75f && color.g > .75f && color.b < .3f) { phase = 4; return true; }
        if (color.b > .7f && color.r < .3f && color.g < .75f) { phase = 7; return true; }
        if (color.r > .75f && color.g > .3f && color.g < .75f && color.b < .3f)
        { phase = 9; return true; }
        return false;
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
                flamePhases[animator.Pointer] = phase;
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

    private static void SetNoteRgb(SpriteState state, Color color)
    {
        var current = state.Displayed;
        if (current.r == color.r && current.g == color.g && current.b == color.b) return;
        color.a = current.a;
        state.Renderer.color = color;
        state.Displayed = color;
    }

    private void RestoreNotes(bool forgetSceneCache = false)
    {
        if (!notesActive && !notesFailed && !noteHookInstalled)
        {
            if (forgetSceneCache)
            {
                notes.Clear();
                noteGroups.Clear();
                notesDiscovered = false;
                nextNotesDiscovery = 0;
            }
            noteHookFailed = false;
            return;
        }
        notesActive = false;
        applyingOwnColor = true;
        foreach (var state in notes.Values)
            try
            {
                if (state.Renderer == null) continue;
                SetRgb(state.Renderer, state.Original);
                state.Displayed = state.Original;
            }
            catch { }
        applyingOwnColor = false;
        notesFailed = false;
        noteHookFailed = false;
        RemoveColorHookIfUnused();
        if (forgetSceneCache)
        {
            notes.Clear();
            noteGroups.Clear();
            notesDiscovered = false;
            nextNotesDiscovery = 0;
        }
    }

    private void RestoreFlames()
    {
        if (flames.Count == 0 && !flamesFailed && !flameUpdateHookInstalled && !clonZonesFlames.IsActive)
        { flameHookFailed = false; return; }
        clonZonesFlames.Restore();
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
        flamePhases.Clear();
        flamesFailed = false;
        flameHookFailed = false;
        nextFlamesDiscovery = 0;
        RemoveFlameUpdateHookIfUnused();
    }

    private void RemoveColorHookIfUnused()
    {
        if (notesActive || !noteHookInstalled) return;
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
        RestoreNotes(forgetSceneCache: true);
        RestoreFlames();
        notesActive = false;
        noteHookFailed = flameHookFailed = false;
        nextPass = nextNotesDiscovery = nextFlamesDiscovery = 0;
    }
}
