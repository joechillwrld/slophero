using System.Diagnostics;
using System.Runtime.InteropServices;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Default-off exact-build cache for repeated sorting-order writes made by
/// GuitarFretAnimator's visible and idle helpers. The patch exists only while
/// an eligible song is active, so menus and paused play have no trampoline
/// overhead. The patch skips only redundant local renderer property writes.
/// </summary>
internal sealed class NativeFretSortingOrderCache
{
    private const int SortingOrderSetterRva = 0x28710E0;
    private const int PageSize = 4096;
    private const int CacheEntries = 512;
    private const int CacheBytes = CacheEntries * 16;
    private const int TableOffset = PageSize;
    private const int SkippedOffset = TableOffset + CacheBytes;
    private const int AllocationBytes = 4 * PageSize;
    private const uint MemCommitReserve = 0x3000, MemRelease = 0x8000;
    private const uint PageReadWrite = 0x04, PageExecuteRead = 0x20, PageExecuteReadWrite = 0x40;

    private readonly record struct Site(int Rva, string ExpectedHex, bool IsTailJump);

    // Both helpers write the same seven renderer/order pairs on every eligible
    // render. The final idle write is a tail jump, so it must stay a JMP when
    // redirected; the shared stub's RET correctly returns to the original
    // caller on a hit and tail-jumps to Unity's setter on a miss.
    private static readonly Site[] Sites =
    {
        new(0xA895F, "E87C877C02", false),
        new(0xA897C, "E85F877C02", false),
        new(0xA8999, "E842877C02", false),
        new(0xA89B3, "E828877C02", false),
        new(0xA89D0, "E80B877C02", false),
        new(0xA89E9, "E8F2867C02", false),
        new(0xA8A02, "E8D9867C02", false),
        new(0xA9F31, "E8AA717C02", false),
        new(0xA9F4E, "E88D717C02", false),
        new(0xA9F67, "E874717C02", false),
        new(0xA9F7D, "E85E717C02", false),
        new(0xA9F96, "E845717C02", false),
        new(0xA9FAF, "E82C717C02", false),
        new(0xA9FCD, "E90E717C02", true)
    };

    // RCX (renderer), EDX (order), and R8 (the always-null method info) remain
    // intact on misses. Pointer/order collisions fail open and call the stock
    // setter. A hit skips only an identical setter for the same renderer.
    private const string CacheStubHex =
        "49BA887766554433221141803A00743F4989CA49C1EA044181E2FF01000049C1E204" +
        "49BB99887766554433224D01DA49390A751441395208750E49BAAABBCCDDEEFF0011" +
        "49FF02C34189520849890A49BBAA9988776655443341FFE3";

    private const long ActiveFlagMarker = 0x1122334455667788;
    private const long TableMarker = 0x2233445566778899;
    private const long SkippedMarker = 0x1100FFEEDDCCBBAA;
    private const long OriginalMarker = 0x33445566778899AA;

    private sealed record Patch(IntPtr Address, byte[] Original, byte[] Replacement);
    private readonly List<Patch> patches = new();
    private IntPtr allocation, table, activeFlag, skippedCounter;
    private bool active, failed;

    internal bool IsActive => active;
    internal bool IsInstalled => allocation != IntPtr.Zero && patches.Count == Sites.Length;

    internal void Update(bool requested, bool eligibleGameplaySong)
    {
        if (!requested || !eligibleGameplaySong)
        {
            Clear();
            return;
        }
        if (failed) return;
        try
        {
            if (!IsInstalled) Install();
            if (failed || !IsInstalled) return;
            if (active) return;

            // Begin every activation empty. This also makes scene/object reuse
            // safe if the component instance survived a song transition.
            Marshal.Copy(new byte[CacheBytes + sizeof(long)], 0, table, CacheBytes + sizeof(long));
            Thread.MemoryBarrier();
            Marshal.WriteByte(activeFlag, 1);
            active = true;
            MelonLogger.Msg("Sloptifine fret sorting-order cache active for this song.");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Fret sorting-order cache stopped: {ex.Message}");
            Clear();
            failed = true;
        }
    }

    internal void Clear()
    {
        if (activeFlag != IntPtr.Zero)
        {
            try { Marshal.WriteByte(activeFlag, 0); }
            catch (Exception ex) { MelonLogger.Warning($"Could not deactivate fret sorting-order cache: {ex.Message}"); }
        }
        Thread.MemoryBarrier();
        active = false;
        if (skippedCounter != IntPtr.Zero)
        {
            try
            {
                long skipped = Marshal.ReadInt64(skippedCounter);
                if (skipped != 0) MelonLogger.Msg($"Fret sorting-order cache skipped {skipped:N0} repeated Unity setter call(s).");
            }
            catch { }
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
                    throw new InvalidOperationException("Another patch changed a fret-sorting callsite; leaving that site untouched.");
                WriteCode(patch.Address, patch.Original);
            }
            catch (Exception ex)
            {
                restored = false;
                MelonLogger.Error($"Could not restore fret-sorting callsite: {ex.Message}");
            }
        }
        if (!restored)
        {
            // A surviving site may still enter our stub. Keep its executable
            // allocation alive and disabled rather than freeing live code.
            failed = true;
            return;
        }

        patches.Clear();
        if (allocation != IntPtr.Zero && !VirtualFree(allocation, 0, MemRelease))
            MelonLogger.Warning("Could not free the inactive fret sorting-order bridge.");
        allocation = table = activeFlag = skippedCounter = IntPtr.Zero;
        failed = false;
    }

    private void Install()
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
            throw new PlatformNotSupportedException("The exact-build fret sorting patch requires Windows x64.");

        using var process = Process.GetCurrentProcess();
        var module = process.Modules.Cast<ProcessModule>().FirstOrDefault(m =>
            string.Equals(m.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase));
        if (module == null) throw new DllNotFoundException("GameAssembly.dll is not loaded.");
        IntPtr gameAssemblyBase = module.BaseAddress;

        // Validate all fourteen sites before changing any byte.
        foreach (var site in Sites)
        {
            byte[] expected = Convert.FromHexString(site.ExpectedHex);
            IntPtr address = IntPtr.Add(gameAssemblyBase, site.Rva);
            byte[] actual = new byte[expected.Length];
            Marshal.Copy(address, actual, 0, actual.Length);
            if (!actual.SequenceEqual(expected))
                throw new InvalidOperationException($"GuitarFretAnimator sorting callsite 0x{site.Rva:X} does not match the supplied game build.");
            long target = site.Rva + 5L + BitConverter.ToInt32(actual, 1);
            if (target != SortingOrderSetterRva)
                throw new InvalidOperationException($"Callsite 0x{site.Rva:X} no longer targets SpriteRenderer.set_sortingOrder.");
            if (site.IsTailJump != (actual[0] == 0xE9))
                throw new InvalidOperationException($"Callsite 0x{site.Rva:X} has an unexpected call/jump form.");
        }

        long candidate = (gameAssemblyBase.ToInt64() + module.ModuleMemorySize + 0xFFFFL) & ~0xFFFFL;
        for (int i = 0; i < 2048 && allocation == IntPtr.Zero; i++)
        {
            long address = candidate + i * 0x10000L;
            if (Math.Abs(address - (gameAssemblyBase.ToInt64() + Sites[0].Rva)) > int.MaxValue) break;
            allocation = VirtualAlloc(new IntPtr(address), AllocationBytes, MemCommitReserve, PageReadWrite);
        }
        if (allocation == IntPtr.Zero)
            throw new InvalidOperationException("No nearby executable space was available for the fret sorting bridge.");

        table = IntPtr.Add(allocation, TableOffset);
        skippedCounter = IntPtr.Add(allocation, SkippedOffset);
        activeFlag = IntPtr.Add(skippedCounter, sizeof(long));
        IntPtr setter = IntPtr.Add(gameAssemblyBase, SortingOrderSetterRva);
        byte[] stub = Convert.FromHexString(CacheStubHex);
        ReplaceAddress(stub, ActiveFlagMarker, activeFlag);
        ReplaceAddress(stub, TableMarker, table);
        ReplaceAddress(stub, SkippedMarker, skippedCounter);
        ReplaceAddress(stub, OriginalMarker, setter);
        Marshal.Copy(stub, 0, allocation, stub.Length);
        if (!VirtualProtect(allocation, PageSize, PageExecuteRead, out _))
            throw new InvalidOperationException("Could not protect the fret sorting bridge as executable.");
        if (!FlushInstructionCache(GetCurrentProcess(), allocation, (nuint)stub.Length))
            throw new InvalidOperationException("Could not flush fret sorting bridge instructions.");

        // Save all originals before writing, so partial installation can roll
        // back through Clear(). Calls remain calls; the stock tail jump remains
        // a jump so Windows x64 stack/return behavior is preserved.
        foreach (var site in Sites)
        {
            IntPtr address = IntPtr.Add(gameAssemblyBase, site.Rva);
            byte[] original = Convert.FromHexString(site.ExpectedHex);
            long displacement = allocation.ToInt64() - (address.ToInt64() + 5);
            if (displacement < int.MinValue || displacement > int.MaxValue)
                throw new InvalidOperationException("The fret sorting bridge is outside relative branch range.");
            byte[] replacement = new byte[5];
            replacement[0] = site.IsTailJump ? (byte)0xE9 : (byte)0xE8;
            BitConverter.GetBytes((int)displacement).CopyTo(replacement, 1);
            patches.Add(new Patch(address, original, replacement));
        }
        foreach (var patch in patches) WriteCode(patch.Address, patch.Replacement);
        MelonLogger.Msg("Fret sorting-order cache prepared at fourteen exact GuitarFretAnimator setter callsites.");
    }

    private static void ReplaceAddress(byte[] code, long marker, IntPtr replacement)
    {
        byte[] markerBytes = BitConverter.GetBytes(marker);
        int index = -1;
        for (int i = 0; i <= code.Length - markerBytes.Length; i++)
        {
            if (!code.AsSpan(i, markerBytes.Length).SequenceEqual(markerBytes)) continue;
            if (index >= 0) throw new InvalidOperationException("Ambiguous fret bridge marker.");
            index = i;
        }
        if (index < 0) throw new InvalidOperationException("Fret bridge marker is missing.");
        BitConverter.GetBytes(replacement.ToInt64()).CopyTo(code, index);
    }

    private static void WriteCode(IntPtr address, byte[] bytes)
    {
        if (!VirtualProtect(address, (nuint)bytes.Length, PageExecuteReadWrite, out uint previous))
            throw new InvalidOperationException("VirtualProtect could not unlock a fret sorting callsite.");
        try
        {
            Marshal.Copy(bytes, 0, address, bytes.Length);
            if (!FlushInstructionCache(GetCurrentProcess(), address, (nuint)bytes.Length))
                throw new InvalidOperationException("Could not flush a fret sorting callsite.");
        }
        finally { VirtualProtect(address, (nuint)bytes.Length, previous, out _); }
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
