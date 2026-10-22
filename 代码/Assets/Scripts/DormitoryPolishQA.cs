using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Opt-in, isolated physical traversal and camera review of generated dorm interiors.
public sealed class DormitoryPolishQA : MonoBehaviour
{
    readonly List<string> checks=new List<string>();
    CampusGame game; DormitoryCorridor dorm; bool finished;
    static bool Baseline=>Environment.GetCommandLineArgs().Contains("-dormitoryBaseline");
    static string Folder
    {
        get {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-dormitoryOutput");return i>=0?Path.GetFullPath(a[i+1]):Path.GetFullPath("QA/DormitoryPolish");}
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate()
    {
        if(!Environment.GetCommandLineArgs().Contains("-dormitoryPolishQA"))return;
        const string p="DormitoryPolishQA20261022.";
        CampusSemester.KeyOverride=p+"semester"; CampusWelcomeStory.KeyOverride=p+"welcome";
        CampusRouteGuide.PreferenceKeyOverride=p+"route";GameClock.KeyOverride=p+"clock";
        CampusWallet.KeyOverride=p+"wallet";CampusInventory.KeyOverride=p+"inventory";
        CafeteriaOrdering.PrefsKeyOverride=p+"orders";LibraryReservationManager.KeyOverride=p+"booking";VendingSystem.KeyOverridePrefix=p+"stock";
        foreach(var k in new[]{"semester","semester.resume","semester.day","semester.initial","semester.backup","welcome","route","clock","wallet","inventory","orders","booking"})PlayerPrefs.DeleteKey(p+k);
        CampusWallet.ResetCache();CampusInventory.ResetCache();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-dormitoryPolishQA"))return;
        var go=new GameObject("Dormitory physical and visual review");DontDestroyOnLoad(go);go.AddComponent<DormitoryPolishQA>();
    }
    void Start(){Directory.CreateDirectory(Folder);Application.logMessageReceived+=Log;StartCoroutine(Run());StartCoroutine(Timeout());}
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string m,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)Finish(false,m+"\n"+stack);}
    void Check(bool ok,string m){if(!ok){Finish(false,m);throw new Exception(m);}checks.Add("PASS: "+m);File.WriteAllLines(Folder+"/progress.txt",checks);}
    void Finish(bool ok,string m)
    {
        if(finished)return;finished=true;File.WriteAllText(Folder+"/"+(Baseline?"baseline":Application.isEditor?"editor":"player")+"-result.txt",(ok?"PASS: ":"FAIL: ")+m+"\n"+string.Join("\n",checks));
        Debug.Log("DORMITORY_POLISH_QA_"+(ok?"PASS":"FAIL")+": "+m);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(ok?0:1);
#else
        Application.Quit(ok?0:1);
#endif
    }
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(240);Finish(false,"Timeout");}
    void Pose(Transform space,Vector3 p,float yaw=0)
    {
        var cc=game.Interiors.IndoorController;cc.enabled=false;
        game.SetTravelPose(space.TransformPoint(p),space.eulerAngles.y+yaw);Physics.SyncTransforms();cc.enabled=true;
    }
    IEnumerator Push(Transform space,Vector3 direction,int count=120)
    {
        for(int i=0;i<count;i++){game.MoveIndoor(space.TransformDirection(direction)*.06f,1f/60);if(i%12==0)yield return null;}
    }
    IEnumerator JumpPush(Transform space,Vector3 direction)
    {
        float velocity=6;
        for(int i=0;i<70;i++)
        {
            velocity-=18f/60;game.Interiors.MoveCharacter(space.TransformDirection(direction)*.045f+Vector3.up*velocity/60);
            if(i%10==0)yield return null;
        }
    }
    IEnumerator Settle(){while(game.Interiors.Busy)yield return null;yield return null;yield return null;}
    IEnumerator Enter(string id)
    {
        var travel=game.Interiors;var entry=travel.entrances.First(e=>e&&e.buildingId==id);
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);game.paused=false;game.SetUiCursor(false);
        Check(travel.TryEnter(entry),"Actual building entrance accepted: "+id);yield return Settle();
        dorm=DormitoryCorridor.Current(game);Check(dorm&&dorm.Floor==1,"Generated dormitory initialized: "+id);
    }
    IEnumerator Exit()
    {
        Pose(dorm.transform,new Vector3(0,.06f,-2.8f));game.SetUiCursor(false);
        Check(game.Interiors.TryExit(),"Actual campus exit accepted");yield return Settle();Check(!game.Interiors.Inside,"Returned to campus");
    }
    void Shot(string name,Transform space,Vector3 at,Vector3 target)
    {
        var cam=game.followCamera;var position=cam.transform.position;var rotation=cam.transform.rotation;float fov=cam.fieldOfView;
        cam.transform.position=space.TransformPoint(at);cam.transform.LookAt(space.TransformPoint(target));cam.fieldOfView=62;
        DormitoryPreview.SaveCamera(cam,Folder+"/"+(Baseline?"before-":"after-")+name+".png");
        cam.transform.SetPositionAndRotation(position,rotation);cam.fieldOfView=fov;
    }
    IEnumerator Room(int index,bool capture)
    {
        yield return dorm.RestoreRoom(index);yield return new WaitForSecondsRealtime(.2f);
        Check(dorm.InsideRoom&&dorm.CurrentRoomIndex==index,"Room portal selects room "+dorm.CurrentRoomNumber);
        var bedroom=dorm.GetComponentsInChildren<Transform>().First(t=>t.name=="Dorm room "+dorm.CurrentRoomNumber);
        if(capture)
        {
            Shot("bedroom",bedroom,new Vector3(0,2.5f,1.25f),new Vector3(0,1.4f,7.3f));
            Shot("study",bedroom,new Vector3(.5f,1.65f,5.8f),new Vector3(-1.85f,1.15f,4.35f));
            Shot("balcony",bedroom,new Vector3(0,1.8f,7.8f),new Vector3(0,1.6f,11.4f));
        }
        if(!Baseline)
        {
            Check(bedroom.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Loft bed quilt").Select(r=>r.sharedMaterial.GetColor("_BaseColor")).Distinct().Count()==4,"Four beds retain distinct fabric colours");
            Check(bedroom.GetComponentsInChildren<Renderer>().Count(r=>r.enabled&&r.name.StartsWith("Combined dorm details"))<30&&bedroom.GetComponentsInChildren<Renderer>().Count(r=>!r.enabled)>100,"Small decorations render through shared material batches");
            Check(bedroom.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported)),"Bedroom materials supported by the active renderer");
            // Central route avoids furniture and exercises the actual capsule and gravity.
            Pose(bedroom,new Vector3(0,.06f,1.15f));yield return Push(bedroom,Vector3.forward,145);
            Check(bedroom.InverseTransformPoint(game.player.position).z>9.5f,"Capsule walks from room entry to balcony");
            yield return Push(bedroom,Vector3.forward,65);
            var p=bedroom.InverseTransformPoint(game.player.position);
            Check(p.z<11.6f&&p.y>-.15f,"Balcony rear blocks capsule and supports feet");
            foreach(int side in new[]{-1,1})
            {
                Pose(bedroom,new Vector3(0,.06f,10.5f));yield return Push(bedroom,Vector3.right*side,90);
                p=bedroom.InverseTransformPoint(game.player.position);Check(Mathf.Abs(p.x)<3.05f&&p.y>-.15f,"Balcony side contains capsule: "+side);
                yield return JumpPush(bedroom,Vector3.right*side);p=bedroom.InverseTransformPoint(game.player.position);
                Check(Mathf.Abs(p.x)<3.05f&&p.y>-.15f,"Jumping against balcony side cannot escape: "+side);
            }
            Pose(bedroom,new Vector3(0,.06f,6.3f));yield return Push(bedroom,Vector3.back,90);
            Check(bedroom.InverseTransformPoint(game.player.position).z<1.6f,"Room central aisle remains traversable both ways");
            Pose(bedroom,new Vector3(0,-4,5));
            Check(dorm.RecoverIfUnsafe()&&dorm.InsideRoom&&bedroom.InverseTransformPoint(game.player.position).z<1.5f,"Injected fall recovers inside the current bedroom");
        }
        Pose(bedroom,new Vector3(0,.06f,.65f),180);game.SetUiCursor(false);
        Check(dorm.TryInteract(),"Room exit interaction accepted");float until=Time.realtimeSinceStartup+4;
        while(dorm.InsideRoom&&Time.realtimeSinceStartup<until)yield return null;
        yield return new WaitForSecondsRealtime(.35f);Check(!dorm.InsideRoom,"Room returns to its corridor");
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(3);game=FindFirstObjectByType<CampusGame>();game.paused=false;game.SetUiCursor(true);
        PlayerPrefs.SetInt(CampusWelcomeStory.ProgressKey,game.Story.Data.stages.Length);game.Story.ReloadProgress();
        QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
        yield return Enter("dormitory-1");game.SetUiCursor(true);
        Shot("corridor",dorm.transform,new Vector3(-19,1.85f,.15f),new Vector3(5,1.55f,0));
        Shot("first-floor-wall",dorm.transform,new Vector3(20,1.85f,.3f),new Vector3(23,1.4f,-1.3f));
        if(!Baseline)
        {
            for(int floor=1;floor<=6;floor++)
            {
                dorm.RestoreFloor(floor);Physics.SyncTransforms();
                foreach(int side in new[]{-1,1})
                {
                    float x=side*23;
                    foreach(int direction in new[]{-1,1})
                    {
                        bool closed=floor==1&&direction<0||floor==6&&direction>0;
                        Pose(dorm.transform,new Vector3(x,.06f,0));yield return Push(dorm.transform,Vector3.forward*direction,65);
                        var p=dorm.transform.InverseTransformPoint(game.player.position);
                        Check(closed?Mathf.Abs(p.z)<.75f:p.z*direction>3.1f,"Floor "+floor+" stair route "+side+"/"+direction+(closed?" sealed by full wall":" reaches landing"));
                        Check(p.y> -1.4f&&p.y<1.5f,"Stair capsule remains supported");
                        if(!closed)
                        {
                            yield return Push(dorm.transform,Vector3.right*side,60);p=dorm.transform.InverseTransformPoint(game.player.position);
                            Check(Mathf.Abs(p.x)<24.1f&&p.y> -1.4f,"Outer stair side has no void gap");
                            yield return Push(dorm.transform,Vector3.left*side,60);p=dorm.transform.InverseTransformPoint(game.player.position);
                            Check(Mathf.Abs(p.x)>21.9f&&p.y> -1.4f,"Inner stair side has no void gap");
                            yield return JumpPush(dorm.transform,Vector3.left*side);p=dorm.transform.InverseTransformPoint(game.player.position);
                            Check(Mathf.Abs(p.x)>21.9f&&p.y> -1.4f,"Stair side remains sealed while jumping");
                            yield return Push(dorm.transform,Vector3.back*direction,65);
                            Check(dorm.transform.InverseTransformPoint(game.player.position).y>-.15f,"Capsule returns from flight to landing");
                        }
                    }
                }
            }
            dorm.RestoreFloor(2);Shot("stairs",dorm.transform,new Vector3(23,2.05f,.02f),new Vector3(23,1.4f,3.3f));
            Pose(dorm.transform,new Vector3(0,-4,0));Check(dorm.RecoverIfUnsafe()&&dorm.Floor==2&&game.player.position.y>-.15f,"Injected corridor fall recovers on the same floor");
            // Exercise E transitions too, rather than only restoring floor state.
            dorm.RestoreFloor(1);
            foreach(int next in new[]{2,3,4,5,6,5,4,3,2,1})
            {
                int direction=next>dorm.Floor?1:-1;Pose(dorm.transform,new Vector3(23,.06f,direction*.4f));game.SetUiCursor(false);
                Check(dorm.TryInteract(),"Stair interaction accepted");while(dorm.Transitioning)yield return null;
                Check(dorm.Floor==next,"Stair transition reaches floor "+next);game.SetUiCursor(true);
            }
        }
        yield return Room(0,true);
        if(!Baseline){dorm.RestoreFloor(6);yield return Room(11,false);dorm.RestoreFloor(1);}
        yield return Exit();
        if(!Baseline)
        {
            // Dormitory 7 has a separate, authored split-level interior.
            foreach(var id in game.Interiors.entrances.Where(e=>e&&e.buildingId.StartsWith("dormitory-")&&e.buildingId!="dormitory-1"&&e.buildingId!="dormitory-7").Select(e=>e.buildingId).Distinct().ToArray())
            {yield return Enter(id);Check(dorm.GetComponentsInChildren<Collider>().Any(c=>c.name=="Terminal floor plaster wall"),"Shared solid wall present in "+id);yield return Exit();}
            var travel=game.Interiors;var seventh=travel.entrances.First(e=>e&&e.buildingId=="dormitory-7");
            game.SetTravelPose(game.MapToWorld(seventh.approachMapPosition),seventh.enterYaw);game.SetUiCursor(false);
            Check(travel.TryEnter(seventh),"Authored split-level seventh dorm entrance accepted");yield return Settle();
            Check(travel.Inside&&!DormitoryCorridor.Current(game),"Seventh dorm retains its separate authored interior");
            var exit=travel.Interior.exits.First(e=>e&&e.gameObject.activeInHierarchy);game.SetTravelPose(exit.position,0);
            Check(travel.TryExit(),"Seventh dorm campus return accepted");yield return Settle();Check(!travel.Inside,"Seventh dorm returns to campus");
        }
        Finish(true,checks.Count+" checks; actual CharacterController movement, room portals, stairs and real camera renders. No frame-rate benchmark.");
    }
}
