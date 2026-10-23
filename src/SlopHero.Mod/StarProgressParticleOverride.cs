using Il2Cpp;
using ParticleSystem = UnityEngine.ParticleSystem;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Optionally suppresses only the particle burst attached to the star-progress
/// HUD. It leaves the star bar, score, and gameplay state unchanged.
/// </summary>
internal sealed class StarProgressParticleOverride
{
    private ParticleSystem? target;
    private GameObject? targetObject;
    private IntPtr targetPointer;
    private bool originalActive;
    private bool hasTarget;
    private bool requested;
    private double nextCheck;

    public bool IsSuppressed
    {
        get
        {
            if (!hasTarget || targetObject == null) return false;
            try { return !targetObject.activeSelf; } catch { return false; }
        }
    }

    public bool IsApplyDue(bool disable, double realtime)
    {
        if (requested != disable) return true;
        if (!disable) return hasTarget;
        return realtime >= nextCheck;
    }

    public void Apply(GameManager? game, bool disable, double realtime)
    {
        requested = disable;
        if (!disable)
        {
            RestoreTarget();
            nextCheck = 0;
            return;
        }

        // Retry at a low cadence in case the HUD is still initializing or
        // Clone Hero replaces its star-progress object during a transition.
        nextCheck = realtime + 1.0;
        var current = game?.starProgress?.starParticles;
        if (current == null)
        {
            RestoreTarget();
            return;
        }

        if (!hasTarget || targetPointer != current.Pointer)
        {
            RestoreTarget();
            target = current;
            targetObject = current.gameObject;
            targetPointer = current.Pointer;
            originalActive = targetObject.activeSelf;
            hasTarget = true;
        }

        if (targetObject != null && targetObject.activeSelf)
        {
            current.Stop();
            current.Clear();
            targetObject.SetActive(false);
        }
    }

    public void Clear()
    {
        requested = false;
        nextCheck = 0;
        RestoreTarget();
    }

    private void RestoreTarget()
    {
        if (hasTarget && targetObject != null)
        {
            try { targetObject.SetActive(originalActive); } catch { }
        }
        target = null;
        targetObject = null;
        targetPointer = IntPtr.Zero;
        originalActive = false;
        hasTarget = false;
    }
}
