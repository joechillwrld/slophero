using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using SlopHero.Core;
using UnityEngine;

namespace SlopHero.Mod;

internal sealed class RingColors
{
    private sealed class Ring
    {
        public DesaturateSprite Component = null!;
        public SpriteRenderer Renderer = null!;
        public BasePlayer Player = null!;
        public Color Original;
        public float Hue, Saturation;
        public Color Applied;
        public bool HasApplied;
    }
    private readonly Dictionary<IntPtr, Ring> rings = new();
    private MaterialPropertyBlock? block;
    private int hueId, saturationId;
    private Color fc, perfect;
    private bool enabled, callbackFailed;
    private bool hooksInstalled, hookInstallFailed;
    private readonly List<MethodInfo> hookTargets = new();
    private long nextDiscovery, nextRefresh;
    internal static RingColors? Active;
    public bool NeedsUpdate => enabled || rings.Count != 0;

    public void SetHookState(HarmonyLib.Harmony harmony, bool requested)
    {
        if (!requested)
        {
            UninstallHooks(harmony);
            hookInstallFailed = false;
            return;
        }
        if (hooksInstalled || hookInstallFailed) return;

        try
        {
            Active = this;
            var hue = new HarmonyMethod(typeof(RingColors).GetMethod(
                nameof(HueWritten), BindingFlags.NonPublic | BindingFlags.Static)!);
            foreach (string name in new[] { "Method_Public_Void_0", "Method_Public_Void_1", "Method_Public_Void_2" })
            {
                var target = typeof(DesaturateSprite).GetMethod(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? throw new MissingMethodException(typeof(DesaturateSprite).FullName, name);
                harmony.Patch(target, postfix: hue);
                hookTargets.Add(target);
            }

            var saturationTarget = typeof(DesaturateSprite).GetMethod("Method_Public_Void_Single_0",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(DesaturateSprite).FullName, "Method_Public_Void_Single_0");
            harmony.Patch(saturationTarget, postfix: new HarmonyMethod(typeof(RingColors).GetMethod(
                nameof(SaturationWritten), BindingFlags.NonPublic | BindingFlags.Static)!));
            hookTargets.Add(saturationTarget);
            hooksInstalled = true;
            MelonLoader.MelonLogger.Msg("FC ring color hooks installed while custom colors are enabled.");
        }
        catch (Exception ex)
        {
            UninstallHooks(harmony);
            hookInstallFailed = true;
            MelonLoader.MelonLogger.Warning($"FC ring color hooks unavailable; vanilla ring colors remain active: {ex.Message}");
        }
    }

    public void Clear(HarmonyLib.Harmony harmony)
    {
        UninstallHooks(harmony);
        rings.Clear(); enabled = false; callbackFailed = false; hookInstallFailed = false;
        nextDiscovery = nextRefresh = 0;
    }

    private void UninstallHooks(HarmonyLib.Harmony harmony)
    {
        enabled = false;
        if (ReferenceEquals(Active, this)) Active = null;
        foreach (var target in hookTargets)
        {
            try { harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id); }
            catch (Exception ex) { MelonLoader.MelonLogger.Warning($"FC ring hook cleanup failed; its callback is disabled: {ex.Message}"); }
        }
        hookTargets.Clear();
        hooksInstalled = false;
    }
    private static void HueWritten(DesaturateSprite __instance) => Active?.Written(__instance, true);
    private static void SaturationWritten(DesaturateSprite __instance) => Active?.Written(__instance, false);
    private void Written(DesaturateSprite component, bool hue)
    {
        if (!enabled || callbackFailed || !rings.TryGetValue(component.Pointer, out var ring) || ring.Renderer == null) return;
        try
        {
            ring.Renderer.GetPropertyBlock(block);
            if (hue) ring.Hue = block!.GetFloat(hueId); else ring.Saturation = block!.GetFloat(saturationId);
            Apply(ring, true);
        }
        catch (Exception ex)
        {
            // Stop callbacks on an unexpected shader binding; never break vanilla callers.
            callbackFailed = true;
            MelonLoader.MelonLogger.Error($"FC shader override stopped: {ex}");
        }
    }
    public void Update(GameManager game, bool use, Color regular, Color pfc)
    {
        if (!use && rings.Count == 0) return;
        if (block == null)
        {
            block = new MaterialPropertyBlock();
            hueId = Shader.PropertyToID("_OutputHue");
            saturationId = Shader.PropertyToID("_OutputSaturation");
        }
        fc = regular; perfect = pfc; enabled = use;
        if (!use)
        {
            foreach (var ring in rings.Values)
            {
                if (ring.Renderer == null) continue;
                ring.Renderer.GetPropertyBlock(block);
                block.SetFloat(hueId, ring.Hue); block.SetFloat(saturationId, ring.Saturation);
                ring.Renderer.SetPropertyBlock(block);
                var c = ring.Original; c.a = ring.Renderer.color.a; ring.Renderer.color = c;
            }
            rings.Clear(); return;
        }
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (now >= nextRefresh)
        {
            nextRefresh = now + System.Diagnostics.Stopwatch.Frequency / 10;
            foreach (var ring in rings.Values) Apply(ring);
        }
        if (now < nextDiscovery || game.playerObjects == null) return;
        nextDiscovery = now + System.Diagnostics.Stopwatch.Frequency / 2;
        foreach (var p in game.playerObjects)
        {
            if (p == null || !p.enabled || p.TryCast<GuitarPlayer>() == null || p.FCIndicator == null || p.engine == null) continue;
            if (p.field_Protected_Il2CppReferenceArray_1_DesaturateSprite_0 == null) continue;
            foreach (var d in p.field_Protected_Il2CppReferenceArray_1_DesaturateSprite_0)
            {
                if (d == null || d.field_Private_SpriteRenderer_0 == null) continue;
                if (!rings.TryGetValue(d.Pointer, out var ring))
                {
                    var r = d.field_Private_SpriteRenderer_0;
                    r.GetPropertyBlock(block);
                    ring = new Ring { Component = d, Renderer = r, Player = p, Original = r.color,
                        Hue = block.GetFloat(hueId), Saturation = block.GetFloat(saturationId) };
                    rings.Add(d.Pointer, ring);
                }
                Apply(ring);
            }
        }
    }
    private void Apply(Ring ring, bool force = false)
    {
        if (ring.Renderer == null) return;
        // Clone Hero's guitar rings use native hue 0 for perfect FC and
        // 1/6 for ordinary FC. Keep the vanilla hue captured by the Harmony
        // postfix; the ghost counter is not the ring's FC state.
        Color c = NativeRingState.IsPerfectFc(ring.Hue) ? perfect : fc;
        if (!force && ring.HasApplied && ring.Applied == c) return;
        if (ring.Player == null || ring.Player.engine == null) return;
        ring.Applied = c; ring.HasApplied = true;
        Color.RGBToHSV(c, out float h, out float s, out float v);
        ring.Renderer.GetPropertyBlock(block);
        block!.SetFloat(hueId, h); block.SetFloat(saturationId, s);
        ring.Renderer.SetPropertyBlock(block);
        // Hue/saturation live in the shader; grayscale vertex tint controls brightness.
        ring.Renderer.color = new Color(v, v, v, ring.Renderer.color.a);
    }
}
