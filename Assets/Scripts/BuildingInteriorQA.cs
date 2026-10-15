using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Exercise the same dispatcher, additive loading, capsule and return path as play.
public class BuildingInteriorQA : MonoBehaviour
{
    CampusGame game;BuildingInteriorTravel travel;string folder;int entered,exited;
    void Start() {
        if(!Environment.GetCommandLineArgs().Contains("-entranceQA"))return;
        game=GetComponent<CampusGame>();travel=GetComponent<BuildingInteriorTravel>();
        folder=Path.Combine(Application.dataPath,"../QA/Interiors");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"result.txt"),"RUNNING\n");
        Application.logMessageReceived+=OnLog;StartCoroutine(Run());StartCoroutine(Timeout());
    }
    void OnDestroy(){Application.logMessageReceived-=OnLog;}
    void OnLog(string condition,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error){File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL: "+condition+"\n"+stack);Application.Quit(1);}}
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(180);throw new Exception("Interior QA timed out");}
    void Require(bool value,string message){if(!value)throw new Exception(message);}
    IEnumerator WaitUntilSettled(){float until=Time.realtimeSinceStartup+20;while(travel.Busy&&Time.realtimeSinceStartup<until)yield return null;Require(!travel.Busy,"Transition timeout");yield return null;}
    IEnumerator Run() {
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;yield return new WaitForSecondsRealtime(2);
        game.paused=false;var campus=game.gameObject.scene;var camera=game.followCamera;int originalLayer=game.player.gameObject.layer;
        travel.Entered+=e=>entered++;travel.Exited+=e=>exited++;
        var libraryDoors=travel.entrances.Where(e=>e.buildingId=="library").ToArray();
        Require(libraryDoors.Length==2&&libraryDoors.Select(e=>e.EntranceId).Distinct().Count()==2,"Library needs two distinct doorway IDs");
        Require(libraryDoors.Select(e=>e.interiorScenePath).Distinct().Count()==1,"Library doors must share one interior scene");
        Require(Vector3.Distance(libraryDoors[0].returnMapPosition,libraryDoors[1].returnMapPosition)>25,"Library door return points are not on opposite sides");
        game.Teleport(new Vector3(3,.23f,194));Require(travel.NearbyEntrance()==null,"Door prompt appeared far from entrance");
        foreach(var entry in travel.entrances) {
            game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw,12);
            Require(travel.NearbyEntrance()==entry,"Wrong entry prompt: "+entry.buildingId);
            game.paused=true;Require(!travel.TryEnter(entry),"Entered while paused");game.paused=false;
            Require(game.Interact(),"E dispatcher did not consume entrance");Require(travel.Busy,"Loading state missing");
            Require(!travel.TryEnter(entry),"Repeated E started a second scene load");yield return WaitUntilSettled();
            Require(travel.Inside&&travel.Current==entry,"Wrong interior: "+entry.buildingId);
            Require(SceneManager.sceneCount==2&&SceneManager.GetActiveScene().path==entry.interiorScenePath,"Scene leaked or wrong active scene");
            Require(!campus.GetRootGameObjects().First(r=>r.name=="开发区校区 · 校园环境").activeSelf,"Campus stayed visible indoors");
            Require(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==1,"Duplicate player camera/listener");
            var spawn=travel.Interior.FindSpawn(entry.spawnId);Require(Vector3.Distance(game.player.position,spawn.position)<.1f,"Wrong spawn");
            var at=game.player.position;game.VisitCafeteria();game.VisitDormitory();game.Teleport(Vector3.one*100);Require(Vector3.Distance(at,game.player.position)<.01f,"Outdoor shortcut escaped interior");
            Require(!travel.TryExit(),"Exit activated from across room");
            // Capsule falls onto floor and is stopped by a wall (real interior colliders).
            for(int i=0;i<30;i++)travel.MoveCharacter(Vector3.down*.1f);Require(game.player.position.y>-.05f&&game.player.position.y<.15f,"Indoor floor collision failed");
            Require(Physics.Raycast(game.player.position+Vector3.up*.9f,Vector3.right,out var wall,80,Physics.DefaultRaycastLayers),"No interior boundary to the right");
            float wallX=wall.point.x;
            for(int i=0;i<Mathf.CeilToInt((wall.distance+3)/.2f);i++)travel.MoveCharacter(Vector3.right*.2f);
            Require(game.player.position.x<wallX-.15f,"Walked through interior wall");
            travel.IndoorController.enabled=false;game.SetTravelPose(new Vector3(0,-20,0),0);travel.IndoorController.enabled=true;yield return null;yield return null;
            Require(Vector3.Distance(game.player.position,spawn.position)<.1f,"Fall recovery failed");
            if(entry.buildingId=="cafeteria") {
                yield return new WaitForSecondsRealtime(.3f);DormitoryPreview.SaveCamera(camera,Path.Combine(folder,"placeholder.png"));
            }
            var exit=travel.Interior.exits.First(e=>e);travel.IndoorController.enabled=false;game.SetTravelPose(exit.position,180);travel.IndoorController.enabled=true;Physics.SyncTransforms();
            Require(travel.InteractionPrompt.Contains("返回"),"Return prompt missing");Require(game.Interact(),"Return E dispatcher failed");Require(!travel.TryExit(),"Duplicate exit accepted");yield return WaitUntilSettled();
            Require(!travel.Inside&&SceneManager.sceneCount==1&&SceneManager.GetActiveScene()==campus,"Interior not unloaded");
            Require(Vector3.Distance(game.player.position,game.MapToWorld(entry.returnMapPosition))<.02f,"Wrong return position");
            Require(game.player.gameObject.layer==originalLayer&&!travel.IndoorController.enabled,"Controller/layer not restored");
            Require(campus.GetRootGameObjects().First(r=>r.name=="开发区校区 · 校园环境").activeSelf,"Campus did not reactivate");
        }
        Require(entered==travel.entrances.Length&&exited==travel.entrances.Length,"Lifecycle callbacks missing");
        var sample=travel.entrances.First(e=>e.buildingId=="cafeteria");game.SetTravelPose(game.MapToWorld(sample.approachMapPosition),sample.enterYaw);
        string path=sample.interiorScenePath;sample.interiorScenePath="Assets/Interiors/Scenes/does-not-exist.unity";
        Require(!travel.TryEnter(sample)&&!travel.Busy&&!travel.Inside,"Missing-scene failure did not recover");sample.interiorScenePath=path;
        string spawnId=sample.spawnId;sample.spawnId="missing-spawn";Require(travel.TryEnter(sample),"Could not test invalid contract");yield return WaitUntilSettled();sample.spawnId=spawnId;
        Require(!travel.Inside&&SceneManager.sceneCount==1&&SceneManager.GetActiveScene()==campus&&!string.IsNullOrEmpty(travel.LastError),"Invalid scene contract stranded player");
        Require(travel.TryEnter(sample),"Could not re-enter after failure");yield return WaitUntilSettled();
        travel.IndoorController.enabled=false;game.SetTravelPose(travel.Interior.exits[0].position,180);travel.IndoorController.enabled=true;Require(travel.TryExit(),"Could not leave after retry");yield return WaitUntilSettled();
        File.WriteAllText(Path.Combine(folder,"result.txt"),$"PASS: {travel.entrances.Length} doorway prompts and E entry/exit round trips; library front/rear share one scene and return to their own doors; duplicate-input guards; paused input; original return points; campus visibility; one camera/listener; floor/wall collisions; fall recovery; outdoor shortcut lockout; missing scene and invalid spawn rollback; retry after failure; lifecycle hooks.\n");
        Application.Quit(0);
    }
}
