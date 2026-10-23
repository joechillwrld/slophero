using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Experimental cap for redundant CPU whammy texture uploads. The target is
/// discovered only when the game assigns its named global texture. The GPU
/// compute path does not use Texture2D.Apply and is intentionally untouched.
/// </summary>
internal sealed class WhammyTextureThrottle
{
    private const string GlobalTextureName = "_GlobalWhammyTexture";
    private const double MinimumUploadIntervalSeconds = 1.0 / 200.0;
    private static readonly long MinimumUploadIntervalTicks = (long)(Stopwatch.Frequency * MinimumUploadIntervalSeconds);
    private static WhammyTextureThrottle? active;

    private readonly List<MethodInfo> patchedMethods = new();
    private Texture? targetTexture;
    private string lastTargetType = "not observed";
    private long lastUploadTimestamp;
    private long allowedUploads;
    private long skippedUploads;
    private int globalTextureId;
    private bool installed;
    private bool failed;
    private bool requested;
    private bool gameplayActive;

    internal bool IsInstalled => installed;

    internal void Update(HarmonyLib.Harmony harmony, bool enabled, bool isGameplay)
    {
        if (requested == enabled && gameplayActive == isGameplay && (enabled ? installed || failed : !installed)) return;
        requested = enabled;
        gameplayActive = isGameplay;
        if (!enabled)
        {
            if (installed || patchedMethods.Count > 0) Restore(harmony);
            failed = false;
            return;
        }

        if (!installed && !failed) Install(harmony);
    }

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        requested = false;
        gameplayActive = false;
        Restore(harmony);
        failed = false;
    }

    internal void LogSummary()
    {
        if (!installed && allowedUploads == 0 && skippedUploads == 0) return;
        MelonLogger.Msg($"Sloptifine whammy upload experiment: target={lastTargetType}, allowed={allowedUploads}, skipped={skippedUploads}; CPU Texture2D.Apply only, 200 Hz cap. GPU compute dispatch is unchanged.");
    }

    private void Install(HarmonyLib.Harmony harmony)
    {
        try
        {
            MethodInfo apply = typeof(Texture2D).GetMethod(nameof(Texture2D.Apply),
                BindingFlags.Instance | BindingFlags.Public, binder: null,
                types: Type.EmptyTypes, modifiers: null)
                ?? throw new MissingMethodException("Texture2D.Apply()");
            MethodInfo setGlobalString = typeof(Shader).GetMethod(nameof(Shader.SetGlobalTexture),
                BindingFlags.Static | BindingFlags.Public, binder: null,
                types: new[] { typeof(string), typeof(Texture) }, modifiers: null)
                ?? throw new MissingMethodException("Shader.SetGlobalTexture(string, Texture)");

            var prefix = new HarmonyMethod(typeof(WhammyTextureThrottle).GetMethod(
                nameof(BeforeTextureApply), BindingFlags.Static | BindingFlags.NonPublic)!);
            var captureString = new HarmonyMethod(typeof(WhammyTextureThrottle).GetMethod(
                nameof(CaptureGlobalTexture), BindingFlags.Static | BindingFlags.NonPublic)!);

            active = this;
            targetTexture = null;
            globalTextureId = Shader.PropertyToID(GlobalTextureName);
            harmony.Patch(apply, prefix: prefix);
            patchedMethods.Add(apply);
            harmony.Patch(setGlobalString, postfix: captureString);
            patchedMethods.Add(setGlobalString);

            // Some Unity code uses a cached property ID instead of the string
            // overload. This overload is optional; the named string hook above
            // is the required, observed game path.
            MethodInfo? setGlobalId = typeof(Shader).GetMethod(nameof(Shader.SetGlobalTexture),
                BindingFlags.Static | BindingFlags.Public, binder: null,
                types: new[] { typeof(int), typeof(Texture) }, modifiers: null);
            if (setGlobalId != null)
            {
                var captureId = new HarmonyMethod(typeof(WhammyTextureThrottle).GetMethod(
                    nameof(CaptureGlobalTextureById), BindingFlags.Static | BindingFlags.NonPublic)!);
                harmony.Patch(setGlobalId, postfix: captureId);
                patchedMethods.Add(setGlobalId);
            }

            installed = true;
            MelonLogger.Msg("Sloptifine experimental whammy upload cap installed; waiting for the game to assign _GlobalWhammyTexture.");
        }
        catch (Exception ex)
        {
            Restore(harmony);
            failed = true;
            MelonLogger.Warning($"Sloptifine whammy upload experiment could not install and remains inactive: {ex.Message}");
        }
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        requested = false;
        gameplayActive = false;
        active = null;
        foreach (MethodInfo method in patchedMethods)
        {
            try { harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"Sloptifine whammy hook cleanup failed: {ex.Message}"); }
        }
        patchedMethods.Clear();
        installed = false;
        targetTexture = null;
        lastUploadTimestamp = 0;
    }

    private static void CaptureGlobalTexture(string name, Texture value)
    {
        WhammyTextureThrottle? instance = active;
        if (instance == null || !instance.requested || !string.Equals(name, GlobalTextureName, StringComparison.Ordinal)) return;
        instance.Capture(value);
    }

    private static void CaptureGlobalTextureById(int nameID, Texture value)
    {
        WhammyTextureThrottle? instance = active;
        if (instance == null || !instance.requested || nameID != instance.globalTextureId) return;
        instance.Capture(value);
    }

    private void Capture(Texture value)
    {
        if (targetTexture == value) return;
        targetTexture = value;
        lastUploadTimestamp = 0;
        lastTargetType = value == null ? "cleared" : value.GetType().Name;
        if (value != null)
        {
            string scope = value is Texture2D
                ? "CPU Texture2D upload cap applies; the GPU compute path is unchanged"
                : "this texture uses a non-Texture2D path, so the upload cap will not affect it";
            MelonLogger.Msg($"Sloptifine observed _GlobalWhammyTexture as {value.GetType().Name}; {scope}.");
        }
    }

    private static bool BeforeTextureApply(Texture2D __instance)
    {
        WhammyTextureThrottle? instance = active;
        if (instance == null || !instance.requested || !instance.gameplayActive || instance.targetTexture == null
            || __instance != instance.targetTexture)
            return true;

        long now = Stopwatch.GetTimestamp();
        if (instance.lastUploadTimestamp != 0 && now - instance.lastUploadTimestamp < MinimumUploadIntervalTicks)
        {
            instance.skippedUploads++;
            return false;
        }

        instance.lastUploadTimestamp = now;
        instance.allowedUploads++;
        return true;
    }
}
