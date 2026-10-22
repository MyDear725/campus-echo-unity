using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

// Opt-in editor regression; LibrarySeatAccessQA isolates every preference key before scene load.
public sealed class RoutineDayQA : MonoBehaviour
{
    public static bool Requested {
        get {
#if UNITY_EDITOR
            return UnityEditor.SessionState.GetBool("CampusLibrarySeatQA",false) && UnityEditor.SessionState.GetString("CampusLibrarySeatQAMode","")=="routine";
#else
            return false;
#endif
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot(){if(Requested){var go=new GameObject("RoutineDayQA");DontDestroyOnLoad(go);go.AddComponent<RoutineDayQA>();}}
    CampusSemester sem; readonly StringBuilder log=new StringBuilder();int passed,failed;
    void Check(bool ok,string message){log.AppendLine((ok?"PASS: ":"FAIL: ")+message);if(ok)passed++;else failed++;File.WriteAllText("QA/RoutineDay/progress.txt",log.ToString());}
    void SetTime(DateTime time){sem.Clock.Restore((time-GameClock.SemesterStart).TotalSeconds);}
    object Field(string name)=>typeof(CampusSemesterUI).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sem.UI);
    void Preview(){sem.UI.Open("life");typeof(CampusSemesterUI).GetMethod("ShowRoutinePlan",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sem.UI,null);}
    IEnumerator Click(bool confirm){
        yield return null;yield return null;
        string key=confirm?"semester:confirm":"semester:return";
        if(!LibraryUiProbe.Rects.ContainsKey(key)){Check(false,"Missing confirmation control "+key);yield break;}
        var p=LibraryUiProbe.Rects[key].center;
        foreach(var type in new[]{EventType.MouseDown,EventType.MouseUp}){
            CampusOverlayPainter.InjectAt(new Event{type=type,button=0,clickCount=1},p,type);yield return null;yield return null;
        }
    }
    IEnumerator Shot(string name){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes("QA/RoutineDay/"+name+".png",t.EncodeToPNG());Destroy(t);}
    void Start(){Directory.CreateDirectory("QA/RoutineDay");StartCoroutine(Run());StartCoroutine(Timeout());}
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(120);Check(false,"Test timeout");Finish();}
    void Finish(){File.WriteAllText("QA/RoutineDay/result.txt",$"{(failed==0?"PASS":"FAIL")}: {passed} passed, {failed} failed\n"+log);
#if UNITY_EDITOR
        UnityEditor.SessionState.SetBool("CampusLibrarySeatQA",false);UnityEditor.EditorApplication.Exit(failed==0?0:1);
#endif
    }
    IEnumerator Run(){
        yield return new WaitForSecondsRealtime(3);LibraryUiProbe.Enabled=true;sem=CampusSemester.Instance;var game=sem.Game;
        PlayerPrefs.SetInt(CampusWelcomeStory.ProgressKey,game.Story.Data.stages.Length);game.Story.ReloadProgress();
        var entry=game.Interiors.entrances.First(e=>e&&e.buildingId=="dormitory-1");
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);game.paused=false;game.SetUiCursor(false);
        Check(game.Interiors.TryEnter(entry),"Enter actual dormitory");while(game.Interiors.Busy)yield return null;yield return null;yield return null;
        sem.Bind();var point=FindObjectsByType<CampusSemesterPoint>(FindObjectsSortMode.None).First(p=>p.location=="dorm");
        var cc=game.Interiors.IndoorController;if(cc)cc.enabled=false;game.SetTravelPose(point.transform.position,0,12);if(cc)cc.enabled=true;
        Check(sem.At("dorm"),"Player at real dormitory action point");
        SetTime(new DateTime(2026,9,1,8,5,0));Preview();yield return null;yield return null;
        string reason=sem.RoutineDayUnavailableReason();Check(reason.Contains("第一周")&&reason.Contains("8点"),"Screenshot date explains BOTH first-week and morning limits");
        string before=JsonUtility.ToJson(sem.State);long money=CampusWallet.BalanceFen;double seconds=sem.Clock.GameSeconds;
        Check(!sem.RoutineDay(),"Backend rejects unavailable day");
        Check(JsonUtility.ToJson(sem.State)==before&&money==CampusWallet.BalanceFen&&seconds==sem.Clock.GameSeconds,"Rejected action preserves all progress, balance and time");
        yield return Click(true);Check(Field("confirmAction")!=null&&sem.Clock.GameSeconds==seconds,"Disabled confirmation cannot run or close preview");
        yield return Shot("01-unavailable");yield return Click(false);Check(Field("confirmAction")==null,"Real mouse return leaves unavailable confirmation");
        SetTime(new DateTime(2026,11,9,7,59,0));sem.State.assignments.Clear();CampusWallet.SetForTest(30000);sem.State.energy=100;
        Check(string.IsNullOrEmpty(sem.RoutineDayUnavailableReason()),"Normal morning at 07:59 is eligible");
        Preview();yield return null;yield return null;
        // Eligibility may change after preview (e.g. another UI transaction spends funds).
        CampusWallet.SetForTest(1999);Check(sem.RoutineDayUnavailableReason().Contains("余额不足"),"Preview rechecks money");
        yield return Click(true);Check(Field("confirmAction")!=null&&CampusWallet.BalanceFen==1999,"Changed eligibility disables confirmation without spending");
        CampusWallet.SetForTest(30000);before=JsonUtility.ToJson(sem.State);seconds=sem.Clock.GameSeconds;
        yield return Click(false);Check(JsonUtility.ToJson(sem.State)==before&&sem.Clock.GameSeconds==seconds,"Cancel keeps progress and time");
        Preview();yield return null;yield return null;yield return Click(true);
        Check(Field("confirmAction")==null,"Real mouse executes and closes valid confirmation");
        Check(sem.Clock.Now.Hour==21&&sem.Clock.Now.Minute==0,"Plan advances clock to 21:00");
        Check(CampusWallet.BalanceFen==28000&&sem.State.energy==65,"Plan settles exactly 20 yuan and 35 energy");
        Check(SemesterRules.Lessons(sem.Clock.Now).All(l=>sem.State.attendance.Contains(l.id)),"Plan records today's classes");
        Check(((string)Field("actionFeedback"))?.Contains("餐费20元")==true,"Successful result remains visible in main content");
        yield return Shot("02-complete");money=CampusWallet.BalanceFen;Check(!sem.RoutineDay()&&CampusWallet.BalanceFen==money,"Repeat execution rejected without duplicate charge");
        SetTime(new DateTime(2026,11,9,8,0,0));Check(sem.RoutineDayUnavailableReason().Contains("8点"),"08:00 boundary is unavailable");
        SetTime(new DateTime(2026,11,10,7,0,0));Check(sem.RoutineDayUnavailableReason().Contains("重要"),"Exam day unavailable");
        SetTime(new DateTime(2026,11,9,7,0,0));sem.State.assignments.Add(new SemesterAssignment{id="qa",course="math",dueDay=sem.Clock.DayIndex});
        Check(sem.RoutineDayUnavailableReason().Contains("作业截止"),"Due assignment blocks ordinary-day plan");sem.State.assignments.Clear();
        SetTime(GameClock.SemesterEnd.Date.AddHours(7));Check(sem.RoutineDayUnavailableReason().Contains("已结束"),"Semester end unavailable");
        // General confirmation errors must stay visible rather than silently dismissing the dialog.
        sem.UI.Open("life");typeof(CampusSemesterUI).GetMethod("Ask",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sem.UI,new object[]{"失败反馈测试","不会改变进度",new Func<bool>(()=>sem.Tell("操作未完成：测试原因")),null});
        yield return null;yield return null;yield return Click(true);
        Check(Field("confirmAction")!=null&&((string)Field("actionFeedback"))?.Contains("测试原因")==true,"Failed transaction keeps confirmation and visible reason");
        sem.UI.Close();Finish();
    }
}
