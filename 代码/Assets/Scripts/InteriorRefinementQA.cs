using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class InteriorRefinementQA : MonoBehaviour
{
    CampusGame game;BuildingInteriorTravel travel;string folder;bool done;readonly StringBuilder checks=new StringBuilder();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void FreshRun()
    {
        if(!Environment.GetCommandLineArgs().Contains("-interiorRefinementQA"))return;
        CampusSemester.KeyOverride="InteriorRefinementQA.semester";
        PlayerPrefs.DeleteKey(CampusSemester.KeyOverride+".resume");
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {if(Environment.GetCommandLineArgs().Contains("-interiorRefinementQA")){var go=new GameObject("Interior refinement QA");DontDestroyOnLoad(go);go.AddComponent<InteriorRefinementQA>();}}
    void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.AppendLine("PASS: "+text);}
    void Awake(){folder=Path.Combine(Application.dataPath,
#if UNITY_EDITOR
        "../Builds/Windows/QA/InteriorRefinement"
#else
        "../QA/InteriorRefinement"
#endif
        );Directory.CreateDirectory(folder);Application.logMessageReceived+=Log;}
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string text,string stack,LogType type)
    {
#if UNITY_EDITOR
        if(stack.Contains("UnityEditor.Search.SearchDatabase")&&stack.Contains("IndexationOnStartup"))return;
#endif
        if(!done&&(type==LogType.Exception||type==LogType.Error)){done=true;File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL: "+text+"\n"+stack+"\n"+checks);Quit(1);}
    }
    static void Quit(int code)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    IEnumerator Start()
    {
        var run=Run();while(true){bool next;try{next=run.MoveNext();}catch(Exception e){Debug.LogException(e);yield break;}if(!next)break;yield return run.Current;}
    }
    void Update(){if(!done&&Time.realtimeSinceStartup>240){Debug.LogError("Interior refinement QA timed out");Quit(1);}}
    IEnumerator Settle()
    {float until=Time.realtimeSinceStartup+35;while(travel.Busy&&Time.realtimeSinceStartup<until)yield return null;Check(!travel.Busy,"Scene transition settles");yield return new WaitForSecondsRealtime(.6f);}
    IEnumerator Enter(string id)
    {
        Debug.Log("INTERIOR_REFINEMENT_QA_ENTER "+id);
        var entry=travel.entrances.First(e=>e.buildingId==id);game.enabled=true;game.player.gameObject.SetActive(true);game.paused=false;
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);Check(travel.TryEnter(entry),"Enter "+id);yield return Settle();
        Check(travel.Inside&&travel.Interior.buildingId==id,"Actual scene entered: "+id);
        Check(travel.Interior.GetComponent<InteriorPresentation>(),"Scene presentation attached: "+id);
        foreach(var r in travel.Interior.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)Check(m&&m.shader&&m.shader.isSupported,"Supported material on "+r.name);
    }
    IEnumerator Exit()
    {
        game.enabled=true;game.player.gameObject.SetActive(true);var exit=travel.Interior.exits.First(e=>e&&e.gameObject.activeInHierarchy);
        game.SetTravelPose(exit.position+Vector3.up*.06f,0);Check(travel.TryExit(),"Exit interaction accepted");yield return Settle();Check(!travel.Inside,"Campus restored");
    }
    IEnumerator Shot(Transform root,Vector3 at,Vector3 target,string name)
    {
        game.enabled=false;game.player.gameObject.SetActive(false);var cam=game.followCamera;
        cam.transform.position=root.TransformPoint(at);cam.transform.LookAt(root.TransformPoint(target));cam.fieldOfView=66;
        yield return new WaitForSecondsRealtime(.5f);
        var labels=root.GetComponent<InteriorFloorTextVisibility>();if(labels)labels.ApplyForHeight(root.TransformPoint(at).y-1.6f);
        DormitoryPreview.SaveCamera(cam,Path.Combine(folder,name+".png"));
        game.player.gameObject.SetActive(true);game.enabled=true;
    }
    IEnumerator Walk(Transform root,Vector3 local)
    {
        var target=root.TransformPoint(local);int steps=0;
        while(Vector2.Distance(new Vector2(target.x,target.z),new Vector2(game.player.position.x,game.player.position.z))>.06f)
        {
            var delta=target-game.player.position;delta.y=0;game.MoveIndoor(Vector3.ClampMagnitude(delta,.09f),1f/60);
            if(++steps>1600)throw new Exception("Physical route blocked at "+game.player.position);if(steps%12==0)yield return null;
        }
        for(int i=0;i<30;i++)game.MoveIndoor(Vector3.zero,1f/60);
        Check(Mathf.Abs(game.player.position.y-target.y)<.2f,"Physical floor reached: "+local);
    }
    void PoseIndoor(Vector3 at)
    {
        var controller=travel.IndoorController;controller.enabled=false;game.SetTravelPose(travel.Interior.transform.TransformPoint(at),0);Physics.SyncTransforms();controller.enabled=true;
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(3);game=FindFirstObjectByType<CampusGame>();travel=game.Interiors;
        Check(InteriorRefinementPalette.Load(),"Shared persisted palette loaded");var data=game.followCamera.GetUniversalAdditionalCameraData();var aa=data.antialiasing;
        yield return Enter("cafeteria");
        var ordering=travel.Interior.GetComponent<CafeteriaOrdering>();Check(ordering&&ordering.orderSpots.Length>=3&&ordering.seatSpots.Length>20,"Food ordering and dining anchors retained");
        yield return Shot(travel.Interior.transform,new Vector3(-13,1.8f,-2),new Vector3(1,1.7f,4),"01-cafeteria-dining");
        yield return Shot(travel.Interior.transform,new Vector3(0,6,-6),new Vector3(-6,5.6f,5),"02-cafeteria-upper");
        PoseIndoor(new Vector3(4.05f,.06f,-8.1f));
        for(int level=0;level<2;level++)
        {
            var diningRoot=travel.Interior.transform;float y=level*4.3f;
            yield return Walk(diningRoot,new Vector3(4.05f,y+2.15f,-12.65f));
            yield return Walk(diningRoot,new Vector3(6.15f,y+2.15f,-12.65f));
            yield return Walk(diningRoot,new Vector3(6.15f,y+4.3f,-8.1f));
            yield return Shot(diningRoot,new Vector3(-13,y+6.1f,-2),new Vector3(-7,y+5.7f,7),"cafeteria-"+(level+2)+"F-modern");
            // Cross to the next flight on the same storey's open north approach.
            yield return Walk(diningRoot,new Vector3(4.05f,y+4.3f,-8.1f));
        }
        yield return Exit();yield return Enter("library");
        Check(LibrarySeatRegistry.Ensure().OnFloor(2).Count>20,"Reservable seats remain registered");
        yield return Shot(travel.Interior.transform,new Vector3(0,1.8f,-10),new Vector3(-3,1.8f,8),"03-library-lobby");
        yield return Shot(travel.Interior.transform,new Vector3(46,6.1f,-7),new Vector3(33,5.9f,3),"04-library-reading");
        PoseIndoor(new Vector3(9.85f,.06f,-9));
        for(int level=0;level<3;level++)
        {
            var libraryRoot=travel.Interior.transform;float y=level*4.3f;
            yield return Walk(libraryRoot,new Vector3(9.85f,y+2.15f,-15));
            yield return Walk(libraryRoot,new Vector3(6.55f,y+2.15f,-15));
            yield return Walk(libraryRoot,new Vector3(6.8f,y+4.3f,-8.0f));
            yield return Shot(libraryRoot,new Vector3(5.9f,y+5.95f,-9.3f),new Vector3(9.7f,y+3.1f,-14),"library-stair-"+(level+1));
            yield return Shot(libraryRoot,new Vector3(46,y+6.1f,-7),new Vector3(33,y+5.9f,3),"library-"+(level+2)+"F-modern");
            if(level<2)yield return Walk(libraryRoot,new Vector3(9.85f,y+4.3f,-8.0f));
        }
        yield return Exit();yield return Enter("dormitory-1");
        var hall=DormitoryCorridor.Current(game);Check(hall&&hall.Layout.floors==6,"Six-floor dormitory remains available");
        yield return Shot(travel.Interior.transform,new Vector3(-18,1.65f,0),new Vector3(12,1.6f,0),"05-dormitory-corridor");
        hall.RestoreFloor(2);yield return hall.RestoreRoom(0);Check(hall.InsideRoom,"Second-floor bedroom entry works");
        var bedroom=travel.Interior.GetComponentsInChildren<Transform>().First(t=>t.name=="Dorm room 201");
        yield return Shot(bedroom,new Vector3(0,1.65f,1.7f),new Vector3(0,1.6f,8),"06-dormitory-room");
        var room=travel.Interior.GetComponent<DormitoryRoomInterior>();game.SetTravelPose(room.ExitApproach,bedroom.eulerAngles.y+180);
        Check(room.TryInteract(),"Bedroom return interaction works");yield return new WaitForSecondsRealtime(1.5f);Check(!hall.InsideRoom,"Back in dormitory corridor");
        hall.RestoreFloor(1);yield return Exit();yield return Enter("dormitory-7");
        var root=travel.Interior.transform;game.SetTravelPose(root.TransformPoint(new Vector3(0,.06f,7)),root.eulerAngles.y+180);
        yield return Walk(root,new Vector3(0,0,3.6f));yield return Walk(root,new Vector3(0,2.85f,-5));yield return Walk(root,new Vector3(0,2.85f,-8));
        yield return Shot(root,new Vector3(-5,1.8f,9),new Vector3(0,2.6f,-6),"07-seventh-LG-stair");
        yield return Exit();Check(data.antialiasing==aa,"Outdoor camera presentation restored");
        done=true;File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: cafeteria 1F to 3F and library 1F to 4F physically walked; material support; ordering and reservation anchors; bedroom entry/return; physical LG to 1F stair walk; exterior restoration; fifteen rendered views.\n"+checks);
        Debug.Log("INTERIOR_REFINEMENT_QA_PASS");Quit(0);
    }
}
