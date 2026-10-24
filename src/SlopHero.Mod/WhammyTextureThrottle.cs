using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Experimental cap on Clone Hero's own whammy texture upload wrapper. The
/// game updates the CPU-backed texture in 5 ms steps, so repeat uploads between
/// those steps are redundant. This hook skips the game wrapper before it calls
/// Texture2D.Apply, without intercepting other Unity texture uploads.
/// </summary>
internal sealed class WhammyTextureThrottle
{
    private const double MinimumUploadIntervalSeconds = 1.0 / 200.0;
    private static readonly long MinimumUploadIntervalTicks =
        (long)(Stopwatch.Frequency * MinimumUploadIntervalSeconds);
    private static WhammyTextureThrottle? active;

    private MethodInfo? uploadMethod;
    private long lastUploadTimestamp;
    private bool installed;
    private bool failed;

    internal bool IsInstalled => installed;

    internal void Update(HarmonyLib.Harmony harmony, bool enabled, bool isGameplay)
    {
        if (!enabled || !isGameplay)
        {
            Restore(harmony);
            failed = false;
            return;
        }

        if (!installed && !failed) Install(harmony);
    }

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        Restore(harmony);
        failed = false;
    }

    private void Install(HarmonyLib.Harmony harmony)
    {
        try
        {
            // In the supplied v1.1.0.6142 metadata, TypeDef 993 is the static
            // whammy uploader. This generated wrapper method maps to RVA
            // 0x22BE10 and is called by GameManager.LateUpdate each frame.
            Type uploaderType = typeof(ObjectPublicAbstractSealedInBoInObDoInSiDoInDoUnique);
            uploadMethod = uploaderType.GetMethod("Method_Public_Static_Void_1",
                BindingFlags.Public | BindingFlags.Static, binder: null,
                types: Type.EmptyTypes, modifiers: null)
                ?? throw new MissingMethodException(
                    uploaderType.FullName, "Method_Public_Static_Void_1()");

            active = this;
            var prefix = new HarmonyMethod(typeof(WhammyTextureThrottle).GetMethod(
                nameof(BeforeWhammyUpload), BindingFlags.Static | BindingFlags.NonPublic)!);
            harmony.Patch(uploadMethod, prefix: prefix);
            installed = true;
        }
        catch (Exception ex)
        {
            Restore(harmony);
            failed = true;
            MelonLogger.Warning($"Sloptifine whammy upload cap could not install and remains inactive: {ex.Message}");
        }
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        if (uploadMethod != null)
        {
            try { harmony.Unpatch(uploadMethod, HarmonyPatchType.All, harmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"Sloptifine whammy upload-hook cleanup failed: {ex.Message}"); }
        }
        if (ReferenceEquals(active, this)) active = null;
        uploadMethod = null;
        installed = false;
        lastUploadTimestamp = 0;
    }

    private static bool BeforeWhammyUpload()
    {
        WhammyTextureThrottle? instance = active;
        if (instance == null) return true;

        long now = Stopwatch.GetTimestamp();
        if (instance.lastUploadTimestamp != 0
            && now - instance.lastUploadTimestamp < MinimumUploadIntervalTicks)
            return false;

        instance.lastUploadTimestamp = now;
        return true;
    }
}
