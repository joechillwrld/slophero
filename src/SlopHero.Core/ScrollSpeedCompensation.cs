namespace SlopHero.Core;

public sealed class ScrollSpeedCompensation
{
    private float original, applied;
    private bool active;

    public float Update(float current, float tempo, bool enabled, float configuredSpeed = float.NaN)
    {
        bool authoritative = float.IsFinite(configuredSpeed) && configuredSpeed > 0;
        if (authoritative) original = configuredSpeed;
        if (!enabled || !float.IsFinite(tempo) || tempo <= 0)
        {
            bool owned = active;
            bool unchanged = active && Math.Abs(current - applied) < .001f;
            active = false;
            return (unchanged || (owned && authoritative)) ? original : current;
        }
        if (!float.IsFinite(current) || current <= 0) return current;
        if (!authoritative && (!active || Math.Abs(current - applied) >= .001f)) original = current;
        applied = original / tempo;
        active = true;
        return applied;
    }
}
