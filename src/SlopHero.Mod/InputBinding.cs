using System.Globalization;
using Il2CppRewired;
using UnityEngine;

namespace SlopHero.Mod;

internal static class InputBinding
{
    internal struct Cache
    {
        public string? Text;
        public KeyCode Key;
        public int JoystickId;
        public int Button;
        public bool IsJoystick;
        public bool Valid;
    }

    private static readonly KeyCode[] Keys = Enum.GetValues<KeyCode>().Distinct()
        .Where(k => k != KeyCode.None && (int)k < (int)KeyCode.JoystickButton0).ToArray();

    public static bool Pressed(string binding, ref Cache cache)
    {
        if (!string.Equals(cache.Text, binding, StringComparison.Ordinal))
        {
            cache = Parse(binding);
        }
        if (!cache.Valid) return false;
        if (!cache.IsJoystick) return Input.GetKey(cache.Key);
        if (!ReInput.isReady) return false;
        var joysticks = ReInput.controllers.Joysticks;
        for (int i = 0; i < ReInput.controllers.joystickCount; i++)
        {
            var joystick = joysticks[i];
            if (joystick.id == cache.JoystickId && cache.Button < joystick.buttonCount)
                return joystick.GetButton(cache.Button);
        }
        return false;
    }

    private static Cache Parse(string binding)
    {
        var result = new Cache { Text = binding, Button = -1 };
        var parts = binding.Split(':');
        if (parts.Length == 2 && parts[0] == "Key" && Enum.TryParse(parts[1], out KeyCode key)
            && Enum.IsDefined(typeof(KeyCode), key) && key != KeyCode.None)
            return new Cache { Text = binding, Key = key, Valid = true };
        if (parts.Length == 3 && parts[0] == "Joy"
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int button) && button >= 0)
            return new Cache { Text = binding, JoystickId = id, Button = button, IsJoystick = true, Valid = true };
        return result;
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
