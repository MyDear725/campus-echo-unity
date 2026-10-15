using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

// Opt-in player regression: loads through E, then walks every stair with the
// same gravity/CharacterController method as keyboard input. No stair teleport.
public class TeachingCInteriorQA : MonoBehaviour
{
    CampusGame game;BuildingInteriorTravel travel;string folder;bool testing;int steps;
    void Start(){
        var args=Environment.GetCommandLineArgs();testing=args.Contains("-teachingCQA");
        if(!testing&&!args.Contains("-teachingCPreview"))return;
        game=GetComponent<CampusGame>();travel=GetComponent<BuildingInteriorTravel>();
        if(testing){folder=Path.Combine(Application.dataPath,"../QA/TeachingCInterior");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"result.txt"),"RUNNING\n");Application.logMessageReceived+=Log;StartCoroutine(Timeout());}
        StartCoroutine(Run());
    }
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string text,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error){File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL: "+text+"\n"+stack);Application.Quit(1);}}
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(240);throw new Exception("Teaching C QA timeout");}
    void Require(bool b,string message){if(!b)throw new Exception(message);}
    void CheckEnvelope(){
        for(int f=0;f<5;f++)foreach(float side in new[]{-1f,1f}){
            float cx=side*12,h=f*4.2f;
            for(float x=cx-7.75f;x<=cx+7.75f;x+=.25f){
                if(Mathf.Abs(x-cx)>1.13f)Require(Physics.Raycast(new Vector3(x,h+1.6f,22),Vector3.back,.65f,Physics.DefaultRaycastLayers),"Washroom wall gap at "+new Vector3(x,h,21.5f));
                Require(Physics.Raycast(new Vector3(x,h+3.35f,22),Vector3.back,.65f,Physics.DefaultRaycastLayers),"Washroom lintel gap");
                Require(Physics.Raycast(new Vector3(x,h+3.3f,24.8f),Vector3.up,.65f,Physics.DefaultRaycastLayers),"Washroom ceiling gap");
                Require(Physics.Raycast(new Vector3(x,h+.1f,25),Vector3.down,.2f,Physics.DefaultRaycastLayers),"Washroom floor gap");
            }
        }
    }
    IEnumerator Settle(){float until=Time.realtimeSinceStartup+25;while(travel.Busy&&Time.realtimeSinceStartup<until)yield return null;Require(!travel.Busy,"Transition timed out");yield return null;}
    IEnumerator Walk(float x,float y,float z,float speed=5.4f,float dt=1f/60){
        var target=new Vector3(x,y,z);int count=0;
        while(true){
            var delta=target-game.player.position;delta.y=0;if(delta.magnitude<.045f)break;
            game.MoveIndoor(Vector3.ClampMagnitude(delta,speed*dt),dt);steps++;count++;
            Require(count<2600,"Blocked walking to "+target+" at "+game.player.position);
            if(count%8==0)yield return null;
        }
        for(int i=0;i<22;i++)game.MoveIndoor(Vector3.zero,1f/60);
        Require(Mathf.Abs(game.player.position.y-y)<.13f,"Wrong physical floor at "+target+" actual "+game.player.position);
        Debug.Log("C_WALK_REACHED "+target);
    }
    IEnumerator Run(){
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;yield return new WaitForSecondsRealtime(1);
        var entry=travel.entrances.Single(e=>e.buildingId=="teaching-c");
        var cameraData=game.followCamera.GetUniversalAdditionalCameraData();var outsideRenderer=cameraData.scriptableRenderer;var outsideAA=cameraData.antialiasing;
        float outsideShadow=UniversalRenderPipeline.asset.shadowDistance;
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);game.paused=false;Require(game.Interact(),"C door E interaction failed");yield return Settle();
        Require(travel.Inside&&travel.Interior&&!travel.Interior.isPlaceholder,"C scene did not replace placeholder: "+travel.LastError);
        if(!testing){game.paused=true;game.Notify("C楼：前方两侧为教室和机房，走廊尽头的楼梯可走到五楼；正门按 E 返回校园。");yield break;}
        Require(SceneManager.sceneCount==2,"Expected one additive interior");
        Require(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==1,"Duplicate camera");
        var colliders=travel.Interior.GetComponentsInChildren<BoxCollider>();
        Require(colliders.Count(c=>c.name.StartsWith("Stair ascending tread"))==48&&colliders.Count(c=>c.name.StartsWith("Stair return tread"))==48,"96 physical steps required");
        CheckEnvelope();Require(cameraData.antialiasing==AntialiasingMode.SubpixelMorphologicalAntiAliasing,"Interior anti-aliasing missing");
        // Door passages and aisles on ground and top floor, using continuous movement.
        yield return Walk(0,0,5.5f);yield return Walk(-5.3f,0,5.5f);yield return Walk(-5.3f,0,8.5f);yield return Walk(-5.3f,0,5.5f);yield return Walk(0,0,5.5f);
        yield return Walk(5.3f,0,5.5f);yield return Walk(5.3f,0,8.5f);yield return Walk(5.3f,0,5.5f);yield return Walk(0,0,5.5f);
        yield return Walk(0,0,20);
        foreach(float side in new[]{-1f,1f}){
            yield return Walk(side*12,0,20);yield return Walk(side*12,0,23);yield return Walk(side*7,0,23);yield return Walk(side*7,0,25.2f);
            yield return Walk(side*10.5f,0,25.2f);yield return Walk(side*10.5f,0,27.5f);yield return Walk(side*10.5f,0,25.2f);yield return Walk(side*7,0,25.2f);
            yield return Walk(side*7,0,23);yield return Walk(side*12,0,23);yield return Walk(side*12,0,20);
        }
        yield return Walk(-2,0,20);
        for(int f=0;f<4;f++){
            float h=f*4.2f;yield return Walk(-2,h,21.1f);yield return Walk(-2,h+2.1f,27.6f);yield return Walk(2,h+2.1f,27.6f);yield return Walk(2,h+4.2f,21.1f);yield return Walk(-2,h+4.2f,21.1f);
        }
        yield return Walk(0,16.8f,20);yield return Walk(0,16.8f,13.5f);yield return Walk(-5.3f,16.8f,13.5f);yield return Walk(0,16.8f,13.5f);yield return Walk(5.3f,16.8f,13.5f);yield return Walk(0,16.8f,13.5f);yield return Walk(0,16.8f,20);
        // A real balcony barrier must stop forward motion into the atrium void.
        yield return Walk(0,16.8f,3);
        for(int i=0;i<120;i++)game.MoveIndoor(Vector3.back*.06f,1f/60);
        Require(game.player.position.z>2.25f&&Mathf.Abs(game.player.position.y-16.8f)<.1f,"Atrium guard rail failed");
        yield return Walk(0,16.8f,20);yield return Walk(2,16.8f,21.1f);
        for(int f=3;f>=0;f--){
            float h=f*4.2f;yield return Walk(2,h+4.2f,21.1f);yield return Walk(2,h+2.1f,27.6f);yield return Walk(-2,h+2.1f,27.6f);yield return Walk(-2,h,21.1f);yield return Walk(2,h,21.1f);
        }
        // Shift sprint with the largest normal frame delta (roughly 17 FPS).
        yield return Walk(-2,0,21.1f);yield return Walk(-2,2.1f,27.6f,10,.06f);yield return Walk(2,2.1f,27.6f,10,.06f);yield return Walk(2,4.2f,21.1f,10,.06f);
        yield return Walk(2,2.1f,27.6f,10,.06f);yield return Walk(-2,2.1f,27.6f,10,.06f);yield return Walk(-2,0,21.1f,10,.06f);
        yield return Walk(0,0,19);yield return Walk(0,0,-7.5f);
        game.enabled=false;game.player.gameObject.SetActive(false);var camera=game.followCamera;camera.fieldOfView=64;
        var positions=new[]{new Vector3(3,1.9f,-10),new Vector3(0,5.9f,3),new Vector3(-5.4f,1.8f,8.8f),new Vector3(5.4f,1.8f,16.8f),new Vector3(-1,1.7f,19.3f),new Vector3(8.3f,1.65f,22.2f),new Vector3(5.8f,10.1f,1),new Vector3(13,1.65f,25.1f),new Vector3(11,1.7f,19.8f),new Vector3(7.3f,1.45f,24.7f)};
        var targets=new[]{new Vector3(-3,3.5f,0),new Vector3(0,5.8f,19),new Vector3(-13,1.7f,2.4f),new Vector3(13,1.6f,10.4f),new Vector3(0,3.8f,27),new Vector3(4.8f,1.4f,24.8f),new Vector3(-6,5,-7),new Vector3(10.5f,1.1f,29),new Vector3(12,1.9f,22),new Vector3(4.8f,.9f,24.1f)};
        string[] names={"lobby","corridor","classroom","computer-lab","stairs","washroom","atrium","cubicles","washroom-entrance","basin-detail"};
        for(int i=0;i<names.Length;i++){camera.transform.position=positions[i];camera.transform.LookAt(targets[i]);yield return new WaitForSecondsRealtime(.35f);DormitoryPreview.SaveCamera(camera,Path.Combine(folder,names[i]+".png"));}
        foreach(var r in travel.Interior.GetComponentsInChildren<MeshRenderer>())foreach(var m in r.sharedMaterials)Require(m&&m.shader&&m.shader.isSupported,"Unsupported interior material "+r.name);
        game.player.gameObject.SetActive(true);game.enabled=true;yield return Walk(0,0,-10);
        Require(travel.InteractionPrompt.Contains("返回"),"Exit prompt missing");Require(game.Interact(),"Exit E failed");yield return Settle();
        Require(!travel.Inside&&SceneManager.sceneCount==1,"Interior failed to unload");Require(Vector3.Distance(game.player.position,game.MapToWorld(entry.returnMapPosition))<.05f,"Wrong outdoor return");
        Require(cameraData.scriptableRenderer==UniversalRenderPipeline.asset.GetRenderer(-1),"Exterior default renderer not restored");
        Require(cameraData.antialiasing==outsideAA,"Exterior anti-aliasing not restored: expected "+outsideAA+" got "+cameraData.antialiasing);
        Require(Mathf.Approximately(UniversalRenderPipeline.asset.shadowDistance,outsideShadow),"Exterior shadow distance not restored: expected "+outsideShadow+" got "+UniversalRenderPipeline.asset.shadowDistance);
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: five floors and 96 solid treads; 1F to 5F and back; 10 m/s stair sprint at 60 ms frame delta; ten washroom envelopes checked for floor/ceiling/front-wall/lintel gaps; both washroom vanity and cubicle aisles walked; classroom/lab passages; balcony collision; entry/exit; exterior camera/renderer settings restored; ten rendered views. Movement steps: "+steps+"\n");Application.Quit(0);
    }
}
