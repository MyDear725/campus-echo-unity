using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
public sealed class LibraryStudyFlowQA : MonoBehaviour {
    public static bool Requested {
        get {
#if UNITY_EDITOR
            return UnityEditor.SessionState.GetBool("CampusLibrarySeatQA",false)&&UnityEditor.SessionState.GetString("CampusLibrarySeatQAMode","")=="ui";
#else
            return false;
#endif
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(Requested){var o=new GameObject("LibraryStudyFlowQA");DontDestroyOnLoad(o);o.AddComponent<LibraryStudyFlowQA>();}}
    readonly StringBuilder log=new StringBuilder(); int passed,failed; CampusGame game;
    void Check(bool ok,string text){log.AppendLine((ok?"PASS: ":"FAIL: ")+text);if(ok)passed++;else failed++;}
    void Start(){Directory.CreateDirectory("QA/LibraryFlow");StartCoroutine(Run());}
    IEnumerator Click(Vector2 p){foreach(var kind in new[]{EventType.MouseDown,EventType.MouseUp}){game.paused=false;CampusOverlayPainter.InjectAt(new Event{type=kind,button=0,clickCount=1},p,kind);yield return null;yield return null;}}
    IEnumerator Click(string key){yield return null;yield return null;if(!LibraryUiProbe.Rects.ContainsKey(key)){Check(false,"Missing UI control "+key);yield break;}log.AppendLine("CLICK "+key+" rect="+LibraryUiProbe.Rects[key]+" screen="+Screen.width+"x"+Screen.height);yield return Click(LibraryUiProbe.Rects[key].center);}
    IEnumerator Shot(string file){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();if(t){File.WriteAllBytes("QA/LibraryFlow/"+file+".png",t.EncodeToPNG());Destroy(t);}}
    IEnumerator Run(){
        yield return new WaitForSecondsRealtime(3);game=FindFirstObjectByType<CampusGame>();var sem=CampusSemester.Instance;
        PlayerPrefs.SetInt(CampusWelcomeStory.ProgressKey,game.Story.Data.stages.Length);game.Story.ReloadProgress();
        sem.Clock.Restore((new DateTime(2026,9,5,12,0,0)-GameClock.SemesterStart).TotalSeconds);
        game.paused=false;game.SetUiCursor(false);
        var entry=game.Interiors.entrances.First(e=>e&&e.buildingId=="library");game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);
        game.Interiors.TryEnter(entry);while(game.Interiors.Busy)yield return null;yield return null;yield return null;
        var seat=LibrarySeatRegistry.Ensure().OnFloor(2).First(s=>s.area=="A");var booking=LibraryReservationManager.Ensure();booking.Cancel();
        LibraryUiProbe.Enabled=true;
        var phone=PhoneUI.Ensure();phone.Open();phone.ShowSeatPageForTest(2);yield return null;yield return null;
        yield return Click("phone:seat:"+seat.id);
        var selection=(LibrarySeat)typeof(PhoneUI).GetField("selected",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(phone);
        Check(selection!=null&&selection.id==seat.id,"Mouse selects the exact seat at its centre");
        yield return Shot("03-phone-selection");
        yield return Click("phone:reserve");Check(booking.ReservedSeatId==seat.id,"Mouse confirms the selected reservation");
        yield return Shot("04-phone-booking");phone.Close();
        var cc=game.player.GetComponent<CharacterController>();if(cc)cc.enabled=false;game.SetTravelPose(seat.standPos,0);if(cc)cc.enabled=true;
        game.paused=false;game.SetUiCursor(false);Check(game.Interact(),"E seats player");
        var study=LibraryStudy.Ensure();Check(study.Seated,"Player is seated before opening subject selector");
        LibraryUiProbe.Enabled=true;yield return null;yield return null;
        yield return Click("seat:start");Check(LibrarySeatInteraction.Instance.SubjectsOpen,"Mouse opens subject selector");yield return Shot("01-study-selector");
        Check(!LibraryUiProbe.WasDrawn("seat:release"),"Release-seat button hidden while subject selector is modal");
        string key="study:math:60";Vector2 point=LibraryUiProbe.Rects.ContainsKey(key)?LibraryUiProbe.Rects[key].center:Vector2.zero;
        if(LibraryUiProbe.WasDrawn("seat:release")){
            Rect release=LibraryUiProbe.Rects["seat:release"];
            foreach(var k in LibraryUiProbe.Rects.Keys.Where(k=>k.StartsWith("study:math:")).ToArray()){
                var r=LibraryUiProbe.Rects[k];if(!r.Overlaps(release))continue;
                point=new Vector2((Mathf.Max(r.xMin,release.xMin)+Mathf.Min(r.xMax,release.xMax))/2,(Mathf.Max(r.yMin,release.yMin)+Mathf.Min(r.yMax,release.yMax))/2);key=k;break;
            }
        }
        log.AppendLine("STUDY_CLICK "+key+" point="+point+" cached="+LibraryUiProbe.Rects.ContainsKey(key));
        int duration=int.Parse(key.Split(':')[2]);float mastery=sem.State.Course("math").mastery;int energy=sem.State.energy;double time=sem.Clock.GameSeconds;
        yield return Click(point);
        log.AppendLine("AFTER_CLICK message="+sem.LastMessage+" energy="+sem.State.energy+" mastery="+sem.State.Course("math").mastery+" time="+sem.Clock.Now+" ui="+sem.UI.IsOpen);
        Check(study.Seated&&booking.ReservedSeatId==seat.id,"Subject click preserves seat and reservation");
        Check(sem.State.Course("math").mastery>mastery,"Subject click increases mathematics mastery");
        Check(sem.State.energy==energy-duration/6,"Energy deducted exactly once");
        Check(Math.Abs(sem.Clock.GameSeconds-time-duration*60)<.1,"Game time advances selected duration exactly once");
        yield return Shot("02-study-result");
        Check(booking.State.inUse,"Subject study retains in-use occupancy");
        var other=LibrarySeatRegistry.Ensure().OnFloor(2).First(s=>s.id!=seat.id);
        Check(!booking.TryReplace(other.id,other.floor,other.areaName,true)&&booking.ReservedSeatId==seat.id,"Cannot replace a seat while seated");
        Check(booking.TryReserve(seat.id,seat.floor,seat.areaName)&&booking.State.inUse,"Reconfirming owned seat preserves occupancy");
        
        foreach(var course in sem.State.courses)foreach(int minutes in new[]{30,60,90}){
            sem.State.energy=100;sem.Clock.Restore((new DateTime(2026,9,5,12,0,0)-GameClock.SemesterStart).TotalSeconds);
            yield return null;yield return null;
            float before=course.mastery;double beforeClock=sem.Clock.GameSeconds;
            yield return Click("study:"+course.id+":"+minutes);
            Check(course.mastery>before,"Mouse study improves "+course.id+" "+minutes+" minutes");
            Check(sem.State.energy==100-minutes/6,"Energy charged once "+course.id+" "+minutes);
            Check(Math.Abs(sem.Clock.GameSeconds-beforeClock-minutes*60)<.1,"Exact study duration "+course.id+" "+minutes);
            Check(study.Seated&&booking.State.inUse&&booking.ReservedSeatId==seat.id,"Seat retained after "+course.id+" "+minutes);
        }
        sem.State.energy=1;
        float saved=sem.State.Course("math").mastery;double savedTime=sem.Clock.GameSeconds;
        yield return Click("study:math:30");
        Check(sem.State.energy==1&&sem.State.Course("math").mastery==saved&&sem.Clock.GameSeconds==savedTime&&study.Seated,"Insufficient energy changes nothing and retains seat");
        sem.State.energy=100;sem.Clock.Restore((new DateTime(2026,9,2,8,0,0)-GameClock.SemesterStart).TotalSeconds);savedTime=sem.Clock.GameSeconds;
        yield return Click("study:math:60");Check(sem.Clock.GameSeconds==savedTime&&sem.State.energy==100,"Timetable conflict waits for confirmation");
        yield return Click("confirm:cancel");Check(study.Seated&&booking.State.inUse&&sem.Clock.GameSeconds==savedTime,"Cancel conflict keeps seat and all progress");
        yield return Click("study:math:60");yield return Click("confirm:accept");
        Check(study.Seated&&sem.State.energy==90&&Math.Abs(sem.Clock.GameSeconds-savedTime-3600)<.1,"Confirmed conflict studies once and retains seat");
        sem.Clock.Restore((new DateTime(2026,9,5,23,50,0)-GameClock.SemesterStart).TotalSeconds);savedTime=sem.Clock.GameSeconds;saved=sem.State.Course("math").mastery;
        yield return Click("study:math:30");yield return Click("confirm:accept");
        Check(study.Seated&&booking.State.inUse&&sem.Clock.GameSeconds==savedTime&&sem.State.Course("math").mastery==saved,"Cross-midnight rejection preserves seat, clock and mastery");
        yield return Click("study:close");yield return null;yield return null;yield return Click("seat:stand");
        Check(!study.Seated&&booking.HasReservation&&!booking.State.inUse,"Standing retains reservation and clears occupancy");
        game.paused=false;Check(game.Interact()&&study.Seated,"Can sit again after standing");
        yield return Click("seat:release");Check(!study.Seated&&!booking.HasReservation&&!game.Seated,"Explicit release alone ends the reservation");
        booking.TryReserve(seat.id,seat.floor,seat.areaName);game.paused=false;game.Interact();LibrarySeatInteraction.Instance.OpenSubjects();
        Check(LibrarySeatInteraction.Instance.SubjectsOpen,"Open self-study menu for forced-exit safety check");
        study.ForceUp();Check(!LibrarySeatInteraction.Instance.SubjectsOpen&&!game.UiBlocking&&!study.Seated,"Forced stand closes owned menu and releases movement lock");
        Check(booking.HasReservation&&!booking.State.inUse,"Forced stand retains booking without occupied flag");
        booking.Cancel();phone.Open();phone.ShowSeatPageForTest(2);yield return Click("phone:area:B");
        var second=LibrarySeatRegistry.Ensure().OnFloor(2).First(s=>s.area=="B");yield return Click("phone:seat:"+second.id);
        selection=(LibrarySeat)typeof(PhoneUI).GetField("selected",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(phone);
        Check(selection!=null&&selection.id==second.id,"Reading-area seat can be selected independently");
        yield return Click("phone:reserve");Check(booking.ReservedSeatId==second.id,"Reading-area reservation uses selected ID");phone.Close();
        File.WriteAllText("QA/LibraryFlow/result.txt",$"{(failed==0?"PASS":"FAIL")}: {passed} passed, {failed} failed.\n"+log);
        Debug.Log("LIBRARY_FLOW_COMPLETE "+passed+" passed "+failed+" failed");
        LibraryUiProbe.Enabled=false;
#if UNITY_EDITOR
        UnityEditor.SessionState.SetBool("CampusLibrarySeatQA",false);UnityEditor.EditorApplication.Exit(0);
#endif
    }
}
