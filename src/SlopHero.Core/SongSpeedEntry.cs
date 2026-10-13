using System.Globalization;

namespace SlopHero.Core;

public sealed class SongSpeedEntry
{
    public const int MaximumDigits = 4;
    public const int MinimumSpeed = 1;
    public const int MaximumSpeed = 5000;
    public string Text { get; private set; } = "";

    public void Clear() => Text = "";

    public void Append(int digit)
    {
        if ((uint)digit > 9 || Text.Length >= MaximumDigits) return;
        Text += (char)('0' + digit);
    }

    public void Backspace()
    {
        if (Text.Length > 0) Text = Text[..^1];
    }

    public bool TryGetSpeed(out int speed)
        => int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out speed)
            && speed is >= MinimumSpeed and <= MaximumSpeed;
}
