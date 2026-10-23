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

    internal bool IsPaused => scanner != null && originalEnabled && !scanner.enabled;

    internal void Apply(bool pause)
    {
        if (!pause)
        {
            Restore();
            return;
        }

        CheatManager? current = CheatManager.instance;
        if (current == null)
        {
            Restore();
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
    }
}
