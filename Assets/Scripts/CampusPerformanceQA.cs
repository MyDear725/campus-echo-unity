using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Opt-in reproducible frame timings; private QA save keys are assigned by the host.
public sealed class CampusPerformanceQA : MonoBehaviour
{
    CampusGame game;BuildingInteriorTravel travel;string folder;readonly List<string> rows=new();
    ProfilerRecorder main,render,draws,triangles,gc;readonly FrameTiming[] timing=new FrameTiming[1];
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot(){if(Environment.GetCommandLineArgs().Contains("-performanceQA")){var go=new GameObject("Campus performance benchmark");DontDestroyOnLoad(go);go.AddComponent<CampusPerformanceQA>();}}
    void Awake()
    {
        folder=Path.Combine(Application.dataPath,"../QA/Performance");Directory.CreateDirectory(folder);
        main=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);render=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Render Thread",1);
        draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1);gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
        rows.Add("view,condition,frames,mean_ms,p95_ms,main_ms,render_ms,gpu_ms,draw_calls,triangles,gc_bytes");
    }
    void OnDestroy(){main.Dispose();render.Dispose();draws.Dispose();triangles.Dispose();gc.Dispose();}
    IEnumerator Sample(string view,string condition)
    {
        yield return new WaitForSecondsRealtime(1.2f);
        var samples=new List<double>();double cpu=0,rt=0,gpu=0,dc=0,tr=0,alloc=0;int gpuSamples=0;
        float until=Time.realtimeSinceStartup+3;
        while(Time.realtimeSinceStartup<until||samples.Count<90)
        {
            yield return null;samples.Add(Time.unscaledDeltaTime*1000);
            if(main.Valid)cpu+=main.LastValue/1e6;if(render.Valid)rt+=render.LastValue/1e6;
            if(draws.Valid)dc+=draws.LastValue;if(triangles.Valid)tr+=triangles.LastValue;if(gc.Valid)alloc+=gc.LastValue;
            FrameTimingManager.CaptureFrameTimings();if(FrameTimingManager.GetLatestTimings(1,timing)>0&&timing[0].gpuFrameTime>0){gpu+=timing[0].gpuFrameTime;gpuSamples++;}
        }
        samples.Sort();int n=samples.Count;string row=FormattableString.Invariant($"{view},{condition},{n},{samples.Average():F2},{samples[(int)((n-1)*.95)]:F2},{cpu/n:F2},{rt/n:F2},{gpu/Math.Max(1,gpuSamples):F2},{dc/n:F0},{tr/n:F0},{alloc/n:F0}");
        rows.Add(row);File.WriteAllLines(folder+"/"+(Environment.GetCommandLineArgs().Contains("-performanceAfter")?"after":"before")+".csv",rows);Debug.Log("PERF_SAMPLE "+row);
    }
    void Pose(Vector3 map,float yaw){if(travel.Inside)travel.IndoorController.enabled=false;game.SetTravelPose(travel.Inside?travel.Interior.transform.TransformPoint(map):game.MapToWorld(map),yaw);Physics.SyncTransforms();if(travel.Inside)travel.IndoorController.enabled=true;}
    IEnumerator Enter(string id)
    {
        var entry=travel.entrances.First(e=>e&&e.buildingId==id);game.enabled=true;game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);
        if(!travel.TryEnter(entry))throw new Exception("Performance entry rejected "+id);while(travel.Busy)yield return null;yield return new WaitForSecondsRealtime(1);
    }
    IEnumerator Exit(){Pose(travel.Interior.transform.InverseTransformPoint(travel.Interior.exits[0].position),0);if(!travel.TryExit())throw new Exception("Performance exit rejected");while(travel.Busy)yield return null;}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(3);game=FindFirstObjectByType<CampusGame>();travel=game.Interiors;game.paused=false;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
        File.WriteAllText(folder+"/hardware.txt",SystemInfo.graphicsDeviceName+"\n"+SystemInfo.processorType+"\n"+Screen.width+"x"+Screen.height+"\nMain recorder valid: "+main.Valid+"\nRender recorder valid: "+render.Valid);
        Pose(new Vector3(3,.23f,184),180);yield return Sample("entrance","full");
        Pose(new Vector3(68,.23f,-29),180);yield return Sample("teaching","full");
        var skins=game.player.GetComponentsInChildren<SkinnedMeshRenderer>().Concat(game.guide.GetComponentsInChildren<SkinnedMeshRenderer>()).ToArray();
        foreach(var skin in skins)skin.forceRenderingOff=true;yield return Sample("teaching","hide-avatars");foreach(var skin in skins)skin.forceRenderingOff=false;
        var pipeline=UniversalRenderPipeline.asset;float distance=pipeline.shadowDistance,scale=pipeline.renderScale;
        pipeline.shadowDistance=0;yield return Sample("teaching","no-shadows");pipeline.shadowDistance=distance;
        pipeline.renderScale=.75f;yield return Sample("teaching","75pct-resolution");pipeline.renderScale=scale;
        Pose(new Vector3(145,6.54f,10),270);yield return Sample("bridge","full");
        yield return Enter("library");Pose(new Vector3(46,4.36f,-7),90);yield return Sample("library","full");yield return Exit();
        yield return Enter("cafeteria");Pose(new Vector3(-13,.06f,-2),90);yield return Sample("cafeteria","full");yield return Exit();
        Debug.Log("PERFORMANCE_QA_COMPLETE");Application.Quit(0);
    }
}
