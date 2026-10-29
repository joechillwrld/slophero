namespace SlopHero.Core;

/// <summary>Shape a rainbow channel without crushing intermediate hues at high settings.</summary>
public static class RainbowColorCurve
{
    public static float Shape(float channel, float saturation, float contrast, float brightness)
    {
        channel = Math.Clamp(channel, 0f, 1f);
        // HSV at full saturation is already on the RGB gamut boundary. The
        // old luminance expansion pushed middle channels below zero, so a
        // bright orange could collapse into plain red. Shape those channels
        // within the gamut instead of clipping them away.
        if (saturation > 1f)
            channel = MathF.Pow(channel, 1f + (saturation - 1f) * .25f);
        if (contrast > 1f)
            channel = MathF.Pow(channel, 1f + (contrast - 1f) * .6f);
        else if (contrast < 1f)
            channel = .5f + (channel - .5f) * contrast;

        if (brightness <= 1f) return channel * brightness;
        // A soft shoulder brightens middle channels without turning the
        // entire rainbow into white or a handful of clipped primaries.
        return (1f - MathF.Pow(1f - channel, brightness))
            * (1f + (brightness - 1f) * .2f);
    }
}
