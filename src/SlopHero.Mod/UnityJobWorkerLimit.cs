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

    public bool IsApplied => applied;

    public void Update(bool requested)
    {
        if (failed || lastRequested == requested) return;
        lastRequested = requested;
        if (requested) Apply();
        else Restore();
    }

    public void Clear()
    {
        Update(false);
        lastRequested = null;
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
