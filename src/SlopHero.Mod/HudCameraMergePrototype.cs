using System.Runtime.InteropServices;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SlopHero.Mod;

/// <summary>
/// Default-off experiment that records the camera-rendered Gameplay HUD into
/// a command buffer on the guitar camera, using HUDCam's original matrices.
/// Camera-space Canvases and ambiguous camera stacks fail closed.
/// </summary>
internal sealed class HudCameraMergePrototype
{
    private sealed record DrawItem(Renderer Renderer, Material?[] Materials, int SortingLayer, int SortingOrder, float CameraDepth);

    private Camera? hudCamera, guitarCamera;
    private CommandBuffer? commandBuffer;
    private List<Renderer> candidateRenderers = new();
    private List<DrawItem> drawItems = new();
    private bool originalHudEnabled;
    private int originalHudCullingMask;
    private bool retainedCameraCanvas;
    private bool active, failed;
    private string? failure;
    private string activationStage = "initialize";

    internal bool IsActive => active;

    internal string? ConsumeFailure()
    {
        string? result = failure;
        failure = null;
        return result;
    }

    internal void Update(bool requested, bool eligible, GameManager? game)
    {
        if (!requested || !eligible || game == null)
        {
            Restore();
            if (!requested) { failed = false; failure = null; }
            return;
        }
        if (active || failed) return;
        try { Activate(game); }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            failure = $"{activationStage}: {ex}";
        }
    }

    private void Activate(GameManager game)
    {
        activationStage = "find HUDCam";
        var hud = GameObject.Find("HUDCam")?.GetComponent<Camera>();
        activationStage = "validate HUDCam configuration";
        if (hud == null || !hud.enabled || hud.targetTexture != null
            || hud.clearFlags != CameraClearFlags.Nothing || !Mathf.Approximately(hud.depth, 6)
            || hud.cullingMask != 4128 || hud.rect != new Rect(0, 0, 1, 1)
            || hud.targetDisplay != 0)
            throw new InvalidOperationException("HUDCam did not match the validated Gameplay camera setup.");

        Camera? guitar = null;
        activationStage = "find the local guitar PlayerCamera";
        if (game.playerObjects == null) throw new InvalidOperationException("Gameplay player objects are unavailable.");
        foreach (var playerObject in game.playerObjects)
        {
            if (playerObject == null || !playerObject.enabled || !playerObject.gameObject.activeInHierarchy
                || playerObject.TryCast<GuitarPlayer>() == null) continue;
            foreach (var candidate in playerObject.GetComponentsInChildren<Camera>(true))
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy
                    || candidate.gameObject.name != "PlayerCamera") continue;
                if (guitar != null) throw new InvalidOperationException("More than one guitar camera is active.");
                guitar = candidate;
            }
        }
        if (guitar == null || guitar.targetTexture != null || guitar.rect != hud.rect
            || guitar.targetDisplay != hud.targetDisplay || (guitar.clearFlags != CameraClearFlags.Depth
                && guitar.clearFlags != CameraClearFlags.Nothing
                && guitar.clearFlags != CameraClearFlags.SolidColor))
            throw new InvalidOperationException("The active guitar camera did not match the solo render setup.");

        Scene scene = SceneManager.GetActiveScene();
        int canvasLayerMask = 0;
        activationStage = "find active camera-space HUD canvases";
        // Object.FindObjectsOfType is used elsewhere in the mod and is known
        // to be preserved by this game's IL2CPP build. FindObjectsOfTypeAll
        // can resolve to a stripped Unity generic wrapper in this player.
        foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
        {
            if (canvas == null || !canvas.gameObject.scene.IsValid() || canvas.gameObject.scene != scene
                || !canvas.enabled || !canvas.gameObject.activeInHierarchy
                || canvas.renderMode == RenderMode.ScreenSpaceOverlay) continue;
            bool usesHudCamera = canvas.worldCamera == hud
                || canvas.renderMode == RenderMode.WorldSpace
                    && (hud.cullingMask & (1 << canvas.gameObject.layer)) != 0;
            if (!usesHudCamera) continue;
            retainedCameraCanvas = true;
            canvasLayerMask |= 1 << canvas.gameObject.layer;
            foreach (var transform in canvas.GetComponentsInChildren<Transform>(true))
                canvasLayerMask |= 1 << transform.gameObject.layer;
        }

        // The command buffer runs immediately after PlayerCamera. Refuse a
        // camera stack that would otherwise draw over the replacement HUD.
        activationStage = "check active camera render order";
        foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>())
        {
            if (camera == null || camera == hud || camera == guitar || !camera.enabled
                || !camera.gameObject.activeInHierarchy || !camera.gameObject.scene.IsValid()
                || camera.gameObject.scene != scene) continue;
            if (camera.depth >= guitar.depth)
                throw new InvalidOperationException($"Camera '{camera.name}' renders at or after the guitar camera and could cover the merged HUD.");
        }

        int mergeMask = hud.cullingMask & ~canvasLayerMask;
        if (mergeMask == 0)
            throw new InvalidOperationException("All HUDCam layers are occupied by camera-space Canvas content; no renderers can be moved to the guitar camera.");
        var candidates = new List<Renderer>();
        activationStage = "find visible HUD renderers";
        foreach (var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>())
        {
            if (renderer == null || !renderer.gameObject.scene.IsValid() || renderer.gameObject.scene != scene
                || (mergeMask & (1 << renderer.gameObject.layer)) == 0) continue;
            candidates.Add(renderer);
        }
        var items = BuildDrawItems(candidates, hud);
        if (items.Count == 0) throw new InvalidOperationException("No active renderer was found on HUDCam's culling layers.");

        var buffer = new CommandBuffer { name = "Sloptifine Experimental HUD Camera Merge" };
        activationStage = "record the HUD camera draw list";
        RecordCommands(buffer, hud, guitar, items, clearDepth: !retainedCameraCanvas);

        hudCamera = hud;
        guitarCamera = guitar;
        commandBuffer = buffer;
        originalHudEnabled = hud.enabled;
        originalHudCullingMask = hud.cullingMask;
        candidateRenderers = candidates;
        drawItems = items;
        activationStage = "change HUDCam render scope";
        if (retainedCameraCanvas)
            hud.cullingMask = originalHudCullingMask & canvasLayerMask;
        else
            hud.enabled = false;
        activationStage = "attach HUD draw list to PlayerCamera";
        guitar.AddCommandBuffer(CameraEvent.AfterEverything, buffer);
        active = true;
        MelonLogger.Msg($"Sloptifine HUD-camera merge prototype active with {items.Count} explicit renderers. HUDCam's orthographic matrices are retained; camera-space Canvas={(retainedCameraCanvas ? "kept on HUDCam" : "none")}.");
    }

    internal void Restore()
    {
        if (guitarCamera != null && commandBuffer != null)
        {
            try { guitarCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, commandBuffer); }
            catch { /* Scene teardown can destroy the camera first. */ }
        }
        try { commandBuffer?.Release(); } catch { }
        commandBuffer = null;
        candidateRenderers.Clear();
        drawItems.Clear();
        bool wasActive = active;
        try
        {
            if (hudCamera != null)
            {
                hudCamera.cullingMask = originalHudCullingMask;
                hudCamera.enabled = originalHudEnabled;
            }
        }
        catch { /* Scene teardown can destroy the camera first. */ }
        hudCamera = guitarCamera = null;
        retainedCameraCanvas = false;
        active = false;
        if (wasActive) MelonLogger.Msg("Sloptifine HUD-camera merge prototype restored HUDCam.");
    }

    internal void Clear()
    {
        Restore();
        failed = false;
        failure = null;
    }

    // Camera matrices and active HUD elements can change during a song. Rebuild
    // the renderer list at the existing 20 Hz override cadence rather than
    // scanning it on every rendered frame.
    internal void RefreshProjection()
    {
        if (!active || hudCamera == null || guitarCamera == null || commandBuffer == null) return;
        try
        {
            activationStage = "refresh HUD camera projection and visible draw list";
            drawItems = BuildDrawItems(candidateRenderers, hudCamera);
            if (drawItems.Count == 0)
                throw new InvalidOperationException("HUDCam's current layers no longer have visible renderer content.");
            RecordCommands(commandBuffer, hudCamera, guitarCamera, drawItems, clearDepth: !retainedCameraCanvas);
        }
        catch (Exception ex)
        {
            Restore();
            failed = true;
            failure = $"The HUD projection refresh failed at '{activationStage}'; the original HUDCam was restored: {ex}";
        }
    }

    private static void RecordCommands(CommandBuffer buffer, Camera hud, Camera guitar, List<DrawItem> items,
        bool clearDepth)
    {
        NativeHudCommands.Resolve();
        buffer.Clear();
        NativeHudCommands.SetMatrices(buffer, hud.worldToCameraMatrix, hud.projectionMatrix);
        // HUDCam uses ClearFlags.Nothing. Clear depth after the guitar pass so
        // its perspective depth values cannot incorrectly occlude orthographic
        // HUD sprites. This runs after the last other camera and clears no color.
        if (clearDepth) NativeHudCommands.ClearDepth(buffer);
        foreach (var item in items)
        {
            for (int submesh = 0; submesh < item.Materials.Length; submesh++)
            {
                Material? material = item.Materials[submesh];
                if (material != null) NativeHudCommands.Draw(buffer, item.Renderer, material, submesh);
            }
        }
        NativeHudCommands.SetMatrices(buffer, guitar.worldToCameraMatrix, guitar.projectionMatrix);
    }

    private static List<DrawItem> BuildDrawItems(List<Renderer> candidates, Camera hud)
    {
        var items = new List<DrawItem>(candidates.Count);
        foreach (var renderer in candidates)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                || !IntersectsCameraViewport(hud, renderer.bounds)) continue;
            var shared = renderer.sharedMaterials;
            if (shared == null || shared.Length == 0) continue;
            var materials = new Material?[shared.Length];
            bool anyMaterial = false;
            for (int i = 0; i < shared.Length; i++)
            {
                materials[i] = shared[i];
                anyMaterial |= materials[i] != null;
            }
            if (!anyMaterial) continue;
            Vector3 cameraPosition = hud.transform.InverseTransformPoint(renderer.bounds.center);
            items.Add(new DrawItem(renderer, materials,
                SortingLayer.GetLayerValueFromID(renderer.sortingLayerID), renderer.sortingOrder, cameraPosition.z));
        }
        items.Sort(static (a, b) =>
        {
            int layer = a.SortingLayer.CompareTo(b.SortingLayer);
            if (layer != 0) return layer;
            int order = a.SortingOrder.CompareTo(b.SortingOrder);
            return order != 0 ? order : a.CameraDepth.CompareTo(b.CameraDepth);
        });
        return items;
    }

    // GeometryUtility.CalculateFrustumPlanes/TestPlanesAABB are stripped in
    // this player. Project the bounds corners through Camera.WorldToScreenPoint
    // instead; that API is already used by Sloptifine's HUD layout code.
    private static bool IntersectsCameraViewport(Camera camera, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        bool hasFrontCorner = false;
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = camera.WorldToScreenPoint(center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z));
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z)
                || point.z < camera.nearClipPlane || point.z > camera.farClipPlane) continue;
            hasFrontCorner = true;
            minX = Math.Min(minX, point.x);
            minY = Math.Min(minY, point.y);
            maxX = Math.Max(maxX, point.x);
            maxY = Math.Max(maxY, point.y);
        }

        if (!hasFrontCorner) return false;
        float marginX = Math.Max(64, Screen.width * .15f);
        float marginY = Math.Max(64, Screen.height * .15f);
        return maxX >= -marginX && minX <= Screen.width + marginX
            && maxY >= -marginY && minY <= Screen.height + marginY;
    }
}

/// <summary>
/// The current IL2CPP build strips CommandBuffer's managed draw/matrix/clear
/// wrappers, even though UnityPlayer registers their native internal calls.
/// Resolve only these exact Unity 2022.3.62f2 bindings; a missing binding
/// aborts the optional HUD merge before HUDCam's state is changed.
/// </summary>
internal static class NativeHudCommands
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetMatricesCall(IntPtr buffer, ref Matrix4x4 view, ref Matrix4x4 projection);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ClearCall(IntPtr buffer, int clearFlags, ref Color background, float depth, uint stencil);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DrawCall(IntPtr buffer, IntPtr renderer, IntPtr material, int submesh, int pass);

    [DllImport("GameAssembly.dll", EntryPoint = "il2cpp_resolve_icall", CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi)]
    private static extern IntPtr ResolveInternalCall(string name);

    private static SetMatricesCall? setMatrices;
    private static ClearCall? clear;
    private static DrawCall? draw;

    internal static void Resolve()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("The native HUD bridge requires the validated Windows x64 game build.");
        if (setMatrices != null && clear != null && draw != null) return;
        setMatrices = Resolve<SetMatricesCall>(
            "UnityEngine.Rendering.CommandBuffer::SetViewProjectionMatrices_Injected");
        clear = Resolve<ClearCall>(
            "UnityEngine.Rendering.CommandBuffer::ClearRenderTarget_Injected");
        draw = Resolve<DrawCall>(
            "UnityEngine.Rendering.CommandBuffer::Internal_DrawRenderer");
    }

    private static T Resolve<T>(string name) where T : Delegate
    {
        IntPtr address = ResolveInternalCall(name);
        if (address == IntPtr.Zero)
            throw new NotSupportedException($"Unity native command '{name}' is unavailable.");
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    internal static void SetMatrices(CommandBuffer buffer, Matrix4x4 view, Matrix4x4 projection)
        => setMatrices!(buffer.Pointer, ref view, ref projection);

    internal static void ClearDepth(CommandBuffer buffer)
    {
        Color clearColor = Color.clear;
        // Unity's legacy ClearRenderTarget(true, false, ...) uses
        // RTClearFlags.DepthStencil (Depth=2 | Stencil=4), depth=1, stencil=0.
        clear!(buffer.Pointer, 6, ref clearColor, 1f, 0);
    }

    internal static void Draw(CommandBuffer buffer, Renderer renderer, Material material, int submesh)
        => draw!(buffer.Pointer, renderer.Pointer, material.Pointer, submesh, -1);
}
