using Il2Cpp;
using SlopHero.Core;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SlopHero.Mod;

// Own only renderer properties. Never deactivate gameplay objects or components.
internal sealed class VisualOverrides
{
    private readonly Dictionary<IntPtr, (SpriteRenderer Renderer, Color Original)> colors = new();
    private readonly Dictionary<IntPtr, (Renderer Renderer, bool Original)> hidden = new();
    private readonly Dictionary<IntPtr, ScrollSpeedCompensation> speeds = new();
    private readonly PracticeClockRate practiceClockRate = new();
    private readonly Dictionary<IntPtr, (Il2CppTMPro.TextMeshProUGUI Text, Color Original)> texts = new();
    private readonly Dictionary<IntPtr, SpriteRenderer[]> spriteChildren = new();
    private readonly Dictionary<IntPtr, SpriteRenderer[]> starFaceSprites = new();
    private readonly Dictionary<IntPtr, Renderer[]> stringRenderers = new();
    private readonly Dictionary<IntPtr, GuitarNoteRenderer> noteRenderers = new();
    private SongProgress[]? songProgress;
    private StarProgress[]? starProgresses;
    private double nextScoreColorRefresh;
    private double nextPlayerPass;
    private readonly Dictionary<IntPtr, (double Window, float Speed)> visibility = new();
    private bool oldSides, oldStrings, oldBeats, oldScore, oldPractice, oldConstantSpeed;
    private bool practiceClockSampling;
    private double nextPracticeClockSample;
    private readonly HashSet<IntPtr> hiddenPlayers = new();
    private bool fullSongSpeedSyncPending = true;
    public bool HasOverrides => hidden.Count != 0 || colors.Count != 0 || speeds.Count != 0 || texts.Count != 0;
    public bool NeedsUpdate => fullSongSpeedSyncPending || HasOverrides;
    public void Clear() { colors.Clear(); hidden.Clear(); speeds.Clear(); practiceClockRate.Reset(); texts.Clear(); spriteChildren.Clear(); starFaceSprites.Clear(); stringRenderers.Clear(); noteRenderers.Clear(); visibility.Clear(); hiddenPlayers.Clear(); oldSides = oldStrings = oldBeats = oldScore = oldPractice = oldConstantSpeed = false; practiceClockSampling = false; nextPracticeClockSample = 0; nextScoreColorRefresh = nextPlayerPass = 0; fullSongSpeedSyncPending = true; songProgress = null; starProgresses = null; }
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

    // Star fills can sit beneath the star graphic in the same hierarchy. Cache
    // the ancestry test once per scene instead of walking every transform each
    // frame while custom score colors are enabled.
    private SpriteRenderer[] StarFaces(StarProgress star)
    {
        var gameObject = star.gameObject;
        if (starFaceSprites.TryGetValue(gameObject.Pointer, out var result)) return result;
        var starCount = star.starCount?.Pointer ?? IntPtr.Zero;
        var progressBar = star.progressBar;
        var progressEnd = star.progressBarEnd;
        var faces = new List<SpriteRenderer>();
        foreach (var renderer in Sprites(gameObject))
        {
            if (renderer == null || renderer.Pointer == starCount) continue;
            if (Under(renderer.transform, progressBar) || Under(renderer.transform, progressEnd)) continue;
            faces.Add(renderer);
        }
        result = faces.ToArray();
        starFaceSprites.Add(gameObject.Pointer, result);
        return result;
    }

    private void Hide(Renderer? r, bool enabled)
    {
        if (r == null) return;
        if (enabled)
        {
            if (hidden.ContainsKey(r.Pointer)) return;
            hidden.Add(r.Pointer, (r, r.enabled));
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
            var current = r.color;
            if (current.r == color.r && current.g == color.g && current.b == color.b) return;
            color.a = current.a;
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
            var current = t.color;
            if (current.r == color.r && current.g == color.g && current.b == color.b) return;
            color.a = current.a; t.color = color;
        }
        else if (texts.Remove(t.Pointer, out var old)) { var c = old.Original; c.a = t.color.a; t.color = c; }
    }
    private void TintChildren(Component? c, bool enabled, Color color)
    {
        if (c == null) return;
        foreach (var r in Sprites(c.gameObject)) Tint(r, enabled, color);
    }

    private void TintProgress(Component? component, bool enabled, Color color, float fill)
    {
        if (component == null) return;
        foreach (var renderer in Sprites(component.gameObject))
        {
            if (renderer == null) continue;
            if (!enabled) { Tint(renderer, false, color); continue; }
            if (!colors.TryGetValue(renderer.Pointer, out var original))
                colors[renderer.Pointer] = original = (renderer, renderer.color);
            var target = Color.Lerp(original.Original, color, fill);
            target.a = renderer.color.a;
            if (renderer.color != target) renderer.color = target;
        }
    }

    // In this Clone Hero build StarProgress stores the current fill fraction at
    // 0x78. This offset is confirmed by the supplied v1.1.0.6142 native dump.
    private static float StarFill(StarProgress star)
    {
        try
        {
            float fill = Marshal.PtrToStructure<float>(IntPtr.Add(star.Pointer, 0x78));
            return float.IsFinite(fill) ? Math.Clamp(fill, 0, 1) : 0;
        }
        catch { return 0; }
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
        bool score, Color stars, Color progress, Color scoreText, Color songProgressColor, bool constantSpeed,
        double realtime, RuntimeTimings timings)
    {
        if (game.playerObjects == null) return;
        // Keep the restore pass alive until every renderer has its captured
        // color back. A one-frame oldScore edge can be missed by the throttled
        // refresh interval, leaving stale colors cached and waking this hook
        // on every frame until the scene reloads.
        bool restoreScore = oldScore || (!score && (colors.Count != 0 || texts.Count != 0));
        oldScore = score;
        bool scorePassNeeded = score || restoreScore;
        bool refreshScoreColors = scorePassNeeded && realtime >= nextScoreColorRefresh;
        // Score colors and star fill are cosmetic; 5 Hz limits traversal and
        // renderer writes while keeping the progress fill visually responsive.
        if (refreshScoreColors) nextScoreColorRefresh = realtime + .2;
        bool practiceNow = game.globalVariables != null && game.globalVariables.isPracticeEnabled;
        bool speedCompensationActive = constantSpeed && practiceNow;
        bool clockSampleActive = speedCompensationActive && game.isSongPlaying && !game.isPaused;
        float clockRate = float.NaN;
        if (clockSampleActive)
        {
            // The measured rate is aggregated over 200 ms. Sampling it at 20 Hz
            // is enough; reading the IL2CPP song clock at every unlocked frame
            // only adds work without improving the compensation estimate.
            if (!practiceClockSampling || realtime >= nextPracticeClockSample)
            {
                practiceClockSampling = true;
                nextPracticeClockSample = realtime + .05;
                clockRate = practiceClockRate.Update(game.songTimeVideoTime, realtime, true);
            }
            else clockRate = practiceClockRate.CurrentRate;
        }
        else if (practiceClockSampling)
        {
            practiceClockRate.Reset();
            practiceClockSampling = false;
            nextPracticeClockSample = 0;
        }
        bool practiceChanged = practiceNow != oldPractice;
        if (practiceNow && !oldPractice)
        {
            // Practice may reuse gameplay renderers after the player returns to
            // the song. Drop per-session compensation and visibility baselines
            // so the new practice session starts from its live native state.
            speeds.Clear();
            visibility.Clear();
        }
        if (!practiceNow && fullSongSpeedSyncPending)
            fullSongSpeedSyncPending = !SyncFullSongSpeed(game);
        if (!sides && !strings && !beats && !score && !constantSpeed && hidden.Count == 0 && colors.Count == 0 && texts.Count == 0 && speeds.Count == 0) return;
        bool playerPassNeeded = sides != oldSides || strings != oldStrings || beats != oldBeats
            || practiceChanged || constantSpeed != oldConstantSpeed || refreshScoreColors;
        if (playerPassNeeded || realtime >= nextPlayerPass)
        {
            nextPlayerPass = realtime + .1;
            long playerTiming = timings.Begin();
            try
            {
                foreach (var p in game.playerObjects)
                {
                    if (p == null || !p.enabled || p.TryCast<GuitarPlayer>() == null || p.engine == null) continue;
                    var b = p.beatRenderer;
                    bool newPlayer = !hiddenPlayers.Contains(p.Pointer);
                    if (newPlayer || sides != oldSides || strings != oldStrings || beats != oldBeats)
                    {
                        Hide(p.trackSidebarLeft?.field_Private_SpriteRenderer_0, sides);
                        Hide(p.trackSidebarRight?.field_Private_SpriteRenderer_0, sides);
                        var neck = p.neckController;
                        var fretStrings = neck?.fretStrings;
                        if (fretStrings != null && (strings || oldStrings))
                            foreach (var go in fretStrings)
                                if (go != null) foreach (var r in Strings(go)) Hide(r, strings);
                        var beatSprites = b?.field_Protected_Il2CppReferenceArray_1_SpriteRenderer_0;
                        if (beatSprites != null && (beats || oldBeats))
                            foreach (var r in beatSprites) Hide(r, beats);
                        if (b != null && beatSprites != null && neck != null && fretStrings != null) hiddenPlayers.Add(p.Pointer);
                    }
                    if (refreshScoreColors) TintText(p.AccuracyText, score, scoreText);
                    bool compensate = speedCompensationActive;
                    if (!compensate && speeds.Count == 0 && !practiceChanged) continue;
                    float ratio = clockRate;
                    float configured = ConfiguredNoteSpeed.Read(p.player?.Pointer ?? IntPtr.Zero);
                    compensate &= ConfiguredNoteSpeed.IsPlausible(configured);
                    if (!noteRenderers.TryGetValue(p.Pointer, out var notes))
                    {
                        notes = p.gameObject.GetComponentInChildren<GuitarNoteRenderer>(true);
                        if (notes != null) noteRenderers.Add(p.Pointer, notes);
                    }
                    if (notes != null)
                    {
                        if (!visibility.TryGetValue(notes.Pointer, out var native) || !double.IsFinite(native.Window) || native.Window <= 0 || native.Speed <= 0)
                        {
                            native = (notes.field_Protected_Double_0, notes.noteSpeed);
                            visibility[notes.Pointer] = native;
                        }
                        float speed = Speed(notes.Pointer, notes.noteSpeed, ratio, compensate, configured);
                        double window = compensate ? NoteVisibilityWindow.Calculate(native.Window, native.Speed, speed) : native.Window;
                        if (double.IsFinite(window) && window > 0 && float.IsFinite(speed) && speed > 0 && speed <= 10000)
                        {
                            if (Math.Abs(speed - notes.noteSpeed) > .0001f) notes.noteSpeed = speed;
                            // Never derive the spawn window from live camera clipping limits.
                            if (Math.Abs(window - notes.field_Protected_Double_0) > .0001) notes.field_Protected_Double_0 = window;
                        }
                        else if (compensate)
                        {
                            if (native.Speed > 0 && float.IsFinite(native.Speed)) notes.noteSpeed = native.Speed;
                            if (native.Window > 0 && double.IsFinite(native.Window)) notes.field_Protected_Double_0 = native.Window;
                        }
                    }
                    if (b != null) b.field_Private_Single_0 = Speed(b.Pointer, b.field_Private_Single_0, ratio, compensate, configured);
                    if (p.highwayScroll != null)
                        p.highwayScroll.field_Private_Single_1 = Speed(p.highwayScroll.Pointer, p.highwayScroll.field_Private_Single_1, ratio, compensate, configured);
                }
            }
            finally { timings.End(4, playerTiming); }
        }
        oldSides = sides; oldStrings = strings; oldBeats = beats;
        oldPractice = practiceNow; oldConstantSpeed = constantSpeed;
        if (!score && !restoreScore) return;
        if (!refreshScoreColors) return;
        long graphicsTiming = timings.Begin();
        try
        {
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
            if (manager != null)
            {
                TintChildren(manager.scoreFont, score, scoreText);
                TintChildren(manager.comboFont, score, scoreText);
            }
            starProgresses ??= UnityEngine.Object.FindObjectsOfType<StarProgress>().ToArray();
            foreach (var star in starProgresses)
            {
                if (star == null) continue;
                float fill = StarFill(star);
                foreach (var renderer in StarFaces(star)) Tint(renderer, score, stars);
                TintProgress(star.progressBar, score, progress, fill);
                TintProgress(star.progressBarEnd, score, progress, fill);
            }
        }
        finally { timings.End(5, graphicsTiming); }
    }

    // A scene reload can reuse renderer objects after their practice speed cache
    // has been cleared. Reconcile those fields once against the profile setting
    // before full-song play so a prior practice tempo cannot leak into the song.
    private bool SyncFullSongSpeed(GameManager game)
    {
        if (game.playerObjects == null) return false;
        bool foundPlayer = false;
        foreach (var player in game.playerObjects)
        {
            if (player == null || !player.enabled || player.TryCast<GuitarPlayer>() == null || player.engine == null) continue;
            float configured = ConfiguredNoteSpeed.Read(player.player?.Pointer ?? IntPtr.Zero);
            if (!ConfiguredNoteSpeed.IsPlausible(configured)) continue;
            foundPlayer = true;
            var notes = player.gameObject.GetComponentInChildren<GuitarNoteRenderer>(true);
            if (notes == null) continue;
            float noteBefore = notes.noteSpeed;
            double windowBefore = notes.field_Protected_Double_0;
            double windowTarget = NoteVisibilityWindow.Calculate(windowBefore, noteBefore, configured);
            bool changed = Math.Abs(noteBefore - configured) > .001f;
            if (changed) notes.noteSpeed = configured;
            if (double.IsFinite(windowTarget) && windowTarget > 0 && Math.Abs(windowTarget - windowBefore) > .0001)
                notes.field_Protected_Double_0 = windowTarget;
            var beat = player.beatRenderer;
            if (beat != null && Math.Abs(beat.field_Private_Single_0 - configured) > .001f) beat.field_Private_Single_0 = configured;
            var scroll = player.highwayScroll;
            if (scroll != null && Math.Abs(scroll.field_Private_Single_1 - configured) > .001f) scroll.field_Private_Single_1 = configured;
        }
        return foundPlayer;
    }

    private static bool Under(Transform? child, Transform? parent)
    {
        if (child == null || parent == null) return false;
        for (var current = child; current != null; current = current.parent)
            if (current.Pointer == parent.Pointer) return true;
        return false;
    }
}
