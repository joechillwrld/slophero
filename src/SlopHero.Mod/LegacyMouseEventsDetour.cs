using System.Diagnostics;
using System.Runtime.InteropServices;
using MonoMod.RuntimeDetour;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Default-off exact-build experiment. While active, detour Unity's
/// SendMouseEvents.DoSendMouseEvents to a no-op; undo it outside active song
/// play so vanilla legacy callbacks run normally in menus and while paused.
/// </summary>
internal sealed class LegacyMouseEventsDetour
{
    private const int DoSendMouseEventsRva = 0x28C4CD0;
    private static readonly byte[] ExpectedPrologue =
    {
        0x40, 0x55, 0x53, 0x41, 0x55, 0x48, 0x8D, 0x6C,
        0x24, 0xA0, 0x48, 0x81, 0xEC, 0x60, 0x01, 0x00, 0x00
    };

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SendMouseEventsDelegate(int skipRtCameras, IntPtr methodInfo);

    // IL2CPP's x64 static method signature includes the hidden MethodInfo*.
    // Keep the delegate rooted for as long as MonoMod holds its function ptr.
    private static readonly SendMouseEventsDelegate SkipMouseEvents = static (_, _) => { };

    private NativeDetour? detour;
    private bool failed;

    internal bool IsInstalled => detour != null;
    internal bool IsSuppressed => detour?.IsApplied == true;

    // Construct and validate the exact-build detour while Gameplay is still
    // initializing, then leave Unity's original mouse path active. The first
    // eligible song update only needs to apply the already-created detour.
    internal void Prepare(bool requested)
    {
        if (!requested || detour != null || failed) return;
        try
        {
            long started = Stopwatch.GetTimestamp();
            Install(logActive: false);
            Disable();
            if (failed || detour?.IsApplied == true)
                throw new InvalidOperationException("The prepared mouse detour could not restore Unity's original path.");
            MelonLogger.Msg($"Sloptifine native legacy-mouse detour prepared before song play in {(Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency:0.00} ms.");
        }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            MelonLogger.Warning($"Sloptifine native legacy-mouse preparation stopped safely: {ex.Message}");
        }
    }

    internal void Update(bool requested, bool gameplayActive)
    {
        if (!requested)
        {
            if (detour != null || failed) Clear();
            return;
        }

        if (!gameplayActive)
        {
            Disable();
            return;
        }

        if (failed) return;
        try
        {
            if (detour == null)
                Install(logActive: true);
            else if (!detour.IsApplied)
            {
                long started = Stopwatch.GetTimestamp();
                detour.Apply();
                MelonLogger.Msg($"Sloptifine native legacy-mouse detour active in {(Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency:0.00} ms.");
            }
        }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            MelonLogger.Warning($"Sloptifine native legacy-mouse experiment stopped safely: {ex.Message}");
        }
    }

    internal void Disable()
    {
        if (detour?.IsApplied != true) return;
        try { detour.Undo(); }
        catch (Exception ex)
        {
            failed = true;
            MelonLogger.Error($"Sloptifine could not restore Unity legacy mouse callbacks: {ex.Message}");
        }
    }

    internal void Clear()
    {
        Restore();
        if (detour == null) failed = false;
    }

    private void Install(bool logActive)
    {
        if (IntPtr.Size != 8 || !OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The exact-build native hook requires Windows x64.");

        using var process = Process.GetCurrentProcess();
        ProcessModule? gameAssembly = process.Modules
            .Cast<ProcessModule>()
            .FirstOrDefault(module => string.Equals(module.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase));
        if (gameAssembly == null)
            throw new DllNotFoundException("Could not find GameAssembly.dll in the running process.");

        IntPtr target = IntPtr.Add(gameAssembly.BaseAddress, DoSendMouseEventsRva);
        var current = new byte[ExpectedPrologue.Length];
        Marshal.Copy(target, current, 0, current.Length);
        if (!current.SequenceEqual(ExpectedPrologue))
            throw new InvalidOperationException("The SendMouseEvents native prologue did not match the supplied v1.1.0.6142 binary.");

        IntPtr replacement = Marshal.GetFunctionPointerForDelegate(SkipMouseEvents);
        // MonoMod applies the detour in its constructor. We create it only on
        // active gameplay; Disable() undoes it on pause/menu/focus transitions.
        detour = new NativeDetour(target, replacement);
        if (!detour.IsApplied)
            throw new InvalidOperationException("MonoMod created the detour but did not apply it.");

        if (logActive)
            MelonLogger.Msg("Sloptifine native legacy-mouse detour active for the exact supplied game build.");
    }

    private void Restore()
    {
        if (detour == null) return;
        try
        {
            if (detour.IsApplied) detour.Undo();
            detour.Dispose();
            detour = null;
        }
        catch (Exception ex)
        {
            // Keep the object and delegate rooted if unpatching fails.
            failed = true;
            MelonLogger.Error($"Sloptifine native legacy-mouse detour cleanup failed; hook state may remain active: {ex.Message}");
        }
    }
}
