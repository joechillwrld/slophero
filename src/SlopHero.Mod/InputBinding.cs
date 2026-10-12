using System.Globalization;
using Il2CppRewired;
using UnityEngine;

namespace SlopHero.Mod;

internal static class InputBinding
{
    private static readonly KeyCode[] Keys = Enum.GetValues<KeyCode>().Distinct()
        .Where(k => k != KeyCode.None && (int)k < (int)KeyCode.JoystickButton0).ToArray();

    public static bool Pressed(string binding)
    {
        var parts = binding.Split(':');
        if (parts.Length == 2 && parts[0] == "Key" && Enum.TryParse<KeyCode>(parts[1], out var key))
            return Enum.IsDefined(typeof(KeyCode), key) && key != KeyCode.None && Input.GetKey(key);
        if (parts.Length == 3 && parts[0] == "Joy" && int.TryParse(parts[1], out int id)
            && int.TryParse(parts[2], out int button) && button >= 0 && ReInput.isReady)
        {
            var joysticks = ReInput.controllers.Joysticks;
            for (int i = 0; i < ReInput.controllers.joystickCount; i++)
            {
                var joystick = joysticks[i];
                if (joystick.id == id && button < joystick.buttonCount)
                    return joystick.GetButton(button);
            }
        }
        return false;
    }

    public static string? Capture()
    {
        foreach (var key in Keys)
            if (key != KeyCode.F8 && key != KeyCode.Escape && key != KeyCode.Mouse0 && Input.GetKeyDown(key))
                return $"Key:{key}";
        if (ReInput.isReady)
        {
            var joysticks = ReInput.controllers.Joysticks;
            for (int i = 0; i < ReInput.controllers.joystickCount; i++)
            {
                var joystick = joysticks[i];
                for (int button = 0; button < joystick.buttonCount; button++)
                    if (joystick.GetButtonDown(button))
                        return string.Create(CultureInfo.InvariantCulture, $"Joy:{joystick.id}:{button}");
            }
        }
        return null;
    }
}
