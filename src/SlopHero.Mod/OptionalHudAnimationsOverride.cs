using Il2Cpp;
using UnityEngine;
using Animator = Il2Cpp.Animator;

namespace SlopHero.Mod;

/// <summary>
/// Exact-scene, optional visual-only suppression. The score notification
/// controller and StarPowerController stay active; only their own graphics
/// and animation components are paused in eligible offline solo play.
/// </summary>
internal sealed class OptionalHudAnimationsOverride
{
    private readonly record struct RendererState(SpriteRenderer Component, bool Enabled);
    private readonly record struct AnimatorState(Animator Component, bool Enabled);
    private readonly record struct ColorState(AnimatorColorBehavior Component, bool Enabled);

    private Canvas? pbCanvas;
    private bool pbCanvasEnabled;
    private Animator? pbAnimator;
    private bool pbAnimatorEnabled;
    private RendererState[] glowRenderers = Array.Empty<RendererState>();
    private AnimatorState[] glowAnimators = Array.Empty<AnimatorState>();
    private ColorState[] glowColors = Array.Empty<ColorState>();
    private IntPtr glowRoot;
    private bool pbSuppressed, glowSuppressed;
    private double nextPbDiscovery, nextGlowDiscovery, nextReconcile;
    private int pbDiscoveryMisses, glowDiscoveryMisses;

    public bool NeedsRestore => pbSuppressed || glowSuppressed;
    public bool PbSuppressed => pbSuppressed;
    public bool GlowSuppressed => glowSuppressed;

    public void Apply(GameManager? game, bool hidePb, bool hideGlows, double now)
    {
        bool reconcile = now >= nextReconcile;
        if (glowRoot != IntPtr.Zero && (glowAnimators.Length == 0 || glowAnimators[0].Component == null))
        {
            RestoreGlows();
            ResetGlowReferences();
        }
        if (!hidePb) RestorePb();
        else
        {
            if (pbCanvas == null && now >= nextPbDiscovery) DiscoverPb(now);
            if (pbCanvas != null && pbAnimator != null && (!pbSuppressed || reconcile))
            {
                if (pbCanvas.enabled) pbCanvas.enabled = false;
                if (pbAnimator.enabled) pbAnimator.enabled = false;
                if (!pbSuppressed) MelonLoader.MelonLogger.Msg("Sloptifine paused the optional personal-best popup visuals.");
                pbSuppressed = true;
            }
        }

        if (!hideGlows) RestoreGlows();
        else
        {
            if (glowRoot == IntPtr.Zero && game != null && now >= nextGlowDiscovery)
                DiscoverGlows(game, now);
            if (glowRoot != IntPtr.Zero && (!glowSuppressed || reconcile))
            {
                foreach (var state in glowAnimators)
                    if (state.Component != null && state.Component.enabled) state.Component.enabled = false;
                foreach (var state in glowColors)
                    if (state.Component != null && state.Component.enabled) state.Component.enabled = false;
                foreach (var state in glowRenderers)
                    if (state.Component != null && state.Component.enabled) state.Component.enabled = false;
                if (!glowSuppressed) MelonLoader.MelonLogger.Msg($"Sloptifine paused Star Power side-glow visuals ({glowAnimators.Length} animators, {glowColors.Length} color scripts, {glowRenderers.Length} sprites).");
                glowSuppressed = true;
            }
        }
        if (reconcile) nextReconcile = now + .2;
    }

    public void Clear()
    {
        RestorePb();
        RestoreGlows();
        pbCanvas = null;
        pbAnimator = null;
        ResetGlowReferences();
        nextPbDiscovery = nextGlowDiscovery = nextReconcile = 0;
        pbDiscoveryMisses = glowDiscoveryMisses = 0;
    }

    private void ResetGlowReferences()
    {
        glowRoot = IntPtr.Zero;
        glowAnimators = Array.Empty<AnimatorState>();
        glowColors = Array.Empty<ColorState>();
        glowRenderers = Array.Empty<RendererState>();
        nextGlowDiscovery = 0;
    }

    private void DiscoverPb(double now)
    {
        nextPbDiscovery = now + 1;
        var root = GameObject.Find("HUDCam")?.transform.Find("HUD/Score/PB Notification");
        if (root == null || root.GetComponent<HighScoreDisplay>() == null)
        {
            ReportMissing(ref pbDiscoveryMisses, "personal-best popup");
            return;
        }
        var canvas = root.GetComponent<Canvas>();
        var animator = root.GetComponent<Animator>();
        // The popup's generated text/graphics can change its CanvasRenderer
        // count at runtime. Its exact path and controller are the identity.
        if (canvas == null || animator == null)
        {
            ReportMissing(ref pbDiscoveryMisses, "personal-best popup");
            return;
        }
        pbDiscoveryMisses = 0;
        pbCanvas = canvas;
        pbCanvasEnabled = canvas.enabled;
        pbAnimator = animator;
        pbAnimatorEnabled = animator.enabled;
    }

    private void DiscoverGlows(GameManager game, double now)
    {
        nextGlowDiscovery = now + 1;
        if (game.playerObjects == null)
        {
            ReportMissing(ref glowDiscoveryMisses, "Star Power side glows");
            return;
        }
        foreach (var player in game.playerObjects)
        {
            var guitar = player?.TryCast<GuitarPlayer>();
            if (guitar == null) continue;
            var root = guitar.transform.Find("Updated SP Animation");
            if (root == null || root.GetComponent<StarPowerController>() == null) continue;
            var left = root.Find("SpSideGlows L");
            var right = root.Find("SpSideGlows R");
            if (left == null || right == null) continue;
            var leftAnimator = left.GetComponent<Animator>();
            var rightAnimator = right.GetComponent<Animator>();
            if (leftAnimator == null || rightAnimator == null) continue;
            // Only these two branches are side glows. The sibling CenterGlow
            // sprite is a separate visual and must remain untouched.
            var animators = new[] { leftAnimator, rightAnimator };
            var colors = left.GetComponentsInChildren<AnimatorColorBehavior>(true).ToArray()
                .Concat(right.GetComponentsInChildren<AnimatorColorBehavior>(true).ToArray()).ToArray();
            var renderers = left.GetComponentsInChildren<SpriteRenderer>(true).ToArray()
                .Concat(right.GetComponentsInChildren<SpriteRenderer>(true).ToArray()).ToArray();
            if (renderers.Length == 0) continue;
            glowDiscoveryMisses = 0;
            glowRoot = root.Pointer;
            glowAnimators = animators.Select(a => new AnimatorState(a, a != null && a.enabled)).ToArray();
            glowColors = colors.Select(c => new ColorState(c, c != null && c.enabled)).ToArray();
            glowRenderers = renderers.Select(r => new RendererState(r, r != null && r.enabled)).ToArray();
            return;
        }
        ReportMissing(ref glowDiscoveryMisses, "Star Power side glows");
    }

    private static void ReportMissing(ref int misses, string target)
    {
        if (++misses == 3)
            MelonLoader.MelonLogger.Warning($"Sloptifine 3 could not find the live {target} visual target; the game's visuals remain enabled.");
    }

    private void RestorePb()
    {
        if (!pbSuppressed) return;
        try { if (pbCanvas != null && pbCanvas.enabled != pbCanvasEnabled) pbCanvas.enabled = pbCanvasEnabled; }
        catch { }
        try { if (pbAnimator != null && pbAnimator.enabled != pbAnimatorEnabled) pbAnimator.enabled = pbAnimatorEnabled; }
        catch { }
        pbSuppressed = false;
    }

    private void RestoreGlows()
    {
        if (!glowSuppressed) return;
        foreach (var state in glowAnimators)
            try { if (state.Component != null && state.Component.enabled != state.Enabled) state.Component.enabled = state.Enabled; }
            catch { }
        foreach (var state in glowColors)
            try { if (state.Component != null && state.Component.enabled != state.Enabled) state.Component.enabled = state.Enabled; }
            catch { }
        foreach (var state in glowRenderers)
            try { if (state.Component != null && state.Component.enabled != state.Enabled) state.Component.enabled = state.Enabled; }
            catch { }
        glowSuppressed = false;
    }
}
