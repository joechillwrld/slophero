namespace SlopHero.Core;

public sealed class ScrollSpeedCompensation
{
    private float original, applied;
    private bool active;

    public float Update(float current, float tempo, bool enabled, float configuredSpeed = float.NaN)
    {
        // Invalid supplied settings must never become a billion-unit scroll speed.
        if (!float.IsNaN(configuredSpeed) && !ConfiguredNoteSpeed.IsPlausible(configuredSpeed)) enabled = false;
        bool authoritative = ConfiguredNoteSpeed.IsPlausible(configuredSpeed);
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
        // Clone Hero's practice path reduces the raw renderer speed as tempo
        // rises. Scale the raw value with tempo to keep visible highway motion
        // at the configured profile speed. `original` is refreshed from the
        // live profile setting above, so this supports every profile speed.
        float target = original * tempo;
        if (!float.IsFinite(target) || target < .01f || target > 10000)
        {
            bool owned = active;
            active = false;
            return owned ? original : current;
        }
        applied = target;
        active = true;
        return applied;
    }
}
