using System.Diagnostics;
using System.Runtime.InteropServices;
using Il2Cpp;
using MelonLoader;
using SlopHero.Core;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Exact-build, default-off filter for the game's seven sustain-helper
/// Renderer.enabled calls. It replaces the old global Harmony setter prefix
/// with a tiny native pointer-table check at those callsites only.
/// </summary>
internal sealed class NativeHeldSustainGlowSuppressor
{
    private const int RendererEnabledSetterRva = 0x2870FC0;
    private const int PageSize = 4096;
    private const int PointerSlots = 512;
    private const int PointerTableBytes = PointerSlots * sizeof(long);
    private const int FlagOffset = PageSize + PointerTableBytes;
    private const int AllocationBytes = 3 * PageSize;
    private const uint MemCommitReserve = 0x3000, MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04, PageExecuteRead = 0x20, PageExecuteReadWrite = 0x40;

    private readonly record struct Site(int Rva, string ExpectedHex);
    private static readonly Site[] Sites =
    {
        new(0x11559DB, "E8E0B57101"),
        new(0x1156127, "E894AE7101"),
        new(0x115615F, "E85CAE7101"),
        new(0x11563BC, "E8FFAB7101"),
        new(0x1156987, "E834A67101"),
        new(0x11569BF, "E8FCA57101"),
        new(0x1156C33, "E888A37101")
    };

    private sealed record Patch(IntPtr Address, byte[] Original, byte[] Replacement);
    private readonly List<Patch> patches = new();
    private readonly Dictionary<IntPtr, (SpriteRenderer Renderer, bool WasEnabled)> renderers = new();
    private readonly List<SpriteRenderer> warmedGlowRenderers = new();
    private IntPtr allocation, table, flag, gameAssemblyBase;
    private bool enabled, failed;
    private double nextWarm;
    private string stage = "initialize";

    internal bool IsActive => enabled;
    internal bool IsInstalled => allocation != IntPtr.Zero && patches.Count == Sites.Length;

    // Keep the Core lifecycle call shape shared with the previous managed
    // prototype while this exact-build implementation uses native callsites.
    internal void Prepare(HarmonyLib.Harmony _, bool requested) => Prepare(requested);
    internal void Warm(bool requested, GameManager? game, double now, HarmonyLib.Harmony _) => Warm(requested, game, now);
    internal void Update(HarmonyLib.Harmony _, bool requested, bool eligible, GameManager? game)
        => Update(requested, eligible, game);
    internal void Clear(HarmonyLib.Harmony _) => Clear();

    internal void Prepare(bool requested)
    {
        if (!requested || IsInstalled || failed) return;
        try { Install(); }
        catch (Exception ex)
        {
            Uninstall();
            failed = true;
            MelonLogger.Warning($"Sloptifine native held-glow filter stopped at '{stage}': {ex.Message}");
        }
    }

    internal void Warm(bool requested, GameManager? game, double now)
    {
        if (!requested || failed || warmedGlowRenderers.Count != 0
            || game?.playerObjects == null || now < nextWarm) return;
        nextWarm = now + .5;
        try
        {
            var materialKinds = new Dictionary<IntPtr, bool>();
            foreach (var renderer in SustainStaticWriteCachePrototype.FindGuitarSpriteRenderers(game))
            {
                if (renderer == null) continue;
                var material = renderer.sharedMaterial;
                if (material == null) continue;
                if (!materialKinds.TryGetValue(material.Pointer, out bool glow))
                    materialKinds[material.Pointer] = glow = IsGlow(material);
                if (glow) warmedGlowRenderers.Add(renderer);
            }
        }
        catch { warmedGlowRenderers.Clear(); }
    }

    internal void Update(bool requested, bool eligible, GameManager? game)
    {
        if (!requested)
        {
            Deactivate();
            Uninstall();
            warmedGlowRenderers.Clear();
            nextWarm = 0;
            if (allocation == IntPtr.Zero) failed = false;
            return;
        }
        if (failed) return;
        if (!eligible || game == null)
        {
            Deactivate();
            Uninstall();
            return;
        }

        if (enabled) return;
        try
        {
            if (!IsInstalled) Prepare(true);
            if (!IsInstalled || failed) return;

            stage = "collect active guitar glow renderers";
            var candidates = warmedGlowRenderers.Count != 0
                ? warmedGlowRenderers
                : FindGlowRenderers(game);
            foreach (var renderer in candidates)
            {
                if (renderer == null) continue;
                IntPtr pointer = renderer.Pointer;
                if (renderers.ContainsKey(pointer)) continue;
                bool wasEnabled = renderer.enabled;
                renderers.Add(pointer, (renderer, wasEnabled));
                if (wasEnabled) renderer.enabled = false;
            }
            if (renderers.Count == 0)
                throw new InvalidOperationException("No sustain-glow renderers were found under the active guitar.");

            stage = "populate native renderer filter";
            ClearPointerTable();
            foreach (IntPtr pointer in renderers.Keys) InsertPointer(pointer);
            Thread.MemoryBarrier();
            Marshal.WriteByte(flag, 1);
            enabled = true;
            MelonLogger.Msg($"Sloptifine native held-glow filter active for {renderers.Count} glow renderers across seven exact sustain setter callsites; sustain bodies and inputs are unchanged.");
        }
        catch (Exception ex)
        {
            Deactivate();
            failed = true;
            MelonLogger.Warning($"Sloptifine native held-glow filter stopped safely at '{stage}': {ex.Message}");
        }
    }

    internal void Clear()
    {
        Deactivate();
        Uninstall();
        warmedGlowRenderers.Clear();
        nextWarm = 0;
        if (allocation == IntPtr.Zero) failed = false;
    }

    private static List<SpriteRenderer> FindGlowRenderers(GameManager game)
    {
        var results = new List<SpriteRenderer>();
        var materialKinds = new Dictionary<IntPtr, bool>();
        foreach (var renderer in SustainStaticWriteCachePrototype.FindGuitarSpriteRenderers(game))
        {
            if (renderer == null) continue;
            var material = renderer.sharedMaterial;
            if (material == null) continue;
            if (!materialKinds.TryGetValue(material.Pointer, out bool glow))
                materialKinds[material.Pointer] = glow = IsGlow(material);
            if (glow) results.Add(renderer);
        }
        return results;
    }

    private static bool IsGlow(Material material)
    {
        string shader = material.shader == null ? "" : material.shader.name;
        return shader.Contains("SustainGlow", StringComparison.OrdinalIgnoreCase)
            || material.name.Contains("SustainGlow", StringComparison.OrdinalIgnoreCase)
            || material.name.Contains("OpenSustainGlow", StringComparison.OrdinalIgnoreCase);
    }

    private void Install()
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
            throw new PlatformNotSupportedException("The exact-callsite filter requires Windows x64.");
        using var process = Process.GetCurrentProcess();
        var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m =>
            string.Equals(m.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase));
        if (module == null) throw new DllNotFoundException("GameAssembly.dll is not loaded.");
        gameAssemblyBase = module.BaseAddress;

        stage = "validate seven Renderer.enabled callsites";
        foreach (var site in Sites)
        {
            byte[] expected = Convert.FromHexString(site.ExpectedHex);
            IntPtr address = IntPtr.Add(gameAssemblyBase, site.Rva);
            byte[] actual = new byte[expected.Length];
            Marshal.Copy(address, actual, 0, actual.Length);
            if (!actual.SequenceEqual(expected))
                throw new InvalidOperationException($"Renderer.enabled callsite 0x{site.Rva:X} does not match the supplied game build.");
        }

        stage = "allocate nearby native filter";
        long candidate = (gameAssemblyBase.ToInt64() + module.ModuleMemorySize + 0xFFFFL) & ~0xFFFFL;
        for (int i = 0; i < 2048 && allocation == IntPtr.Zero; i++)
        {
            long address = candidate + i * 0x10000L;
            if (Math.Abs(address - (gameAssemblyBase.ToInt64() + Sites[0].Rva)) > int.MaxValue) break;
            allocation = VirtualAlloc(new IntPtr(address), AllocationBytes, MemCommitReserve, PageReadWrite);
        }
        if (allocation == IntPtr.Zero)
            throw new InvalidOperationException("No native filter space was available near GameAssembly.dll.");
        table = IntPtr.Add(allocation, PageSize);
        flag = IntPtr.Add(allocation, FlagOffset);
        IntPtr setter = IntPtr.Add(gameAssemblyBase, RendererEnabledSetterRva);

        stage = "write native call filter";
        byte[] code = NativeRendererFilterStub.Build(flag, table, setter);
        Marshal.Copy(code, 0, allocation, code.Length);
        if (!VirtualProtect(allocation, PageSize, PageExecuteRead, out _))
            throw new InvalidOperationException("Could not protect the native filter as executable.");
        if (!FlushInstructionCache(GetCurrentProcess(), allocation, (nuint)code.Length))
            throw new InvalidOperationException("Could not flush native filter instructions.");

        foreach (var site in Sites)
        {
            IntPtr call = IntPtr.Add(gameAssemblyBase, site.Rva);
            byte[] original = Convert.FromHexString(site.ExpectedHex);
            long displacement = allocation.ToInt64() - (call.ToInt64() + 5);
            if (displacement < int.MinValue || displacement > int.MaxValue)
                throw new InvalidOperationException("The native filter is outside the callsite's relative range.");
            byte[] replacement = new byte[5];
            replacement[0] = 0xE8;
            BitConverter.GetBytes((int)displacement).CopyTo(replacement, 1);
            patches.Add(new Patch(call, original, replacement));
        }

        stage = "install seven native call filters";
        foreach (var patch in patches) WriteCode(patch.Address, patch.Replacement);
        MelonLogger.Msg("Sloptifine native held-glow filter prepared at seven exact sustain setter callsites.");
    }

    private void InsertPointer(IntPtr pointer)
    {
        int slot = (int)(((ulong)pointer.ToInt64() >> 4) & (PointerSlots - 1));
        for (int probe = 0; probe < PointerSlots; probe++)
        {
            IntPtr entry = IntPtr.Add(table, slot * sizeof(long));
            long value = Marshal.ReadInt64(entry);
            if (value == 0 || value == pointer.ToInt64())
            {
                Marshal.WriteInt64(entry, pointer.ToInt64());
                return;
            }
            slot = (slot + 1) & (PointerSlots - 1);
        }
        throw new InvalidOperationException("The native glow pointer table is full.");
    }

    private void ClearPointerTable()
    {
        byte[] zeroes = new byte[PointerTableBytes];
        Marshal.Copy(zeroes, 0, table, zeroes.Length);
    }

    private static void WriteCode(IntPtr address, byte[] bytes)
    {
        if (!VirtualProtect(address, (nuint)bytes.Length, PageExecuteReadWrite, out uint previous))
            throw new InvalidOperationException("VirtualProtect could not unlock a sustain callsite.");
        try
        {
            Marshal.Copy(bytes, 0, address, bytes.Length);
            if (!FlushInstructionCache(GetCurrentProcess(), address, (nuint)bytes.Length))
                throw new InvalidOperationException("Could not flush a sustain callsite.");
        }
        finally { VirtualProtect(address, (nuint)bytes.Length, previous, out _); }
    }

    private void Deactivate()
    {
        if (flag != IntPtr.Zero) try { Marshal.WriteByte(flag, 0); } catch { }
        Thread.MemoryBarrier();
        enabled = false;
        foreach (var state in renderers.Values)
        {
            try
            {
                if (state.Renderer != null && state.Renderer.enabled != state.WasEnabled)
                    state.Renderer.enabled = state.WasEnabled;
            }
            catch { /* Scene teardown can destroy pooled renderers. */ }
        }
        renderers.Clear();
    }

    private void Uninstall()
    {
        if (allocation == IntPtr.Zero && patches.Count == 0)
        {
            gameAssemblyBase = table = flag = IntPtr.Zero;
            return;
        }
        bool restored = true;
        foreach (var patch in patches)
        {
            try
            {
                byte[] current = new byte[patch.Replacement.Length];
                Marshal.Copy(patch.Address, current, 0, current.Length);
                if (current.SequenceEqual(patch.Original)) continue;
                if (!current.SequenceEqual(patch.Replacement))
                    throw new InvalidOperationException("Another mod changed a held-glow callsite; leaving it untouched.");
                WriteCode(patch.Address, patch.Original);
            }
            catch (Exception ex)
            {
                restored = false;
                MelonLogger.Error($"Could not restore native held-glow callsite: {ex.Message}");
            }
        }
        if (!restored)
        {
            // Keep the executable bridge alive if any patched call may still reach it.
            failed = true;
            return;
        }
        patches.Clear();
        if (allocation != IntPtr.Zero && !VirtualFree(allocation, 0, MemRelease))
            MelonLogger.Warning("Could not free the inactive native held-glow bridge.");
        allocation = table = flag = gameAssemblyBase = IntPtr.Zero;
        enabled = false;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, nuint size, uint allocationType, uint protect);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFree(IntPtr address, nuint size, uint freeType);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualProtect(IntPtr address, nuint size, uint newProtect, out uint oldProtect);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, nuint size);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
}
