using UnityEngine;
using UnityEngine.Rendering.Universal;

// Per-building interior presentation, mirroring TeachingCInteriorPresentation but
// parameterised so cafeterias and libraries can share it.
//
// While the player is inside this building it switches the follow camera to the
// building's own interior renderer (SSAO contact shadows), turns on SMAA High and
// post-processing, trims the far clip, and widens the URP shadow distance to suit the
// interior size. Everything captured in Apply() is put back on exit, on a failed
// unload, or when the object is destroyed, so no state leaks into the campus or into
// another building.
[RequireComponent(typeof(InteriorSceneRoot))]
public sealed class InteriorPresentation : MonoBehaviour
{
    [Tooltip("Renderer index this building uses while occupied.")]
    public int rendererIndex = 1;

    [Tooltip("Renderer to restore on returning to campus (-1 uses the pipeline default).")]
    public int exteriorRendererIndex = -1;

    [Tooltip("Camera far clip while inside, sized to the interior's diagonal.")]
    public float interiorFarClip = 90f;

    [Tooltip("Shadow distance for the interior; larger halls need more.")]
    public float interiorShadowDistance = 45f;

    [Tooltip("Shadow cascades while inside (0 keeps the pipeline default).")]
    public int interiorCascades = 4;

    CampusGame game;
    Camera cam;
    UniversalAdditionalCameraData data;
    UniversalRenderPipelineAsset pipeline;

    AntialiasingMode aa;
    AntialiasingQuality aaQuality;
    bool post;
    bool msaa;
    bool active;
    float farClip;
    float shadowDistance;
    int cascades;

    void Awake()
    {
        var root = GetComponent<InteriorSceneRoot>();
        if (root)
        {
            root.onPlayerEntered.AddListener(Apply);
            root.onPlayerLeaving.AddListener(Restore);
        }
    }

    void Apply()
    {
        if (active) return;

        game = FindFirstObjectByType<CampusGame>();
        if (!game || !game.followCamera) return;

        cam = game.followCamera;
        data = cam.GetUniversalAdditionalCameraData();
        pipeline = UniversalRenderPipeline.asset;
        if (!data) return;

        // Snapshot everything before touching it.
        aa = data.antialiasing;
        aaQuality = data.antialiasingQuality;
        post = data.renderPostProcessing;
        msaa = cam.allowMSAA;
        farClip = cam.farClipPlane;
        if (pipeline)
        {
            shadowDistance = pipeline.shadowDistance;
            cascades = pipeline.shadowCascadeCount;
        }

        if (pipeline)
        {
            pipeline.shadowDistance = interiorShadowDistance;
            if (interiorCascades > 0) pipeline.shadowCascadeCount = interiorCascades;
        }

        data.SetRenderer(rendererIndex);
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        cam.allowMSAA = true;
        cam.farClipPlane = interiorFarClip;

        active = true;
    }

    void Restore()
    {
        if (!active) return;
        active = false;

        if (data)
        {
            data.SetRenderer(exteriorRendererIndex);
            data.antialiasing = aa;
            data.antialiasingQuality = aaQuality;
            data.renderPostProcessing = post;
        }
        if (cam) cam.allowMSAA = msaa;
        if (cam) cam.farClipPlane = farClip;
        if (pipeline)
        {
            pipeline.shadowDistance = shadowDistance;
            pipeline.shadowCascadeCount = cascades;
        }

        data = null;
        cam = null;
        pipeline = null;
    }

    // Covers the interior being unloaded out from under us.
    void OnDisable() => Restore();
    void OnDestroy() => Restore();
}
