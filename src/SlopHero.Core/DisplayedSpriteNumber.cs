namespace SlopHero.Core;

/// <summary>Decode SpriteFont's least-significant-digit-first display slots.</summary>
public static class DisplayedSpriteNumber
{
    public static bool TryRead(ReadOnlySpan<int> slots, out int value)
    {
        value = 0;
        long place = 1, result = 0;
        bool sawDigit = false;
        foreach (int glyph in slots)
        {
            if (glyph == -1) continue; // unused leading slot
            if (glyph == 10) continue; // thousands separator
            if (glyph is < 0 or > 9) return false;
            sawDigit = true;
            result += glyph * place;
            if (result > int.MaxValue) return false;
            place *= 10;
        }
        if (!sawDigit) return false;
        value = (int)result;
        return true;
    }
}
