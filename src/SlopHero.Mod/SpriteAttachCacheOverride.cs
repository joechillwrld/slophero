using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlopHero.Mod;

/// <summary>
/// Default-off exact-build experiment for SpriteAttach.Update. The game only
/// writes its rotation and scale targets when its viewport/camera/sprite inputs
/// change. We verify those inputs and the last-written outputs each rendered
/// update, then skip the native method only while they remain identical.
/// </summary>
internal sealed class SpriteAttachCacheOverride
{
    private sealed class State
    {
        private IntPtr cameraPointer, playerCameraPointer, attachedTransformPointer;
        private IntPtr rendererPointer, spritePointer, spriteTransformPointer;
        private IntPtr objectArrayPointer, factorArrayPointer;
        private ulong nativeScaleVector;
        private int screenWidth, screenHeight;
        private Rect cameraRect, cameraPixelRect;
        private Matrix4x4 cameraWorldToCamera, cameraProjection, spriteLocalToWorld;
        private Vector3 attachedPosition, cameraPosition, top, bottom;
        private Bounds spriteBounds;
        private Vector2 offset;
        private float playerCameraScale;
        private Quaternion outputRotation;
        private Transform?[] targets = Array.Empty<Transform?>();
        private IntPtr[] targetPointers = Array.Empty<IntPtr>();
        private float[] factors = Array.Empty<float>();
        private Vector3[] outputScales = Array.Empty<Vector3>();
        private bool valid;

        public bool Matches(SpriteAttach attach, Func<ulong> readNativeScaleVector)
        {
            if (!TryRead(attach, out var camera, out var playerCamera, out var sourceTransform,
                    out var renderer, out var sprite, out var spriteTransform,
                    out var objects, out var multipliers)) return false;

            if (!valid || camera.Pointer != cameraPointer || playerCamera.Pointer != playerCameraPointer
                || sourceTransform.Pointer != attachedTransformPointer || renderer.Pointer != rendererPointer
                || sprite.Pointer != spritePointer || spriteTransform.Pointer != spriteTransformPointer
                || objects.Pointer != objectArrayPointer || multipliers.Pointer != factorArrayPointer
                || objects.Length != targets.Length || multipliers.Length != factors.Length
                || objects.Length != multipliers.Length
                || Screen.width != screenWidth || Screen.height != screenHeight
                || !camera.rect.Equals(cameraRect) || !camera.pixelRect.Equals(cameraPixelRect)
                || !camera.worldToCameraMatrix.Equals(cameraWorldToCamera)
                || !camera.projectionMatrix.Equals(cameraProjection)
                || !spriteTransform.localToWorldMatrix.Equals(spriteLocalToWorld)
                || !sourceTransform.position.Equals(attachedPosition)
                || !camera.transform.position.Equals(cameraPosition)
                || !attach.anchorTop.Equals(top) || !attach.anchorBottom.Equals(bottom)
                || !sprite.bounds.Equals(spriteBounds) || !attach.scaleOffset.Equals(offset)
                || !playerCamera.field_Private_Single_0.Equals(playerCameraScale)
                || readNativeScaleVector() != nativeScaleVector
                || !sourceTransform.rotation.Equals(outputRotation)) return false;

            for (int i = 0; i < targets.Length; i++)
            {
                Transform? target = objects[i];
                if (target == null || target.Pointer != targetPointers[i]
                    || !SameFloat(multipliers[i], factors[i])
                    || !target.localScale.Equals(outputScales[i])) return false;
            }
            return true;
        }

        public bool Capture(SpriteAttach attach, Func<ulong> readNativeScaleVector)
        {
            if (!TryRead(attach, out var camera, out var playerCamera, out var sourceTransform,
                    out var renderer, out var sprite, out var spriteTransform,
                    out var objects, out var multipliers)
                || objects.Length != multipliers.Length) return valid = false;

            int count = objects.Length;
            if (targets.Length != count)
            {
                targets = new Transform?[count];
                targetPointers = new IntPtr[count];
                factors = new float[count];
                outputScales = new Vector3[count];
            }

            for (int i = 0; i < count; i++)
            {
                Transform? target = objects[i];
                if (target == null) return valid = false;
                targets[i] = target;
                targetPointers[i] = target.Pointer;
                factors[i] = multipliers[i];
                outputScales[i] = target.localScale;
            }

            cameraPointer = camera.Pointer;
            playerCameraPointer = playerCamera.Pointer;
            attachedTransformPointer = sourceTransform.Pointer;
            rendererPointer = renderer.Pointer;
            spritePointer = sprite.Pointer;
            spriteTransformPointer = spriteTransform.Pointer;
            objectArrayPointer = objects.Pointer;
            factorArrayPointer = multipliers.Pointer;
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            cameraRect = camera.rect;
            cameraPixelRect = camera.pixelRect;
            cameraWorldToCamera = camera.worldToCameraMatrix;
            cameraProjection = camera.projectionMatrix;
            spriteLocalToWorld = spriteTransform.localToWorldMatrix;
            attachedPosition = sourceTransform.position;
            cameraPosition = camera.transform.position;
            top = attach.anchorTop;
            bottom = attach.anchorBottom;
            spriteBounds = sprite.bounds;
            offset = attach.scaleOffset;
            playerCameraScale = playerCamera.field_Private_Single_0;
            nativeScaleVector = readNativeScaleVector();
            outputRotation = sourceTransform.rotation;
            valid = true;
            return true;
        }

        private static bool TryRead(SpriteAttach attach,
            out Camera camera,
            out PlayerCamera playerCamera,
            out Transform sourceTransform,
            out SpriteRenderer renderer,
            out Sprite sprite,
            out Transform spriteTransform,
            out Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Transform> objects,
            out Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float> multipliers)
        {
            camera = attach.playerCam;
            playerCamera = attach.field_Private_PlayerCamera_0;
            sourceTransform = attach.transform;
            renderer = attach.referenceSprite;
            objects = attach.objectsToScale;
            multipliers = attach.field_Private_Il2CppStructArray_1_Single_0;
            sprite = renderer == null ? null! : renderer.sprite;
            spriteTransform = renderer == null ? null! : renderer.transform;
            return camera != null && playerCamera != null && sourceTransform != null
                && renderer != null && sprite != null && spriteTransform != null
                && objects != null && multipliers != null;
        }

        private static bool SameFloat(float left, float right)
            => BitConverter.SingleToInt32Bits(left) == BitConverter.SingleToInt32Bits(right);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong NativeScaleVectorGetter(IntPtr methodInfo);

    private static SpriteAttachCacheOverride? active;
    private readonly Dictionary<IntPtr, State> states = new();
    private MethodInfo? updateMethod;
    private NativeScaleVectorGetter? nativeScaleVectorGetter;
    private bool installed, failed, cacheActive;
    private string? failure;

    internal string? Failure => failure;

    internal string? ConsumeFailure()
    {
        string? result = failure;
        failure = null;
        return result;
    }

    internal void Update(HarmonyLib.Harmony harmony, bool requested, bool eligibleOfflineSoloSong)
    {
        if (!requested || !eligibleOfflineSoloSong)
        {
            cacheActive = false;
            states.Clear();
            if (!requested || !eligibleOfflineSoloSong) Restore(harmony);
            if (!requested) { failed = false; failure = null; }
            return;
        }

        if (failed)
        {
            cacheActive = false;
            Restore(harmony);
            return;
        }
        if (!installed && !failed) Install(harmony);
        cacheActive = installed && !failed;
    }

    internal void Disable()
    {
        cacheActive = false;
        states.Clear();
    }

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        Disable();
        Restore(harmony);
        failed = false;
        failure = null;
    }

    private void Install(HarmonyLib.Harmony harmony)
    {
        try
        {
            updateMethod = typeof(SpriteAttach).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: Type.EmptyTypes, modifiers: null)
                ?? throw new MissingMethodException("Il2Cpp.SpriteAttach.Update()");
            ProcessModule? module = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .FirstOrDefault(m => string.Equals(m.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase));
            if (module == null) throw new DllNotFoundException("GameAssembly.dll");
            // SpriteAttach.Update reads a packed Vector2 through this exact-build
            // helper. BuildGuard matches the supplied v1.1.0.6142 binary first.
            nativeScaleVectorGetter = Marshal.GetDelegateForFunctionPointer<NativeScaleVectorGetter>(
                IntPtr.Add(module.BaseAddress, 0x64C60));
            active = this;
            var prefix = new HarmonyMethod(typeof(SpriteAttachCacheOverride).GetMethod(
                nameof(BeforeUpdate), BindingFlags.Static | BindingFlags.NonPublic)!);
            var postfix = new HarmonyMethod(typeof(SpriteAttachCacheOverride).GetMethod(
                nameof(AfterUpdate), BindingFlags.Static | BindingFlags.NonPublic)!);
            harmony.Patch(updateMethod, prefix: prefix, postfix: postfix);
            installed = true;
            MelonLogger.Msg("Sloptifine SpriteAttach input-cache experiment installed for offline solo songs.");
        }
        catch (Exception ex)
        {
            active = null;
            if (updateMethod != null)
            {
                try { harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id); }
                catch { /* Inactive callbacks pass through if cleanup fails. */ }
            }
            updateMethod = null;
            nativeScaleVectorGetter = null;
            installed = false;
            cacheActive = false;
            failed = true;
            failure = ex.Message;
        }
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        if (!installed && updateMethod == null) return;
        cacheActive = false;
        if (ReferenceEquals(active, this)) active = null;
        try
        {
            if (updateMethod != null) harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id);
        }
        catch (Exception ex)
        {
            failure = $"SpriteAttach hook cleanup failed: {ex.Message}";
        }
        updateMethod = null;
        nativeScaleVectorGetter = null;
        installed = false;
        states.Clear();
    }

    private bool ShouldRun(SpriteAttach attach)
    {
        if (!cacheActive || ReferenceEquals(attach, null) || attach.Pointer == IntPtr.Zero
            || !OnDemandRendering.willCurrentFrameRender) return true;
        try
        {
            IntPtr key = attach.Pointer;
            if (states.TryGetValue(key, out var state))
                return !state.Matches(attach, ReadNativeScaleVector);
            return true;
        }
        catch (Exception ex)
        {
            FailOpen(ex);
            return true;
        }
    }

    private void Record(SpriteAttach attach)
    {
        if (!cacheActive || ReferenceEquals(attach, null) || attach.Pointer == IntPtr.Zero
            || !OnDemandRendering.willCurrentFrameRender) return;
        try
        {
            IntPtr key = attach.Pointer;
            if (!states.TryGetValue(key, out var state)) states.Add(key, state = new State());
            if (!state.Capture(attach, ReadNativeScaleVector)) states.Remove(key);
        }
        catch (Exception ex)
        {
            FailOpen(ex);
        }
    }

    private void FailOpen(Exception ex)
    {
        cacheActive = false;
        states.Clear();
        if (failure != null) return;
        failed = true;
        failure = ex.Message;
    }

    private ulong ReadNativeScaleVector()
        => nativeScaleVectorGetter?.Invoke(IntPtr.Zero)
            ?? throw new InvalidOperationException("SpriteAttach native scale input is unavailable.");

    private static bool BeforeUpdate(SpriteAttach __instance) => active?.ShouldRun(__instance) != false;
    private static void AfterUpdate(SpriteAttach __instance) => active?.Record(__instance);
}
