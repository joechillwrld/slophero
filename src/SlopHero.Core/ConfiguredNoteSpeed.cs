using System.Runtime.InteropServices;

namespace SlopHero.Core;

// Exact v1.1.0.6142 x64 layout, read only. The mod calls this only after
// BuildGuard matches and the native player/renderer are initialized.
public static class ConfiguredNoteSpeed
{
    public static bool IsPlausible(float speed) => float.IsFinite(speed) && speed >= 1 && speed <= 1000;

    public static float Read(IntPtr playerData)
    {
        if (playerData == IntPtr.Zero) return float.NaN;
        IntPtr profile = Marshal.ReadIntPtr(playerData, 0x10);
        if (profile == IntPtr.Zero) return float.NaN;
        IntPtr setting = Marshal.ReadIntPtr(profile, 0x20);
        if (setting == IntPtr.Zero) return float.NaN;
        // Native renderer initialization reads the concrete raw int here.
        // The generated generic field accessor returned a pointer-like value.
        int speed = Marshal.ReadInt32(setting, 0x10);
        return IsPlausible(speed) ? speed : float.NaN;
    }
}
