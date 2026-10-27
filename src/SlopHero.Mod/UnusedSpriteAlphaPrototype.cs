using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlopHero.Mod;

/// <summary>
/// Exact-build, default-off GPU experiment. The four mapped Windows guitar
/// materials enable ETC1_EXTERNAL_ALPHA despite their alpha-switch being zero
/// and the supplied sprite assets having no split-alpha texture. Selecting
/// the no-ETC1 variant removes a redundant alpha texture sample. Only change
/// materials whose current live SpriteRenderers pass the same checks.
/// </summary>
internal sealed class UnusedSpriteAlphaPrototype
{
    private const string AlphaKeyword = "ETC1_EXTERNAL_ALPHA";
    private readonly Dictionary<IntPtr, Material> changed = new();
    private bool active, failed;
    private string stage = "initialize";

    internal bool IsSustainActive { get; private set; }

    internal void Update(bool requested, bool eligible, GameManager? game)
    {
        if (!requested || !eligible || game == null)
        {
            Restore();
            if (!requested) failed = false;
            return;
        }
        if (active || failed) return;
        try { Activate(game); }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            MelonLogger.Warning($"Sloptifine unused-alpha experiment stopped at '{stage}' and restored materials: {ex}");
        }
    }

    internal void Clear()
    {
        Restore();
        failed = false;
    }

    private void Activate(GameManager game)
    {
        stage = "collect mapped Gameplay sprite materials";
        var scene = SceneManager.GetActiveScene();
        var materials = new Dictionary<IntPtr, Candidate>();
        foreach (var renderer in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
        {
            if (renderer == null || !renderer.gameObject.scene.IsValid()
                || renderer.gameObject.scene != scene) continue;
            Observe(renderer, materials);
        }
        // FindObjectsOfType omits inactive pooled guitar notes. Include those
        // as well because a pool slot can become visible later in this song.
        if (game.playerObjects != null)
        {
            foreach (var player in game.playerObjects)
            {
                if (player == null || !player.enabled || player.TryCast<GuitarPlayer>() == null) continue;
                foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer != null) Observe(renderer, materials);
            }
        }

        stage = "validate alpha-disabled materials and live sprite textures";
        foreach (var candidate in materials.Values)
        {
            var material = candidate.Material;
            if (candidate.HasSplitAlpha || candidate.AssignedSprites == 0
                || !material.HasProperty("_EnableExternalAlpha")
                || !Mathf.Approximately(material.GetFloat("_EnableExternalAlpha"), 0)
                || Array.IndexOf(material.shaderKeywords, AlphaKeyword) < 0) continue;

            stage = "select no-ETC1 shader variant";
            // Keep the original shared material and restore only our keyword
            // on pause/exit. This does not change sprite geometry or sorting.
            changed.Add(material.Pointer, material);
            material.DisableKeyword(AlphaKeyword);
            if (material.shader.name == "CloneHero/Sustains") IsSustainActive = true;
        }
        if (changed.Count == 0)
            throw new InvalidOperationException("No mapped material passed the runtime alpha guards.");
        active = true;
        MelonLogger.Msg($"Sloptifine unused-alpha prototype selected no-ETC1 variants on {changed.Count} guarded guitar sprite material(s).");
    }

    private static void Observe(SpriteRenderer renderer, Dictionary<IntPtr, Candidate> materials)
    {
        var material = renderer.sharedMaterial;
        if (material == null || material.shader == null || !IsMappedMaterial(material.name, material.shader.name)) return;
        if (!materials.TryGetValue(material.Pointer, out var candidate))
        {
            candidate = new Candidate(material);
            materials.Add(material.Pointer, candidate);
        }
        var sprite = renderer.sprite;
        if (sprite == null) return;
        candidate.AssignedSprites++;
        if (sprite.associatedAlphaSplitTexture != null)
            candidate.HasSplitAlpha = true;
    }

    private static bool IsMappedMaterial(string name, string shader)
    {
        if (name.EndsWith(" (Instance)", StringComparison.Ordinal))
            name = name[..^11];
        return (name == "FadeSprite" && shader == "CloneHero/Sprite Track FadeIn")
            || ((name == "FadeSpriteAdd" || name == "FadeSpriteAdd1Intense")
                && shader == "CloneHero/Sprite Track FadeIn Additive")
            || (name == "Sustains" && shader == "CloneHero/Sustains");
    }

    private void Restore()
    {
        foreach (var material in changed.Values)
        {
            try
            {
                if (material != null && Array.IndexOf(material.shaderKeywords, AlphaKeyword) < 0)
                    material.EnableKeyword(AlphaKeyword);
            }
            catch { /* The scene may already be unloading. */ }
        }
        changed.Clear();
        active = false;
        IsSustainActive = false;
    }

    private sealed class Candidate
    {
        internal readonly Material Material;
        internal int AssignedSprites;
        internal bool HasSplitAlpha;
        internal Candidate(Material material) => Material = material;
    }
}
