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
                DiscoverGlows(now);
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
        // Resolve the live controller rather than assuming the serialized
        // Gameplay hierarchy path is unchanged after scene initialization.
        var displays = UnityEngine.Object.FindObjectsByType<HighScoreDisplay>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        HighScoreDisplay? display = null;
        int gameplayDisplays = 0;
        foreach (var candidate in displays)
        {
            if (candidate == null || candidate.gameObject.scene.name != "Gameplay") continue;
            gameplayDisplays++;
            if (display != null)
            {
                ReportMissing(ref pbDiscoveryMisses, "personal-best popup",
                    pbDiscoveryMisses == 2 ? $"Gameplay displays={gameplayDisplays}" : "");
                return;
            }
            display = candidate;
        }
        var canvas = display?.GetComponent<Canvas>();
        var animator = display?.GetComponent<Animator>();
        if (canvas == null || animator == null)
        {
            ReportMissing(ref pbDiscoveryMisses, "personal-best popup",
                pbDiscoveryMisses == 2 ? $"Gameplay displays={gameplayDisplays}; Canvas={canvas != null}; Animator={animator != null}" : "");
            return;
        }
        pbDiscoveryMisses = 0;
        pbCanvas = canvas;
        pbCanvasEnabled = canvas.enabled;
        pbAnimator = animator;
        pbAnimatorEnabled = animator.enabled;
    }

    private void DiscoverGlows(double now)
    {
        nextGlowDiscovery = now + 1;
        // The live player hierarchy can differ from the prefab path. The
        // current scene has two named side-glow Animators, both parented under
        // the guitar player; locate that pair directly.
        Animator? leftAnimator = null, rightAnimator = null;
        List<string>? observed = glowDiscoveryMisses == 2 ? new List<string>(4) : null;
        foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (animator == null || animator.gameObject.scene.name != "Gameplay") continue;
            bool underGuitar = animator.GetComponentInParent<GuitarPlayer>() != null;
            if (observed != null && observed.Count < 6)
                observed.Add($"{animator.gameObject.name}:{(underGuitar ? "guitar" : "other")}");
            if (!underGuitar) continue;
            if (animator.gameObject.name == "SpSideGlows L") leftAnimator = animator;
            else if (animator.gameObject.name == "SpSideGlows R") rightAnimator = animator;
        }
        if (leftAnimator != null && rightAnimator != null
            && leftAnimator.transform.parent != null && rightAnimator.transform.parent != null
            && leftAnimator.transform.parent.Pointer == rightAnimator.transform.parent.Pointer)
        {
            // Only these branches are side glows. The sibling CenterGlow
            // sprite is a separate visual and stays active.
            var colors = leftAnimator.GetComponentsInChildren<AnimatorColorBehavior>(true).ToArray()
                .Concat(rightAnimator.GetComponentsInChildren<AnimatorColorBehavior>(true).ToArray()).ToArray();
            var renderers = leftAnimator.GetComponentsInChildren<SpriteRenderer>(true).ToArray()
                .Concat(rightAnimator.GetComponentsInChildren<SpriteRenderer>(true).ToArray()).ToArray();
            if (renderers.Length == 0)
            {
                ReportMissing(ref glowDiscoveryMisses, "Star Power side glows",
                    glowDiscoveryMisses == 2 ? $"animators={string.Join(",", observed ?? new List<string>())}; renderers=0" : "");
                return;
            }
            glowDiscoveryMisses = 0;
            glowRoot = leftAnimator.transform.parent.Pointer;
            glowAnimators = new[] { new AnimatorState(leftAnimator, leftAnimator.enabled),
                new AnimatorState(rightAnimator, rightAnimator.enabled) };
            glowColors = colors.Select(c => new ColorState(c, c != null && c.enabled)).ToArray();
            glowRenderers = renderers.Select(r => new RendererState(r, r != null && r.enabled)).ToArray();
            return;
        }
        ReportMissing(ref glowDiscoveryMisses, "Star Power side glows",
            glowDiscoveryMisses == 2 ? $"animators={string.Join(",", observed ?? new List<string>())}" : "");
    }

    private static void ReportMissing(ref int misses, string target, string detail)
    {
        if (++misses == 3)
            MelonLoader.MelonLogger.Warning($"Sloptifine 3 could not find the live {target} visual target ({detail}); the game's visuals remain enabled.");
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
