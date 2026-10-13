using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using SlopHero.Core;
using UnityEngine;

namespace SlopHero.Mod;

internal static class SongSpeedControl
{
    private const int SongOptionsSelectionOffset = 0x19C;
    private const int SpeedValueOffset = 0x10;
    private const double SpeedEntryCommitDelay = .4;
    private const int MinimumSpeed = SongSpeedEntry.MinimumSpeed;
    private const int MaximumSpeed = SongSpeedEntry.MaximumSpeed;
    private const string SettingsClass = "ʹʺʽˁʽˁˀʼʶʷʼ";
    private const string SpeedSettingField = "ʳʷʴʺʳʹʷʾʲʷʾ";
    private const string UpdateValueMethod = "ʸʾʵʽʳʾʾʻʷʲʽ";
    private static IntPtr speedSettingField;
    private static Il2Cpp.SongOptions? songOptions;
    private static bool trackingMenuSpeed;
    private static bool checkedLowSpeedRecovery;
    private static double nextLowSpeedRecoveryAttempt;
    private static int lastMenuSpeed;
    private static readonly SongSpeedEntry speedEntry = new();
    private static double lastSpeedKeyTime;
    private static bool editingSpeed;
    private static TextMeshProUGUI? nativeSpeedLabel;
    private static string? nativeDisplayedDigits;
    private static bool nativeDisplayedPending;
    private static float nextNativeLabelCheck;
    private static string speedLabelPrefix = "Speed: ", speedLabelSuffix = "%";
    private static readonly KeyCode[] TopRowDigits =
    {
        KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
        KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
    };
    private static readonly KeyCode[] KeypadDigits =
    {
        KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4,
        KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9
    };

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

    public static void ResetMenuCache()
    {
        songOptions = null;
        trackingMenuSpeed = false;
        checkedLowSpeedRecovery = false;
        nextLowSpeedRecoveryAttempt = 0;
        speedEntry.Clear();
        lastSpeedKeyTime = 0;
        editingSpeed = false;
        nativeSpeedLabel = null;
        nativeDisplayedDigits = null;
        nativeDisplayedPending = false;
        nextNativeLabelCheck = 0;
    }

    public static void ProcessSelectedSpeedInput(int step, double now)
    {
        if (!checkedLowSpeedRecovery && now >= nextLowSpeedRecoveryAttempt)
        {
            nextLowSpeedRecoveryAttempt = now + 1;
            if (TryRead(out int storedSpeed))
                checkedLowSpeedRecovery = storedSpeed >= MinimumSpeed || TryWrite(MinimumSpeed);
        }

        // Handle Escape before validating the selected row. Once Clone Hero
        // changes selection or closes SongOptions, the normal validation path
        // can run first and leave our temporary display state behind.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (TryRead(out int escapedSpeed))
                DrawNativeSpeedEntry(escapedSpeed.ToString(CultureInfo.InvariantCulture));
            ClearEntryState();
            trackingMenuSpeed = false;
            nativeSpeedLabel = null;
            return;
        }

        if (!IsSpeedOptionOpen() || !TryRead(out int current))
        {
            if (editingSpeed && speedEntry.TryGetSpeed(out int pendingSpeed))
                TryWrite(pendingSpeed);
            if (nativeSpeedLabel != null && TryRead(out int lastValidSpeed))
                DrawNativeSpeedEntry(lastValidSpeed.ToString(CultureInfo.InvariantCulture));
            nativeSpeedLabel = null;
            nativeDisplayedDigits = null;
            trackingMenuSpeed = false;
            ClearEntryState();
            return;
        }

        if (!trackingMenuSpeed)
        {
            lastMenuSpeed = current;
            trackingMenuSpeed = true;
        }
        else if (current != lastMenuSpeed)
        {
            int delta = current - lastMenuSpeed;
            if (delta != 0 && delta % 5 == 0)
            {
                int adjusted = Math.Clamp(lastMenuSpeed + Math.Sign(delta) * ClampStep(step) * (Math.Abs(delta) / 5), MinimumSpeed, MaximumSpeed);
                if (TryWrite(adjusted)) current = adjusted;
            }
            lastMenuSpeed = current;
            speedEntry.Clear();
            editingSpeed = false;
        }

        int typedDigit = -1;
        for (int digit = 0; digit <= 9; digit++)
        {
            if (Input.GetKeyDown(TopRowDigits[digit]) || Input.GetKeyDown(KeypadDigits[digit]))
            {
                typedDigit = digit;
                break;
            }
        }

        bool deleteLast = Input.GetKeyDown(KeyCode.Backspace);
        bool clearAll = Input.GetKeyDown(KeyCode.Delete);
        if (typedDigit >= 0 || deleteLast || clearAll)
        {
            if (!editingSpeed) speedEntry.Clear();
            editingSpeed = true;
            lastSpeedKeyTime = now;
            if (clearAll) speedEntry.Clear();
            else if (deleteLast) speedEntry.Backspace();
            else if (typedDigit >= 0) speedEntry.Append(typedDigit);
        }

        // Keep the game's setting untouched while the user types. Applying
        // each prefix (for example 1 -> 13 -> 137) causes needless speed
        // recalculations and can expose unsupported intermediate values.
        bool confirmEntry = Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetKeyDown(KeyCode.Space);
        if (editingSpeed && (confirmEntry || now - lastSpeedKeyTime >= SpeedEntryCommitDelay))
        {
            if (speedEntry.TryGetSpeed(out int typedValue) && typedValue != current && TryWrite(typedValue))
            {
                current = typedValue;
                lastMenuSpeed = typedValue;
            }
            editingSpeed = false;
            speedEntry.Clear();
        }

        if (editingSpeed) DrawNativeSpeedEntry(speedEntry.Text, pending: true);
        else DrawNativeSpeedEntry(current.ToString(CultureInfo.InvariantCulture));
    }

    private static void ClearEntryState()
    {
        speedEntry.Clear();
        editingSpeed = false;
        lastSpeedKeyTime = 0;
    }

    private static void DrawNativeSpeedEntry(string digits, bool pending = false)
    {
        if (songOptions == null) return;
        try
        {
            if (nativeSpeedLabel == null || nativeSpeedLabel.Pointer == IntPtr.Zero)
            {
                var labels = songOptions.textObjects;
                if (labels == null) return;
                TextMeshProUGUI? percentFallback = null;
                foreach (var label in labels)
                {
                    if (label == null || label.Pointer == IntPtr.Zero) continue;
                    string text = label.text ?? "";
                    if (!text.Contains("%", StringComparison.Ordinal)) continue;
                    percentFallback ??= label;
                    if (!text.Contains("speed", StringComparison.OrdinalIgnoreCase)) continue;
                    nativeSpeedLabel = label;
                    var match = Regex.Match(text, @"^(?<prefix>.*?)(?<number>\d+)(?<suffix>\s*%.*)$", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        speedLabelPrefix = match.Groups["prefix"].Value;
                        speedLabelSuffix = match.Groups["suffix"].Value;
                    }
                    else { speedLabelPrefix = "Speed: "; speedLabelSuffix = "%"; }
                    break;
                }
                if (nativeSpeedLabel == null && percentFallback != null)
                {
                    nativeSpeedLabel = percentFallback;
                    var text = nativeSpeedLabel.text ?? "";
                    var match = Regex.Match(text, @"^(?<prefix>.*?)(?<number>\d+)(?<suffix>\s*%.*)$", RegexOptions.Singleline);
                    if (match.Success)
                    {
                        speedLabelPrefix = match.Groups["prefix"].Value;
                        speedLabelSuffix = match.Groups["suffix"].Value;
                    }
                    else { speedLabelPrefix = "Speed: "; speedLabelSuffix = "%"; }
                }
                nativeDisplayedDigits = null;
                nativeDisplayedPending = false;
            }
            if (nativeSpeedLabel != null)
            {
                // Avoid repeated IL2CPP string reads and TMP rebuilds when the
                // desired value is unchanged. Recheck occasionally in case the
                // game's own menu refresh overwrites the temporary edit text.
                if (string.Equals(nativeDisplayedDigits, digits, StringComparison.Ordinal)
                    && nativeDisplayedPending == pending
                    && Time.unscaledTime < nextNativeLabelCheck)
                    return;
                string display = speedLabelPrefix + digits + speedLabelSuffix + (pending ? "*" : "");
                if (!string.Equals(nativeSpeedLabel.text, display, StringComparison.Ordinal))
                    nativeSpeedLabel.text = display;
                nativeDisplayedDigits = digits;
                nativeDisplayedPending = pending;
                nextNativeLabelCheck = Time.unscaledTime + .15f;
            }
        }
        catch { nativeSpeedLabel = null; nativeDisplayedDigits = null; nativeDisplayedPending = false; }
    }

    public static bool TryRead(out int speed)
    {
        IntPtr setting = GetSettingPointer();
        if (setting == IntPtr.Zero)
        {
            speed = 0;
            return false;
        }
        speed = Marshal.ReadInt32(setting, SpeedValueOffset);
        return speed >= 1 && speed <= MaximumSpeed;
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
        if (exception == IntPtr.Zero && Marshal.ReadInt32(setting, SpeedValueOffset) == speed)
            return true;

        return false;
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
