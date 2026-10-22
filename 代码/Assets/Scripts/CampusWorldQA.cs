using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Explicit opt-in. Reuses the semester QA's isolated preferences before scenes awaken.
public sealed class CampusWorldQA : MonoBehaviour
{
    readonly List<string> checks=new List<string>();
    CampusSemester semester;CampusGame game;
    string folder;
    bool done;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-campusWorldQA")||FindFirstObjectByType<CampusWorldQA>())return;
        var go=new GameObject("CampusWorldQA");DontDestroyOnLoad(go);go.AddComponent<CampusWorldQA>();
    }
    void Start()
    {
        folder=Path.GetFullPath(Environment.GetCommandLineArgs().Contains("-environmentQualityQA")?"QA/EnvironmentQuality/runtime":"QA/WorldGuidance");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"result.txt"),"RUNNING");
        Application.logMessageReceived+=Log;StartCoroutine(Run());StartCoroutine(Timeout());
    }
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string text,string stack,LogType type)
    {
        if((type==LogType.Exception||type==LogType.Error)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(false,text+"\n"+stack);
    }
    void Check(bool pass,string message)
    {
        if(!pass){Finish(false,message);throw new Exception("CampusWorldQA: "+message);}checks.Add("PASS: "+message);
    }
    void Finish(bool pass,string message)
    {
        if(done)return;done=true;File.WriteAllText(Path.Combine(folder,"result.txt"),(pass?"PASS: ":"FAIL: ")+message+"\n"+string.Join("\n",checks));
        Debug.Log("CAMPUS_WORLD_QA_"+(pass?"PASS":"FAIL")+": "+message);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(pass?0:1);
#else
        Application.Quit(pass?0:1);
#endif
    }
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(300);Finish(false,"Timeout");}
    void AtHour(float hour)
    {
        semester.Clock.Restore((new DateTime(2026,9,2).AddHours(hour)-GameClock.SemesterStart).TotalSeconds);semester.Lighting.ApplyNow();
    }
    IEnumerator Shot(string name)
    {
        // Environment review already renders real camera views before Play Mode.
        // Batch-mode players do not issue WaitForEndOfFrame callbacks.
        if(Environment.GetCommandLineArgs().Contains("-environmentQualityQA")){yield return null;yield break;}
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));yield return new WaitForSecondsRealtime(.4f);
    }
    IEnumerator Route(string target)
    {
        semester.UI.Track(target);float deadline=Time.realtimeSinceStartup+35;
        yield return null;
        while((semester.Route.Searching||semester.Route.Path.Count<2)&&Time.realtimeSinceStartup<deadline)yield return null;
        yield return null;
        Check(semester.Route.Path.Count>=2,"Navigable route found: "+target+" ("+semester.Route.Status+")");
        Check(semester.Route.Line.enabled,"Guidance line is visible for "+target);
    }
    IEnumerator Enter(string building)
    {
        if(game.Interiors.Inside)
        {
            var exit=game.Interiors.Interior.exits.First(e=>e&&e.gameObject.activeInHierarchy);game.SetTravelPose(exit.position,0,12);game.paused=false;game.SetUiCursor(false);
            Check(game.Interiors.TryExit(),"Leave real interior");while(game.Interiors.Busy)yield return null;
        }
        var entry=game.Interiors.entrances.First(e=>e&&e.buildingId==building);
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw,12);game.paused=false;game.SetUiCursor(false);
        Check(game.Interiors.TryEnter(entry),"Enter actual building "+building);while(game.Interiors.Busy)yield return null;
        yield return null;yield return null;game.SetUiCursor(true);
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(3);semester=CampusSemester.Instance;game=semester.Game;
        game.paused=false;game.SetUiCursor(true);
        Check(semester&&semester.Route&&semester.Lighting,"Route and day/night systems are bound to the real semester clock");
        Check(CampusDayNight.LightsScheduled(18)&&!CampusDayNight.LightsScheduled(17.999f),"Campus lights switch on exactly at 18:00");
        Check(CampusDayNight.LightsScheduled(5.999f)&&!CampusDayNight.LightsScheduled(6),"Campus lights switch off exactly at 06:00");
        Check(CampusDayNight.DaylightAt(16.5f)>CampusDayNight.DaylightAt(17.5f)&&CampusDayNight.DaylightAt(17.5f)>CampusDayNight.DaylightAt(18.5f),"Daylight decreases continuously through dusk");
        AtHour(12);Check(semester.Lighting.LampCount>8,"Actual campus path and courtyard lamps were created");Check(!semester.Lighting.LampsOn,"Midday lamps are off");float noon=semester.Lighting.Sun.intensity;
        var presentation=game.GetComponentInChildren<CampusEnvironmentPresentation>();
        var outdoorVolume=presentation?presentation.GetComponent<UnityEngine.Rendering.Volume>():null;
        Check(outdoorVolume&&outdoorVolume.weight==1,"Outdoor atmosphere is active on campus");
        CampusEnvironmentPresentation.CreateProfile(game.gameObject);
        Check(game.GetComponentsInChildren<CampusEnvironmentPresentation>().Length==1,"Repeated binding reuses one outdoor profile");
        AtHour(17.5f);Check(semester.Lighting.Sun.intensity<noon,"Afternoon sun dims before lights switch on");Check(!semester.Lighting.LampsOn,"17:30 lamps stay off");
        AtHour(18);Check(semester.Lighting.LampsOn,"Saved clock at 18:00 enables lamps immediately");
        AtHour(22);Check(semester.Lighting.Sun.intensity<noon*.1f,"Night main-light intensity is substantially below midday");Check(Shader.GetGlobalFloat("_CampusNightAmount")>.99f,"Fixed-brightness campus shaders receive night state");
        AtHour(12);
        game.SetTravelPose(game.MapToWorld(new Vector3(3,.23f,194)),180,18);
        yield return Route("a");
        var data=JsonUtility.FromJson<CampusData>(game.mapData.text);
        var path=semester.Route.Path;
        for(int i=1;i<path.Count;i++)
        {
            var a=game.WorldToMap(path[i-1]);var b=game.WorldToMap(path[i]);int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.35f));
            bool clear=true;for(int j=0;j<=steps;j++){var p=Vector3.Lerp(a,b,(float)j/steps);if(CampusCollision.Blocked(p.x,p.z,data.obstacles)){clear=false;break;}}
            Check(clear,"Outdoor rendered segment "+i+" avoids actual campus collisions");
        }
        yield return Shot("01-outdoor-route");
        semester.Route.SetEnabled(false);yield return null;Check(!semester.Route.Line.enabled,"Turning guidance off hides the line immediately");
        Check(semester.Route.Resolve(out _,out _),"Direction guidance remains available with the line disabled");
        semester.Route.SetEnabled(true);Check(semester.Route.LineEnabled,"Guidance preference is saved and can be enabled again");
        yield return Route("library");
        yield return Enter("teaching-a");yield return Route("a");
        Check(outdoorVolume.weight==0,"Outdoor grading is removed inside the actual teaching building");
        Check(semester.Route.Path.All(p=>p.y<1),"First-floor classroom route remains on its own floor");
        Check(semester.Route.Path.Last().x< -4,"Indoor route reaches the actual room rather than stopping in the corridor");
        yield return Shot("02-indoor-route");
        Color indoorAmbient=RenderSettings.ambientSkyColor;AtHour(22);
        Check(RenderSettings.ambientSkyColor==indoorAmbient,"Day/night does not overwrite authored interior ambient light");
        yield return Route("library");Check(semester.Route.Status.Contains("出口"),"A different building is routed through the current real exit");
        yield return Enter("dormitory-1");
        var dorm=DormitoryCorridor.Current(game);dorm.RestoreFloor(4);
        yield return Route("dorm");Check(semester.Route.Status.Contains("下"),"Upper dormitory floor routes to the downstairs interaction");
        // Show a fixed campus view across day, dusk and night, using the same game camera.
        dorm.RestoreFloor(1);
        var exitDoor=game.Interiors.Interior.exits.First(e=>e&&e.gameObject.activeInHierarchy);game.SetTravelPose(exitDoor.position,0,12);game.paused=false;game.SetUiCursor(false);game.Interiors.TryExit();while(game.Interiors.Busy)yield return null;
        game.SetUiCursor(true);semester.State.trackedLocation="";
        yield return null;
        Check(outdoorVolume.weight==1,"Outdoor atmosphere returns after exiting the dormitory");
        game.SetTravelPose(game.MapToWorld(new Vector3(245,.23f,-90)),180,12);game.enabled=false;
        var camera=game.followCamera;
        camera.transform.position=game.MapToWorld(new Vector3(245,12,-49));camera.transform.LookAt(game.MapToWorld(new Vector3(245,1,-91)));
        foreach(float hour in new[]{12f,17.5f,18f,22f,6f})
        {
            AtHour(hour);yield return new WaitForSecondsRealtime(.5f);yield return Shot(hour==12?"03-noon":hour==17.5f?"04-dusk":hour==18?"05-lights-1800":hour==22?"06-night":"07-dawn");
        }
        game.enabled=true;
        game.SetUiCursor(false);semester.UI.Open("life");Check(semester.UI.IsOpen,"Settings handbook opens for the guidance switch");yield return Shot("08-route-setting");semester.UI.Close();
        semester.State.trackedLocation="a";semester.Save();CampusSemesterSave.CaptureDay(semester,true);
        semester.Route.SetEnabled(false);
        Check(CampusSemesterSave.RestoreDay(semester),"Daily reset accepts the tracked target state");while(semester.Restoring)yield return null;game=semester.Game;
        Check(!semester.Route.LineEnabled,"Reset preserves the user's guidance display preference");
        Check(semester.State.trackedLocation=="a","Daily reset restores the tracked task together with semester progress");
        semester.Lighting.ApplyNow();Check(!semester.Lighting.LampsOn,"Lighting resynchronizes to restored morning time");
        Finish(true,checks.Count+" checks passed in the original Unity project.");
    }
}
