using MelonLoader;
using UnityEngine.Rendering;

namespace SlopHero.Mod;

/// <summary>
/// Default-off solo experiment. Clone Hero currently uses two simulation
/// updates per scheduled render. This changes only Unity's render interval;
/// it never writes the song clock, time scale, or input polling configuration.
/// </summary>
internal sealed class RenderEveryUpdatePrototype
{
    private int originalInterval;
    private bool active, failed;

    internal bool IsActive => active;

    internal void Update(bool requested, bool eligible)
    {
        if (!requested || !eligible)
        {
            Restore();
            if (!requested) failed = false;
            return;
        }
        if (failed) return;
        try
        {
            int current = OnDemandRendering.renderFrameInterval;
            if (active)
            {
                // Another game/mod setting has taken ownership. Do not fight
                // it every frame or restore an obsolete value later.
                if (current != 1)
                {
                    active = false;
                    failed = true;
                    MelonLogger.Warning($"Sloptifine render-every-update test yielded to interval {current}.");
                }
                return;
            }
            if (current != 2)
            {
                failed = true;
                MelonLogger.Msg($"Sloptifine render-every-update test skipped: game interval is {current}, expected 2.");
                return;
            }
            originalInterval = current;
            active = true;
            OnDemandRendering.renderFrameInterval = 1;
            if (OnDemandRendering.renderFrameInterval != 1)
                throw new InvalidOperationException("Unity did not accept render interval 1.");
            MelonLogger.Msg("Sloptifine render-every-update test active: interval 2 → 1 for offline solo play.");
        }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            MelonLogger.Warning($"Sloptifine render-every-update test stopped: {ex.Message}");
        }
    }

    internal void Restore()
    {
        if (!active) return;
        active = false;
        try
        {
            if (OnDemandRendering.renderFrameInterval == 1)
            {
                OnDemandRendering.renderFrameInterval = originalInterval;
                MelonLogger.Msg($"Sloptifine render interval restored to {originalInterval}.");
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"Sloptifine could not restore the render interval: {ex.Message}");
        }
    }

    internal void Clear()
    {
        Restore();
        failed = false;
    }
}
