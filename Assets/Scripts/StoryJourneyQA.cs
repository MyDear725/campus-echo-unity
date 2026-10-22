using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Accelerated chronological playthrough: only normal transactions earn milestones.
// Travel poses skip repetitive walking; waits/sleep advance dates, never injected flags.
public sealed class StoryJourneyQA : MonoBehaviour
{
    static string Folder {get {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"-storyJourneyOutput");return i>=0?Path.GetFullPath(a[i+1]):Path.GetFullPath("QA/StoryJourney");}}
    readonly List<string> checks=new List<string>();
    readonly HashSet<string> storyChoices=new HashSet<string>(),welcomeChoices=new HashSet<string>(),dailyChoices=new HashSet<string>();
    CampusSemester sem;CampusGame game;bool finished;int route;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate()
    {
        if(!Environment.GetCommandLineArgs().Contains("-storyJourneyQA"))return;
        const string p="StoryJourneyQA20261022.";
        CampusSemester.KeyOverride=p+"semester";CampusWelcomeStory.KeyOverride=p+"welcome";GameClock.KeyOverride=p+"clock";
        CampusRouteGuide.PreferenceKeyOverride=p+"route";CampusWallet.KeyOverride=p+"wallet";CampusInventory.KeyOverride=p+"inventory";
        CafeteriaOrdering.PrefsKeyOverride=p+"orders";LibraryReservationManager.KeyOverride=p+"booking";VendingSystem.KeyOverridePrefix=p+"stock";
        foreach(var k in new[]{"semester","semester.day","semester.resume","semester.backup","semester.initial","welcome","welcomePending","clock","route","wallet","inventory","orders","booking"})PlayerPrefs.DeleteKey(p+k);
        foreach(var s in JsonUtility.FromJson<WelcomeStoryData>(Resources.Load<TextAsset>("Onboarding/story").text).stages)PlayerPrefs.DeleteKey(p+"welcomeChoice_"+s.id);
        CampusWallet.ResetCache();CampusInventory.ResetCache();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {if(Environment.GetCommandLineArgs().Contains("-storyJourneyQA")){var go=new GameObject("Chronological story journey QA");DontDestroyOnLoad(go);go.AddComponent<StoryJourneyQA>();}}
    void Start(){Directory.CreateDirectory(Folder);Application.logMessageReceived+=Log;StartCoroutine(Run());StartCoroutine(Timeout());}
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string m,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)Finish(false,m+"\n"+stack);}
    void Check(bool ok,string m)
    {
        if(!ok){Finish(false,m);throw new Exception(m);}
        checks.Add("PASS: route "+route+" / "+m);File.WriteAllLines(Folder+"/progress.txt",checks);
    }
    void Finish(bool ok,string message)
    {
        if(finished)return;finished=true;
        File.WriteAllText(Folder+"/result.txt",(ok?"PASS: ":"FAIL: ")+message+"\n"+string.Join("\n",checks));
        File.WriteAllLines(Folder+"/covered-choices.txt",storyChoices.OrderBy(x=>x).Select(x=>"story "+x).Concat(welcomeChoices.OrderBy(x=>x).Select(x=>"welcome "+x)).Concat(dailyChoices.OrderBy(x=>x).Select(x=>"daily "+x)));
        Debug.Log("STORY_JOURNEY_QA_"+(ok?"PASS":"FAIL")+": "+message);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(ok?0:1);
#else
        Application.Quit(ok?0:1);
#endif
    }
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(1200);Finish(false,"Timeout");}
    void Bind()
    {
        game=sem.Game;game.enabled=false;game.paused=false;
        // Freeze passive seconds only. Every date advance below still uses the
        // real WaitUntil/Rest transaction and clock day-change listeners.
        sem.Clock.enabled=false;
    }
    void Pose(Vector3 p,float yaw=0)
    {
        var cc=game.Interiors.IndoorController;bool enabled=cc&&cc.enabled;if(cc)cc.enabled=false;
        game.SetTravelPose(p,yaw);Physics.SyncTransforms();if(cc)cc.enabled=enabled;
    }
    IEnumerator Settle(){float end=Time.realtimeSinceStartup+50;while(game.Interiors.Busy&&Time.realtimeSinceStartup<end)yield return null;Check(!game.Interiors.Busy,"Travel settles");yield return null;yield return null;sem.Bind();Bind();}
    IEnumerator Exit()
    {
        sem.UI.Close();game.SetUiCursor(false);
        var dorm=DormitoryCorridor.Current(game);
        if(dorm&&dorm.InsideRoom)
        {
            var room=dorm.GetComponent<DormitoryRoomInterior>();Pose(room.ExitApproach);
            Check(dorm.TryInteract(),"Leave bedroom");while(dorm.InsideRoom)yield return null;yield return new WaitForSecondsRealtime(.35f);
        }
        if(dorm&&dorm.Floor!=1)yield return Stair(1);
        if(LibraryStudy.Instance)LibraryStudy.Instance.ForceUp();
        var exit=game.Interiors.Interior.exits.First(e=>e&&e.gameObject.activeInHierarchy);
        Pose(exit.position);game.SetUiCursor(false);Check(game.Interiors.TryExit(),"Actual interior exit");yield return Settle();
    }
    IEnumerator Enter(string building)
    {
        if(game.Interiors.Inside&&game.Interiors.Interior.buildingId==building)yield break;
        if(game.Interiors.Inside)yield return Exit();
        sem.UI.Close();game.SetUiCursor(false);
        var e=game.Interiors.entrances.First(x=>x&&x.buildingId==building);Pose(game.MapToWorld(e.approachMapPosition),e.enterYaw);
        Check(game.Interact(),"E enters "+building);yield return Settle();Check(game.Interiors.Inside&&game.Interiors.Interior.buildingId==building,"Correct interior "+building);
    }
    IEnumerator At(string location)
    {
        sem.UI.Close();
        if(location=="bridge"||location=="lake"||location=="track"){if(game.Interiors.Inside)yield return Exit();}
        else yield return Enter(location=="a"?"teaching-a":location=="b"?"teaching-b":location=="c"?"teaching-c":location=="dorm"?"dormitory-1":location);
        sem.Bind();Bind();var point=FindObjectsByType<CampusSemesterPoint>(FindObjectsSortMode.None).First(p=>p.location==location&&p.gameObject.activeInHierarchy);
        Pose(point.transform.position);game.SetUiCursor(true);Check(sem.At(location),"Actual story/activity point: "+location);
    }
    IEnumerator Stair(int floor)
    {
        var dorm=DormitoryCorridor.Current(game);
        while(dorm.Floor!=floor)
        {
            int direction=floor>dorm.Floor?1:-1;Pose(dorm.transform.TransformPoint(new Vector3(23,.06f,direction*.4f)));game.SetUiCursor(false);
            Check(game.Interact(),"E stair interaction");while(dorm.Transitioning)yield return null;
        }
    }
    void CallWelcome(string method,params object[] args)=>typeof(CampusWelcomeStory).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game.Story,args);
    IEnumerator Shot(string name)
    {
        if(Application.isBatchMode)yield break;
        yield return null;yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");yield return new WaitForSecondsRealtime(.15f);
    }
    IEnumerator Dialogue(int index,int choice)
    {
        game.SetUiCursor(false);float until=Time.realtimeSinceStartup+4;
        while(!game.Story.DialogueOpen&&Time.realtimeSinceStartup<until)yield return null;
        Check(game.Story.DialogueOpen&&game.Story.StageIndex==index,"Welcome stage opened naturally: "+index);
        var stage=game.Story.Data.stages[index];double before=sem.Clock.GameSeconds;
        // Close/reopen is the same callback used by ESC/J; it must not complete a stage.
        CallWelcome("CloseDialogue");Check(game.Story.StageIndex==index&&!game.UiBlocking,"Dismiss welcome preserves stage and releases input");
        CallWelcome("OpenDialogue");
        for(int i=1;i<stage.lines.Length;i++)CallWelcome("Advance");
        if(route==0&&index==6)yield return Shot("01-welcome-farewell-choices");
        CallWelcome("Choose",choice);CallWelcome("Advance");
        if(route==0&&index==6)yield return Shot("02-welcome-room-and-rest-guidance");
        CallWelcome("Advance");
        Check(game.Story.StageIndex==index+1&&!game.Story.DialogueOpen&&!game.UiBlocking,"Choice/reply/closing advances exactly one welcome stage");
        Check(PlayerPrefs.GetInt(CampusWelcomeStory.ProgressKey+"Choice_"+stage.id,-1)==choice&&sem.Clock.GameSeconds==before,"Welcome choice saved; onboarding remains untimed");
        welcomeChoices.Add(stage.id+":"+choice);yield return null;
    }
    IEnumerator Meal()
    {
        yield return Enter("cafeteria");
        var order=CampusSystemsRegistry.FindOrdering();var ui=order.GetComponent<CafeteriaOrderingUI>();order.prepareSeconds=1;order.eatSeconds=1;
        var spot=order.orderSpots.First(s=>s&&s.GetComponent<CafeteriaCounter>()&&CafeteriaShops.Get(s.GetComponent<CafeteriaCounter>().shop).business==CafeteriaBusiness.Meal);
        Pose(spot.position);game.SetUiCursor(false);Check(game.Interact()&&order.UiOpen,"E opens a real meal counter");
        order.CartAdd(0);long before=CampusWallet.BalanceFen;int cost=order.CartTotalFen();Check(order.TryPay()&&CampusWallet.BalanceFen==before-cost,"Normal meal order deducts actual price once");order.ClosePanel();
        var carried=order.OrderAt(order.OrderCount-1);var collect=order.CollectAnchorFor((CafeteriaShop)carried.shop,spot.position);Pose(collect.position);
        float end=Time.realtimeSinceStartup+5;while(!order.CanCollect&&Time.realtimeSinceStartup<end)yield return null;
        Check(game.Interact()&&order.State==CafeteriaOrderState.Carrying,"E collects after real shortened preparation countdown");
        var seat=order.seatSpots.First(s=>s&&s.gameObject.activeInHierarchy);Pose(seat.position-seat.forward*.85f,seat.eulerAngles.y);game.SetUiCursor(false);
        Check(game.Interact()&&order.Seated,"E takes an actual dining seat");
        end=Time.realtimeSinceStartup+5;while(order.State!=CafeteriaOrderState.TrayToReturn&&Time.realtimeSinceStartup<end)yield return null;
        Check(order.State==CafeteriaOrderState.TrayToReturn,"Real eating progression finishes meal");ui.StandUpForTest();
        Pose(order.returnSpots.First(s=>s&&s.gameObject.activeInHierarchy).position);game.SetUiCursor(false);
        Check(game.Interact()&&!order.HasTray&&sem.State.Has("meal"),"E tray return earns actual meal milestone");
    }
    IEnumerator Library(bool welcome)
    {
        yield return Enter("library");var registry=LibrarySeatRegistry.Ensure();float end=Time.realtimeSinceStartup+8;
        while(registry.All.Count==0&&Time.realtimeSinceStartup<end)yield return null;
        var seat=registry.All.First();var booking=LibraryReservationManager.Ensure();if(!booking.IsReservedSeat(seat.id))Check(booking.TryReserve(seat.id,seat.floor,seat.areaName),"Real library reservation");
        Pose(seat.sitPos-seat.facing*Vector3.forward*.7f,seat.facing.eulerAngles.y);game.SetUiCursor(false);
        var service=LibraryStudy.Ensure();var nearest=service.SeatNearPlayer();
        var bound=typeof(LibraryStudy).GetField("game",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service) as CampusGame;
        Check(game.Interact()&&LibraryStudy.Instance.Seated,"E sits at reserved library seat; boundGame="+(bool)bound+", currentGame="+(bound==game)+", nearest="+(nearest==null?"none":nearest.id));
        if(welcome){LibraryStudy.Instance.StartStudy();Check(LibraryStudy.Instance.Studying,"Onboarding start-study callback accepted");LibraryStudy.Instance.StopStudy();}
        else
        {
            foreach(var course in sem.State.courses)
            {
                Check(sem.Study(course.id,30,true),"Actual targeted study earns prerequisite: "+course.id);
                foreach(var task in sem.State.assignments.Where(t=>t.course==course.id&&t.prepared&&!t.submitted).ToArray())Check(sem.Submit(task.id),"Prepared homework submitted normally");
            }
        }
        LibraryStudy.Instance.FinishAndRelease();game.SetUiCursor(true);
    }
    IEnumerator Welcome(int choice)
    {
        Pose(game.guide.position+Vector3.forward);game.SetUiCursor(false);Check(game.Interact(),"E begins welcome at actual guide");yield return Dialogue(0,choice);
        if(route==0)
        {
            yield return Enter("dormitory-7");
            Check(game.Story.StageIndex==1&&!DormitoryCorridor.Current(game)&&game.Story.Objective.Contains("一至六舍"),"Welcome explicitly directs to six-storey dorms instead of seventh dorm split-level hall");
        }
        yield return Enter("dormitory-1");yield return Stair(2);yield return Dialogue(1,choice);
        yield return Enter("cafeteria");yield return Dialogue(2,choice);yield return Meal();yield return Dialogue(3,choice);
        yield return Enter("teaching-a");yield return Dialogue(4,choice);
        yield return Library(true);yield return Dialogue(5,choice);
        yield return Enter("dormitory-1");yield return Dialogue(6,choice);
        Check(sem.Ready&&sem.State.Has("meal"),"All welcome milestones earned without injecting progress");
    }
    void Wait(DateTime target)
    {
        if(target<=sem.Clock.Now)return;
        Check(sem.WaitUntil(target,true),"Normal waiting advances within current date to "+target.ToString("MM-dd HH:mm"));
    }
    IEnumerator Stories()
    {
        bool repeat;
        do
        {
            repeat=false;
            foreach(var beat in sem.Catalog.beats.OrderBy(b=>b.day))
            {
                if(!SemesterRules.Available(sem.State,beat,sem.Clock.DayIndex))continue;
                yield return At(beat.location);
                var before=JsonUtility.ToJson(sem.State);double seconds=sem.Clock.GameSeconds;
                Check(!sem.CompleteBeat(beat.id,-1,true)&&JsonUtility.ToJson(sem.State)==before&&sem.Clock.GameSeconds==seconds,"Invalid choice leaves story untouched: "+beat.id);
                string intro=sem.StoryText(beat);
                if(!string.IsNullOrEmpty(beat.recallFlag))Check(intro.Contains(sem.State.Has(beat.recallFlag)?beat.recallText:beat.alternateRecallText),"Earlier real choice is recalled: "+beat.id);
                int selected=beat.id=="club"?route%2:route/2;
                Check(sem.CompleteBeat(beat.id,selected,true),"Natural prerequisites permit story "+beat.id+" choice "+selected);
                Check(sem.State.Has(beat.id+"-choice-"+selected)&&sem.LastMessage.Contains(beat.replies[selected]),"Selected reply and completion are recorded: "+beat.id);
                long money=CampusWallet.BalanceFen;string state=JsonUtility.ToJson(sem.State);seconds=sem.Clock.GameSeconds;
                Check(!sem.CompleteBeat(beat.id,1-selected,true)&&CampusWallet.BalanceFen==money&&JsonUtility.ToJson(sem.State)==state&&sem.Clock.GameSeconds==seconds,"Revisit cannot switch choice or duplicate rewards: "+beat.id);
                storyChoices.Add(beat.id+":"+selected);repeat=true;
            }
        }while(repeat);
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(3);sem=CampusSemester.Instance;Bind();QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
        if(!Application.isBatchMode)Screen.SetResolution(1440,900,FullScreenMode.Windowed);
        if(Environment.GetCommandLineArgs().Contains("-storyJourneyResetProbe"))
        {
            yield return Library(true);Check(CampusSemesterSave.RestoreSemester(sem),"Reset after using persistent library service");
            while(sem.Restoring)yield return null;Bind();yield return Library(true);
            Finish(true,"Persistent library seat E works after semester reset");yield break;
        }
        for(route=0;route<4;route++)
        {
            if(route>0){Check(CampusSemesterSave.RestoreSemester(sem),"Begin independent fresh journey");while(sem.Restoring)yield return null;Bind();}
            Check(sem.State.flags.Count==0&&game.Story.StageIndex==0,"Fresh route has no fabricated story prerequisites");
            yield return Welcome(route/2);
            yield return At("dorm");Check(sem.Rest()&&sem.Clock.DayIndex==1,"Actual sleep begins independent September 2");
            for(int day=1;day<=138;day++)
            {
                Check(sem.Clock.DayIndex==day,"Monotonic chronological day "+day);
                if(day<=5)
                {
                    foreach(var lesson in SemesterRules.Lessons(sem.Clock.Now))
                    {
                        Wait(lesson.start.AddMinutes(-15));yield return At(lesson.course=="math"?"a":lesson.course=="code"?"c":"b");
                        yield return Stories();yield return At(lesson.course=="math"?"a":lesson.course=="code"?"c":"b");
                        Check(sem.Attend(lesson.course,route/2==0,true),"Actual scheduled class: "+lesson.course);
                    }
                    if(day==1||day==3)yield return Library(false);
                }
                if(day==70||day==133){yield return At("a");Check(sem.Exam(day==133,true),"Actual "+(day==70?"midterm":"final")+" creates exam prerequisite");}
                yield return Stories();
                if(day<=8)
                {
                    Wait(sem.Clock.Now.Date.AddHours(18));
                    foreach(var activity in sem.DailyOptions.ToArray())
                    {
                        yield return At(activity.location);int selected=route/2;
                        Check(sem.CompleteDaily(activity.id,selected,true),"Daily choice earns participation naturally: "+activity.id+":"+selected);
                        dailyChoices.Add(activity.id+":"+selected);
                    }
                    yield return Stories();
                }
                if(day==40||day==100)
                {
                    string before=JsonUtility.ToJson(sem.State);sem.Save();sem.ReloadState();
                    Check(JsonUtility.ToJson(sem.State)==before,"Mid-journey save/reload retains exact choices and prerequisites");
                }
                if(day<138){yield return At("dorm");Check(sem.Rest(),"Sleep advances to next day without injecting dates");}
            }
            var expected=sem.Catalog.beats.Where(b=>string.IsNullOrEmpty(b.clubRequirement)||b.clubRequirement==sem.State.club).ToArray();
            Check(expected.All(b=>sem.State.Has(b.id)),"Every reachable story completed in chronological route; missing="+string.Join(",",expected.Where(b=>!sem.State.Has(b.id)).Select(b=>b.id)));
            Check(sem.State.projectStep==5&&sem.State.Has("ending")&&sem.State.Has("goodbye-list"),"Five project stages and both farewell stages reached naturally");
            if(route==0&&!Application.isBatchMode)
            {
                game.SetUiCursor(false);sem.UI.Open("story");typeof(CampusSemesterUI).GetField("selectedBeat",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(sem.UI,"goodbye-list");
                Check(sem.UI.IsOpen&&!CampusHud.Ensure().OverlayVisible,"Handbook owns its header without overlapping persistent HUD");
                sem.Tell("这一学期的故事和选择已保留，可以在这里翻阅回忆。");
                yield return Shot("03-semester-farewell-memory");
                Screen.SetResolution(1280,720,FullScreenMode.Windowed);yield return Shot("04-semester-memory-1280x720");
                Screen.SetResolution(1440,900,FullScreenMode.Windowed);sem.UI.Close();
                Check(CampusHud.Ensure().OverlayVisible,"Closing handbook restores persistent HUD");
            }
            Check(sem.ClaimAward(),"Actual project/contribution merits permit one semester award");long balance=CampusWallet.BalanceFen;
            Check(!sem.ClaimAward()&&CampusWallet.BalanceFen==balance,"Final award cannot repeat");
            var snapshot=CampusSemesterSave.Capture(sem);Check(CampusSemesterSave.Apply(sem,snapshot),"Full end-of-route save round trip accepted");while(sem.Restoring)yield return null;Bind();
            Check(sem.State.Has("ending")&&sem.State.awardClaimed&&sem.State.club==(route%2==0?"编程兴趣小组":"湖畔摄影小组"),"Reload preserves ending, exclusive club and claimed reward");
            File.WriteAllText(Folder+"/route-"+route+"-state.json",JsonUtility.ToJson(sem.State,true));
        }
        Check(storyChoices.Count==sem.Catalog.beats.Sum(b=>b.choices.Length),"All 78 semester dialogue choices exercised across independent routes");
        Check(welcomeChoices.Count==game.Story.Data.stages.Sum(s=>s.choices.Length),"All 14 welcome choices exercised");
        Check(dailyChoices.Count==sem.DailyCatalog.activities.Sum(a=>a.choices.Length),"All 24 daily choices exercised");
        Finish(true,checks.Count+" checks; four chronological journeys; no injected story flags, exam results, club selections or daily milestones; travel poses and passive-time acceleration used. Native mouse not tested.");
    }
}
