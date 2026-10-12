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
        // Keep the raw renderer fields at the live profile setting. Practice
        // tempo changes chart timing; multiplying or dividing this field causes
        // the highway to run too fast or too slowly. The profile is read live,
        // so this works at any configured note speed.
        float target = original;
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
