using Il2CppInterop.Runtime;
using Il2Cpp;
using MelonLoader;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Batches the regular, upcoming sustain bodies into one world-space mesh.
/// This is a separate opt-in from the older material-flag experiment. Held
/// tails, open tails, glows, and all note/gameplay producers remain native.
/// </summary>
internal sealed class SustainGeometryBatchPrototype
{
    private const int MinimumTails = 8;
    private readonly List<SpriteRenderer> sources = new();
    private readonly List<SpriteRenderer> activeCandidates = new();
    private readonly List<SpriteRenderer> suppressed = new();
    private readonly List<Vector3> vertices = new(250 * 6);
    private readonly List<Vector4> uvs = new(250 * 6);
    private readonly List<Color32> colors = new(250 * 6);
    private readonly List<int> triangles = new(250 * 12);
    private Camera? playerCamera;
    private GameObject? batchObject;
    private Mesh? mesh;
    private MeshRenderer? batchRenderer;
    private Material? sourceMaterial;
    private Material? batchMaterial;
    private MaterialPropertyBlock? propertyBlock;
    private Camera.CameraCallback? preCullCallback, postRenderCallback;
    private Texture2D? atlas;
    private int lengthProperty;
    private float nextCandidateScan;
    private bool active, failed, inRender, loggedFirstBatch;
    private bool skipExternalAlphaSample;
    private bool originalExternalAlphaKeyword;
    private string stage = "initialize";

    internal void Update(bool requested, bool eligible, GameManager? game)
    {
        if (!requested || !eligible || game == null)
        {
            Restore();
            if (!requested) failed = false;
            return;
        }
        if (active || failed) return;
        try { Activate(game); }
        catch (Exception ex) { Fail(ex); }
    }

    internal void Clear()
    {
        Restore();
        failed = false;
    }

    private void Activate(GameManager game)
    {
        stage = "find one active guitar player and camera";
        if (game.playerObjects == null) throw new InvalidOperationException("No player objects.");
        Camera? camera = null;
        foreach (var player in game.playerObjects)
        {
            if (player == null || !player.enabled || !player.gameObject.activeInHierarchy
                || player.TryCast<GuitarPlayer>() == null) continue;
            foreach (var candidate in player.GetComponentsInChildren<Camera>(true))
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy
                    || candidate.gameObject.name != "PlayerCamera") continue;
                if (camera != null) throw new InvalidOperationException("Multiple guitar cameras.");
                camera = candidate;
            }
            foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null || renderer.sharedMaterial?.shader == null
                    || renderer.sharedMaterial.shader.name != "CloneHero/Sustains") continue;
                sources.Add(renderer);
            }
        }
        if (camera == null || sources.Count < MinimumTails)
            throw new InvalidOperationException("Mapped guitar camera or sustain pool is missing.");
        playerCamera = camera;

        stage = "create replacement mesh and material";
        sourceMaterial = sources[0].sharedMaterial;
        if (sourceMaterial == null || sourceMaterial.shader == null
            || sourceMaterial.shader.name != "CloneHero/Sustains")
            throw new InvalidOperationException("Mapped sustain material is missing.");
        batchMaterial = new Material(sourceMaterial) { name = "Sloptifine regular sustain geometry batch" };
        originalExternalAlphaKeyword = Array.IndexOf(batchMaterial.shaderKeywords, "ETC1_EXTERNAL_ALPHA") >= 0;
        skipExternalAlphaSample = !originalExternalAlphaKeyword;
        // The exact shipped shader has a variant without external sizing.
        // Predeform each vertex so one draw can hold different tail lengths.
        batchMaterial.SetFloat("ExternalVertexSizing", 0);
        batchMaterial.DisableKeyword("EXTERNALVERTEXSIZING_ON");
        batchMaterial.SetColor("_RendererColor", Color.white);
        batchMaterial.SetVector("_Flip", new Vector4(1, 1, 1, 1));
        batchMaterial.SetOverrideTag("DisableBatching", "True");
        mesh = new Mesh { name = "Sloptifine regular sustain batch" };
        mesh.MarkDynamic();
        batchObject = new GameObject("Sloptifine regular sustain batch");
        batchObject.layer = sources[0].gameObject.layer;
        var filter = batchObject.AddComponent<MeshFilter>();
        batchRenderer = batchObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        batchRenderer.sharedMaterial = batchMaterial;
        batchRenderer.sortingLayerID = sources[0].sortingLayerID;
        batchRenderer.sortingOrder = sources[0].sortingOrder;
        batchRenderer.enabled = false;
        propertyBlock = new MaterialPropertyBlock();
        lengthProperty = Shader.PropertyToID("_SustainLength");
        stage = "resolve native material-property-block length reader";
        NativePropertyBlockFloat.Resolve();

        stage = "attach camera callbacks";
        preCullCallback = DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(OnPreCull));
        postRenderCallback = DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(OnPostRender));
        Camera.onPreCull += preCullCallback;
        Camera.onPostRender += postRenderCallback;
        active = true;
        MelonLogger.Msg($"Sloptifine regular sustain geometry prototype ready for {sources.Count} pooled renderers; batches only frames with at least {MinimumTails} compatible upcoming tails.");
    }

    private void OnPreCull(Camera camera)
    {
        if (!active || camera == null || camera != playerCamera || inRender) return;
        try { BuildFrame(); }
        catch (Exception ex) { Fail(ex); }
    }

    internal void SetSkipExternalAlphaSample(bool skip)
    {
        // If the guarded alpha switch ran before this mesh material was
        // cloned, its source keyword was already off. The switch only marks
        // this shader active after removing a previously enabled keyword.
        if (skip) originalExternalAlphaKeyword = true;
        if (!originalExternalAlphaKeyword) return;
        if (batchMaterial == null || skip == skipExternalAlphaSample) return;
        if (skip) batchMaterial.DisableKeyword("ETC1_EXTERNAL_ALPHA");
        else batchMaterial.EnableKeyword("ETC1_EXTERNAL_ALPHA");
        skipExternalAlphaSample = skip;
    }

    private void BuildFrame()
    {
        RestoreFrame();
        // Scan the 250-slot pool at 20 Hz. New tails that appear between scans
        // simply stay on the native renderer until the next scan, so this
        // discovery rate cannot delay a note or leave a hole on screen.
        if (Time.unscaledTime >= nextCandidateScan)
        {
            stage = "refresh active sustain pool slots";
            activeCandidates.Clear();
            foreach (var source in sources)
            {
                if (source == null || !source.gameObject.activeInHierarchy) continue;
                var sprite = source.sprite;
                if (sprite != null && IsUpcomingSprite(sprite.name)) activeCandidates.Add(source);
            }
            nextCandidateScan = Time.unscaledTime + .05f;
        }
        if (activeCandidates.Count < MinimumTails) return;
        stage = "read visible regular tails";
        vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear();
        Texture2D? currentAtlas = null;
        int sortingLayer = sources[0].sortingLayerID, sortingOrder = sources[0].sortingOrder;
        int layer = sources[0].gameObject.layer;
        foreach (var source in activeCandidates)
        {
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy
                || source.sortingLayerID != sortingLayer || source.sortingOrder != sortingOrder
                || source.gameObject.layer != layer || source.sharedMaterial != sourceMaterial) continue;
            var sprite = source.sprite;
            if (sprite == null || !IsUpcomingSprite(sprite.name)) continue;
            if (sprite.packed && sprite.packingRotation != SpritePackingRotation.None) continue;
            var texture = sprite.texture;
            if (texture == null || (currentAtlas != null && currentAtlas != texture)) continue;
            // This prototype reproduces the shipped 73x70 sprite with its
            // 38-pixel upper border and a 100-unit sliced renderer. Refuse a
            // different sprite layout instead of drawing a distorted tail.
            var rect = sprite.rect;
            var border = sprite.border;
            var size = source.size;
            if (Mathf.Abs(rect.width - 73) > .01f || Mathf.Abs(rect.height - 70) > .01f
                || Mathf.Abs(border.w - 38) > .01f || border.x != 0 || border.y != 0
                || border.z != 0 || Mathf.Abs(size.y - 100) > .01f
                || Mathf.Abs(sprite.pixelsPerUnit - 100) > .01f) continue;
            currentAtlas ??= texture;
            propertyBlock!.Clear();
            source.GetPropertyBlock(propertyBlock);
            float length = NativePropertyBlockFloat.Get(propertyBlock, lengthProperty);
            if (float.IsNaN(length) || float.IsInfinity(length)) continue;
            AddTail(source, sprite, size, length);
            suppressed.Add(source);
        }

        if (suppressed.Count < MinimumTails || currentAtlas == null)
        {
            suppressed.Clear();
            return;
        }

        stage = "upload combined sustain mesh";
        mesh!.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.colors32 = colors.ToArray();
        mesh.SetUVs(0, uvs.ToArray());
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateBounds();
        if (atlas != currentAtlas)
        {
            batchMaterial!.SetTexture("_MainTex", currentAtlas);
            atlas = currentAtlas;
        }
        batchRenderer!.enabled = true;
        stage = "suppress only emitted source renderers";
        // Re-enable after this camera finishes, so the game sees its original
        // renderer state on the next simulation update and on other cameras.
        foreach (var source in suppressed) source.enabled = false;
        inRender = true;
        if (!loggedFirstBatch)
        {
            loggedFirstBatch = true;
            MelonLogger.Msg($"Sloptifine sustain geometry drew its first batch: {suppressed.Count} regular tails in one mesh.");
        }
    }

    private void AddTail(SpriteRenderer renderer, Sprite sprite, Vector2 size, float length)
    {
        Rect tex = sprite.textureRect;
        Texture2D texture = sprite.texture;
        float u0 = tex.xMin / texture.width, u1 = tex.xMax / texture.width;
        float v0 = tex.yMin / texture.height, v1 = tex.yMax / texture.height;
        float vm = v0 + (v1 - v0) * ((sprite.rect.height - sprite.border.w) / sprite.rect.height);
        float halfWidth = size.x * .5f;
        float yMiddle = Mathf.Max(Mathf.Max(length, 0) + (size.y - sprite.border.w / sprite.pixelsPerUnit) - 100, 0);
        float yTop = Mathf.Max(Mathf.Max(length, 0) + size.y - 100, 0);
        Matrix4x4 transform = renderer.transform.localToWorldMatrix;
        Color32 tint = renderer.color;
        int first = vertices.Count;
        AddVertex(-halfWidth, 0, u0, v0);
        AddVertex(halfWidth, 0, u1, v0);
        AddVertex(-halfWidth, yMiddle, u0, vm);
        AddVertex(halfWidth, yMiddle, u1, vm);
        AddVertex(-halfWidth, yTop, u0, v1);
        AddVertex(halfWidth, yTop, u1, v1);
        triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
        triangles.Add(first + 1); triangles.Add(first + 2); triangles.Add(first + 3);
        triangles.Add(first + 2); triangles.Add(first + 4); triangles.Add(first + 3);
        triangles.Add(first + 3); triangles.Add(first + 4); triangles.Add(first + 5);
        void AddVertex(float x, float y, float u, float v)
        {
            vertices.Add(transform.MultiplyPoint3x4(new Vector3(x, y, 0)));
            uvs.Add(new Vector4(u, v, 0, 0));
            colors.Add(tint);
        }
    }

    private static bool IsUpcomingSprite(string name)
        => name == "spr_sustain_strip6_0" || name == "spr_sustain_strip6_1"
            || name == "spr_sustain_strip6_2";

    private void OnPostRender(Camera camera)
    {
        if (camera != playerCamera) return;
        RestoreFrame();
    }

    private void RestoreFrame()
    {
        if (!inRender && suppressed.Count == 0) return;
        foreach (var source in suppressed)
        {
            try { if (source != null && !source.enabled) source.enabled = true; }
            catch { /* Scene teardown can destroy a pooled renderer. */ }
        }
        suppressed.Clear();
        inRender = false;
        try { if (batchRenderer != null) batchRenderer.enabled = false; } catch { }
    }

    private void Fail(Exception ex)
    {
        Restore();
        failed = true;
        MelonLogger.Warning($"Sloptifine sustain geometry prototype stopped at '{stage}' and restored native renderers: {ex}");
    }

    private void Restore()
    {
        RestoreFrame();
        active = false;
        try { if (preCullCallback != null) Camera.onPreCull -= preCullCallback; } catch { }
        try { if (postRenderCallback != null) Camera.onPostRender -= postRenderCallback; } catch { }
        preCullCallback = null; postRenderCallback = null;
        if (batchObject != null) { try { UnityEngine.Object.Destroy(batchObject); } catch { } }
        if (mesh != null) { try { UnityEngine.Object.Destroy(mesh); } catch { } }
        if (batchMaterial != null) { try { UnityEngine.Object.Destroy(batchMaterial); } catch { } }
        batchObject = null; mesh = null; batchRenderer = null; sourceMaterial = null; batchMaterial = null;
        propertyBlock = null; playerCamera = null; atlas = null;
        sources.Clear();
        activeCandidates.Clear();
        nextCandidateScan = 0;
        loggedFirstBatch = false;
        skipExternalAlphaSample = false;
        originalExternalAlphaKeyword = false;
    }
}

/// <summary>
/// This Unity player strips MaterialPropertyBlock.GetFloat's managed wrapper
/// while registering the native GetFloatImpl internal call. Resolve the exact
/// x64 call only when the separate geometry option is explicitly enabled.
/// </summary>
internal static class NativePropertyBlockFloat
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate float GetFloatCall(IntPtr block, int propertyId);

    [DllImport("GameAssembly.dll", EntryPoint = "il2cpp_resolve_icall", CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi)]
    private static extern IntPtr ResolveInternalCall(string name);

    private static GetFloatCall? getFloat;

    internal static void Resolve()
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("The sustain geometry bridge requires the validated Windows x64 game build.");
        if (getFloat != null) return;
        IntPtr address = ResolveInternalCall("UnityEngine.MaterialPropertyBlock::GetFloatImpl");
        if (address == IntPtr.Zero)
            throw new NotSupportedException("The Unity material-property-block float reader is unavailable.");
        getFloat = Marshal.GetDelegateForFunctionPointer<GetFloatCall>(address);
    }

    internal static float Get(MaterialPropertyBlock block, int propertyId)
        => getFloat!(block.Pointer, propertyId);
}
