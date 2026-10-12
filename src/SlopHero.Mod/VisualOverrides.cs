using Il2Cpp;
using SlopHero.Core;
using UnityEngine;

namespace SlopHero.Mod;

// Own only renderer properties. Never deactivate gameplay objects or components.
internal sealed class VisualOverrides
{
    private readonly Dictionary<IntPtr, (SpriteRenderer Renderer, Color Original)> colors = new();
    private readonly Dictionary<IntPtr, (Renderer Renderer, bool Original)> hidden = new();
    private readonly Dictionary<IntPtr, ScrollSpeedCompensation> speeds = new();
    private readonly Dictionary<IntPtr, (Il2CppTMPro.TextMeshProUGUI Text, Color Original)> texts = new();
    private readonly Dictionary<IntPtr, SpriteRenderer[]> spriteChildren = new();
    private readonly Dictionary<IntPtr, Renderer[]> stringRenderers = new();
    private readonly Dictionary<IntPtr, GuitarNoteRenderer> noteRenderers = new();
    private SongProgress[]? songProgress;
    public void Clear() { colors.Clear(); hidden.Clear(); speeds.Clear(); texts.Clear(); spriteChildren.Clear(); stringRenderers.Clear(); noteRenderers.Clear(); songProgress = null; }
    private SpriteRenderer[] Sprites(GameObject go)
    {
        if (!spriteChildren.TryGetValue(go.Pointer, out var result))
            spriteChildren.Add(go.Pointer, result = go.GetComponentsInChildren<SpriteRenderer>(true).ToArray());
        return result;
    }
    private Renderer[] Strings(GameObject go)
    {
        if (!stringRenderers.TryGetValue(go.Pointer, out var result))
            stringRenderers.Add(go.Pointer, result = go.GetComponentsInChildren<Renderer>(true).ToArray());
        return result;
    }

    private void Hide(Renderer? r, bool enabled)
    {
        if (r == null) return;
        if (enabled)
        {
            if (!hidden.ContainsKey(r.Pointer)) hidden.Add(r.Pointer, (r, r.enabled));
            r.enabled = false;
        }
        else if (hidden.Remove(r.Pointer, out var old)) r.enabled = old.Original;
    }
    private void Tint(SpriteRenderer? r, bool enabled, Color color)
    {
        if (r == null) return;
        if (enabled)
        {
            if (!colors.ContainsKey(r.Pointer)) colors.Add(r.Pointer, (r, r.color));
            // Preserve alpha: fades and native visibility remain authoritative.
            color.a = r.color.a;
            r.color = color;
        }
        else if (colors.Remove(r.Pointer, out var old)) { var c = old.Original; c.a = r.color.a; r.color = c; }
    }
    private void TintText(Il2CppTMPro.TextMeshProUGUI? t, bool enabled, Color color)
    {
        if (t == null) return;
        if (enabled)
        {
            if (!texts.ContainsKey(t.Pointer)) texts.Add(t.Pointer, (t, t.color));
            color.a = t.color.a; t.color = color;
        }
        else if (texts.Remove(t.Pointer, out var old)) { var c = old.Original; c.a = t.color.a; t.color = c; }
    }
    private void TintChildren(Component? c, bool enabled, Color color)
    {
        if (c == null) return;
        foreach (var r in Sprites(c.gameObject)) Tint(r, enabled, color);
    }

    // Remember the unmodified value; repeated frames never divide an already
    // compensated speed. Changes made by vanilla settings replace the baseline.
    private float Speed(IntPtr pointer, float current, float ratio, bool enabled, float configuredSpeed)
    {
        if (!speeds.TryGetValue(pointer, out var state))
        {
            if (!enabled) return current;
            state = new ScrollSpeedCompensation();
            speeds.Add(pointer, state);
        }
        float value = state.Update(current, ratio, enabled, configuredSpeed);
        if (!enabled) speeds.Remove(pointer);
        return value;
    }

    public void Apply(GameManager game, bool sides, bool strings, bool beats,
        bool score, Color stars, Color progress, Color scoreText, Color songProgressColor, bool constantSpeed)
    {
        if (game.playerObjects == null) return;
        if (!sides && !strings && !beats && !score && !constantSpeed && hidden.Count == 0 && colors.Count == 0 && texts.Count == 0 && speeds.Count == 0) return;
        foreach (var p in game.playerObjects)
        {
            if (p == null || !p.enabled || p.TryCast<GuitarPlayer>() == null || p.engine == null) continue;
            Hide(p.trackSidebarLeft?.field_Private_SpriteRenderer_0, sides);
            Hide(p.trackSidebarRight?.field_Private_SpriteRenderer_0, sides);
            if (p.neckController?.fretStrings != null)
                foreach (var go in p.neckController.fretStrings)
                    if (go != null) foreach (var r in Strings(go)) Hide(r, strings);
            var b = p.beatRenderer;
            if (b?.field_Protected_Il2CppReferenceArray_1_SpriteRenderer_0 != null)
                foreach (var r in b.field_Protected_Il2CppReferenceArray_1_SpriteRenderer_0) Hide(r, beats);
            TintText(p.AccuracyText, score, scoreText);
            bool compensate = constantSpeed && game.globalVariables.isPracticeEnabled;
            float ratio = game.practiceSpeed;
            if (!constantSpeed && speeds.Count == 0) continue;
            ObjectPublicObBoObAcBo1ObBoObObObUnique? profile = p.player?.field_Public_Object1PublicObBoObStBoObObObObUnique_0;
            float configured = profile?.field_Public_Object2PublicBoSiInSiDoStSiStStUnique_0?.field_Protected_T_0 ?? float.NaN;
            if (!noteRenderers.TryGetValue(p.Pointer, out var notes))
            {
                notes = p.gameObject.GetComponentInChildren<GuitarNoteRenderer>(true);
                if (notes != null) noteRenderers.Add(p.Pointer, notes);
            }
            if (notes != null)
            {
                float speed = Speed(notes.Pointer, notes.noteSpeed, ratio, compensate, configured);
                if (Math.Abs(speed - notes.noteSpeed) > .0001f)
                {
                    notes.noteSpeed = speed;
                    // Same horizon expression as native BaseNoteRenderer.Start.
                    notes.field_Protected_Double_0 = (notes.noteZPosFarLimit - notes.strikeLine) / speed;
                }
            }
            if (b != null) b.field_Private_Single_0 = Speed(b.Pointer, b.field_Private_Single_0, ratio, compensate, configured);
            if (p.highwayScroll != null)
                p.highwayScroll.field_Private_Single_1 = Speed(p.highwayScroll.Pointer, p.highwayScroll.field_Private_Single_1, ratio, compensate, configured);
        }
        if (score || colors.Count > 0)
        {
            songProgress ??= UnityEngine.Object.FindObjectsOfType<SongProgress>().ToArray();
            foreach (var progressBar in songProgress)
            {
                if (progressBar == null) continue;
                TintChildren(progressBar.barTransform, score, songProgressColor);
                TintChildren(progressBar.endTransform, score, songProgressColor);
            }
        }
        var manager = game.scoreManager;
        if (manager == null) return;
        TintChildren(manager.scoreFont, score, scoreText);
        TintChildren(manager.comboFont, score, scoreText);
        var star = manager.starProgress;
        if (star != null)
        {
            Tint(star.starCount, score, stars);
            TintChildren(star.progressBar, score, progress);
            TintChildren(star.progressBarEnd, score, progress);
        }
    }
}
