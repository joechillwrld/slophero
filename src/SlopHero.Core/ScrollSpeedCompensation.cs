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
        // Clone Hero applies practice tempo to the raw renderer speed. Counter
        // that multiplier so the visible scroll speed remains at the profile
        // value. `original` is refreshed from the live profile setting above,
        // so this works for any configured note speed, not a particular default.
        float target = original / tempo;
        if (!float.IsFinite(target) || target <= 0 || target > 10000)
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
