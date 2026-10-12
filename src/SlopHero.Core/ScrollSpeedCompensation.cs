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
        if (!enabled || !float.IsFinite(tempo) || tempo < .1f || tempo > 4)
        {
            bool owned = active;
            bool unchanged = active && Math.Abs(current - applied) < .001f;
            active = false;
            return (unchanged || (owned && authoritative)) ? original : current;
        }
        if (!float.IsFinite(current) || current <= 0) return current;
        if (!authoritative && (!active || Math.Abs(current - applied) >= .001f)) original = current;
        // Native speed inspection shows practice tempo only weakly affects the
        // rendered highway. A small logarithmic correction addresses the slight
        // slowdown observed at 135% without the large swings from using tempo
        // directly. The profile is read live, so every configured speed works.
        float target = original * MathF.Pow(tempo, .1f);
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
