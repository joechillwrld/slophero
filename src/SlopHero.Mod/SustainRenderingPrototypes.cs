using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Default-off exact-build experiment that skips repeated, identical writes
/// to regular sustain renderers. Position and changing renderer properties
/// remain on the game's path.
/// </summary>
internal sealed class SustainStaticWriteCachePrototype
{
    private sealed class OutputState
    {
        internal bool HasSprite, HasColor;
        internal IntPtr Sprite;
        internal Color Color;
    }

    private readonly record struct PendingWrite(IntPtr Renderer, IntPtr Value, Color Color, byte Kind);

    private static SustainStaticWriteCachePrototype? active;
    private readonly HashSet<IntPtr> sustainRenderers = new();
    private readonly HashSet<IntPtr> linkedBlocks = new();
    private readonly Dictionary<IntPtr, IntPtr> rendererBlocks = new();
    private readonly Dictionary<IntPtr, OutputState> outputs = new();
    private readonly Dictionary<IntPtr, float> lengths = new();
    private readonly List<MethodInfo> patchedMethods = new();
    private MethodInfo? spriteSetter, colorSetter, setPropertyBlock, blockSetFloat, blockClear;
    private bool installed, failed, enabled;
    private int sustainLengthProperty;
    private string installStage = "initialize";

    internal bool IsInstalled => installed;
    internal bool IsActive => installed && enabled;

    // Install the five global setter hooks when Gameplay initializes, before
    // the first active song update. Renderer membership is still discovered
    // only when an eligible local guitar player exists.
    internal void Prepare(HarmonyLib.Harmony harmony, bool requested)
    {
        if (!requested || installed || failed) return;
        try { InstallHooks(harmony); }
        catch (Exception ex)
        {
            Restore(harmony);
            failed = true;
            MelonLogger.Warning($"Sloptifine sustain write-cache setup stopped at '{installStage}': {ex}");
        }
    }

    internal void Update(HarmonyLib.Harmony harmony, bool requested, bool eligible, GameManager? game)
    {
        if (!requested)
        {
            enabled = false;
            Restore(harmony);
            failed = false;
            return;
        }
        if (!eligible || game == null)
        {
            if (enabled)
            {
                enabled = false;
                ClearSongState();
            }
            return;
        }

        if (failed) return;
        if (!installed)
        {
            Prepare(harmony, requested: true);
            if (!installed) return;
        }
        if (!enabled)
        {
            try
            {
                installStage = "scan regular sustain renderers";
                foreach (var renderer in FindRegularSustainRenderers(game))
                    if (renderer != null) sustainRenderers.Add(renderer.Pointer);
                if (sustainRenderers.Count == 0)
                    throw new InvalidOperationException("No regular sustain renderers with the mapped CloneHero/Sustains shader were found.");
                enabled = true;
                MelonLogger.Msg($"Sloptifine sustain write-cache prototype active for {sustainRenderers.Count} regular sustain renderers; positions and per-frame changing output remain vanilla.");
            }
            catch (Exception ex)
            {
                enabled = false;
                ClearSongState();
                failed = true;
                MelonLogger.Warning($"Sloptifine sustain write-cache prototype stopped safely at '{installStage}': {ex}");
            }
        }
    }

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        enabled = false;
        Restore(harmony);
        failed = false;
    }

    private void InstallHooks(HarmonyLib.Harmony harmony)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        installStage = "resolve SpriteRenderer.sprite setter";
        spriteSetter = typeof(SpriteRenderer).GetProperty("sprite", flags)?.GetSetMethod(true)
            ?? throw new MissingMethodException("UnityEngine.SpriteRenderer.sprite setter");
        installStage = "resolve SpriteRenderer.color setter";
        colorSetter = typeof(SpriteRenderer).GetProperty("color", flags)?.GetSetMethod(true)
            ?? throw new MissingMethodException("UnityEngine.SpriteRenderer.color setter");
        installStage = "resolve Renderer.SetPropertyBlock";
        setPropertyBlock = typeof(Renderer).GetMethod("SetPropertyBlock", flags, null,
            new[] { typeof(MaterialPropertyBlock) }, null)
            ?? throw new MissingMethodException("UnityEngine.Renderer.SetPropertyBlock(MaterialPropertyBlock)");
        installStage = "resolve MaterialPropertyBlock.SetFloat";
        blockSetFloat = typeof(MaterialPropertyBlock).GetMethod("SetFloat", flags, null,
            new[] { typeof(int), typeof(float) }, null)
            ?? throw new MissingMethodException("UnityEngine.MaterialPropertyBlock.SetFloat(int, float)");
        installStage = "resolve MaterialPropertyBlock.Clear";
        blockClear = typeof(MaterialPropertyBlock).GetMethod("Clear", flags, null, Type.EmptyTypes, null)
            ?? throw new MissingMethodException("UnityEngine.MaterialPropertyBlock.Clear()");
        installStage = "resolve _SustainLength shader property";
        sustainLengthProperty = Shader.PropertyToID("_SustainLength");

        active = this;
        installStage = "patch Unity SpriteRenderer.sprite setter";
        Patch(harmony, spriteSetter, nameof(BeforeSprite), nameof(AfterSprite));
        installStage = "patch Unity SpriteRenderer.color setter";
        Patch(harmony, colorSetter, nameof(BeforeColor), nameof(AfterColor));
        installStage = "patch Unity Renderer.SetPropertyBlock";
        Patch(harmony, setPropertyBlock, null, nameof(AfterSetPropertyBlock));
        installStage = "patch Unity MaterialPropertyBlock.SetFloat";
        Patch(harmony, blockSetFloat, nameof(BeforeSetFloat), nameof(AfterSetFloat));
        installStage = "patch Unity MaterialPropertyBlock.Clear";
        Patch(harmony, blockClear, null, nameof(AfterClearBlock));
        installed = true;
        MelonLogger.Msg("Sloptifine sustain write-cache setter hooks prepared at Gameplay initialization.");
    }

    internal static IEnumerable<SpriteRenderer> FindRegularSustainRenderers(GameManager game)
    {
        foreach (var renderer in FindGuitarSpriteRenderers(game))
        {
            if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null) continue;
            if (string.Equals(renderer.sharedMaterial.shader.name, "CloneHero/Sustains", StringComparison.Ordinal))
                yield return renderer;
        }
    }

    internal static IEnumerable<SpriteRenderer> FindGuitarSpriteRenderers(GameManager game)
    {
        if (game.playerObjects == null) yield break;
        foreach (var playerObject in game.playerObjects)
        {
            if (playerObject == null || !playerObject.enabled || !playerObject.gameObject.activeInHierarchy
                || playerObject.TryCast<GuitarPlayer>() == null) continue;
            // The exact GuitarNoteRenderer generic lookup is stripped from this
            // Unity player. SpriteRenderer child enumeration is already used
            // by Sloptifine's working visual controls and is sufficient here:
            // the callers filter to the sustain body/glow shaders afterward.
            foreach (var renderer in playerObject.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer != null) yield return renderer;
        }
    }

    private static void Patch(HarmonyLib.Harmony harmony, MethodInfo target, string? prefixName, string? postfixName)
    {
        HarmonyMethod? prefix = prefixName == null ? null : new HarmonyMethod(typeof(SustainStaticWriteCachePrototype)
            .GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic)!);
        HarmonyMethod? postfix = postfixName == null ? null : new HarmonyMethod(typeof(SustainStaticWriteCachePrototype)
            .GetMethod(postfixName, BindingFlags.Static | BindingFlags.NonPublic)!);
        harmony.Patch(target, prefix: prefix, postfix: postfix);
        active!.patchedMethods.Add(target);
    }

    private static bool BeforeSprite(SpriteRenderer __instance, Sprite? value, out PendingWrite __state)
    {
        __state = default;
        var owner = active;
        if (owner == null || !owner.enabled) return true;
        IntPtr renderer = __instance.Pointer;
        if (!owner.sustainRenderers.Contains(renderer)) return true;
        IntPtr sprite = value == null ? IntPtr.Zero : value.Pointer;
        if (owner.outputs.TryGetValue(renderer, out var state) && state.HasSprite && state.Sprite == sprite)
            return false;
        __state = new PendingWrite(renderer, sprite, default, 1);
        return true;
    }

    private static void AfterSprite(PendingWrite __state)
    {
        var owner = active;
        if (owner == null || !owner.enabled || __state.Kind != 1) return;
        var state = GetOutputState(owner, __state.Renderer);
        state.Sprite = __state.Value;
        state.HasSprite = true;
    }

    private static bool BeforeColor(SpriteRenderer __instance, Color value, out PendingWrite __state)
    {
        __state = default;
        var owner = active;
        if (owner == null || !owner.enabled) return true;
        IntPtr renderer = __instance.Pointer;
        if (!owner.sustainRenderers.Contains(renderer)) return true;
        if (owner.outputs.TryGetValue(renderer, out var state) && state.HasColor && state.Color.Equals(value))
            return false;
        __state = new PendingWrite(renderer, IntPtr.Zero, value, 2);
        return true;
    }

    private static void AfterColor(PendingWrite __state)
    {
        var owner = active;
        if (owner == null || !owner.enabled || __state.Kind != 2) return;
        var state = GetOutputState(owner, __state.Renderer);
        state.Color = __state.Color;
        state.HasColor = true;
    }

    private static void AfterSetPropertyBlock(Renderer __instance, MaterialPropertyBlock? properties)
    {
        var owner = active;
        if (owner == null || !owner.enabled || !owner.sustainRenderers.Contains(__instance.Pointer)) return;
        IntPtr renderer = __instance.Pointer;
        IntPtr nextBlock = properties == null ? IntPtr.Zero : properties.Pointer;
        if (owner.rendererBlocks.TryGetValue(renderer, out IntPtr priorBlock) && priorBlock != nextBlock)
        {
            owner.rendererBlocks.Remove(renderer);
            if (!owner.rendererBlocks.ContainsValue(priorBlock))
            {
                owner.linkedBlocks.Remove(priorBlock);
                owner.lengths.Remove(priorBlock);
            }
        }
        if (properties == null)
        {
            return;
        }
        owner.rendererBlocks[renderer] = nextBlock;
        owner.linkedBlocks.Add(nextBlock);
    }

    private static bool BeforeSetFloat(MaterialPropertyBlock __instance, int nameID, float value,
        out PendingWrite __state)
    {
        __state = default;
        var owner = active;
        if (owner == null || !owner.enabled || nameID != owner.sustainLengthProperty) return true;
        IntPtr block = __instance.Pointer;
        if (!owner.linkedBlocks.Contains(block)) return true;
        if (owner.lengths.TryGetValue(block, out float previous) && previous.Equals(value)) return false;
        __state = new PendingWrite(block, IntPtr.Zero, new Color(value, 0, 0, 0), 3);
        return true;
    }

    private static void AfterSetFloat(MaterialPropertyBlock __instance, int nameID, float value, PendingWrite __state)
    {
        var owner = active;
        if (owner == null || !owner.enabled || __state.Kind != 3 || nameID != owner.sustainLengthProperty) return;
        owner.lengths[__instance.Pointer] = value;
    }

    private static void AfterClearBlock(MaterialPropertyBlock __instance)
    {
        var owner = active;
        if (owner == null || !owner.enabled) return;
        IntPtr block = __instance.Pointer;
        owner.linkedBlocks.Remove(block);
        owner.lengths.Remove(block);
    }

    private static OutputState GetOutputState(SustainStaticWriteCachePrototype owner, IntPtr renderer)
    {
        if (!owner.outputs.TryGetValue(renderer, out var state))
            owner.outputs.Add(renderer, state = new OutputState());
        return state;
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        enabled = false;
        if (ReferenceEquals(active, this)) active = null;
        foreach (var target in patchedMethods)
        {
            try { harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"Sustain write-cache hook cleanup failed; its inactive prefix now passes through: {ex.Message}"); }
        }
        patchedMethods.Clear();
        ClearSongState();
        spriteSetter = colorSetter = setPropertyBlock = blockSetFloat = blockClear = null;
        installed = false;
    }

    private void ClearSongState()
    {
        sustainRenderers.Clear();
        linkedBlocks.Clear();
        rendererBlocks.Clear();
        outputs.Clear();
        lengths.Clear();
    }
}

/// <summary>
/// Default-off experiment that asks Unity's existing sustain materials to
/// allow batching/instancing. It changes only shared material flags and
/// restores the shader's mapped DisableBatching tag when the solo scope ends.
/// </summary>
internal sealed class SustainBatchingPrototype
{
    private sealed record MaterialState(Material Material, bool EnableInstancing, string DisableBatchingTag);
    private readonly Dictionary<IntPtr, MaterialState> changed = new();
    private bool failed;
    private string updateStage = "initialize";

    internal void Update(bool requested, bool eligible, GameManager? game)
    {
        if (!requested || !eligible || game == null)
        {
            Restore();
            if (!requested) failed = false;
            return;
        }
        if (failed || changed.Count > 0) return;
        try
        {
            updateStage = "scan regular sustain renderers";
            foreach (var renderer in SustainStaticWriteCachePrototype.FindRegularSustainRenderers(game))
            {
                updateStage = "read sustain shared material";
                Material? material = renderer == null ? null : renderer.sharedMaterial;
                if (material == null || material.shader == null) continue;
                IntPtr pointer = material.Pointer;
                if (changed.ContainsKey(pointer)) continue;
                updateStage = "read material DisableBatching tag";
                string currentTag = material.GetTag("DisableBatching", false);
                if (!string.Equals(currentTag, "True", StringComparison.OrdinalIgnoreCase)) continue;
                updateStage = "read material instancing flag";
                bool instancing = material.enableInstancing;
                changed.Add(pointer, new MaterialState(material, instancing, currentTag));
                updateStage = "set material DisableBatching override";
                material.SetOverrideTag("DisableBatching", "False");
                updateStage = "enable material instancing";
                material.enableInstancing = true;
            }
            if (changed.Count == 0)
                throw new InvalidOperationException("No sustain body material with the mapped DisableBatching tag was found.");
            MelonLogger.Msg($"Sloptifine sustain batching prototype active on {changed.Count} shared material(s); shader geometry parity is unverified.");
        }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            MelonLogger.Warning($"Sloptifine sustain batching prototype stopped and restored materials at '{updateStage}': {ex}");
        }
    }

    internal void Clear()
    {
        Restore();
        failed = false;
    }

    private void Restore()
    {
        foreach (var state in changed.Values)
        {
            try
            {
                if (state.Material == null) continue;
                if (string.Equals(state.Material.GetTag("DisableBatching", false), "False", StringComparison.OrdinalIgnoreCase))
                    state.Material.SetOverrideTag("DisableBatching", state.DisableBatchingTag);
                if (state.Material.enableInstancing)
                    state.Material.enableInstancing = state.EnableInstancing;
            }
            catch { /* The shared assets may already be unloading. */ }
        }
        changed.Clear();
    }
}

/// <summary>Suppresses only sustain glow renderers; base sustain bodies remain intact.</summary>
internal sealed class PlainHeldSustainPrototype
{
    private static PlainHeldSustainPrototype? active;
    private readonly Dictionary<IntPtr, (SpriteRenderer Renderer, bool WasEnabled)> renderers = new();
    private MethodInfo? enabledSetter;
    private bool hookInstalled;
    private long blockedReenables;
    private bool failed;
    private string updateStage = "initialize";

    internal bool IsActive => renderers.Count != 0;

    internal void Update(HarmonyLib.Harmony harmony, bool requested, bool eligible, GameManager? game)
    {
        if (!requested || !eligible || game == null)
        {
            Restore(harmony);
            if (!requested) failed = false;
            return;
        }
        if (failed || renderers.Count != 0) return;
        try
        {
            updateStage = "scan live guitar sprite renderers";
            foreach (var renderer in SustainStaticWriteCachePrototype.FindGuitarSpriteRenderers(game))
            {
                updateStage = "read guitar sprite material";
                if (renderer == null || !IsGlow(renderer.sharedMaterial)) continue;
                IntPtr pointer = renderer.Pointer;
                if (renderers.ContainsKey(pointer)) continue;
                updateStage = "read glow renderer enabled state";
                bool prior = renderer.enabled;
                renderers.Add(pointer, (renderer, prior));
                updateStage = "disable glow renderer using the game's supported enabled property";
                renderer.enabled = false;
            }
            if (renderers.Count == 0)
                throw new InvalidOperationException("No sustain glow renderers were found under the active guitar renderer.");
            updateStage = "patch Renderer.enabled setter";
            enabledSetter = typeof(Renderer).GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod()
                ?? throw new MissingMethodException("UnityEngine.Renderer.enabled setter");
            harmony.Patch(enabledSetter, prefix: new HarmonyMethod(typeof(PlainHeldSustainPrototype)
                .GetMethod(nameof(BeforeSetEnabled), BindingFlags.Static | BindingFlags.NonPublic)!));
            hookInstalled = true;
            active = this;
            MelonLogger.Msg($"Sloptifine plain-held-sustain prototype hid {renderers.Count} pooled glow renderer(s) and blocks game re-enables while active; base sustain bodies remain visible.");
        }
        catch (Exception ex)
        {
            Restore(harmony);
            failed = true;
            MelonLogger.Warning($"Sloptifine plain-held-sustain prototype stopped safely at '{updateStage}': {ex}");
        }
    }

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        Restore(harmony);
        failed = false;
    }

    private static bool BeforeSetEnabled(Renderer __instance, bool value)
    {
        var owner = active;
        if (owner == null || !value || !owner.renderers.ContainsKey(__instance.Pointer)) return true;
        owner.blockedReenables++;
        return false;
    }

    private static bool IsGlow(Material? material)
    {
        if (material == null) return false;
        string shader = material.shader == null ? "" : material.shader.name;
        return shader.Contains("SustainGlow", StringComparison.OrdinalIgnoreCase)
            || material.name.Contains("SustainGlow", StringComparison.OrdinalIgnoreCase)
            || material.name.Contains("OpenSustainGlow", StringComparison.OrdinalIgnoreCase);
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        if (ReferenceEquals(active, this)) active = null;
        if (hookInstalled && enabledSetter != null)
        {
            try { harmony.Unpatch(enabledSetter, HarmonyPatchType.All, harmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"Held-sustain glow hook cleanup failed; its inactive prefix now passes through: {ex.Message}"); }
        }
        hookInstalled = false;
        enabledSetter = null;
        if (renderers.Count != 0 && blockedReenables != 0)
            MelonLogger.Msg($"Sloptifine plain-held-sustain prototype blocked {blockedReenables} glow re-enable write(s) during this song.");
        blockedReenables = 0;
        foreach (var (renderer, wasEnabled) in renderers.Values)
        {
            try
            {
                if (renderer != null && renderer.enabled != wasEnabled)
                    renderer.enabled = wasEnabled;
            }
            catch { /* Scene teardown may have destroyed the renderer. */ }
        }
        renderers.Clear();
    }
}
