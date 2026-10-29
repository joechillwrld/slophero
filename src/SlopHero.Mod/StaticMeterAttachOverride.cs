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

        internal void CopyFrom(Snapshot other)
        {
            Width = other.Width; Height = other.Height;
            View = other.View; Projection = other.Projection;
            Root = other.Root; Reference = other.Reference;
            CameraRect = other.CameraRect; CameraPixelRect = other.CameraPixelRect;
            AnchorTop = other.AnchorTop; AnchorBottom = other.AnchorBottom;
            ScaleOffset = other.ScaleOffset; Sprite = other.Sprite;
            if (Targets.Length != other.Targets.Length) Targets = new TargetPose[other.Targets.Length];
            other.Targets.AsSpan().CopyTo(Targets);
        }
    }

    private sealed class State
    {
        internal readonly SpriteAttach Attach;
        internal readonly bool OriginalEnabled;
        internal readonly Snapshot Last = new();
        internal readonly Snapshot Scratch = new();
        internal bool HasLast;
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
                bool captured = Capture(attach, state.Scratch);
                if (!captured || !state.HasLast || !state.Scratch.Matches(state.Last))
                {
                    Restore(state);
                    if (captured)
                    {
                        state.Last.CopyFrom(state.Scratch);
                        state.HasLast = true;
                    }
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

    private static bool Capture(SpriteAttach attach, Snapshot result)
    {
        var camera = attach.playerCam;
        var reference = attach.referenceSprite;
        var targets = attach.objectsToScale;
        if (camera == null || reference == null || targets == null) return false;
        result.Width = Screen.width;
        result.Height = Screen.height;
        result.View = camera.worldToCameraMatrix;
        result.Projection = camera.projectionMatrix;
        result.CameraRect = camera.rect;
        result.CameraPixelRect = camera.pixelRect;
        result.Root = attach.transform.localToWorldMatrix;
        result.Reference = reference.transform.localToWorldMatrix;
        result.AnchorTop = attach.anchorTop;
        result.AnchorBottom = attach.anchorBottom;
        result.ScaleOffset = attach.scaleOffset;
        result.Sprite = reference.sprite?.Pointer ?? IntPtr.Zero;
        if (result.Targets.Length != targets.Length) result.Targets = new TargetPose[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            if (target == null) return false;
            result.Targets[i] = new TargetPose(target.Pointer, target.localPosition,
                target.localRotation, target.localScale);
        }
        return true;
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
        state.HasLast = false;
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
