using Il2Cpp;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityAnimator = UnityEngine.Animator;

namespace SlopHero.Mod;

/// <summary>
/// Exact-scene, optional visual-only suppression. The score notification
/// controller and StarPowerController stay active; only their own visual
/// branches are paused in eligible offline solo play.
/// </summary>
internal sealed class OptionalHudAnimationsOverride
{
    private Canvas? pbCanvas;
    private bool pbCanvasEnabled;
    private UnityAnimator? pbAnimator;
    private bool pbAnimatorEnabled;
    private GameObject? leftGlow, rightGlow;
    private bool leftGlowWasActive, rightGlowWasActive;
    private IntPtr glowRoot;
    private bool pbSuppressed, glowSuppressed;
    private HarmonyLib.Harmony? focusHarmony;
    private MethodInfo? focusMethod;
    private bool focusHookInstalled, focusHookFailed, focusResumePending;
    private double nextPbDiscovery, nextGlowDiscovery, nextReconcile;
    private int pbDiscoveryMisses, glowDiscoveryMisses;

    public bool NeedsRestore => pbSuppressed || glowSuppressed;
    public bool PbSuppressed => pbSuppressed;
    public bool GlowSuppressed => glowSuppressed;
    public bool FocusResumePending => focusResumePending;

    public void UpdateFocusHook(HarmonyLib.Harmony harmony, bool requested, bool gameplayScene)
    {
        if (!requested || !gameplayScene)
        {
            if (focusHookInstalled || focusHookFailed || focusResumePending)
            {
                RemoveFocusHook();
                focusHookFailed = false;
                focusResumePending = false;
            }
            return;
        }
        if (focusHookInstalled || focusHookFailed) return;
        try
        {
            focusMethod = typeof(GameManager).GetMethod("OnApplicationFocus",
                BindingFlags.Instance | BindingFlags.NonPublic, binder: null,
                types: new[] { typeof(bool) }, modifiers: null)
                ?? throw new MissingMethodException("GameManager.OnApplicationFocus(bool)");
            focusHarmony = harmony;
            focusHookOwner = this;
            var postfix = typeof(OptionalHudAnimationsOverride).GetMethod(nameof(AfterApplicationFocus),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(focusMethod, postfix: new HarmonyMethod(postfix));
            focusHookInstalled = true;
        }
        catch (Exception ex)
        {
            RemoveFocusHook();
            focusHookFailed = true;
            MelonLoader.MelonLogger.Warning($"Sloptifine could not install its optional visual-resume hook; normal visual refresh remains active: {ex.Message}");
        }
    }

    public void Apply(GameManager? game, bool hidePb, bool hideGlows, double now, bool forceReapply = false)
    {
        bool reconcile = now >= nextReconcile;
        if (glowRoot != IntPtr.Zero && (leftGlow == null || rightGlow == null))
        {
            RestoreGlows();
            ResetGlowReferences();
        }
        bool findPb = hidePb && pbCanvas == null && now >= nextPbDiscovery;
        bool findGlows = hideGlows && glowRoot == IntPtr.Zero && game != null && now >= nextGlowDiscovery;
        if (!hidePb) RestorePb();
        else
        {
            if (findPb) DiscoverPb(now);
            if (pbCanvas != null && pbAnimator != null && (!pbSuppressed || reconcile || forceReapply))
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
            if (findGlows) DiscoverGlows(game!, now);
            if (glowRoot != IntPtr.Zero && leftGlow != null && rightGlow != null
                && (!glowSuppressed || reconcile || forceReapply))
            {
                if (leftGlow.activeSelf) leftGlow.SetActive(false);
                if (rightGlow.activeSelf) rightGlow.SetActive(false);
                if (!glowSuppressed) MelonLoader.MelonLogger.Msg("Sloptifine paused the two Star Power side-glow branches.");
                glowSuppressed = true;
            }
        }
        if (reconcile) nextReconcile = now + .2;
    }

    public void Clear()
    {
        RemoveFocusHook();
        focusHookFailed = false;
        focusResumePending = false;
        RestorePb();
        RestoreGlows();
        pbCanvas = null;
        pbAnimator = null;
        ResetGlowReferences();
        nextPbDiscovery = nextGlowDiscovery = nextReconcile = 0;
        pbDiscoveryMisses = glowDiscoveryMisses = 0;
    }

    public void ClearFocusResumePending() => focusResumePending = false;

    private void ResetGlowReferences()
    {
        glowRoot = IntPtr.Zero;
        leftGlow = rightGlow = null;
        leftGlowWasActive = rightGlowWasActive = false;
        nextGlowDiscovery = 0;
    }

    private void DiscoverPb(double now)
    {
        nextPbDiscovery = now + 1;
        // Both this display and the side glows use UnityEngine.Animator. The
        // game also has an unrelated custom Il2Cpp.Animator sprite script;
        // resolving that type would never find these visual targets.
        // The serialized Gameplay display is active and has the Canvas and
        // Unity animator on the same object.
        var display = UnityEngine.Object.FindObjectOfType<HighScoreDisplay>();
        bool inGameplay = display != null && display.gameObject.scene.name == "Gameplay";
        var canvas = inGameplay ? display!.GetComponent<Canvas>() : null;
        var animator = inGameplay ? display!.GetComponent<UnityAnimator>() : null;
        if (canvas == null || animator == null)
        {
            ReportMissing(ref pbDiscoveryMisses, "personal-best popup",
                pbDiscoveryMisses == 2 ? $"Gameplay display={inGameplay}; Canvas={canvas != null}; Unity Animator={animator != null}" : "");
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
        // The current StarPowerController serializes direct references to its
        // left and right Unity animators. This avoids a scene-wide
        // search and keeps the lookup scoped to the local guitar player.
        UnityAnimator? leftAnimator = null, rightAnimator = null;
        if (game.playerObjects != null)
            foreach (var player in game.playerObjects)
                if (player != null && player.enabled && player.TryCast<GuitarPlayer>() != null)
                {
                    var controller = player.gameObject.GetComponentInChildren<StarPowerController>(true);
                    if (controller == null) continue;
                    leftAnimator = controller.animatorLeft;
                    rightAnimator = controller.animatorRight;
                    break;
                }
        if (leftAnimator != null && rightAnimator != null
            && leftAnimator.gameObject.name == "SpSideGlows L"
            && rightAnimator.gameObject.name == "SpSideGlows R"
            && leftAnimator.transform.parent != null && rightAnimator.transform.parent != null
            && leftAnimator.transform.parent.Pointer == rightAnimator.transform.parent.Pointer)
        {
            // Only these branches are side glows. The sibling CenterGlow
            // sprite is a separate visual and stays active.
            int rendererCount = leftAnimator.GetComponentsInChildren<SpriteRenderer>(true).Length
                + rightAnimator.GetComponentsInChildren<SpriteRenderer>(true).Length;
            if (rendererCount == 0)
            {
                ReportMissing(ref glowDiscoveryMisses, "Star Power side glows",
                    glowDiscoveryMisses == 2 ? "side-glow renderers=0" : "");
                return;
            }
            glowDiscoveryMisses = 0;
            glowRoot = leftAnimator.transform.parent.Pointer;
            leftGlow = leftAnimator.gameObject;
            rightGlow = rightAnimator.gameObject;
            leftGlowWasActive = leftGlow.activeSelf;
            rightGlowWasActive = rightGlow.activeSelf;
            return;
        }
        ReportMissing(ref glowDiscoveryMisses, "Star Power side glows",
            glowDiscoveryMisses == 2 ? $"left={leftAnimator?.gameObject.name ?? "missing"}; right={rightAnimator?.gameObject.name ?? "missing"}" : "");
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
        try { if (leftGlow != null && leftGlow.activeSelf != leftGlowWasActive) leftGlow.SetActive(leftGlowWasActive); }
        catch { }
        try { if (rightGlow != null && rightGlow.activeSelf != rightGlowWasActive) rightGlow.SetActive(rightGlowWasActive); }
        catch { }
        glowSuppressed = false;
    }

    private static OptionalHudAnimationsOverride? focusHookOwner;

    private static void AfterApplicationFocus(bool __0)
    {
        if (__0 && focusHookOwner != null) focusHookOwner.focusResumePending = true;
    }

    private void RemoveFocusHook()
    {
        if (ReferenceEquals(focusHookOwner, this)) focusHookOwner = null;
        try
        {
            if (focusHarmony != null && focusMethod != null)
                focusHarmony.Unpatch(focusMethod, HarmonyPatchType.All, focusHarmony.Id);
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"Sloptifine focus-resume hook cleanup failed: {ex.Message}");
        }
        focusHarmony = null;
        focusMethod = null;
        focusHookInstalled = false;
    }
}
