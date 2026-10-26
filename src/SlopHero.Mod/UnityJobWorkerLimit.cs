using MelonLoader;
using Unity.Jobs.LowLevel.Unsafe;

namespace SlopHero.Mod;

/// <summary>
/// Experimental, reversible limit on Unity's active job-queue workers.
/// The setting is deliberately restricted by Core to offline solo gameplay.
/// </summary>
internal sealed class UnityJobWorkerLimit
{
    private bool? lastRequested;
    private bool applied;
    private bool failed;
    private int originalCount;
    private int limitedCount;
    private double eligibleSince = double.NaN;
    private double ineligibleSince = double.NaN;

    public bool IsApplied => applied;

    public void Update(bool requested, double now, bool briefSoloPause = false)
    {
        if (!requested)
        {
            eligibleSince = double.NaN;
            // A short pause should not tear down and rebuild Unity's native
            // worker pool. Hard boundaries (option off, online, scene exit)
            // pass false here and restore immediately.
            if (briefSoloPause && applied && double.IsFinite(now))
            {
                if (!double.IsFinite(ineligibleSince) || now < ineligibleSince)
                    ineligibleSince = now;
                if (now - ineligibleSince < 1.0) return;
            }
            ineligibleSince = double.NaN;
            if (lastRequested != false) Restore();
            lastRequested = false;
            return;
        }
        ineligibleSince = double.NaN;
        if (failed || lastRequested == true || !double.IsFinite(now)) return;
        if (!double.IsFinite(eligibleSince) || now < eligibleSince)
        {
            eligibleSince = now;
            return;
        }
        // Song loading briefly reports an eligible solo state before pausing
        // again. Reconfiguring Unity's worker pool for those short intervals
        // can cost more than the entire experimental saving.
        if (now - eligibleSince < .25) return;
        lastRequested = true;
        Apply();
    }

    public void Clear()
    {
        Update(false, 0);
        lastRequested = null;
        eligibleSince = double.NaN;
        ineligibleSince = double.NaN;
    }

    private void Apply()
    {
        bool baselineCaptured = false;
        try
        {
            originalCount = JobsUtility.GetJobQueueWorkerThreadCount();
            baselineCaptured = originalCount > 0;
            if (originalCount <= 1)
            {
                MelonLogger.Msg($"Unity job-worker experiment unchanged: Unity reports {originalCount} active workers.");
                return;
            }

            limitedCount = originalCount - 1;
            JobsUtility.SetJobQueueMaximumActiveThreadCount(limitedCount);
            int actualCount = JobsUtility.GetJobQueueWorkerThreadCount();
            if (actualCount != limitedCount)
                throw new InvalidOperationException($"Unity accepted {actualCount} active workers, expected {limitedCount}.");

            applied = true;
            MelonLogger.Msg($"Unity job-worker experiment active for offline solo play: {originalCount} → {actualCount} workers.");
        }
        catch (Exception ex)
        {
            if (baselineCaptured)
            {
                try { JobsUtility.SetJobQueueMaximumActiveThreadCount(originalCount); }
                catch { }
            }
            applied = false;
            failed = true;
            MelonLogger.Warning($"Unity job-worker experiment stopped safely: {ex.Message}");
        }
    }

    private void Restore()
    {
        if (!applied) return;
        try
        {
            int currentCount = JobsUtility.GetJobQueueWorkerThreadCount();
            if (currentCount == limitedCount)
            {
                JobsUtility.SetJobQueueMaximumActiveThreadCount(originalCount);
                int restoredCount = JobsUtility.GetJobQueueWorkerThreadCount();
                if (restoredCount != originalCount)
                    throw new InvalidOperationException($"Unity restored {restoredCount} active workers, expected {originalCount}.");
                MelonLogger.Msg($"Unity job-worker experiment restored the game's {restoredCount}-worker limit.");
            }
            else if (currentCount != originalCount)
            {
                // Do not overwrite a value changed by the game or another mod.
                MelonLogger.Warning($"Unity job-worker count changed externally to {currentCount}; leaving it untouched instead of restoring {originalCount}.");
            }
        }
        catch (Exception ex)
        {
            failed = true;
            MelonLogger.Warning($"Unity job-worker restore failed: {ex.Message}");
        }
        finally { applied = false; }
    }
}
