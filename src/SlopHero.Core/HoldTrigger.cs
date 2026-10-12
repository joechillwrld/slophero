namespace SlopHero.Core;

/// <summary>Uses monotonic, unscaled time. Fires once per hold and requires release
/// after a scene change, pause, or unavailable gameplay state.</summary>
public sealed class HoldTrigger
{
    private double? startedAt;
    private double? previousTime;
    private bool latched;

    public bool Update(bool pressed, bool allowed, double now, double holdSeconds)
    {
        if (!double.IsFinite(now)) throw new ArgumentOutOfRangeException(nameof(now));
        if (!double.IsFinite(holdSeconds) || holdSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(holdSeconds));
        if (previousTime.HasValue && now < previousTime.Value)
        {
            startedAt = null;
            latched = pressed;
        }
        previousTime = now;
        if (!pressed)
        {
            startedAt = null;
            latched = false;
            return false;
        }
        if (!allowed)
        {
            startedAt = null;
            latched = true;
            return false;
        }
        if (latched) return false;
        startedAt ??= now;
        if (now - startedAt.Value < holdSeconds) return false;
        latched = true;
        return true;
    }

    public void RequireRelease()
    {
        startedAt = null;
        latched = true;
        previousTime = null;
    }
}
