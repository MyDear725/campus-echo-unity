using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Capture after the campus has been hidden and the interior environment applied.
// OnAwake capture would include exterior geometry overlapping the local interior.
[RequireComponent(typeof(InteriorSceneRoot))]
public sealed class GymnasiumLighting : MonoBehaviour
{
    UniversalRenderPipelineAsset pipeline;int previousLimit;bool active;
    void Awake(){var root=GetComponent<InteriorSceneRoot>();root.onPlayerEntered.AddListener(Refresh);root.onPlayerLeaving.AddListener(Restore);}
    void Refresh(){
        if(!active){pipeline=UniversalRenderPipeline.asset;if(pipeline){previousLimit=pipeline.maxAdditionalLightsCount;pipeline.maxAdditionalLightsCount=8;active=true;}}
        StartCoroutine(Capture());
    }
    void Restore(){if(active&&pipeline)pipeline.maxAdditionalLightsCount=previousLimit;active=false;}
    void OnDestroy(){Restore();}
    IEnumerator Capture(){
        yield return null;
        foreach(var probe in GetComponentsInChildren<ReflectionProbe>()){
            int id=probe.RenderProbe();float until=Time.realtimeSinceStartup+4;
            while(!probe.IsFinishedRendering(id)&&Time.realtimeSinceStartup<until)yield return null;
        }
    }
}
