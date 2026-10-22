using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Opt-in integration checks. GithubIntegrationQA assigns private save keys first.
public sealed class DailyLifeQA : MonoBehaviour
{
    static string Folder
    {
        get
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-dailyLifeOutput");
            return index>=0&&index+1<args.Length?Path.GetFullPath(args[index+1]):"QA/DailyLife";
        }
    }
    readonly List<string> checks=new List<string>();
    CampusSemester sem;CampusGame game;bool finished;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void FreshRun()
    {
        if(!Environment.GetCommandLineArgs().Contains("-dailyLifeQA"))return;
        const string prefix="DailyLifeQA20261022.";
        CampusSemester.KeyOverride=prefix+"semester";CampusWelcomeStory.KeyOverride=prefix+"welcome";
        CampusRouteGuide.PreferenceKeyOverride=prefix+"route";GameClock.KeyOverride=prefix+"clock";
        CampusWallet.KeyOverride=prefix+"wallet";CampusInventory.KeyOverride=prefix+"inventory";
        CafeteriaOrdering.PrefsKeyOverride=prefix+"orders";LibraryReservationManager.KeyOverride=prefix+"booking";VendingSystem.KeyOverridePrefix=prefix+"stock";
        foreach(var key in new[]{"semester","semester.resume","semester.day","semester.initial","semester.backup","welcome","welcomePending","route","clock","wallet","inventory","orders","booking","stockvending_drinks_1f","stockvending_snacks_1f"})PlayerPrefs.DeleteKey(prefix+key);
        CampusWallet.ResetCache();CampusInventory.ResetCache();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(Environment.GetCommandLineArgs().Contains("-dailyLifeQA"))
        {var go=new GameObject("Daily life integration review");DontDestroyOnLoad(go);go.AddComponent<DailyLifeQA>();}
    }
    void Start()
    {
        Directory.CreateDirectory(Folder);Application.logMessageReceived+=Log;
        StartCoroutine(Run());StartCoroutine(Timeout());
    }
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string message,string stack,LogType type)
    {if(type==LogType.Exception||type==LogType.Error)Finish(false,message+"\n"+stack);}
    void Check(bool ok,string message)
    {
        if(!ok){Finish(false,message);throw new Exception(message);}
        checks.Add("PASS: "+message);File.WriteAllLines(Folder+"/progress.txt",checks);
    }
    void Finish(bool ok,string message)
    {
        if(finished)return;finished=true;
        File.WriteAllText(Folder+(Application.isEditor?"/editor-result.txt":"/player-result.txt"),(ok?"PASS: ":"FAIL: ")+message+"\n"+string.Join("\n",checks));
        Debug.Log("DAILY_LIFE_QA_"+(ok?"PASS":"FAIL")+": "+message);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(ok?0:1);
#else
        Application.Quit(ok?0:1);
#endif
    }
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(240);Finish(false,"Timed out");}
    void TimeAt(int day,int hour,int minute=0)=>sem.Clock.Restore((GameClock.SemesterStart.Date.AddDays(day).AddHours(hour).AddMinutes(minute)-GameClock.SemesterStart).TotalSeconds);
    void Field(string key,object value)=>typeof(CampusSemesterUI).GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(sem.UI,value);
    object Field(string key)=>typeof(CampusSemesterUI).GetField(key,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sem.UI);
    void OpenPage(string page)
    {
        game.SetUiCursor(false);sem.UI.Open(page);
        Check(sem.UI.IsOpen,"Handbook opens through normal input ownership: "+page);
    }
    IEnumerator AtPoint(string location)
    {
        sem.UI.Close();game=sem.Game;
        var travel=game.Interiors;
        if(travel.Inside)
        {
            game.SetTravelPose(travel.Interior.exits.First(e=>e&&e.gameObject.activeInHierarchy).position,0);
            game.paused=false;game.SetUiCursor(false);
            Check(travel.TryExit(),"Actual interior exit accepted");
            while(travel.Busy)yield return null;
        }
        if(location!="bridge"&&location!="lake"&&location!="track")
        {
            string building=location=="a"?"teaching-a":location=="b"?"teaching-b":location=="c"?"teaching-c":location=="dorm"?"dormitory-1":location;
            var entry=travel.entrances.First(e=>e&&e.buildingId==building);
            game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);game.paused=false;game.SetUiCursor(false);
            Check(travel.TryEnter(entry),"Actual interior entry accepted: "+building);
            while(travel.Busy)yield return null;
        }
        yield return null;yield return null;sem.Bind();
        var point=FindObjectsByType<CampusSemesterPoint>(FindObjectsSortMode.None).First(p=>p.location==location&&p.gameObject.activeInHierarchy);
        var cc=travel.IndoorController;if(cc)cc.enabled=false;
        game.SetTravelPose(point.transform.position,0);Physics.SyncTransforms();if(cc)cc.enabled=true;
        game.paused=false;game.SetUiCursor(true);
        Check(sem.At(location),"Player reaches the real activity point: "+location);
    }
    void CallUi(string method)=>typeof(CampusSemesterUI).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sem.UI,null);
    IEnumerator Shot(string name)
    {
        if(Application.isBatchMode)yield break;
        yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForSecondsRealtime(.2f);
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(3);sem=CampusSemester.Instance;game=sem.Game;
        QualitySettings.vSyncCount=1;Application.targetFrameRate=60;
        if(!Application.isBatchMode)Screen.SetResolution(1440,900,FullScreenMode.Windowed);
        game.SetUiCursor(true);
        Check(sem.DailyCatalog.activities.Length==12&&sem.Catalog.beats.Length==39,"Twelve daily templates and 39 story beats loaded");
        Check(sem.DailyCatalog.activities.All(a=>a.choices.Length==2&&a.choices.All(c=>c.minutes>0)),"Every daily activity has two complete, timed choices");
        Check(Enumerable.Range(1,138).All(d=>CampusDailyRules.ForDay(sem.DailyCatalog,d).Select(a=>a.category).Distinct().Count()==3),"Every semester day offers exactly three distinct activity categories");
        Check(CampusDailyRules.ForDay(sem.DailyCatalog,1).Select(a=>a.id).SequenceEqual(CampusDailyRules.ForDay(sem.DailyCatalog,1).Select(a=>a.id)),"Date-based selection is deterministic");
        Check(Enumerable.Range(1,4).SelectMany(d=>CampusDailyRules.ForDay(sem.DailyCatalog,d)).Select(a=>a.id).Distinct().Count()==12,"All twelve activities appear across four consecutive dates");
        Check(!sem.CompleteDaily("margin-note",0),"Onboarding cannot be bypassed through daily activities");
        PlayerPrefs.SetInt(CampusWelcomeStory.ProgressKey,game.Story.Data.stages.Length);game.Story.ReloadProgress();game.paused=false;
        yield return AtPoint("bridge");TimeAt(1,8,20);sem.State.energy=80;
        string before=JsonUtility.ToJson(sem.State);double seconds=sem.Clock.GameSeconds;long money=CampusWallet.BalanceFen;
        Check(!sem.CompleteDaily("margin-note",0)&&JsonUtility.ToJson(sem.State)==before&&sem.Clock.GameSeconds==seconds,"Remote completion rejects without state or time changes");
        yield return AtPoint("a");TimeAt(1,8,20);
        Check(!sem.CompleteDaily("margin-note",-1)&&!sem.CompleteDaily("margin-note",2),"Invalid choices reject safely");
        before=JsonUtility.ToJson(sem.State);
        Check(!sem.CompleteDaily("margin-note",0)&&JsonUtility.ToJson(sem.State)==before,"Class conflict requires an explicit override");
        if(!Application.isBatchMode)
        {
            OpenPage("daily");Field("selectedDaily","margin-note");
            Check(sem.UI.PreviewDaily("margin-note",1),"Daily option button callback opens its confirmation");
            Check(Field("confirmAction")!=null&&((string)Field("confirmText")).Contains("高等数学"),"GUI confirmation explains the overlapping course before committing");
            yield return Shot("02-conflict-confirmation");CallUi("CancelConfirmation");
            Check(sem.State.dailyActivities.Count==0&&sem.Clock.GameSeconds==seconds,"Cancel button callback preserves daily history and time");
            Check(sem.UI.PreviewDaily("margin-note",1),"Daily confirmation can reopen after cancel");
            sem.State.energy=0;CallUi("ExecuteConfirmation");
            Check(Field("confirmAction")!=null&&sem.State.dailyActivities.Count==0,"Confirm button rechecks changed energy and keeps the rejected preview");
            sem.State.energy=80;CallUi("ExecuteConfirmation");
            Check(Field("confirmAction")==null&&sem.State.dailyActivities.Count==1,"Confirm button callback completes exactly one daily activity");
        }
        else Check(sem.CompleteDaily("margin-note",1,true),"Validated daily transaction commits with explicit conflict override");
        Check(sem.Clock.Now==GameClock.SemesterStart.Date.AddDays(1).AddHours(8).AddMinutes(35)&&sem.State.energy==78&&sem.State.Course("math").mastery==21,"Chosen activity settles exactly 15 minutes, two energy and one mastery");
        Check(CampusWallet.BalanceFen==money&&sem.State.attendance.Count==0&&sem.State.assignments.Count==0,"Daily study does not charge money or fabricate classes and homework");
        before=JsonUtility.ToJson(sem.State);seconds=sem.Clock.GameSeconds;
        Check(!sem.CompleteDaily("margin-note",0,true)&&JsonUtility.ToJson(sem.State)==before&&sem.Clock.GameSeconds==seconds,"Repeat choice cannot farm rewards, time or energy");
        sem.UI.Close();game.SetUiCursor(true);TimeAt(5,21,50);
        Check(sem.DailyUnavailableReason("margin-note",0,true).Contains("参与时段"),"An activity must finish inside its opening window");
        TimeAt(5,10);sem.State.energy=0;
        Check(!sem.CompleteDaily("margin-note",0,true),"Insufficient energy rejects without committing");sem.State.energy=80;
        Check(sem.DailyFollowUp(sem.DailyOptions.First(a=>a.id=="margin-note")).Contains("上回留下的片段"),"Recurring activity recalls the previous chosen reply");
        yield return AtPoint("lake");TimeAt(1,21,40);sem.State.energy=99;
        Check(sem.CompleteDaily("lake-pause",0)&&sem.State.energy==100,"Restorative activity caps energy at 100");
        yield return AtPoint("cafeteria");TimeAt(1,12);
        Check(sem.CompleteDaily("lunch-talk",0),"Meal conversation completes at the real cafeteria point");
        yield return AtPoint("bridge");TimeAt(2,12);
        Check(sem.CompleteDaily("wayfinding",1),"Next-day social activity rotates and has its own choice");
        yield return AtPoint("library");TimeAt(3,15);
        Check(sem.CompleteDaily("shared-table",0)&&sem.State.Has("daily-company-3"),"Three real social activities unlock a persistent story milestone");
        TimeAt(12,15);
        Check(sem.CompleteBeat("daily-company-memory",1),"Daily milestone story completes at the actual library");
        var code=sem.Catalog.beats.First(b=>b.id=="club-code-workshop");var photo=sem.Catalog.beats.First(b=>b.id=="club-photo-walk");sem.State.Mark("club");
        sem.State.club="编程兴趣小组";
        Check(SemesterRules.Available(sem.State,code,19)&&!SemesterRules.Available(sem.State,photo,19),"Programming club unlocks its own story and locks the alternate route");
        sem.State.club="湖畔摄影小组";
        Check(!SemesterRules.Available(sem.State,code,19)&&SemesterRules.Available(sem.State,photo,19),"Photography club unlocks its own story and locks the alternate route");
        var promise=sem.Catalog.beats.First(b=>b.id=="study-promise");
        Check(sem.StoryText(promise)==promise.text,"Locked future dialogue does not invent an unseen earlier choice");
        sem.State.Mark("classmate-choice-1");string alternate=sem.StoryText(promise);sem.State.Mark("classmate-choice-0");
        Check(alternate!=sem.StoryText(promise)&&sem.StoryText(promise).Contains("一起核对课表"),"Later dialogue changes its recall according to the earlier choice");
        string saved=JsonUtility.ToJson(sem.State);sem.Save();sem.ReloadState();
        Check(JsonUtility.ToJson(sem.State)==saved,"Daily history, choices and story milestones survive save and reload");
        var legacy=SemesterState.Fresh();legacy.energy=77;legacy.Mark("existing-progress");
        string legacyJson=JsonUtility.ToJson(legacy).Replace(",\"dailyActivities\":[]","");
        Check(!legacyJson.Contains("dailyActivities"),"Legacy fixture has no new daily-history field");
        PlayerPrefs.SetString(CampusSemester.SaveKey,legacyJson);sem.ReloadState();
        Check(sem.State.energy==77&&sem.State.Has("existing-progress")&&sem.State.dailyActivities!=null&&sem.State.dailyActivities.Count==0,"Older saves retain their progress and acquire an empty daily history");
        PlayerPrefs.SetString(CampusSemester.SaveKey,saved);sem.ReloadState();
        yield return AtPoint("lake");TimeAt(9,18);sem.State.energy=70;Check(sem.CompleteDaily("lake-pause",1),"Second restorative day is recorded independently");
        TimeAt(13,18);sem.State.energy=70;CampusSemesterSave.CaptureDay(sem,true);int count=sem.State.dailyActivities.Count;
        Check(sem.CompleteDaily("lake-pause",0)&&sem.State.Has("daily-rest-3"),"Third restorative day unlocks its story");
        Check(CampusSemesterSave.RestoreDay(sem),"Complete daily reset accepted");while(sem.Restoring)yield return null;game=sem.Game;
        Check(sem.State.dailyActivities.Count==count&&!sem.State.Has("daily-rest-3")&&sem.State.energy==70,"Daily reset rolls back completion, milestone and recovered energy together");
        yield return AtPoint("dorm");TimeAt(9,7);sem.State.assignments.Clear();CampusWallet.SetForTest(30000);count=sem.State.dailyActivities.Count;
        Check(sem.RoutineDay()&&sem.State.dailyActivities.Count==count,"Ordinary-day auto-plan never fabricates optional daily participation");
        string recap=sem.DailyRecap;Check(sem.Rest()&&sem.LastMessage.Contains(recap),"Sleep preserves yesterday's recap while preparing the next day");
        Check(sem.DailyOptions.Length==3&&!sem.DailyOptions.Any(a=>sem.DailyCompleted(a.id)),"New morning refreshes daily offers without erasing earlier history");
        count=sem.State.dailyActivities.Count;
        Check(CampusSemesterSave.RestoreSemester(sem),"Full semester reset accepted");while(sem.Restoring)yield return null;game=sem.Game;
        Check(sem.State.dailyActivities.Count==0&&!sem.State.Has("daily-company-3"),"Full reset removes new daily history and unlocks");
        Check(CampusSemesterSave.RestoreBackup(sem),"Complete pre-reset backup accepted");while(sem.Restoring)yield return null;game=sem.Game;
        Check(sem.State.dailyActivities.Count==count&&sem.State.Has("daily-company-3"),"Backup restores daily choices and related story progress together");
        if(!Application.isBatchMode)
        {
            game.SetUiCursor(true);TimeAt(4,20);OpenPage("daily");yield return Shot("01-daily-overview");
            Field("selectedDaily","evening-reset");yield return Shot("03-daily-choices");
            sem.State.Mark("group-friction");sem.State.Mark("group-friction-choice-0");TimeAt(35,17);OpenPage("story");Field("selectedBeat","followthrough");yield return Shot("04-story-continuity");
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);OpenPage("daily");yield return Shot("05-daily-1280x720");
        }
        Finish(true,checks.Count+" checks passed; actual scene actions, isolated save keys"+(Application.isBatchMode?"; GUI screenshots/clicks omitted in batch mode":Application.isEditor?"; focused Game View and button callbacks verified; native mouse not tested":"; actual GUI rendering and button callbacks at two window sizes; native mouse not tested"));
    }
}
