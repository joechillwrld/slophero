namespace SlopHero.Core;

/// <summary>
/// Allows SlopHero's optional held-shortcut checks to run at 120 Hz without
/// changing how often the game samples instrument input.
/// </summary>
public sealed class ShortcutPollingGate
{
    private const double SampleInterval = 1d / 120d;
    private double nextSample;
    private string? restartBinding;
    private string? practiceBinding;
    private bool hasSample;
    private bool wasThrottled;

    public bool ShouldPoll(bool throttle, bool context, double now,
        string currentRestartBinding, string currentPracticeBinding)
    {
        if (!throttle)
        {
            hasSample = false;
            wasThrottled = false;
            return context;
        }

        if (!context)
        {
            hasSample = false;
            restartBinding = currentRestartBinding;
            practiceBinding = currentPracticeBinding;
            wasThrottled = throttle;
            return false;
        }

        bool bindingsChanged = !string.Equals(restartBinding, currentRestartBinding, StringComparison.Ordinal)
            || !string.Equals(practiceBinding, currentPracticeBinding, StringComparison.Ordinal);
        bool shouldPoll = !hasSample || !wasThrottled || bindingsChanged || now >= nextSample;
        if (!shouldPoll) return false;

        hasSample = true;
        wasThrottled = throttle;
        restartBinding = currentRestartBinding;
        practiceBinding = currentPracticeBinding;
        nextSample = throttle ? now + SampleInterval : now;
        return true;
    }
}
