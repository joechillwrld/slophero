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
    private readonly PracticeClockRate practiceClockRate = new();
    private readonly Dictionary<IntPtr, (Il2CppTMPro.TextMeshProUGUI Text, Color Original)> texts = new();
    private readonly Dictionary<IntPtr, SpriteRenderer[]> spriteChildren = new();
    private readonly Dictionary<IntPtr, Renderer[]> stringRenderers = new();
    private readonly Dictionary<IntPtr, GuitarNoteRenderer> noteRenderers = new();
    private SongProgress[]? songProgress;
    private StarProgress[]? starProgresses;
    private readonly Dictionary<IntPtr, (double Window, float Speed)> visibility = new();
    private bool oldSides, oldStrings, oldBeats, oldScore, oldPractice;
    private readonly HashSet<IntPtr> hiddenPlayers = new();
    private readonly HashSet<IntPtr> speedLog = new();
    private readonly HashSet<IntPtr> speedTransitionLog = new();
    private bool fullSongSpeedSyncPending = true;
    public void Clear() { colors.Clear(); hidden.Clear(); speeds.Clear(); practiceClockRate.Reset(); texts.Clear(); spriteChildren.Clear(); stringRenderers.Clear(); noteRenderers.Clear(); visibility.Clear(); hiddenPlayers.Clear(); speedLog.Clear(); speedTransitionLog.Clear(); oldSides = oldStrings = oldBeats = oldScore = oldPractice = false; fullSongSpeedSyncPending = true; songProgress = null; starProgresses = null; }
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
        bool score, Color stars, Color progress, Color scoreText, Color songProgressColor, bool constantSpeed, double realtime)
    {
        if (game.playerObjects == null) return;
        bool practiceNow = game.globalVariables != null && game.globalVariables.isPracticeEnabled;
        float clockRate = practiceClockRate.Update(game.songTimeVideoTime, realtime,
            practiceNow && game.isSongPlaying && !game.isPaused);
        bool practiceChanged = practiceNow != oldPractice;
        if (practiceNow && !oldPractice)
        {
            // Practice may reuse gameplay renderers after the player returns to
            // the song. Drop per-session compensation and visibility baselines
            // so the new practice session starts from its live native state.
            speeds.Clear();
            visibility.Clear();
            speedLog.Clear();
            speedTransitionLog.Clear();
        }
        if (!practiceNow && fullSongSpeedSyncPending)
            fullSongSpeedSyncPending = !SyncFullSongSpeed(game);
        if (!sides && !strings && !beats && !score && !constantSpeed && hidden.Count == 0 && colors.Count == 0 && texts.Count == 0 && speeds.Count == 0) return;
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
            if (score || oldScore) TintText(p.AccuracyText, score, scoreText);
            bool compensate = constantSpeed && practiceNow;
            if (!compensate && speeds.Count == 0 && !practiceChanged) continue;
            float ratio = clockRate;
            float configured = ConfiguredNoteSpeed.Read(p.player?.Pointer ?? IntPtr.Zero);
            compensate &= ConfiguredNoteSpeed.IsPlausible(configured);
            if (!noteRenderers.TryGetValue(p.Pointer, out var notes))
            {
                notes = p.gameObject.GetComponentInChildren<GuitarNoteRenderer>(true);
                if (notes != null) noteRenderers.Add(p.Pointer, notes);
            }
            float noteSpeedBefore = notes == null ? float.NaN : notes.noteSpeed;
            float beatSpeedBefore = b == null ? float.NaN : b.field_Private_Single_0;
            float highwaySpeedBefore = p.highwayScroll == null ? float.NaN : p.highwayScroll.field_Private_Single_1;
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
                if (compensate && speedLog.Add(notes.Pointer))
                    MelonLoader.MelonLogger.Msg($"Practice rendering: profile={configured}, chartTempo={game.practiceSpeed}, clockRate={ratio}, nativeSpeed={native.Speed}, targetSpeed={speed}, nativeWindow={native.Window}, targetWindow={window}.");
            }
            if (b != null) b.field_Private_Single_0 = Speed(b.Pointer, b.field_Private_Single_0, ratio, compensate, configured);
            if (p.highwayScroll != null)
                p.highwayScroll.field_Private_Single_1 = Speed(p.highwayScroll.Pointer, p.highwayScroll.field_Private_Single_1, ratio, compensate, configured);
            if (practiceChanged && speedTransitionLog.Add(p.Pointer))
                MelonLoader.MelonLogger.Msg($"Practice speed transition: practice={practiceNow}, chartTempo={game.practiceSpeed:0.###}, clockRate={ratio:0.###}, profile={configured:0.###}, note={noteSpeedBefore:0.###}->{(notes == null ? float.NaN : notes.noteSpeed):0.###}, beat={beatSpeedBefore:0.###}->{(b == null ? float.NaN : b.field_Private_Single_0):0.###}, highway={highwaySpeedBefore:0.###}->{(p.highwayScroll == null ? float.NaN : p.highwayScroll.field_Private_Single_1):0.###}.");
        }
        oldSides = sides; oldStrings = strings; oldBeats = beats;
        oldPractice = practiceNow;
        bool restoreScore = oldScore;
        oldScore = score;
        if (!score && !restoreScore) return;
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
            foreach (var renderer in Sprites(star.gameObject))
            {
                if (renderer == null || renderer.Pointer == star.starCount?.Pointer) continue;
                if (Under(renderer.transform, star.progressBar) || Under(renderer.transform, star.progressBarEnd)) continue;
                Tint(renderer, score, stars);
            }
            TintChildren(star.progressBar, score, progress);
            TintChildren(star.progressBarEnd, score, progress);
        }
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
            float beatBefore = beat == null ? float.NaN : beat.field_Private_Single_0;
            if (beat != null && Math.Abs(beatBefore - configured) > .001f) beat.field_Private_Single_0 = configured;
            var scroll = player.highwayScroll;
            float scrollBefore = scroll == null ? float.NaN : scroll.field_Private_Single_1;
            if (scroll != null && Math.Abs(scrollBefore - configured) > .001f) scroll.field_Private_Single_1 = configured;
            if (changed || (beat != null && Math.Abs(beatBefore - configured) > .001f) || (scroll != null && Math.Abs(scrollBefore - configured) > .001f))
                MelonLoader.MelonLogger.Msg($"Full-song speed reset after scene change: profile={configured:0.###}, note={noteBefore:0.###}->{notes.noteSpeed:0.###}, window={windowBefore:0.######}->{notes.field_Protected_Double_0:0.######}, beat={beatBefore:0.###}->{(beat == null ? float.NaN : beat.field_Private_Single_0):0.###}, highway={scrollBefore:0.###}->{(scroll == null ? float.NaN : scroll.field_Private_Single_1):0.###}.");
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
