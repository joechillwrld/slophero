using System.Diagnostics;
using System.Runtime.InteropServices;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Exact-build, default-off cache for one regular upcoming-tail output.
/// A repeat skips its GetPropertyBlock/SetFloat/SetPropertyBlock sequence at
/// the producer; other tail submissions invalidate the renderer slot.
/// </summary>
internal sealed class NativeRegularSustainCache
{
    private const int SetPropertyBlockRva = 0x2870DE0;
    private const int GetPropertyBlockRva = 0x2870D50;
    private const int EarlyCheckRva = 0x1156B10;
    private const int AfterRegularPropertyBlockRva = 0x1156B49;
    private const int CacheEntries = 128;
    private const int CacheBytes = CacheEntries * 16;
    private const int PageSize = 4096;
    private const int AllocationBytes = 2 * PageSize;
    private const int InvalidationStubOffset = 256;
    private const int EarlyCheckStubOffset = 512;
    private const uint MemCommitReserve = 0x3000, MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04, PageExecuteRead = 0x20, PageExecuteReadWrite = 0x40;

    private readonly record struct Site(int Rva, string ExpectedHex, bool Cache);
    private static readonly Site[] Sites =
    {
        new(EarlyCheckRva, "E83BA27101", false), // skip whole unchanged regular sequence
        new(0x1156B44, "E897A27101", true),  // regular upcoming tail
        new(0x1155B9D, "E83EB27101", false), // held tail body
        new(0x1155C52, "E889B17101", false), // held tail glow
        new(0x11562D0, "E80BAB7101", false), // other regular tail path
        new(0x11565E7, "E8F4A77101", false), // other held/open tail path
        new(0x1156E74, "E8679F7101", false)  // open upcoming tail
    };

    // Pure x64 stubs assembled from the documented register state at the
    // exact callsites. They use only volatile GPRs and preserve RCX/RDX/R8
    // for the original IL2CPP calls. The regular path compares XMM12's length
    // against a direct-mapped renderer slot; collisions are safe misses.
    private const string CacheStubHex =
        "49BA887766554433221141803A0074384989CA49C1EA044183E27F49C1E204" +
        "49BB99887766554433224D01DA49390A750B66440F7EE0413942087419" +
        "49890A66440F7EE04189420849BBAA9988776655443341FFE349FF8308080000C3";
    private const string InvalidateStubHex =
        "49BA887766554433221141803A0074284989CA49C1EA044183E27F49C1E204" +
        "49BB99887766554433224D01DA49390A750749C70200000000" +
        "49BBAA9988776655443341FFE3";
    // The early check only reads slots committed at the regular setter. A
    // repeat changes its call return address to the first instruction after
    // that setter, preserving the producer's color/counter and loop work.
    // On a miss, it tail-calls the original GetPropertyBlock.
    private const string EarlyCheckStubHex =
        "49BA887766554433221141803A0074484989CA49C1EA044183E27F49C1E204" +
        "49BB99887766554433224D01DA49390A752766450F7EE345395A08751C" +
        "49BBAABBCCDDEEFF001149FF0349BB80706050403020104C891C24C3" +
        "49BBAA9988776655443341FFE3";
    private const long FlagMarker = 0x1122334455667788;
    private const long TableMarker = 0x2233445566778899;
    private const long OriginalMarker = 0x33445566778899AA;
    private const long SkipTargetMarker = 0x1020304050607080;

    private sealed record Patch(IntPtr Address, byte[] Original, byte[] Replacement);
    private readonly List<Patch> patches = new();
    private IntPtr allocation, table, flag, skipped, gameAssemblyBase;
    private bool enabled, failed;

    internal bool IsInstalled => allocation != IntPtr.Zero && patches.Count == Sites.Length;
    internal bool IsActive => enabled;

    internal void Prepare(bool requested)
    {
        if (!requested || IsInstalled || failed) return;
        try { Install(); }
        catch (Exception ex)
        {
            Clear();
            failed = true;
            MelonLogger.Warning($"Sloptifine native regular-sustain cache did not install: {ex.Message}");
        }
    }

    internal void Update(bool requested, bool eligible)
    {
        if (!requested)
        {
            if (allocation != IntPtr.Zero || failed) Clear();
            return;
        }
        if (!eligible)
        {
            // Remove the callsite detours outside active play as well as
            // clearing the cache. A disabled per-call trampoline would cost
            // more than it can save in menus, pause screens, and online lobbies.
            if (allocation != IntPtr.Zero || patches.Count != 0) Clear();
            return;
        }
        if (failed) return;
        if (!IsInstalled && !failed) Prepare(true);
        if (failed || !IsInstalled) return;
        if (enabled) return;
        try
        {
            Marshal.Copy(new byte[CacheBytes + 16], 0, table, CacheBytes + 16);
            Marshal.WriteByte(flag, 1);
            enabled = true;
            MelonLogger.Msg("Sloptifine native regular-sustain cache active for this song.");
        }
        catch (Exception ex)
        {
            failed = true;
            MelonLogger.Error($"Could not enable native regular-sustain cache: {ex.Message}");
            Clear();
            failed = true;
        }
    }

    internal void Disable()
    {
        if (!enabled) return;
        enabled = false;
        Marshal.WriteByte(flag, 0);
        long saved = Marshal.ReadInt64(skipped);
        MelonLogger.Msg($"Sloptifine native regular-sustain cache skipped {saved} repeat property-block sequence(s).");
    }

    internal void Clear()
    {
        if (allocation == IntPtr.Zero && patches.Count == 0)
        {
            failed = false;
            return;
        }
        try { Disable(); }
        catch (Exception ex) { MelonLogger.Warning($"Sustain-cache disable failed: {ex.Message}"); }
        bool restored = true;
        foreach (var patch in patches)
        {
            try
            {
                byte[] current = new byte[patch.Replacement.Length];
                Marshal.Copy(patch.Address, current, 0, current.Length);
                if (current.SequenceEqual(patch.Original)) continue;
                if (!current.SequenceEqual(patch.Replacement))
                    throw new InvalidOperationException("Another mod changed a native sustain callsite; leaving it untouched.");
                WriteCode(patch.Address, patch.Original);
            }
            catch (Exception ex)
            {
                restored = false;
                MelonLogger.Error($"Could not restore native sustain callsite: {ex.Message}");
            }
        }
        if (!restored)
        {
            // Keep the native stubs and table alive if any patched callsite
            // might still reach them. Never free executable code underneath it.
            failed = true;
            return;
        }
        patches.Clear();
        if (allocation != IntPtr.Zero && !VirtualFree(allocation, 0, MemRelease))
            MelonLogger.Warning("Could not free the inactive native sustain bridge.");
        allocation = table = flag = skipped = gameAssemblyBase = IntPtr.Zero;
        enabled = failed = false;
    }

    private void Install()
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
            throw new PlatformNotSupportedException("This exact-build native patch requires Windows x64.");
        using var process = Process.GetCurrentProcess();
        var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m =>
            string.Equals(m.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase));
        if (module == null) throw new DllNotFoundException("GameAssembly.dll is not loaded.");
        gameAssemblyBase = module.BaseAddress;

        // Validate every direct call before changing any. BuildGuard has
        // already verified the full game and metadata hashes at mod startup.
        foreach (var site in Sites)
        {
            byte[] original = Convert.FromHexString(site.ExpectedHex);
            var address = IntPtr.Add(gameAssemblyBase, site.Rva);
            byte[] actual = new byte[original.Length];
            Marshal.Copy(address, actual, 0, actual.Length);
            if (!actual.SequenceEqual(original))
                throw new InvalidOperationException($"Callsite 0x{site.Rva:X} does not match this game build.");
        }

        long candidate = (gameAssemblyBase.ToInt64() + module.ModuleMemorySize + 0xFFFFL) & ~0xFFFFL;
        for (int i = 0; i < 2048 && allocation == IntPtr.Zero; i++)
        {
            long address = candidate + i * 0x10000L;
            if (Math.Abs(address - (gameAssemblyBase.ToInt64() + Sites[0].Rva)) > int.MaxValue)
                break;
            allocation = VirtualAlloc(new IntPtr(address), AllocationBytes, MemCommitReserve, PageReadWrite);
        }
        if (allocation == IntPtr.Zero)
            throw new InvalidOperationException("No executable bridge space was available near GameAssembly.dll.");

        table = IntPtr.Add(allocation, PageSize);
        flag = IntPtr.Add(table, CacheBytes);
        skipped = IntPtr.Add(flag, 8);
        var originalTarget = IntPtr.Add(gameAssemblyBase, SetPropertyBlockRva);
        WriteStub(allocation, CacheStubHex, originalTarget);
        WriteStub(IntPtr.Add(allocation, InvalidationStubOffset), InvalidateStubHex, originalTarget);
        WriteEarlyCheckStub(IntPtr.Add(allocation, EarlyCheckStubOffset));
        if (!VirtualProtect(allocation, PageSize, PageExecuteRead, out _))
            throw new InvalidOperationException("Could not protect the native sustain bridge as executable.");
        if (!FlushInstructionCache(GetCurrentProcess(), allocation, PageSize))
            throw new InvalidOperationException("Could not flush the native sustain bridge instructions.");

        // Register all sites before writing any. Partial installation can
        // then be unwound using their saved original instruction bytes.
        foreach (var site in Sites)
        {
            IntPtr call = IntPtr.Add(gameAssemblyBase, site.Rva);
            byte[] original = Convert.FromHexString(site.ExpectedHex);
            IntPtr stub = site.Rva == EarlyCheckRva ? IntPtr.Add(allocation, EarlyCheckStubOffset)
                : site.Cache ? allocation : IntPtr.Add(allocation, InvalidationStubOffset);
            long displacement = stub.ToInt64() - (call.ToInt64() + 5);
            if (displacement < int.MinValue || displacement > int.MaxValue)
                throw new InvalidOperationException("The sustain bridge is outside the relative-call range.");
            byte[] replacement = new byte[5];
            replacement[0] = 0xE8;
            BitConverter.GetBytes((int)displacement).CopyTo(replacement, 1);
            patches.Add(new Patch(call, original, replacement));
        }
        foreach (var patch in patches) WriteCode(patch.Address, patch.Replacement);
        MelonLogger.Msg("Sloptifine native regular-sustain cache prepared at seven exact game callsites.");
    }

    private void WriteStub(IntPtr destination, string hex, IntPtr originalTarget)
    {
        byte[] bytes = Convert.FromHexString(hex);
        ReplaceAddress(bytes, FlagMarker, flag);
        ReplaceAddress(bytes, TableMarker, table);
        ReplaceAddress(bytes, OriginalMarker, originalTarget);
        Marshal.Copy(bytes, 0, destination, bytes.Length);
    }

    private void WriteEarlyCheckStub(IntPtr destination)
    {
        byte[] bytes = Convert.FromHexString(EarlyCheckStubHex);
        ReplaceAddress(bytes, FlagMarker, flag);
        ReplaceAddress(bytes, TableMarker, table);
        ReplaceAddress(bytes, 0x1100FFEEDDCCBBAA, skipped);
        ReplaceAddress(bytes, SkipTargetMarker, IntPtr.Add(gameAssemblyBase, AfterRegularPropertyBlockRva));
        ReplaceAddress(bytes, OriginalMarker, IntPtr.Add(gameAssemblyBase, GetPropertyBlockRva));
        Marshal.Copy(bytes, 0, destination, bytes.Length);
    }

    private static void ReplaceAddress(byte[] code, long marker, IntPtr replacement)
    {
        byte[] oldBytes = BitConverter.GetBytes(marker);
        int match = -1;
        for (int i = 0; i <= code.Length - oldBytes.Length; i++)
        {
            if (!code.AsSpan(i, oldBytes.Length).SequenceEqual(oldBytes)) continue;
            if (match >= 0) throw new InvalidOperationException("Ambiguous native bridge marker.");
            match = i;
        }
        if (match < 0) throw new InvalidOperationException("Native bridge marker is missing.");
        BitConverter.GetBytes(replacement.ToInt64()).CopyTo(code, match);
    }

    private static void WriteCode(IntPtr address, byte[] bytes)
    {
        if (!VirtualProtect(address, (nuint)bytes.Length, PageExecuteReadWrite, out uint previous))
            throw new InvalidOperationException("VirtualProtect could not unlock a sustain callsite.");
        try
        {
            Marshal.Copy(bytes, 0, address, bytes.Length);
            if (!FlushInstructionCache(GetCurrentProcess(), address, (nuint)bytes.Length))
                throw new InvalidOperationException("Could not flush a native sustain callsite.");
        }
        finally
        {
            VirtualProtect(address, (nuint)bytes.Length, previous, out _);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, nuint size, uint allocationType, uint protect);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFree(IntPtr address, nuint size, uint freeType);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualProtect(IntPtr address, nuint size, uint newProtect, out uint oldProtect);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, nuint size);
}
