using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Opt-in pause for the two meter-only SpriteAttach scripts in the current
/// guitar prefab. A 20 Hz input check wakes them for at least two checks after
/// camera, screen, sprite, or target-transform changes. No note or input
/// component is touched.
/// </summary>
internal sealed class StaticMeterAttachOverride
{
    private readonly record struct TargetPose(IntPtr Pointer, Vector3 Position, Quaternion Rotation, Vector3 Scale);

    private sealed class Snapshot
    {
        internal int Width, Height;
        internal Matrix4x4 View, Projection, Root, Reference;
        internal Rect CameraRect;
        internal Rect CameraPixelRect;
        internal Vector3 AnchorTop, AnchorBottom;
        internal Vector2 ScaleOffset;
        internal IntPtr Sprite;
        internal TargetPose[] Targets = Array.Empty<TargetPose>();

        internal bool Matches(Snapshot other)
        {
            return Width == other.Width && Height == other.Height
                && View.Equals(other.View) && Projection.Equals(other.Projection)
                && CameraRect.Equals(other.CameraRect) && CameraPixelRect.Equals(other.CameraPixelRect)
                && Root.Equals(other.Root)
                && Reference.Equals(other.Reference) && AnchorTop.Equals(other.AnchorTop)
                && AnchorBottom.Equals(other.AnchorBottom) && ScaleOffset == other.ScaleOffset
                && Sprite == other.Sprite && Targets.AsSpan().SequenceEqual(other.Targets);
        }
    }

    private sealed class State
    {
        internal readonly SpriteAttach Attach;
        internal readonly bool OriginalEnabled;
        internal Snapshot? Last;
        internal int StableChecks;
        internal bool Paused;

        internal State(SpriteAttach attach)
        {
            Attach = attach;
            OriginalEnabled = attach.enabled;
        }
    }

    private readonly List<State> states = new(2);
    private bool failed, reported;

    internal bool NeedsRestore => states.Count != 0;

    internal void Update(bool requested, bool eligible, GameManager? game)
    {
        if (!requested)
        {
            Clear();
            return;
        }
        if (failed) return;
        if (!eligible || game == null)
        {
            Restore();
            return;
        }
        try
        {
            if (states.Count == 0 && !Discover(game)) return;
            int pausedCount = 0;
            foreach (var state in states)
            {
                var attach = state.Attach;
                if (attach == null || !attach.gameObject.activeInHierarchy || !state.OriginalEnabled)
                {
                    Restore(state);
                    continue;
                }
                Snapshot? current = Capture(attach);
                if (current == null || state.Last == null || !current.Matches(state.Last))
                {
                    Restore(state);
                    state.Last = current;
                    continue;
                }
                if (++state.StableChecks >= 2 && attach.enabled)
                {
                    attach.enabled = false;
                    state.Paused = true;
                }
                if (state.Paused) pausedCount++;
            }
            if (pausedCount == 2 && !reported)
            {
                reported = true;
                MelonLogger.Msg("Sloptifine paused two stable meter SpriteAttach scripts; camera and target changes wake them.");
            }
        }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            MelonLogger.Warning($"Meter SpriteAttach experiment stopped; original alignment restored: {ex.Message}");
        }
    }

    private bool Discover(GameManager game)
    {
        if (game.playerObjects == null || game.actualPlayerCount != 1) return false;
        foreach (var player in game.playerObjects)
        {
            if (player == null || !player.enabled || !player.gameObject.activeInHierarchy
                || player.TryCast<GuitarPlayer>() == null) continue;
            // The exact guitar prefab has these as direct children. Avoid a
            // stripped GetComponentsInChildren<T> specialization and avoid
            // touching other instruments' identically named meters.
            SpriteAttach? spBar = player.transform.Find("SPBar")?.GetComponent<SpriteAttach>();
            SpriteAttach? rockMeter = player.transform.Find("RockMeter")?.GetComponent<SpriteAttach>();
            if (spBar == null || rockMeter == null) return false;
            states.Add(new State(spBar));
            states.Add(new State(rockMeter));
            return true;
        }
        return false;
    }

    private static Snapshot? Capture(SpriteAttach attach)
    {
        var camera = attach.playerCam;
        var reference = attach.referenceSprite;
        var targets = attach.objectsToScale;
        if (camera == null || reference == null || targets == null) return null;
        var result = new Snapshot
        {
            Width = Screen.width,
            Height = Screen.height,
            View = camera.worldToCameraMatrix,
            Projection = camera.projectionMatrix,
            CameraRect = camera.rect,
            CameraPixelRect = camera.pixelRect,
            Root = attach.transform.localToWorldMatrix,
            Reference = reference.transform.localToWorldMatrix,
            AnchorTop = attach.anchorTop,
            AnchorBottom = attach.anchorBottom,
            ScaleOffset = attach.scaleOffset,
            Sprite = reference.sprite?.Pointer ?? IntPtr.Zero,
            Targets = new TargetPose[targets.Length]
        };
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (target == null) return null;
            result.Targets[i] = new TargetPose(target.Pointer, target.localPosition,
                target.localRotation, target.localScale);
        }
        return result;
    }

    private static void Restore(State state)
    {
        if (state.Paused)
        {
            try { if (state.Attach != null) state.Attach.enabled = state.OriginalEnabled; }
            catch { }
        }
        state.Paused = false;
        state.StableChecks = 0;
        state.Last = null;
    }

    private void Restore()
    {
        foreach (var state in states) Restore(state);
        reported = false;
    }

    internal void Clear()
    {
        Restore();
        states.Clear();
        failed = false;
    }
}
