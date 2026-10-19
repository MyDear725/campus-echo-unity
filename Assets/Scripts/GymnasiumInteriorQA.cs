using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

// Opt-in validation uses the real entry interaction and continuous character movement.
public sealed class GymnasiumInteriorQA : MonoBehaviour
{
    CampusGame game;BuildingInteriorTravel travel;bool testing;string folder;int steps;
    void Start(){
        var args=Environment.GetCommandLineArgs();testing=args.Contains("-gymInteriorQA");if(!testing&&!args.Contains("-gymInteriorPreview"))return;
        game=GetComponent<CampusGame>();travel=GetComponent<BuildingInteriorTravel>();
        if(testing){folder=Path.Combine(Application.dataPath,"../QA/GymnasiumInterior");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"result.txt"),"RUNNING\n");Application.logMessageReceived+=Log;StartCoroutine(Timeout());}
        StartCoroutine(Run());
    }
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error){File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL: "+message+"\n"+stack);Application.Quit(1);}}
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(360);throw new Exception("Gym QA timeout");}
    void Require(bool ok,string text){if(!ok)throw new Exception(text);}
    IEnumerator Settle(){float until=Time.realtimeSinceStartup+30;while(travel.Busy&&Time.realtimeSinceStartup<until)yield return null;Require(!travel.Busy,"Interior transition timed out");yield return null;}
    IEnumerator Walk(float x,float h,float z,float speed=5.4f,float dt=1f/60){
        var target=new Vector3(x,h,z);int count=0;
        while(true){var delta=target-game.player.position;delta.y=0;if(delta.magnitude<.045f)break;game.MoveIndoor(Vector3.ClampMagnitude(delta,speed*dt),dt);steps++;count++;Require(count<2200,"Blocked walking to "+target+" at "+game.player.position);if(count%8==0)yield return null;}
        for(int i=0;i<30;i++)game.MoveIndoor(Vector3.zero,1f/60);
        Require(Mathf.Abs(game.player.position.y-h)<.15f,"Wrong floor at "+target+" actual "+game.player.position);Debug.Log("GYM_WALK_REACHED "+target);
    }
    IEnumerator Run(){
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;yield return new WaitForSecondsRealtime(1);
        var entry=travel.entrances.Single(e=>e.buildingId=="gymnasium");var data=game.followCamera.GetUniversalAdditionalCameraData();var outsideAA=data.antialiasing;float outsideShadow=UniversalRenderPipeline.asset.shadowDistance;int outsideLights=UniversalRenderPipeline.asset.maxAdditionalLightsCount;
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);game.paused=false;Require(game.Interact(),"Gym E entry failed");yield return Settle();
        Require(travel.Inside&&travel.Interior&&!travel.Interior.isPlaceholder,"Gym scene missing: "+travel.LastError);
        if(!testing){game.paused=true;game.Notify("体育馆已开放三层：沿中央通道到后侧楼梯可步行上下楼，正门按 E 返回校园。");yield break;}
        Require(SceneManager.sceneCount==2,"Unexpected loaded scenes");Require(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==1,"Duplicate gameplay camera");
        var colliders=travel.Interior.GetComponentsInChildren<BoxCollider>();Require(colliders.Count(c=>c.name.StartsWith("Gym stair"))==96,"Expected 96 physical stair treads");
        for(int f=0;f<3;f++){
            float h=f*8.4f;yield return Walk(0,h,-4);yield return Walk(-8,h,-4);yield return Walk(-8,h,-11.5f);yield return Walk(-32,h,-11.5f);yield return Walk(-8,h,-11.5f);yield return Walk(-8,h,-4);yield return Walk(0,h,-4);
            yield return Walk(8,h,-4);yield return Walk(8,h,0);yield return Walk(8,h,-4);yield return Walk(0,h,-4);
            yield return Walk(0,h,12);yield return Walk(8,h,12);yield return Walk(8,h,20);yield return Walk(8,h,12);yield return Walk(0,h,12);
            // Closed glazing keeps the player inside; all front-lobby floor joins are supported.
            foreach(float x in new[]{-41f,-23f,-4f,0f,4f,23f,41f})Require(Physics.Raycast(new Vector3(x,h+.5f,-19),Vector3.down,1),"Lobby floor gap");
            yield return Walk(-2,h,15.4f);
            if(f<2){yield return Walk(-2,h+4.2f,26.5f);yield return Walk(2,h+4.2f,26.5f);yield return Walk(2,h+8.4f,15.4f);yield return Walk(0,h+8.4f,14.5f);}
        }
        yield return Walk(2,16.8f,15.4f);
        for(int f=1;f>=0;f--){float h=f*8.4f;yield return Walk(2,h+4.2f,26.5f);yield return Walk(-2,h+4.2f,26.5f);yield return Walk(-2,h,15.4f);yield return Walk(2,h,15.4f);}
        yield return Walk(-2,0,15.4f);yield return Walk(-2,4.2f,26.5f,10,.06f);yield return Walk(2,4.2f,26.5f,10,.06f);yield return Walk(2,8.4f,15.4f,10,.06f);
        yield return Walk(2,4.2f,26.5f,10,.06f);yield return Walk(-2,4.2f,26.5f,10,.06f);yield return Walk(-2,0,15.4f,10,.06f);
        game.enabled=false;game.player.gameObject.SetActive(false);var camera=game.followCamera;camera.fieldOfView=64;
        var positions=new[]{new Vector3(2,1.8f,-22),new Vector3(-9,2.4f,-9),new Vector3(8,1.8f,-9),new Vector3(8,1.8f,12),new Vector3(-8,10.8f,-9),new Vector3(8,10.8f,-10),new Vector3(-8,18.6f,-10),new Vector3(8,18.5f,-10),new Vector3(1,1.8f,13.5f)};
        var targets=new[]{new Vector3(-5,2,-13),new Vector3(-25,2.1f,10),new Vector3(26,1.4f,0),new Vector3(26,1.7f,25),new Vector3(-25,10.2f,9),new Vector3(24,11,10),new Vector3(-27,17.7f,8),new Vector3(27,18,8),new Vector3(-1,5,26)};
        string[] names={"01-lobby","02-volleyball","03-fitness","04-dance","05-badminton","06-basketball","07-table-tennis","08-billiards","09-stairs"};
        for(int i=0;i<names.Length;i++){camera.transform.position=positions[i];camera.transform.LookAt(targets[i]);yield return new WaitForSecondsRealtime(.6f);DormitoryPreview.SaveCamera(camera,Path.Combine(folder,names[i]+".png"));}
        foreach(var r in travel.Interior.GetComponentsInChildren<MeshRenderer>())foreach(var m in r.sharedMaterials)Require(m&&m.shader&&m.shader.isSupported,"Unsupported gym shader: "+r.name);
        game.player.gameObject.SetActive(true);game.enabled=true;yield return Walk(0,0,13);yield return Walk(0,0,-22);
        Require(travel.InteractionPrompt.Contains("返回"),"Gym exit prompt missing");Require(game.Interact(),"Gym exit failed");yield return Settle();
        Require(!travel.Inside&&SceneManager.sceneCount==1,"Gym not unloaded");Require(Vector3.Distance(game.player.position,game.MapToWorld(entry.returnMapPosition))<.1f,"Wrong exterior return point");
        Require(UniversalRenderPipeline.asset.maxAdditionalLightsCount==outsideLights,"Exterior light limit not restored");
        Require(data.antialiasing==outsideAA&&Mathf.Approximately(UniversalRenderPipeline.asset.shadowDistance,outsideShadow),"Outdoor camera settings not restored");
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: E entry/exit; seven room routes on three storeys; all 96 solid stair treads; continuous 1F-3F ascent/descent; stair sprint at 10m/s with 60ms frame delta; lobby floor joints; materials; nine rendered views; exterior settings restored. Movement steps: "+steps);Application.Quit(0);
    }
}
