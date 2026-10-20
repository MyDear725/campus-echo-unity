using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Explicit opt-in only. Every gameplay preference is isolated from the user's save.
public sealed class SemesterIntegrationQA : MonoBehaviour
{
    const string Prefix="SemesterQA20261020.";
    readonly List<string> checks=new List<string>();
    CampusSemester semester;
    CampusGame game;
    string folder;
    bool done;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate()
    {
        if(!Environment.GetCommandLineArgs().Contains("-semesterQA"))return;
        CampusRouteGuide.PreferenceKeyOverride=Prefix+"routeLine";
        CampusSemester.KeyOverride=Prefix+"state";
        GameClock.KeyOverride=Prefix+"clock";
        CampusWallet.KeyOverride=Prefix+"wallet";
        CampusInventory.KeyOverride=Prefix+"inventory";
        CampusWelcomeStory.KeyOverride=Prefix+"welcome";
        CafeteriaOrdering.PrefsKeyOverride=Prefix+"orders";
        LibraryReservationManager.KeyOverride=Prefix+"reservation";
        VendingSystem.KeyOverridePrefix=Prefix+"vending_";
        CampusWallet.ResetCache();CampusInventory.ResetCache();
        foreach(var key in new[]{"state","clock","wallet","inventory","welcome","welcomePending","orders","reservation","state.day","state.resume","state.initial","state.backup","routeLine"})PlayerPrefs.DeleteKey(Prefix+key);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-semesterQA")||Environment.GetCommandLineArgs().Contains("-campusWorldQA")||FindFirstObjectByType<SemesterIntegrationQA>())return;
        var go=new GameObject("SemesterIntegrationQA");DontDestroyOnLoad(go);go.AddComponent<SemesterIntegrationQA>();
    }
    void Start()
    {
        folder=Path.GetFullPath("QA/Semester");Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"result.txt"),"RUNNING\n");
        Application.logMessageReceived+=Log;
        StartCoroutine(Run());StartCoroutine(Deadline());
    }
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string text,string stack,LogType kind)
    {
        if(kind!=LogType.Exception&&kind!=LogType.Error)return;
        if(stack.Contains("UnityEditor.Search.SearchDatabase"))return;
        Finish(false,text+"\n"+stack);
    }
    void Check(bool condition,string message)
    {
        if(!condition){Finish(false,message);throw new Exception("SemesterQA: "+message);}
        checks.Add("PASS: "+message);
    }
    IEnumerator Deadline(){yield return new WaitForSecondsRealtime(300);Finish(false,"Timed out");}
    void Finish(bool passed,string message)
    {
        if(done)return;done=true;
        File.WriteAllText(Path.Combine(folder,"result.txt"),(passed?"PASS":"FAIL")+": "+message+"\n"+string.Join("\n",checks));
        Debug.Log("SEMESTER_QA_"+(passed?"PASS":"FAIL")+": "+message);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(passed?0:1);
#else
        Application.Quit(passed?0:1);
#endif
    }
    IEnumerator Enter(string building)
    {
        if(semester.UI.IsOpen)semester.UI.Close();
        game=semester.Game;
        if(game.Interiors.Inside)
        {
            var exit=game.Interiors.Interior.exits[0];
            game.SetTravelPose(exit.position,0,12);game.paused=false;game.SetUiCursor(false);
            Check(game.Interiors.TryExit(),"Leave previous building using real travel");
            while(game.Interiors.Busy)yield return null;
        }
        var entry=game.Interiors.entrances.First(e=>e&&e.buildingId==building);
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw,12);game.paused=false;game.SetUiCursor(false);
        Check(game.Interact(),"Original E dispatcher enters "+building);
        float deadline=Time.realtimeSinceStartup+50;
        while(game.Interiors.Busy&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(game.Interiors.Inside&&game.Interiors.Interior.buildingId==building,"Real interior loaded: "+building);
        yield return null;yield return null;
        Check(FindObjectsByType<CampusSemesterPoint>(FindObjectsSortMode.None).Any(p=>p.gameObject.scene==game.Interiors.Interior.gameObject.scene),"Interior points appear automatically after real entry");
    }
    void GoPoint(string location)
    {
        var point=FindObjectsByType<CampusSemesterPoint>(FindObjectsSortMode.None).First(p=>p.location==location);
        var cc=game.Interiors.IndoorController;if(cc)cc.enabled=false;
        game.SetTravelPose(point.transform.position,0,12);if(cc)cc.enabled=true;
        Check(semester.At(location),"Action point and player share location: "+location);
        var blockers=Physics.OverlapCapsule(point.transform.position+Vector3.up*.4f,point.transform.position+Vector3.up*1.45f,.28f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Where(c=>c.gameObject.scene==point.gameObject.scene&&c.transform!=game.player&&!c.transform.IsChildOf(game.player)).ToArray();
        Check(blockers.Length==0,"Point has capsule clearance: "+location+" "+string.Join(",",blockers.Select(c=>c.name)));
        if(!game.Interiors.Inside)Check(game.IsMapWalkable(game.WorldToMap(point.transform.position)),"Outdoor point is on the actual walkable campus map: "+location);
    }
    IEnumerator Shot(string name,string page)
    {
        semester.UI.Open(page);yield return null;yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        yield return new WaitForSecondsRealtime(.7f);
        semester.UI.Close();
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(3);
        semester=CampusSemester.Ensure();game=semester.Game;
        Check(game&&game.Story&&game.Interiors,"Original CampusLiving gameplay systems are present");
        Check(semester.Clock.Now==GameClock.SemesterStart,"Fresh fixed calendar starts at 2026-09-01 08:00");
        Check(semester.Clock.normalRate==5,"Exploration pace is five game seconds per real second");
        Check(!semester.Attend("math",true),"Onboarding cannot bypass classroom gating");
        Check(semester.Catalog.beats.Length>=20,"Full-semester story catalog loaded");
        Check(semester.Catalog.beats.All(b=>b.choices.Length==2&&b.replies.Length==2),"Every story has two complete choices and replies");
        Check(semester.Catalog.beats.Select(b=>b.id).Distinct().Count()==semester.Catalog.beats.Length,"Story IDs are unique");
        Check(SemesterRules.Lessons(new DateTime(2026,9,2))[0].room=="A101","First independent morning has real A101 class");
        Check(SemesterRules.Lessons(new DateTime(2026,9,5)).Count==0,"Weekend permits free campus life");
        Check(SemesterRules.Lessons(new DateTime(2027,1,18)).Count==0,"Classes stop after the semester");
        var c=semester.State.Course("math");c.mastery=30;float low=SemesterRules.StudyGain(c,60);c.mastery=90;
        Check(SemesterRules.StudyGain(c,60)<low,"Repeated mastery has diminishing study returns");c.mastery=20;
        yield return Shot("01-onboarding-calendar","today");
        PlayerPrefs.SetInt(CampusWelcomeStory.ProgressKey,game.Story.Data.stages.Length);
        game.Story.ReloadProgress();game.paused=false;
        yield return Enter("dormitory-1");GoPoint("dorm");
        Check(semester.Rest(),"Dormitory rest starts first independent day");
        Check(semester.Clock.Now==new DateTime(2026,9,2,7,0,0),"Rest advances to September 2 at 07:00");
        Check(semester.State.energy==100,"Sleep restores energy");
        double time=semester.Clock.GameSeconds;game.SetUiCursor(true);yield return new WaitForSecondsRealtime(.1f);
        Check(semester.Clock.GameSeconds==time,"Reading or owned UI pauses the common calendar");game.SetUiCursor(false);
        yield return Enter("teaching-a");GoPoint("a");
        Check(semester.CompleteBeat("classmate",0),"First classroom encounter records chosen conversation");
        Check(!semester.Attend("math",true),"Cannot attend too early");
        Check(semester.WaitUntil(new DateTime(2026,9,2,8,10,0)),"Waiting reaches classroom preparation time");
        Check(game.Interact()&&semester.UI.IsOpen,"Contextual E opens actual classroom handbook");semester.UI.Close();
        Vector3 standing=game.player.position;
        Check(semester.BeginClassroom("math"),"Classroom can use an existing first-row chair");
        Check(game.Seated&&semester.ClassroomSeated&&game.player.GetComponent<CafeteriaSitPose>().active,"Classroom uses actual seated pose and movement lock");
        var seatedSave=CampusSemesterSave.Capture(semester);
        Check(Vector3.Distance(seatedSave.position,standing)<.01f,"Classroom snapshots keep safe standing position");
        semester.EndClassroom();
        Check(!game.Seated&&Vector3.Distance(game.player.position,standing)<.01f,"Leaving classroom interaction restores safe standing state");
        Check(semester.Attend("math",true),"First real classroom session completes");
        Check(!semester.Attend("math",true),"Same lesson cannot be attended twice");
        Check(semester.State.assignments.Count==1&&semester.State.Has("class-first"),"Class issues one assignment and first-class milestone");
        Check(!semester.Submit(semester.State.assignments[0].id),"Unprepared homework cannot be submitted");
        Check(semester.CompleteBeat("first-note",1),"Teacher responds to actual completed class");
        yield return Shot("02-classroom-timetable","today");
        yield return Enter("library");
        var registry=LibrarySeatRegistry.Ensure();float seatsDeadline=Time.realtimeSinceStartup+10;
        while(registry.All.Count==0&&Time.realtimeSinceStartup<seatsDeadline)yield return null;
        Check(registry.All.Count>0,"Original library seats are registered");
        var seat=registry.All.First();
        var reservation=LibraryReservationManager.Ensure();
        Check(reservation.TryReserve(seat.id,seat.floor,seat.areaName),"Use existing reservation service");
        Check(LibraryStudy.Ensure().TrySit(seat),"Use existing real seat and seated pose");
        float before=semester.State.Course("math").mastery;
        var librarySnapshot=CampusSemesterSave.Capture(semester);
        Check(Vector3.Distance(librarySnapshot.position,LibraryStudy.Instance.StandingPosition)<.01f,"Library snapshots keep safe standing position");
        Check(semester.Study("math",60),"Targeted library study advances mastery");
        Check(semester.State.Course("math").mastery>before,"Study outcome changes specific course knowledge");
        Check(semester.Submit(semester.State.assignments[0].id),"Prepared homework can be submitted");
        Check(!semester.Submit(semester.State.assignments[0].id),"Homework credit cannot be claimed twice");
        yield return Shot("03-study-homework","study");
        LibraryStudy.Instance.ForceUp();
        // Snapshot round trip covers transactions and the gameplay flags that produced them.
        semester.State.energy=61;semester.Save();CampusSemesterSave.CaptureDay(semester,true);
        var snapshot=CampusSemesterSave.Read(PlayerPrefs.GetString(CampusSemesterSave.DayKey));
        long money=CampusWallet.BalanceFen;int items=CampusInventory.CountOf("课堂笔记");
        CampusWallet.Credit(7777);CampusInventory.Add("课堂笔记",2);semester.State.Mark("artificial-reward");semester.State.energy=3;semester.State.Course("math").mastery=99;
        Check(CampusSemesterSave.RestoreDay(semester),"Daily reset accepted valid complete snapshot");
        while(semester.Restoring)yield return null;
        game=semester.Game;
        Check(CampusWallet.BalanceFen==money&&CampusInventory.CountOf("课堂笔记")==items,"Daily reset restores balance and inventory together");
        Check(!semester.State.Has("artificial-reward")&&semester.State.energy==61,"Daily reset rolls back reward flags and energy");
        Check(semester.State.Course("math").mastery<99,"Daily reset rolls back course knowledge");
        Check(game.Interiors.Inside&&game.Interiors.Interior.buildingId=="library","Daily reset restores additive interior location");
        Check(CampusSemesterSave.HasBackup,"Reset keeps a complete recoverable backup");
        yield return Enter("dormitory-1");GoPoint("dorm");
        var dorm=DormitoryCorridor.Current(game);dorm.RestoreFloor(4);
        yield return dorm.RestoreRoom(0);yield return null;
        Check(dorm.Floor==4&&dorm.InsideRoom,"Actual dormitory fourth-floor room is entered");
        semester.Save();CampusSemesterSave.CaptureDay(semester,true);
        Check(CampusSemesterSave.RestoreDay(semester),"Daily reset supports logical floor and bedroom state");while(semester.Restoring)yield return null;game=semester.Game;
        Check(DormitoryCorridor.Current(game).Floor==4&&DormitoryCorridor.Current(game).InsideRoom,"Daily reset restores fourth floor and bedroom geometry");
        Check(Vector3.Distance(game.player.position,CampusSemesterSave.Read(PlayerPrefs.GetString(CampusSemesterSave.DayKey)).position)<.1f,"Bedroom reset restores recorded player position");
        // Leave the restored room by reloading its exterior snapshot through the supported restore path.
        var outside=CampusSemesterSave.Capture(semester);outside.entrance=null;outside.roomIndex=-1;outside.floor=1;outside.position=game.MapToWorld(new Vector3(3,.23f,194));
        Check(CampusSemesterSave.Apply(semester,outside),"Restore from bedroom to campus has no stale room state");while(semester.Restoring)yield return null;game=semester.Game;
        Check(!game.Interiors.Inside,"Bedroom transition cleans up the old additive interior");
        Check(!CampusSemesterSave.Apply(semester,new SemesterSnapshot()),"Malformed snapshot is rejected before mutation");
        // All later story stages are exercised against real world points; dates are moved only in QA.
        var remaining=semester.Catalog.beats.Where(b=>!semester.State.Has(b.id)).OrderBy(b=>b.day).ToArray();
        foreach(var beat in remaining)
        {
            if(beat.id=="ending")continue;
            foreach(var required in (beat.requires??"").Split(','))if(!string.IsNullOrEmpty(required))semester.State.Mark(required);
            semester.Clock.Restore((GameClock.SemesterStart.Date.AddDays(Math.Max(beat.day,2)).AddHours(17)-GameClock.SemesterStart).TotalSeconds);
            semester.State.energy=100;
            if(beat.location=="bridge"||beat.location=="lake"||beat.location=="track")
            {
                if(game.Interiors.Inside)
                {
                    var exit=game.Interiors.Interior.exits[0];game.SetTravelPose(exit.position,0,12);game.paused=false;game.SetUiCursor(false);Check(game.Interiors.TryExit(),"Return to campus for outdoor story");while(game.Interiors.Busy)yield return null;
                }
                semester.Bind();GoPoint(beat.location);
            }
            else
            {
                string building=beat.location=="a"?"teaching-a":beat.location=="b"?"teaching-b":beat.location=="c"?"teaching-c":beat.location;
                yield return Enter(building);GoPoint(beat.location);
            }
            Check(semester.CompleteBeat(beat.id,1,true),"Story completes at actual location: "+beat.id);
            long reward=CampusWallet.BalanceFen;Check(!semester.CompleteBeat(beat.id,1,true)&&CampusWallet.BalanceFen==reward,"Story/reward cannot repeat: "+beat.id);
        }
        semester.Clock.Restore((new DateTime(2027,1,12,17,0,0)-GameClock.SemesterStart).TotalSeconds);semester.State.energy=100;
        yield return Enter("teaching-a");GoPoint("a");
        foreach(var course in semester.State.courses){course.mastery=90;course.attended=12;course.homeworkSubmitted=7;}
        Check(semester.Exam(false,true),"Midterm evaluates actual course progress");semester.State.energy=100;
        Check(semester.Exam(true,true),"Final evaluates actual course progress");
        Check(semester.State.courses.All(x=>x.final>=0&&x.Grade>=60),"All courses yield final grades and earned credits");
        Check(semester.ClaimAward(),"Applied eligible semester award can be claimed");
        long rewarded=CampusWallet.BalanceFen;Check(!semester.ClaimAward()&&CampusWallet.BalanceFen==rewarded,"Semester money cannot be collected twice");
        yield return Shot("04-semester-grades","growth");
        Check(semester.State.projectStep==5&&CampusInventory.CountOf("期末项目作品")==1,"Five-step project produces an actual inventory artifact");
        semester.Clock.Restore((new DateTime(2027,1,17,18,0,0)-GameClock.SemesterStart).TotalSeconds);
        var ending=semester.Catalog.beats.First(b=>b.id=="ending");
        var exitFinal=game.Interiors.Interior.exits[0];game.SetTravelPose(exitFinal.position,0,12);game.paused=false;game.SetUiCursor(false);game.Interiors.TryExit();while(game.Interiors.Busy)yield return null;semester.Bind();GoPoint("bridge");
        Check(semester.CompleteBeat(ending.id,0),"Semester ending responds to completed finals");
        yield return Shot("05-campus-stories","story");
        yield return Shot("06-campus-memories","life");
        // Pure clock advancement uses isolated state and checks exact daily boundaries.
        semester.Clock.Restore((new DateTime(2026,9,7,23,59,0)-GameClock.SemesterStart).TotalSeconds);
        semester.State.lastAllowanceDay=0;semester.State.energy=23;long beforeWeek=CampusWallet.BalanceFen;
        semester.Clock.AdvanceMinutes(2);
        Check(semester.Clock.Now==new DateTime(2026,9,8,0,1,0),"Clock crosses midnight at exact calendar boundary");
        Check(semester.State.energy==23,"Midnight does not restore energy");
        Check(CampusWallet.BalanceFen==beforeWeek+30000,"New week grants one allowance");
        var dayStart=CampusSemesterSave.Read(PlayerPrefs.GetString(CampusSemesterSave.DayKey));
        Check(dayStart.day==7&&GameClock.SemesterStart.AddSeconds(dayStart.seconds).Hour==0,"Natural midnight writes actual new-day checkpoint");
        Check(!SemesterRules.Available(SemesterState.Fresh(),semester.Catalog.beats.First(b=>b.id=="club"),7),"Future story cannot be started before its opening date");
        semester.Clock.Restore((new DateTime(2026,9,10,7,0,0)-GameClock.SemesterStart).TotalSeconds);
        yield return Enter("dormitory-1");GoPoint("dorm");
        Check(semester.RoutineDay(),"Ordinary-day plan works from morning dormitory point");
        Check(semester.Clock.Now.TimeOfDay==TimeSpan.FromHours(21),"Ordinary-day plan ends exactly at 21:00");
        Check(!semester.RoutineDay(),"Ordinary-day plan cannot be repeated at night");
        semester.Clock.Restore((new DateTime(2026,9,6,7,0,0)-GameClock.SemesterStart).TotalSeconds);
        Check(!semester.RoutineDay(),"Important or first-week day cannot be silently skipped");
        semester.Clock.Restore((new DateTime(2027,1,17,18,0,0)-GameClock.SemesterStart).TotalSeconds);
        semester.UI.RequestReset(true);
        yield return Shot("07-reset-confirmation","today");
        Check(CampusSemesterSave.RestoreSemester(semester),"Full-semester reset is accepted");while(semester.Restoring)yield return null;
        Check(semester.Clock.Now==GameClock.SemesterStart,"Full reset returns exactly to September 1 at 08:00");
        Check(CampusWallet.BalanceFen==CampusWallet.DefaultStartingFen&&CampusInventory.Load().Total==0,"Full reset restores initial allowance and empty inventory");
        Check(!semester.Game.Story.Finished&&semester.State.flags.Count==0,"Full reset restores welcome and every academic/story flag");
        semester.Game.paused=true;
        yield return CampusSemesterSave.RestorePose(semester,CampusSemesterSave.Capture(semester));
        Check(semester.Game.paused,"Resume position restoration preserves the initial character/pause screen");
        semester.Game.paused=false;
        Check(CampusSemesterSave.RestoreBackup(semester),"Complete pre-reset backup can be restored");while(semester.Restoring)yield return null;
        Check(semester.State.Has("ending")&&semester.State.awardClaimed,"Backup restores original ending and claimed reward together");
        Finish(true,checks.Count+" checks passed in the original Unity project with isolated preferences.");
    }
}
