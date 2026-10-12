namespace SlopHero.Core;

public sealed class ScrollSpeedCompensation
{
    private float original, applied;
    private bool active;

    public float Update(float current, float tempo, bool enabled)
    {
        if (!enabled || !float.IsFinite(tempo) || tempo <= 0)
        {
            bool unchanged = active && Math.Abs(current - applied) < .001f;
            active = false;
            return unchanged ? original : current;
        }
        if (!float.IsFinite(current) || current <= 0) return current;
        if (!active || Math.Abs(current - applied) >= .001f) original = current;
        applied = original / tempo;
        active = true;
        return applied;
    }
}
