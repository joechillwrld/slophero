using System.Diagnostics;
using System.Runtime.InteropServices;
using MelonLoader;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Optional exact-build interception of the native SpriteRenderer color
/// setter. Unlike Harmony's byref GuitarNoteContainer trampoline, this passes
/// only three raw pointers across the native boundary and always calls the
/// original Unity setter. Non-note renderers pass through unchanged.
/// </summary>
internal sealed class NativeRainbowColorBridge
{
    private const int SetterRva = 0x2884F00;
    private const int InternalSetterCacheRva = 0x3F1E840;
    private static readonly byte[] ExpectedPrologue =
        { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x57, 0x48, 0x83, 0xEC, 0x20,
          0x48, 0x8B, 0x05, 0x2F, 0x99, 0x69, 0x01 };

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetColorDelegate(IntPtr renderer, IntPtr color, IntPtr methodInfo);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetColorInternalDelegate(IntPtr renderer, IntPtr color);

    private static readonly SetColorDelegate Replacement = Intercept;
    private static SetColorInternalDelegate? original;
    private static NativeRainbowColorBridge? active;
    private NativeDetour? detour;
    private RainbowHighway? owner;
    private bool failed, logged;

    internal bool IsActive => detour?.IsApplied == true;

    internal void SetEnabled(bool enabled, RainbowHighway highway)
    {
        if (!enabled)
        {
            Disable();
            return;
        }
        if (failed || IsActive) return;
        try
        {
            if (detour == null && !Install()) return;
            owner = highway;
            active = this;
            detour!.Apply();
            if (!detour.IsApplied) throw new InvalidOperationException("Native color detour did not apply.");
            MelonLogger.Msg("Experimental rainbow native color bridge active for tracked notes.");
        }
        catch (Exception ex)
        {
            failed = true;
            Disable();
            MelonLogger.Warning($"Experimental rainbow native color bridge unavailable: {ex.Message}");
        }
    }

    private bool Install()
    {
        if (IntPtr.Size != 8 || !OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The native rainbow color bridge requires Windows x64.");
        using var process = Process.GetCurrentProcess();
        var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m =>
            string.Equals(m.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase));
        if (module == null) throw new DllNotFoundException("GameAssembly.dll was not loaded.");
        var unity = process.Modules.Cast<ProcessModule>().FirstOrDefault(m =>
            string.Equals(m.ModuleName, "UnityPlayer.dll", StringComparison.OrdinalIgnoreCase));
        if (unity == null) throw new DllNotFoundException("UnityPlayer.dll was not loaded.");
        IntPtr target = IntPtr.Add(module.BaseAddress, SetterRva);
        byte[] current = new byte[ExpectedPrologue.Length];
        Marshal.Copy(target, current, 0, current.Length);
        if (!current.SequenceEqual(ExpectedPrologue))
            throw new InvalidOperationException("SpriteRenderer color setter does not match the supplied game binary.");

        // The exact wrapper loads a cached Unity internal-call pointer from
        // this RVA and calls it with the same renderer/color arguments. Call
        // that original target directly; no IL2CPP byref trampoline or
        // MonoMod-generated managed proxy is involved.
        IntPtr internalSetter = Marshal.ReadIntPtr(IntPtr.Add(module.BaseAddress, InternalSetterCacheRva));
        if (internalSetter == IntPtr.Zero) return false; // game has not used the setter yet
        long address = internalSetter.ToInt64(), unityStart = unity.BaseAddress.ToInt64();
        if (address < unityStart || address >= unityStart + unity.ModuleMemorySize)
            throw new InvalidOperationException("The cached sprite-color internal call is outside UnityPlayer.dll.");
        original = Marshal.GetDelegateForFunctionPointer<SetColorInternalDelegate>(internalSetter);
        detour = new NativeDetour(target, Marshal.GetFunctionPointerForDelegate(Replacement),
            new NativeDetourConfig { ManualApply = true });
        return true;
    }

    private static void Intercept(IntPtr renderer, IntPtr color, IntPtr methodInfo)
    {
        var bridge = active;
        if (bridge?.owner != null && color != IntPtr.Zero)
        {
            try
            {
                var value = new Color(
                    BitConverter.Int32BitsToSingle(Marshal.ReadInt32(color, 0)),
                    BitConverter.Int32BitsToSingle(Marshal.ReadInt32(color, 4)),
                    BitConverter.Int32BitsToSingle(Marshal.ReadInt32(color, 8)),
                    BitConverter.Int32BitsToSingle(Marshal.ReadInt32(color, 12)));
                if (bridge.owner.TryRewriteNativeNoteColor(renderer, ref value))
                {
                    Marshal.WriteInt32(color, 0, BitConverter.SingleToInt32Bits(value.r));
                    Marshal.WriteInt32(color, 4, BitConverter.SingleToInt32Bits(value.g));
                    Marshal.WriteInt32(color, 8, BitConverter.SingleToInt32Bits(value.b));
                    if (!bridge.logged)
                    {
                        bridge.logged = true;
                        MelonLogger.Msg("Experimental rainbow bridge recolored an exact-game note setter call.");
                    }
                }
            }
            catch (Exception ex)
            {
                bridge.failed = true;
                bridge.owner = null;
                MelonLogger.Warning($"Experimental rainbow bridge stopped recoloring; original setter remains available: {ex.Message}");
            }
        }
        original!(renderer, color);
    }

    internal void Disable()
    {
        owner = null;
        if (ReferenceEquals(active, this)) active = null;
        if (detour?.IsApplied == true)
            try { detour.Undo(); }
            catch (Exception ex)
            {
                failed = true;
                MelonLogger.Error($"Experimental rainbow bridge could not restore the original setter: {ex.Message}");
            }
    }

    internal void Clear()
    {
        Disable();
        if (detour == null) { failed = logged = false; return; }
        try
        {
            if (!detour.IsApplied)
            {
                detour.Dispose();
                detour = null;
                original = null;
                failed = logged = false;
            }
        }
        catch (Exception ex)
        {
            failed = true;
            MelonLogger.Error($"Experimental rainbow bridge cleanup failed: {ex.Message}");
        }
    }
}
