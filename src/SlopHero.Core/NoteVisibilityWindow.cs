namespace SlopHero.Core;

// Keep the native initialized lookahead. Live clip-plane values are not a
// reliable source after camera/practice reconfiguration.
public static class NoteVisibilityWindow
{
    public static double Calculate(double initializedWindow, float initializedSpeed, float targetSpeed)
    {
        if (!double.IsFinite(initializedWindow) || initializedWindow <= 0 ||
            !float.IsFinite(initializedSpeed) || initializedSpeed <= 0 ||
            !float.IsFinite(targetSpeed) || targetSpeed <= 0)
            return double.NaN;
        double scaled = initializedWindow * initializedSpeed / targetSpeed;
        return double.IsFinite(scaled) && scaled > 0 ? scaled : double.NaN;
    }
}
