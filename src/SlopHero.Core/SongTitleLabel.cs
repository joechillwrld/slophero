namespace SlopHero.Core;

public static class SongTitleLabel
{
    public static string Format(string title, float tempo, bool showSpeed)
    {
        if (!showSpeed || string.IsNullOrEmpty(title)) return title;
        return $"{title} ({ChartSpeedLabel.Format(tempo)})";
    }
}
