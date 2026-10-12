using System.Globalization;

namespace SlopHero.Core;

public static class ChartSpeedLabel
{
    public static string Format(float tempo)
    {
        double percent = (double)tempo * 100;
        return float.IsFinite(tempo) && tempo > 0
            ? percent.ToString("0.##", CultureInfo.InvariantCulture) + "%" : "—";
    }
}
