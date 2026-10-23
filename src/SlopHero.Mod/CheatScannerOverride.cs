using Il2Cpp;

namespace SlopHero.Mod;

/// <summary>
/// Reversibly pauses the persistent cheat-code scanner during an active solo
/// chart. Its v1.1 OnEnable/OnDisable methods are empty, so disabling the
/// component leaves registered codes intact for the next menu or pause.
/// </summary>
internal sealed class CheatScannerOverride
{
    private CheatManager? scanner;
    private bool originalEnabled;
    private bool lookupAttempted;

    internal bool IsAvailable => scanner != null && scanner.Pointer != IntPtr.Zero;
    internal string Status => !IsAvailable ? "missing" : !originalEnabled ? "already-disabled" : scanner!.enabled ? "running" : "paused";

    internal void Apply(bool pause)
    {
        if (!pause)
        {
            Restore();
            return;
        }

        CheatManager? current = CheatManager.instance;
        if (current == null || current.Pointer == IntPtr.Zero)
        {
            // The game's static singleton can be temporarily unset during a
            // scene transition (or by another mod). Keep using our cached
            // instance when possible, otherwise do one scene lookup for this
            // active-song interval instead of rescanning on every game tick.
            current = scanner;
            if ((current == null || current.Pointer == IntPtr.Zero) && !lookupAttempted)
            {
                lookupAttempted = true;
                current = UnityEngine.Object.FindObjectOfType<CheatManager>();
            }
        }
        if (current == null)
        {
            scanner = null;
            originalEnabled = false;
            return;
        }
        if (scanner == null || scanner.Pointer != current.Pointer)
        {
            Restore();
            scanner = current;
            originalEnabled = current.enabled;
        }
        if (originalEnabled && current.enabled) current.enabled = false;
    }

    internal void Restore()
    {
        if (scanner != null)
        {
            try { if (scanner.enabled != originalEnabled) scanner.enabled = originalEnabled; }
            catch { /* The persistent object may have been destroyed during shutdown. */ }
        }
        scanner = null;
        originalEnabled = false;
        lookupAttempted = false;
    }
}
