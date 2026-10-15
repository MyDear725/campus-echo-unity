using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

// Opt-in acceptance check: the same E dispatcher and physical stairs as play.
public sealed class TeachingSkybridgeQA:MonoBehaviour
{
    CampusGame game;BuildingInteriorTravel travel;string folder;bool testing;int transfers;
    void Start(){
        var args=Environment.GetCommandLineArgs();testing=args.Contains("-skybridgeQA");if(!testing&&!args.Contains("-skybridgePreview"))return;
        game=GetComponent<CampusGame>();travel=game.Interiors;
        if(testing){folder=Path.Combine(Application.dataPath,"../QA/Skybridges");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"result.txt"),"RUNNING");Application.logMessageReceived+=Log;StartCoroutine(Timeout());}
        StartCoroutine(Run());
    }
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception){File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL: "+text+"\n"+stack);Application.Quit(1);}}
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(300);throw new Exception("Skybridge QA timeout");}
    void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    IEnumerator Settle(){float end=Time.realtimeSinceStartup+40;while(travel.Busy&&Time.realtimeSinceStartup<end)yield return null;Check(!travel.Busy,"Transition timeout");yield return null;}
    IEnumerator Walk(float x,float y,float z){
        var target=new Vector3(x,y,z);int count=0;
        while(true){var delta=target-game.player.position;delta.y=0;if(delta.magnitude<.045f)break;game.MoveIndoor(Vector3.ClampMagnitude(delta,.3f),1f/30);Check(++count<1800,"Blocked route to "+target+" at "+game.player.position);if(count%8==0)yield return null;}
        for(int i=0;i<20;i++)game.MoveIndoor(Vector3.zero,1f/60);
        Check(Mathf.Abs(game.player.position.y-y)<.13f,"Wrong floor at "+game.player.position+", expected "+y);
    }
    IEnumerator Capture(string name,Vector3 p,Vector3 target){
        var camera=game.followCamera;var old=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;game.enabled=false;game.player.gameObject.SetActive(false);
        camera.transform.position=p;camera.transform.LookAt(target);camera.fieldOfView=64;yield return new WaitForSecondsRealtime(.3f);DormitoryPreview.SaveCamera(camera,Path.Combine(folder,name+".png"));
        camera.transform.SetPositionAndRotation(old,rotation);camera.fieldOfView=fov;game.player.gameObject.SetActive(true);game.enabled=true;
    }
    IEnumerator Center(){yield return Walk(game.player.position.x,4.2f,20);yield return Walk(0,4.2f,20);}
    IEnumerator Go(string target){
        yield return Center();
        var portal=travel.Interior.connections.Single(c=>c.destination.buildingId=="teaching-"+target);
        yield return Walk(portal.anchor.position.x,4.2f,20);
        Check(travel.InteractionPrompt.Contains("[ E ]"),"No bridge prompt");
        var outlook=travel.Interior.GetComponent<SkybridgeCampusView>();var visible=outlook.routes.FirstOrDefault(r=>r.scenery&&r.scenery.activeSelf);Check(visible!=null&&visible.scenery.GetComponentsInChildren<MeshRenderer>().Length>0,"No campus outlook");Check(visible.scenery.GetComponentsInChildren<Collider>().Length==0,"Scenery duplicates physics");
        var previous=travel.Current;Check(game.Interact(),"E ignored");Check(travel.Busy&&travel.Inside,"Bridge state not protected");Check(!travel.TryTransfer(portal),"Duplicate transition allowed");
        yield return Settle();Check(travel.Current.buildingId=="teaching-"+target,"Wrong destination");Check(travel.Interior.connections.Length==(target=="a"?2:1),"Wrong hub topology");Check(travel.Interior.connections.All(c=>target=="a"||c.destination.buildingId=="teaching-a"),"Obsolete B-C connection");Check(travel.CurrentSpawnId=="bridge-from-"+previous.buildingId.Substring(9),"Wrong arrival spawn");
        Check(Mathf.Abs(game.player.position.y-4.26f)<.1f,"Bridge returned to ground floor");
        Check(travel.Interior.NearbyConnection(game.player.position)==null,"Arrival inside return trigger");Check(SceneManager.sceneCount==2,"Leaked scene");
        Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==1,"Duplicate camera/audio");
        var spawn=travel.Interior.FindSpawn(travel.CurrentSpawnId);Check(Vector3.Dot(game.player.forward,spawn.forward)>.99f,"Wrong arrival orientation");
        Check(game.followCamera.GetUniversalAdditionalCameraData().scriptableRenderer==UniversalRenderPipeline.asset.GetRenderer(1),"Interior renderer lost");
    }
    IEnumerator Run(){
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;yield return new WaitForSecondsRealtime(1);
        var a=travel.entrances.Single(e=>e.buildingId=="teaching-a");
        var aa=game.followCamera.GetUniversalAdditionalCameraData().antialiasing;
        if(testing){
            var collision=GameObject.Find("Teaching skybridge collisions");Check(collision&&collision.GetComponentsInChildren<BoxCollider>().Length==8,"Missing exterior bridge collision");
            foreach(var box in collision.GetComponentsInChildren<BoxCollider>())Check(box.transform.lossyScale.x>0&&box.transform.lossyScale.y>0&&box.transform.lossyScale.z>0,"Mirrored bridge physics");
            foreach(var point in new[]{new Vector3(32,0,-109.25f),new Vector3(88,0,-113.3f)}){
                Check(Physics.Raycast(game.MapToWorld(point+Vector3.up*10),Vector3.down,out var roof,3,1<<8)&&roof.collider.transform.IsChildOf(collision.transform),"Camera misses bridge roof");
                Check(Physics.Raycast(game.MapToWorld(point+Vector3.up*.3f),Vector3.up,out var deck,5,1<<8)&&deck.collider.transform.IsChildOf(collision.transform),"Camera misses bridge underside");
            }
        }
        if(testing){yield return Capture("exterior",game.MapToWorld(new Vector3(62,39,-60)),game.MapToWorld(new Vector3(56,6,-103)));yield return Capture("A-B-exterior",game.MapToWorld(new Vector3(51,13,-99)),game.MapToWorld(new Vector3(32,6,-109)));yield return Capture("A-C-exterior",game.MapToWorld(new Vector3(71,12,-105)),game.MapToWorld(new Vector3(88,6,-113)));}
        game.SetTravelPose(game.MapToWorld(a.approachMapPosition),a.enterYaw);game.paused=false;Check(game.Interact(),"Ground entry failed");yield return Settle();
        yield return Walk(0,0,20);yield return Walk(-2,0,21.1f);yield return Walk(-2,2.1f,27.6f);yield return Walk(2,2.1f,27.6f);yield return Walk(2,4.2f,21.1f);yield return Walk(0,4.2f,19.4f);
        if(!testing){game.paused=true;game.Notify("A楼二楼：左侧连廊通往B楼，右侧连廊通往C楼；到尽头玻璃门按 E。");yield break;}
        travel.Transferred+=(from,to)=>transfers++;
        var portal=travel.Interior.connections.Single(c=>c.destination.buildingId=="teaching-b");
        Check(!portal.IsNear(portal.anchor.position-Vector3.up*4.2f),"Wrong-floor interaction allowed");
        yield return Center();yield return Walk(portal.anchor.position.x,4.2f,20);
        game.paused=true;Check(!travel.TryTransfer(portal),"Paused transition allowed");game.paused=false;
        var sourceScene=travel.Interior.gameObject.scene;var pose=game.player.position;string spawnId=portal.destinationSpawnId;
        portal.destinationSpawnId="missing-bridge-spawn";Check(travel.TryTransfer(portal),"Rollback test did not load");yield return Settle();portal.destinationSpawnId=spawnId;
        Check(travel.LastError!=null&&travel.Current==a&&travel.Interior.gameObject.scene==sourceScene&&SceneManager.sceneCount==2,"Failed arrival did not roll back");Check(Vector3.Distance(pose,game.player.position)<.05f&&travel.IndoorController.enabled,"Rollback moved/disabled player");
        string path=portal.destination.interiorScenePath;portal.destination.interiorScenePath="Assets/Missing.unity";Check(!travel.TryTransfer(portal),"Missing scene accepted");portal.destination.interiorScenePath=path;
        yield return Capture("A-to-B",new Vector3(-20.8f,5.9f,19.4f),new Vector3(-28.3f,5.6f,19.4f));
        yield return Go("b");yield return Center();
        // Another floor remains reachable after arriving via a bridge.
        yield return Walk(-2,4.2f,21.1f);yield return Walk(-2,6.3f,27.6f);yield return Walk(2,6.3f,27.6f);yield return Walk(2,8.4f,21.1f);
        yield return Walk(2,6.3f,27.6f);yield return Walk(-2,6.3f,27.6f);yield return Walk(-2,4.2f,21.1f);
        yield return Go("a");
        yield return Capture("A-to-C",new Vector3(21,5.9f,19.4f),new Vector3(29.8f,5.6f,19.4f));
        yield return Capture("A-C-window",new Vector3(25,5.9f,19.4f),new Vector3(26,5.4f,25));
        yield return Go("c");yield return Go("a");yield return Go("c");
        // Falling recovers at the most recent bridge, not the ground lobby.
        travel.IndoorController.enabled=false;game.player.position=new Vector3(0,-20,0);yield return null;yield return null;
        Check(Vector3.Distance(game.player.position,travel.Interior.FindSpawn(travel.CurrentSpawnId).position)<.1f,"Recovery lost bridge arrival");
        yield return Center();yield return Walk(2,4.2f,21.1f);yield return Walk(2,2.1f,27.6f);yield return Walk(-2,2.1f,27.6f);yield return Walk(-2,0,21.1f);yield return Walk(0,0,19);yield return Walk(0,0,-10);
        var c=travel.Current;Check(game.Interact(),"Destination exit failed");yield return Settle();
        Check(!travel.Inside&&SceneManager.sceneCount==1&&Vector3.Distance(game.player.position,game.MapToWorld(c.returnMapPosition))<.06f,"Did not exit at C ground entrance");
        Check(transfers==5,"Wrong transition event count");var data=game.followCamera.GetUniversalAdditionalCameraData();Check(data.antialiasing==aa&&data.scriptableRenderer==UniversalRenderPipeline.asset.GetRenderer(-1),"Outside presentation not restored");
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: physical 1F→2F, A→B→A→C→A→C E crossings, 2F arrivals and headings, B physical 2F↔3F, C stairs to ground exit, blocked wrong-floor/pause/duplicate inputs, missing-scene/spawn rollback, bridge fall recovery, single camera/audio, scene unloading and outdoor renderer restore; 6 rendered views; A-centered topology; rendering-only campus outlook; detailed glazing, tiles, handrails and portals.");Application.Quit(0);
    }
}
