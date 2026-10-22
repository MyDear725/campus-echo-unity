using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

// Opt-in integration check, using the same E dispatcher and continuous physical
// movement as the player. Preview flags enter the real scene through its door.
public sealed class TeachingABInteriorQA : MonoBehaviour
{
    CampusGame game;BuildingInteriorTravel travel;bool testing;string folder;int steps;
    void Start(){
        var args=Environment.GetCommandLineArgs();testing=args.Contains("-teachingABQA");
        string letter=args.Contains("-teachingAPreview")?"A":args.Contains("-teachingBPreview")?"B":null;
        if(!testing&&letter==null)return;
        game=GetComponent<CampusGame>();travel=GetComponent<BuildingInteriorTravel>();
        if(testing){folder=Path.Combine(Application.dataPath,"../QA/TeachingABInterior");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"result.txt"),"RUNNING");Application.logMessageReceived+=Log;StartCoroutine(Timeout());}
        StartCoroutine(Run(letter));
    }
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL: "+text+"\n"+stack);Application.Quit(1);}}
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(300);throw new Exception("A/B QA timeout");}
    void Check(bool result,string message){if(!result)throw new Exception(message);}
    IEnumerator Settle(){float until=Time.realtimeSinceStartup+30;while(travel.Busy&&Time.realtimeSinceStartup<until)yield return null;Check(!travel.Busy,"Transition timed out");yield return null;}
    IEnumerator Walk(float x,float y,float z,float speed=5.4f,float dt=1f/60){
        var target=new Vector3(x,y,z);int count=0;
        while(true){var delta=target-game.player.position;delta.y=0;if(delta.magnitude<.045f)break;game.MoveIndoor(Vector3.ClampMagnitude(delta,speed*dt),dt);steps++;count++;Check(count<2300,"Blocked: "+target+" actual "+game.player.position);if(count%10==0)yield return null;}
        for(int i=0;i<22;i++)game.MoveIndoor(Vector3.zero,1f/60);
        Check(Mathf.Abs(game.player.position.y-y)<.13f,"Wrong floor: "+target+" actual "+game.player.position);
    }
    IEnumerator Floor(string letter,int floor){
        float y=floor*4.2f;
        yield return Walk(0,y,20);yield return Walk(0,y,5.5f);
        foreach(float side in new[]{-1f,1f}){
            yield return Walk(side*5.3f,y,5.5f);
            if(letter=="A"){
                for(int row=0;row<5;row++)yield return Walk(side*5.3f,y+(row+1)*.18f,7.1f+row*1.8f);
                yield return Walk(side*5.3f,y+.9f,16.2f);
                yield return Walk(side*5.3f,y,5.5f,10,.06f);
            }else yield return Walk(side*5.3f,y,8.5f);
            yield return Walk(side*5.3f,y,5.5f);yield return Walk(0,y,5.5f);
        }
        if(letter=="B"){
            yield return Walk(0,y,13.5f);
            foreach(float side in new[]{-1f,1f}){yield return Walk(side*5.3f,y,13.5f);yield return Walk(side*5.3f,y,16.5f);yield return Walk(side*5.3f,y,13.5f);yield return Walk(0,y,13.5f);}
            yield return Walk(0,y,-6.8f);
            foreach(float side in new[]{-1f,1f}){yield return Walk(side*5.2f,y,-6.8f);yield return Walk(side*1.7f,y,-6.8f);yield return Walk(side*1.7f,y,0);yield return Walk(0,y,0);yield return Walk(0,y,-6.8f);}
        }
        yield return Walk(0,y,20);
        foreach(float side in new[]{-1f,1f}){yield return Walk(side*12,y,20);yield return Walk(side*12,y,23);yield return Walk(side*7,y,23);yield return Walk(side*7,y,25.2f);yield return Walk(side*7,y,23);yield return Walk(side*12,y,23);yield return Walk(side*12,y,20);}
        yield return Walk(-2,y,20);
    }
    IEnumerator Capture(string letter){
        var camera=game.followCamera;var pose=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        game.enabled=false;game.player.gameObject.SetActive(false);camera.fieldOfView=68;
        Vector3[] positions=letter=="A"?new[]{new Vector3(3,1.9f,-10),new Vector3(-5.6f,2.5f,15.7f),new Vector3(-17.8f,1.8f,4.7f),new Vector3(-1,1.7f,19.3f)}:new[]{new Vector3(0,1.9f,-9.5f),new Vector3(-5.4f,1.8f,8.8f),new Vector3(-5.4f,1.8f,16.8f),new Vector3(5.4f,1.8f,8.8f),new Vector3(5.4f,1.8f,16.8f),new Vector3(0,10.2f,-9.5f)};
        Vector3[] targets=letter=="A"?new[]{new Vector3(-3,3.5f,0),new Vector3(-13,1.8f,2.5f),new Vector3(-12,1.8f,13),new Vector3(0,3.8f,27)}:new[]{new Vector3(2.8f,1.4f,-2.5f),new Vector3(-13,1.7f,2.4f),new Vector3(-13,1.7f,10.4f),new Vector3(13,1.4f,3),new Vector3(13,1.5f,12),new Vector3(3.1f,9.7f,-2.5f)};
        string[] names=letter=="A"?new[]{"lobby","lecture-rear","lecture-front","stairs"}:new[]{"study","ordinary","multimedia","language","seminar","study-3f"};
        for(int i=0;i<names.Length;i++){camera.transform.position=positions[i];camera.transform.LookAt(targets[i]);yield return new WaitForSecondsRealtime(.4f);DormitoryPreview.SaveCamera(camera,Path.Combine(folder,letter+"-"+names[i]+".png"));}
        camera.transform.SetPositionAndRotation(pose,rotation);camera.fieldOfView=fov;game.player.gameObject.SetActive(true);game.enabled=true;
    }
    IEnumerator Run(string preview){
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;yield return new WaitForSecondsRealtime(1);
        foreach(string letter in testing?new[]{"A","B"}:new[]{preview}){
            var entry=travel.entrances.Single(e=>e.buildingId=="teaching-"+letter.ToLowerInvariant());
            var cameraData=game.followCamera.GetUniversalAdditionalCameraData();var outsideAA=cameraData.antialiasing;
            game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);game.paused=false;Check(game.Interact(),letter+" entrance E failed");yield return Settle();
            Check(travel.Inside&&!travel.Interior.isPlaceholder,letter+" still placeholder");
            if(!testing){game.paused=true;game.Notify(letter=="A"?"A楼：每层两间阶梯教室；后侧实体楼梯通往五楼，正门 E 返回。":"B楼：普通、多媒体、语音、研讨教室；中部自习区，后侧实体楼梯通往五楼。");yield break;}
            Check(SceneManager.sceneCount==2&&FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==1,"Duplicate scene/camera");
            var boxes=travel.Interior.GetComponentsInChildren<BoxCollider>();Check(boxes.Count(c=>c.name.StartsWith("Stair ascending tread"))==48&&boxes.Count(c=>c.name.StartsWith("Stair return tread"))==48,"Missing physical stairs");
            Check(boxes.Count(c=>c.name.StartsWith("Lecture tier"))==(letter=="A"?50:0),"Wrong lecture tier count");
            for(int f=0;f<5;f++){
                yield return Floor(letter,f);
                if(f<4){float y=f*4.2f;yield return Walk(-2,y,21.1f);yield return Walk(-2,y+2.1f,27.6f,10,.06f);yield return Walk(2,y+2.1f,27.6f);yield return Walk(2,y+4.2f,21.1f,10,.06f);}
            }
            yield return Walk(2,16.8f,21.1f);
            for(int f=3;f>=0;f--){float y=f*4.2f;yield return Walk(2,y+2.1f,27.6f);yield return Walk(-2,y+2.1f,27.6f);yield return Walk(-2,y,21.1f);yield return Walk(2,y,21.1f);}
            yield return Walk(0,0,19);yield return Walk(0,0,-7.5f);yield return Capture(letter);
            foreach(var r in travel.Interior.GetComponentsInChildren<MeshRenderer>())foreach(var m in r.sharedMaterials)Check(m&&m.shader&&m.shader.isSupported,"Missing material "+r.name);
            yield return Walk(0,0,-10);Check(game.Interact(),"Exit E failed");yield return Settle();
            Check(!travel.Inside&&SceneManager.sceneCount==1,"Interior unload failed");Check(Vector3.Distance(game.player.position,game.MapToWorld(entry.returnMapPosition))<.06f,"Wrong outdoor return");
            Check(cameraData.antialiasing==outsideAA&&cameraData.scriptableRenderer==UniversalRenderPipeline.asset.GetRenderer(-1),"Exterior renderer not restored");
        }
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: A/B five-floor physical ascent/descent; 192 stair treads; every lecture tier, room doorway, study-floor aisle and washroom approach walked; 10 rendered views; E entry/exit and exterior rendering restored. Movement steps: "+steps);Application.Quit(0);
    }
}
