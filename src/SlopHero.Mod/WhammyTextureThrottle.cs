using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
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

    private readonly List<MethodInfo> captureMethods = new();
    private MethodInfo? applyMethod;
    private MethodInfo? setPixelsMethod;
    private MethodInfo? setPixelsImplMethod;
    private Texture? targetTexture;
    private IntPtr targetTexturePointer;
    private string lastTargetType = "not observed";
    private long lastUploadTimestamp;
    private long matchedApplyCalls;
    private long allowedUploads;
    private long skippedUploads;
    private long matchedColumnWrites;
    private long skippedColumnWrites;
    private long matchedImplColumnWrites;
    private long skippedImplColumnWrites;
    private bool columnHookEverInstalled;
    private bool implHookEverInstalled;
    private int globalTextureId;
    private bool installed;
    private bool failed;
    private bool captureHooksInstalled;
    private bool requested;
    private bool gameplayActive;

    internal bool IsInstalled => installed;
    internal bool IsColumnWriteHookInstalled => setPixelsMethod != null || setPixelsImplMethod != null;

    internal void Update(HarmonyLib.Harmony harmony, bool enabled, bool isGameplay)
    {
        if (!enabled)
        {
            requested = false;
            gameplayActive = false;
            if (installed || applyMethod != null) RestoreApply(harmony);
            if (captureHooksInstalled || captureMethods.Count > 0) RestoreCapture(harmony);
            failed = false;
            return;
        }

        requested = true;
        gameplayActive = isGameplay;

        // Keep only the low-frequency global-texture assignment hooks active
        // while enabled, so a texture assigned before a chart is remembered.
        // Texture2D.Apply is much hotter, so its prefix exists only in Gameplay.
        if (!captureHooksInstalled && captureMethods.Count == 0 && !failed) InstallCaptureHooks(harmony);
        if (failed) return;

        if (isGameplay)
        {
            if (!installed) InstallApplyHook(harmony);
        }
        else if (installed || applyMethod != null)
        {
            RestoreApply(harmony);
        }
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
        if (!installed && matchedApplyCalls == 0 && allowedUploads == 0 && skippedUploads == 0
            && matchedColumnWrites == 0 && skippedColumnWrites == 0) return;
        string matchState = matchedApplyCalls > 0
            ? "Texture2D.Apply calls matched the captured Unity object"
            : "no matching Texture2D.Apply calls were observed";
        MelonLogger.Msg($"Sloptifine whammy upload experiment: wrapper={lastTargetType}, matchedApply={matchedApplyCalls}, allowed={allowedUploads}, skipped={skippedUploads}, columnHooksEverInstalled={columnHookEverInstalled}/{implHookEverInstalled}, matchedWhammyColumnWrites={matchedColumnWrites}, skippedWhammyColumnWrites={skippedColumnWrites}, implMatches={matchedImplColumnWrites}, implSkips={skippedImplColumnWrites}; {matchState}; CPU upload cap, 200 Hz. GPU compute dispatch is unchanged.");
    }

    private void InstallCaptureHooks(HarmonyLib.Harmony harmony)
    {
        try
        {
            MethodInfo setGlobalString = typeof(Shader).GetMethod(nameof(Shader.SetGlobalTexture),
                BindingFlags.Static | BindingFlags.Public, binder: null,
                types: new[] { typeof(string), typeof(Texture) }, modifiers: null)
                ?? throw new MissingMethodException("Shader.SetGlobalTexture(string, Texture)");

            var captureString = new HarmonyMethod(typeof(WhammyTextureThrottle).GetMethod(
                nameof(CaptureGlobalTexture), BindingFlags.Static | BindingFlags.NonPublic)!);

            active = this;
            globalTextureId = Shader.PropertyToID(GlobalTextureName);
            harmony.Patch(setGlobalString, postfix: captureString);
            captureMethods.Add(setGlobalString);

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
                captureMethods.Add(setGlobalId);
            }

            captureHooksInstalled = true;
            MelonLogger.Msg("Sloptifine whammy texture tracking ready; the upload cap will install only during Gameplay.");
        }
        catch (Exception ex)
        {
            RestoreCapture(harmony);
            failed = true;
            MelonLogger.Warning($"Sloptifine whammy texture tracking could not install; the upload cap remains inactive: {ex.Message}");
        }
    }

    private void InstallApplyHook(HarmonyLib.Harmony harmony)
    {
        try
        {
            MethodInfo apply = typeof(Texture2D).GetMethod(nameof(Texture2D.Apply),
                BindingFlags.Instance | BindingFlags.Public, binder: null,
                types: Type.EmptyTypes, modifiers: null)
                ?? throw new MissingMethodException("Texture2D.Apply()");
            var prefix = new HarmonyMethod(typeof(WhammyTextureThrottle).GetMethod(
                nameof(BeforeTextureApply), BindingFlags.Static | BindingFlags.NonPublic)!);
            harmony.Patch(apply, prefix: prefix);
            applyMethod = apply;
            installed = true;
            InstallColumnWriteHook(harmony);
            MelonLogger.Msg("Sloptifine experimental whammy upload cap installed for Gameplay.");
        }
        catch (Exception ex)
        {
            RestoreApply(harmony);
            failed = true;
            MelonLogger.Warning($"Sloptifine whammy upload experiment could not install and remains inactive: {ex.Message}");
        }
    }

    private void InstallColumnWriteHook(HarmonyLib.Harmony harmony)
    {
        MethodInfo? setPixels = null;
        try
        {
            // Clone Hero's CPU whammy producer writes one 1x24 column, then
            // calls Apply immediately. Reject redundant columns before their
            // CPU-side pixel copy; the Apply prefix remains the final guard
            // and fallback for other writes to the same texture.
            setPixels = typeof(Texture2D).GetMethod(nameof(Texture2D.SetPixels),
                BindingFlags.Instance | BindingFlags.Public, binder: null,
                types: new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(Il2CppStructArray<Color>) },
                modifiers: null)
                ?? throw new MissingMethodException("Texture2D.SetPixels(int, int, int, int, Color[])");
            var prefix = new HarmonyMethod(typeof(WhammyTextureThrottle).GetMethod(
                nameof(BeforeWhammyColumnWrite), BindingFlags.Static | BindingFlags.NonPublic)!);
            harmony.Patch(setPixels, prefix: prefix);
            setPixelsMethod = setPixels;
            columnHookEverInstalled = true;
        }
        catch (Exception ex)
        {
            if (setPixels != null)
            {
                try { harmony.Unpatch(setPixels, HarmonyPatchType.All, harmony.Id); }
                catch { /* Keep the verified Apply-only fallback. */ }
            }
            // The Apply cap still works when this Unity overload is absent.
            MelonLogger.Warning($"Sloptifine could not skip redundant whammy pixel writes; retaining the Apply-only cap: {ex.Message}");
        }

        // Unity's newer SetPixels overloads can route to this shared native
        // implementation without entering the five-argument wrapper above.
        // Patch it separately, with the same exact texture/shape guard. A
        // missing implementation leaves the existing Apply cap in place.
        MethodInfo? setPixelsImpl = null;
        try
        {
            setPixelsImpl = typeof(Texture2D).GetMethod("SetPixelsImpl",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(Il2CppStructArray<Color>), typeof(int), typeof(int) }, modifiers: null)
                ?? throw new MissingMethodException("Texture2D.SetPixelsImpl(int, int, int, int, Color[], int, int)");
            var implPrefix = new HarmonyMethod(typeof(WhammyTextureThrottle).GetMethod(
                nameof(BeforeWhammyColumnWriteImpl), BindingFlags.Static | BindingFlags.NonPublic)!);
            harmony.Patch(setPixelsImpl, prefix: implPrefix);
            setPixelsImplMethod = setPixelsImpl;
            implHookEverInstalled = true;
        }
        catch (Exception ex)
        {
            if (setPixelsImpl != null)
            {
                try { harmony.Unpatch(setPixelsImpl, HarmonyPatchType.All, harmony.Id); }
                catch { /* The Apply cap remains the fallback. */ }
            }
            MelonLogger.Warning($"Sloptifine could not gate lower-level whammy pixel writes; retaining other whammy hooks: {ex.Message}");
        }
    }

    private void RestoreApply(HarmonyLib.Harmony harmony)
    {
        gameplayActive = false;
        try
        {
            if (setPixelsMethod != null) harmony.Unpatch(setPixelsMethod, HarmonyPatchType.All, harmony.Id);
        }
        catch (Exception ex) { MelonLogger.Warning($"Sloptifine whammy SetPixels-hook cleanup failed: {ex.Message}"); }
        setPixelsMethod = null;
        try
        {
            if (setPixelsImplMethod != null) harmony.Unpatch(setPixelsImplMethod, HarmonyPatchType.All, harmony.Id);
        }
        catch (Exception ex) { MelonLogger.Warning($"Sloptifine whammy SetPixelsImpl-hook cleanup failed: {ex.Message}"); }
        setPixelsImplMethod = null;
        try
        {
            if (applyMethod != null) harmony.Unpatch(applyMethod, HarmonyPatchType.All, harmony.Id);
        }
        catch (Exception ex) { MelonLogger.Warning($"Sloptifine whammy Apply-hook cleanup failed: {ex.Message}"); }
        applyMethod = null;
        installed = false;
        lastUploadTimestamp = 0;
    }

    private void RestoreCapture(HarmonyLib.Harmony harmony)
    {
        foreach (MethodInfo method in captureMethods)
        {
            try { harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); }
            catch (Exception ex) { MelonLogger.Warning($"Sloptifine whammy texture-tracking cleanup failed: {ex.Message}"); }
        }
        captureMethods.Clear();
        captureHooksInstalled = false;
        targetTexture = null;
        targetTexturePointer = IntPtr.Zero;
        active = null;
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        requested = false;
        gameplayActive = false;
        RestoreApply(harmony);
        RestoreCapture(harmony);
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
        IntPtr valuePointer = value?.Pointer ?? IntPtr.Zero;
        if (targetTexturePointer == valuePointer) return;
        targetTexture = value;
        targetTexturePointer = valuePointer;
        lastUploadTimestamp = 0;
        lastTargetType = valuePointer == IntPtr.Zero ? "cleared" : value!.GetType().Name;
        if (valuePointer != IntPtr.Zero)
            MelonLogger.Msg($"Sloptifine observed _GlobalWhammyTexture through IL2CPP wrapper type {lastTargetType}; actual effect is determined by matching Texture2D.Apply calls on the same Unity object.");
    }

    private static bool BeforeTextureApply(Texture2D __instance)
    {
        WhammyTextureThrottle? instance = active;
        if (instance == null || !instance.requested || !instance.gameplayActive
            || instance.targetTexturePointer == IntPtr.Zero || __instance.Pointer != instance.targetTexturePointer)
            return true;

        instance.matchedApplyCalls++;
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

    private static bool BeforeWhammyColumnWrite(Texture2D __instance, int x, int y,
        int blockWidth, int blockHeight, Il2CppStructArray<Color> colors)
    {
        return ShouldWriteColumn(__instance, x, y, blockWidth, blockHeight, colors, fromImpl: false);
    }

    private static bool BeforeWhammyColumnWriteImpl(Texture2D __instance, int x, int y,
        int w, int h, Il2CppStructArray<Color> pixel, int miplevel, int frame)
    {
        if (miplevel != 0 || frame != 0) return true;
        return ShouldWriteColumn(__instance, x, y, w, h, pixel, fromImpl: true);
    }

    private static bool ShouldWriteColumn(Texture2D texture, int x, int y,
        int width, int height, Il2CppStructArray<Color> colors, bool fromImpl)
    {
        // The older renderer writes each frame as a single column at y=0
        // across all 24 instrument lanes. Row clears are 1024x1 and must
        // continue to run; unrelated texture writes also pass through.
        if (x < 0 || y != 0 || width != 1 || height != 24
            || colors == null || colors.Length != 24)
            return true;

        WhammyTextureThrottle? instance = active;
        if (instance == null || !instance.requested || !instance.gameplayActive
            || instance.targetTexturePointer == IntPtr.Zero || texture.Pointer != instance.targetTexturePointer)
            return true;

        instance.matchedColumnWrites++;
        if (fromImpl) instance.matchedImplColumnWrites++;
        long last = instance.lastUploadTimestamp;
        if (last == 0 || Stopwatch.GetTimestamp() - last >= MinimumUploadIntervalTicks)
            return true;

        instance.skippedColumnWrites++;
        if (fromImpl) instance.skippedImplColumnWrites++;
        return false;
    }
}
