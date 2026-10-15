using UnityEngine;
using UnityEngine.Rendering.Universal;

// Local presentation settings only while C is occupied. Restore the shared
// camera/pipeline on exit, including failed unload/destruction paths.
[RequireComponent(typeof(InteriorSceneRoot))]
public sealed class TeachingCInteriorPresentation : MonoBehaviour
{
    public int rendererIndex=1;
    public int exteriorRendererIndex=-1;
    Camera camera;UniversalAdditionalCameraData data;UniversalRenderPipelineAsset pipeline;
    AntialiasingMode aa;AntialiasingQuality quality;bool post,msaa,active;float farClip,shadowDistance;int cascades;
    void Awake(){var root=GetComponent<InteriorSceneRoot>();root.onPlayerEntered.AddListener(Apply);root.onPlayerLeaving.AddListener(Restore);}
    void Apply(){
        if(active)return;var game=FindFirstObjectByType<CampusGame>();if(!game)return;
        camera=game.followCamera;data=camera.GetUniversalAdditionalCameraData();pipeline=UniversalRenderPipeline.asset;
        aa=data.antialiasing;quality=data.antialiasingQuality;post=data.renderPostProcessing;msaa=camera.allowMSAA;farClip=camera.farClipPlane;
        if(pipeline){shadowDistance=pipeline.shadowDistance;cascades=pipeline.shadowCascadeCount;pipeline.shadowDistance=42;pipeline.shadowCascadeCount=4;}
        data.SetRenderer(rendererIndex);data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.High;
        camera.allowMSAA=true;camera.farClipPlane=90;active=true;
    }
    void Restore(){
        if(!active)return;active=false;
        if(data){data.SetRenderer(exteriorRendererIndex);data.antialiasing=aa;data.antialiasingQuality=quality;data.renderPostProcessing=post;}
        if(camera){camera.allowMSAA=msaa;camera.farClipPlane=farClip;}
        if(pipeline){pipeline.shadowDistance=shadowDistance;pipeline.shadowCascadeCount=cascades;}
    }
    void OnDestroy(){Restore();}
}
