using System.Globalization;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace SlopHero.Mod;

internal static class SongSpeedControl
{
    private const int SongOptionsSelectionOffset = 0x19C;
    private const int SpeedValueOffset = 0x10;
    private const int MinimumSpeed = 1;
    private const int MaximumSpeed = 1000;
    private const string SettingsClass = "ʹʺʽˁʽˁˀʼʶʷʼ";
    private const string SpeedSettingField = "ʳʷʴʺʳʹʷʾʲʷʾ";
    private const string UpdateValueMethod = "ʸʾʵʽʳʾʾʻʷʲʽ";
    private static IntPtr speedSettingField;
    private static Il2Cpp.SongOptions? songOptions;

    public static bool IsSpeedOptionOpen()
    {
        if (songOptions == null || songOptions.Pointer == IntPtr.Zero)
        {
            var optionsObject = GameObject.Find("SongOptions");
            if (optionsObject == null) return false;
            songOptions = optionsObject.GetComponent<Il2Cpp.SongOptions>();
        }
        return songOptions != null && songOptions.gameObject.activeInHierarchy
            && Marshal.ReadInt32(songOptions.Pointer, SongOptionsSelectionOffset) == 0;
    }

    public static void ResetMenuCache() => songOptions = null;

    public static bool TryRead(out int speed)
    {
        IntPtr setting = GetSettingPointer();
        if (setting == IntPtr.Zero)
        {
            speed = 0;
            return false;
        }
        speed = Marshal.ReadInt32(setting, SpeedValueOffset);
        return speed >= MinimumSpeed && speed <= MaximumSpeed;
    }

    public static unsafe bool TryWrite(int speed)
    {
        if (speed < MinimumSpeed || speed > MaximumSpeed) return false;
        IntPtr setting = GetSettingPointer();
        if (setting == IntPtr.Zero) return false;
        IntPtr settingClass = IL2CPP.il2cpp_object_get_class(setting);
        IntPtr setter = IntPtr.Zero;
        for (IntPtr type = settingClass; type != IntPtr.Zero && setter == IntPtr.Zero; type = IL2CPP.il2cpp_class_get_parent(type))
            setter = IL2CPP.il2cpp_class_get_method_from_name(type, UpdateValueMethod, 1);
        if (setter == IntPtr.Zero) return false;
        int value = speed;
        void* valuePointer = &value;
        void** args = stackalloc void*[1];
        args[0] = valuePointer;
        IntPtr exception = IntPtr.Zero;
        IL2CPP.il2cpp_runtime_invoke(setter, setting, args, ref exception);
        return exception == IntPtr.Zero && Marshal.ReadInt32(setting, SpeedValueOffset) == speed;
    }

    private static unsafe IntPtr GetSettingPointer()
    {
        if (speedSettingField == IntPtr.Zero)
        {
            IntPtr domain = IL2CPP.il2cpp_domain_get();
            IntPtr assemblyName = Marshal.StringToHGlobalAnsi("CloneHero.dll");
            try
            {
                IntPtr assembly = IL2CPP.il2cpp_domain_assembly_open(domain, assemblyName);
                if (assembly == IntPtr.Zero) return IntPtr.Zero;
                IntPtr image = IL2CPP.il2cpp_assembly_get_image(assembly);
                IntPtr settingsClass = IL2CPP.il2cpp_class_from_name(image, "", SettingsClass);
                if (settingsClass == IntPtr.Zero) return IntPtr.Zero;
                IL2CPP.il2cpp_runtime_class_init(settingsClass);
                speedSettingField = IL2CPP.il2cpp_class_get_field_from_name(settingsClass, SpeedSettingField);
            }
            finally { Marshal.FreeHGlobal(assemblyName); }
        }
        if (speedSettingField == IntPtr.Zero) return IntPtr.Zero;
        IntPtr setting = IntPtr.Zero;
        IL2CPP.il2cpp_field_static_get_value(speedSettingField, &setting);
        return setting;
    }

    public static bool TryParsePercent(string text, out int speed)
    {
        text = text.Trim().TrimEnd('%').Trim();
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out speed)
            && speed >= MinimumSpeed && speed <= MaximumSpeed;
    }

    public static int ClampStep(int value) => Math.Clamp(value, 1, 500);
}
