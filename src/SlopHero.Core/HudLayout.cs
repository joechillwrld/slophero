namespace SlopHero.Core;

public static class HudLayout
{
    public static float Unit(float value, float fallback = 1)
        => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : fallback;

    public static (int X, int Y) ClampPosition(int x, int y, int width, int height,
        int screenWidth, int screenHeight)
        => (Math.Clamp(x, 0, Math.Max(0, screenWidth - width)),
            Math.Clamp(y, 0, Math.Max(0, screenHeight - height)));
}
