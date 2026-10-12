namespace SlopHero.Core;

public static class NativeRingState
{
    // Clone Hero 1.1 writes hue 0 for perfect FC and approximately 1/6 for
    // ordinary FC on guitar rings. Tolerate minor float representation noise.
    public static bool IsPerfectFc(float nativeHue) =>
        float.IsFinite(nativeHue) && Math.Abs(nativeHue) <= 0.01f;
}
