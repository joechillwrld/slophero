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
        // `tempo` is the measured rate of GameManager.songTimeVideoTime against
        // real time, not the practice menu percentage. Native note, beat, and
        // highway update paths multiply chart-time deltas by these renderer
        // speeds, so inverse measured clock rate holds world motion constant.
        float target = original / tempo;
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
