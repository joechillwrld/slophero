namespace SlopHero.Core;

/// <summary>A stable pool-slot offset that adds simultaneous hues without frame-to-frame flicker.</summary>
public static class RainbowNoteVariation
{
    public static int PaletteIndex(int lanePhase, int poolSlot, float variety)
    {
        float amount = float.IsFinite(variety) ? Math.Clamp(variety, 0f, 1f) : 0f;
        int seed = (int)(((long)poolSlot * 37 + 17) % 60);
        if (seed < 0) seed += 60;
        int offset = (int)MathF.Round((seed - 30) * amount);
        int index = lanePhase * 5 + offset;
        index %= 60;
        return index < 0 ? index + 60 : index;
    }
}
