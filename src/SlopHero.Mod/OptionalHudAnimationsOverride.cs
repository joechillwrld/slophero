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
                if (!glowSuppressed) MelonLoader.MelonLogger.Msg("Sloptifine paused Star Power side-glow visuals (2 animators, 8 color scripts, 13 sprites).");
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
        if (root == null || root.GetComponent<HighScoreDisplay>() == null) return;
        var canvas = root.GetComponent<Canvas>();
        var animator = root.GetComponent<Animator>();
        if (canvas == null || animator == null || root.GetComponentsInChildren<CanvasRenderer>(true).Length != 4) return;
        pbCanvas = canvas;
        pbCanvasEnabled = canvas.enabled;
        pbAnimator = animator;
        pbAnimatorEnabled = animator.enabled;
    }

    private void DiscoverGlows(GameManager game, double now)
    {
        nextGlowDiscovery = now + 1;
        if (game.playerObjects == null) return;
        foreach (var player in game.playerObjects)
        {
            var guitar = player?.TryCast<GuitarPlayer>();
            if (guitar == null) continue;
            var root = guitar.transform.Find("Updated SP Animation");
            if (root == null || root.GetComponent<StarPowerController>() == null) continue;
            var animators = root.GetComponentsInChildren<Animator>(true).ToArray();
            var colors = root.GetComponentsInChildren<AnimatorColorBehavior>(true).ToArray();
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true).ToArray();
            if (animators.Length != 2 || colors.Length != 8 || renderers.Length != 13
                || !animators.Any(a => a != null && a.gameObject.name == "SpSideGlows L")
                || !animators.Any(a => a != null && a.gameObject.name == "SpSideGlows R")) continue;
            glowRoot = root.Pointer;
            glowAnimators = animators.Select(a => new AnimatorState(a, a != null && a.enabled)).ToArray();
            glowColors = colors.Select(c => new ColorState(c, c != null && c.enabled)).ToArray();
            glowRenderers = renderers.Select(r => new RendererState(r, r != null && r.enabled)).ToArray();
            return;
        }
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
