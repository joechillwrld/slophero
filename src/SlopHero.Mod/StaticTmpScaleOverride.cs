using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using Animator = Il2Cpp.Animator;
using LegacyAnimation = UnityEngine.Animation;

namespace SlopHero.Mod;

/// <summary>
/// Experimental per-label suppression of TMP's SDF scale-update queue.
/// TMP's layout, graphic rebuild, and culling queues remain untouched.
/// </summary>
internal sealed class StaticTmpScaleOverride
{
    private sealed class Candidate
    {
        public Candidate(TMP_Text text, Vector3 scale) { Text = text; Scale = scale; }
        public TMP_Text Text { get; }
        public Vector3 Scale { get; }
    }

    private readonly Dictionary<IntPtr, Candidate> candidates = new();
    private readonly Dictionary<IntPtr, Candidate> animatedCandidates = new();
    private readonly Dictionary<IntPtr, TMP_Text> modified = new();
    private readonly HashSet<IntPtr> excluded = new();
    private bool active, discoveryAttempted;
    private double nextScaleCheck, nextAnimatedScaleCheck;

    public bool IsActive => active;
    public int ModifiedCount => modified.Count;
    public bool NeedsRestore => modified.Count != 0;
    public bool NoTargetsForScene => discoveryAttempted && candidates.Count == 0
        && animatedCandidates.Count == 0;

    /// <summary>Applies only while eligible; otherwise restores our changes.</summary>
    public void Apply(bool eligible, double realtime)
    {
        if (!eligible)
        {
            RestoreModified();
            active = false;
            return;
        }

        active = true;
        if (!discoveryAttempted)
        {
            Discover();
            discoveryAttempted = true;
        }
        if (NoTargetsForScene)
        {
            active = false;
            return;
        }

        if (realtime >= nextScaleCheck)
        {
            nextScaleCheck = realtime + .25;
            ApplyCandidates(candidates);
        }
        if (realtime >= nextAnimatedScaleCheck)
        {
            nextAnimatedScaleCheck = realtime + .05;
            ApplyCandidates(animatedCandidates);
        }
    }

    public void Clear()
    {
        RestoreModified();
        candidates.Clear();
        animatedCandidates.Clear();
        excluded.Clear();
        active = false;
        discoveryAttempted = false;
        nextScaleCheck = nextAnimatedScaleCheck = 0;
    }

    private void Discover()
    {
        // The discovery order is irrelevant: candidates are keyed by native
        // pointer and never presented in hierarchy order. Avoid sorting the
        // scene-wide result array on this opt-in one-time scan.
        foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            if (text == null || !text.enabled || !text.gameObject.activeInHierarchy) continue;
            try
            {
                if (!string.Equals(text.gameObject.scene.name, "Gameplay", StringComparison.OrdinalIgnoreCase)
                    || text.isTextObjectScaleStatic) continue;
                IntPtr pointer = text.Pointer;
                if (pointer == IntPtr.Zero || candidates.ContainsKey(pointer) || animatedCandidates.ContainsKey(pointer)) continue;
                var destination = HasAnimationInParents(text) ? animatedCandidates : candidates;
                destination.Add(pointer, new Candidate(text, text.transform.lossyScale));
            }
            catch { }
        }
    }

    private void ApplyCandidates(Dictionary<IntPtr, Candidate> set)
    {
        foreach (var pair in set)
        {
            if (excluded.Contains(pair.Key)) continue;
            var candidate = pair.Value;
            var text = candidate.Text;
            if (text == null || !text.enabled || !text.gameObject.activeInHierarchy)
            {
                RestoreOne(pair.Key, text);
                continue;
            }

            try
            {
                // Animator/Animation parents are checked at 20 Hz. If a
                // script changes scale, restore TMP's queue registration and
                // leave that label alone for the rest of this scene.
                if (text.transform.lossyScale != candidate.Scale)
                {
                    RestoreOne(pair.Key, text);
                    excluded.Add(pair.Key);
                    continue;
                }

                if (!text.isTextObjectScaleStatic)
                {
                    text.isTextObjectScaleStatic = true;
                    modified[pair.Key] = text;
                }
            }
            catch
            {
                RestoreOne(pair.Key, text);
                excluded.Add(pair.Key);
            }
        }
    }

    private static bool HasAnimationInParents(TMP_Text text)
    {
        for (Transform? node = text.transform; node != null; node = node.parent)
        {
            // Any Animator/Animation in the chain can animate scale later in
            // the song, so conservatively keep those TMP labels registered.
            if (node.GetComponent<Animator>() != null || node.GetComponent<LegacyAnimation>() != null)
                return true;
        }
        return false;
    }

    private void RestoreModified()
    {
        foreach (var pair in modified.ToArray()) RestoreOne(pair.Key, pair.Value);
        modified.Clear();
    }

    private void RestoreOne(IntPtr pointer, TMP_Text? text)
    {
        if (!modified.Remove(pointer)) return;
        try
        {
            if (text != null && text.isTextObjectScaleStatic)
                text.isTextObjectScaleStatic = false;
        }
        catch { }
    }
}
