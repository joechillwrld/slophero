using System.Reflection;
using System.Threading;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace SlopHero.Mod;

/// <summary>
/// Experimental dirty gate for Clone Hero's own whammy texture upload path.
/// The game writes its CPU-backed texture in 5 ms steps. A writer postfix marks
/// the texture dirty, and the upload prefix skips calls until that data changes.
/// Other Unity texture uploads never enter these hooks.
/// </summary>
internal sealed class WhammyTextureThrottle
{
    private static WhammyTextureThrottle? active;

    private MethodInfo? uploadMethod;
    private MethodInfo? dataWriterMethod;
    private long dataVersion = 1;
    private long uploadedVersion;
    private bool installed;
    private bool failed;
    private volatile bool passThrough;

    internal bool IsInstalled => installed;
    internal bool IsSuppressed => installed && passThrough;

    // Keep the prepared hook installed, but return to the native upload path
    // immediately outside eligible offline solo play.
    internal void SetSuppressed(bool value) => passThrough = installed && value;

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

            // TypeDef 777 owns the CPU texture and its NativeArray<byte>. This
            // generated wrapper maps to RVA 0x1C7D20, the method that writes
            // the 32 whammy lanes whenever the 5 ms producer step advances.
            Type dataWriterType = typeof(ObjectPublicTeNa1ByByUnique);
            dataWriterMethod = dataWriterType.GetMethod("Method_Public_Void_Int32_Int32_PDM_1",
                BindingFlags.Public | BindingFlags.Instance, binder: null,
                types: new[] { typeof(int), typeof(int) }, modifiers: null)
                ?? throw new MissingMethodException(
                    dataWriterType.FullName, "Method_Public_Void_Int32_Int32_PDM_1(int, int)");

            active = this;
            var dirtyPostfix = new HarmonyMethod(typeof(WhammyTextureThrottle).GetMethod(
                nameof(AfterWhammyDataWrite), BindingFlags.Static | BindingFlags.NonPublic)!);
            harmony.Patch(dataWriterMethod, postfix: dirtyPostfix);

            var uploadPrefix = new HarmonyMethod(typeof(WhammyTextureThrottle).GetMethod(
                nameof(BeforeWhammyUpload), BindingFlags.Static | BindingFlags.NonPublic)!);
            var uploadPostfix = new HarmonyMethod(typeof(WhammyTextureThrottle).GetMethod(
                nameof(AfterWhammyUpload), BindingFlags.Static | BindingFlags.NonPublic)!);
            harmony.Patch(uploadMethod, prefix: uploadPrefix, postfix: uploadPostfix);
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
        // Leave any prefix that fails to unpatch in pass-through mode.
        passThrough = false;
        if (ReferenceEquals(active, this)) active = null;
        if (uploadMethod != null)
        {
            try { harmony.Unpatch(uploadMethod, HarmonyPatchType.All, harmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"Sloptifine whammy upload-hook cleanup failed: {ex.Message}"); }
        }
        if (dataWriterMethod != null)
        {
            try { harmony.Unpatch(dataWriterMethod, HarmonyPatchType.All, harmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"Sloptifine whammy writer-hook cleanup failed: {ex.Message}"); }
        }
        uploadMethod = null;
        dataWriterMethod = null;
        installed = false;
        Volatile.Write(ref dataVersion, 1);
        Volatile.Write(ref uploadedVersion, 0);
    }

    private static void AfterWhammyDataWrite()
    {
        WhammyTextureThrottle? instance = active;
        if (instance != null && !instance.passThrough) Interlocked.Increment(ref instance.dataVersion);
    }

    private static bool BeforeWhammyUpload(out long __state)
    {
        __state = 0;
        WhammyTextureThrottle? instance = active;
        if (instance == null || instance.passThrough) return true;

        long currentVersion = Volatile.Read(ref instance.dataVersion);
        if (currentVersion == Volatile.Read(ref instance.uploadedVersion)) return false;
        __state = currentVersion;
        return true;
    }

    private static void AfterWhammyUpload(long __state)
    {
        WhammyTextureThrottle? instance = active;
        if (instance != null && __state != 0)
            Volatile.Write(ref instance.uploadedVersion, __state);
    }
}
