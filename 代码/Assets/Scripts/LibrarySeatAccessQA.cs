using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
public sealed class LibrarySeatAccessQA : MonoBehaviour
{
    const string Prefix="LibrarySeatQA20261022.";
    static bool Requested {
        get {
#if UNITY_EDITOR
            return UnityEditor.SessionState.GetBool("CampusLibrarySeatQA",false);
#else
            return false;
#endif
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate(){
        if(!Requested)return;
        CampusSemester.KeyOverride=Prefix+"state";GameClock.KeyOverride=Prefix+"clock";
        CampusWallet.KeyOverride=Prefix+"wallet";CampusInventory.KeyOverride=Prefix+"inventory";
        CampusWelcomeStory.KeyOverride=Prefix+"welcome";CafeteriaOrdering.PrefsKeyOverride=Prefix+"orders";
        LibraryReservationManager.KeyOverride=Prefix+"reservation";VendingSystem.KeyOverridePrefix=Prefix+"vending_";
        CampusRouteGuide.PreferenceKeyOverride=Prefix+"route";
        CampusWallet.ResetCache();CampusInventory.ResetCache();
        foreach(var k in new[]{"state","state.resume","state.initial","state.day","state.backup","clock","wallet","inventory","welcome","welcomePending","orders","reservation"})PlayerPrefs.DeleteKey(Prefix+k);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot(){if(Requested && !LibraryStudyFlowQA.Requested && !RoutineDayQA.Requested){var go=new GameObject("LibrarySeatAccessQA");DontDestroyOnLoad(go);go.AddComponent<LibrarySeatAccessQA>();}}
    void Start(){StartCoroutine(Run());}
    IEnumerator Run(){
        yield return new WaitForSecondsRealtime(3);
        var log=new StringBuilder();var game=FindFirstObjectByType<CampusGame>();
        game.paused=false;game.SetUiCursor(false);
        var entry=game.Interiors.entrances.First(e=>e&&e.buildingId=="library");
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);
        game.Interiors.TryEnter(entry);
        float deadline=Time.realtimeSinceStartup+40;
        while(game.Interiors.Busy&&Time.realtimeSinceStartup<deadline)yield return null;
        yield return null;yield return null;
        var reg=LibrarySeatRegistry.Ensure();var ui=LibrarySeatInteraction.Ensure();var study=LibraryStudy.Ensure();
        log.AppendLine("Library loaded: "+game.Interiors.Inside+" seats="+reg.All.Count+" interaction active="+ui.isActiveAndEnabled);
        log.AppendLine("Seat floors: "+string.Join(", ",reg.All.GroupBy(s=>s.floor).Select(g=>g.Key+"F="+g.Count())));
        int failed=0,passed=0;
        Action<bool,string> check=(ok,message)=>{log.AppendLine((ok?"PASS: ":"FAIL: ")+message);if(ok)passed++;else failed++;};
        check(reg.All.Count==192,"All 192 actual reading chairs have seat markers");
        check(reg.All.Select(s=>s.id).Distinct().Count()==reg.All.Count,"Seat IDs unique");
        check(reg.CountOnFloor(4)==64,"Fourth floor supports 64 reservations");
        PlayerPrefs.SetInt(CampusWelcomeStory.ProgressKey,game.Story.Data.stages.Length);game.Story.ReloadProgress();
        var semester=CampusSemester.Instance;
        semester.Clock.Restore((new DateTime(2026,9,2,12,0,0)-GameClock.SemesterStart).TotalSeconds);
        var booking=LibraryReservationManager.Ensure();
        foreach(var s in reg.All.ToArray()){
            booking.Cancel();check(booking.TryReserve(s.id,s.floor,s.areaName),"Reserve "+s.id);
            var cc=game.player.GetComponent<CharacterController>();if(cc)cc.enabled=false;
            game.SetTravelPose(s.standPos,0);if(cc)cc.enabled=true;
            game.paused=false;game.SetUiCursor(false);yield return null;
            check(study.SeatNearPlayer()?.id==s.id,"Recognize owned chair from approach "+s.id);
            var near=typeof(LibrarySeatInteraction).GetField("nearSeat",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(ui) as LibrarySeat;
            check(near!=null&&near.id==s.id,"Prompt target matches booking "+s.id);
            check(game.Interact()&&study.Seated&&study.Seat.id==s.id,"Real E dispatcher seats player "+s.id);
            check(game.Seated&&(!cc||!cc.enabled),"Movement locked while seated "+s.id);
            if(s.id.EndsWith("001")){
                float before=semester.State.Course("math").mastery;
                check(semester.Study("math",30,true),"Targeted study transaction "+s.id);
                check(semester.State.Course("math").mastery>before,"Study improves mastery "+s.id);
            }
            study.TryStandUp();check(!study.Seated&&!game.Seated&&(!cc||cc.enabled),"Stand and restore movement "+s.id);
        }
        var first=reg.OnFloor(2).First();booking.Cancel();
        game.SetTravelPose(first.standPos,0);game.paused=false;game.SetUiCursor(false);yield return null;
        check(game.Interact()&&!study.Seated,"Unreserved seat cannot be used");
        booking.TryReserve(first.id,first.floor,first.areaName);
        game.SetTravelPose(first.standPos+Vector3.up*4.3f,0);yield return null;
        check(study.SeatNearPlayer()?.id!=first.id,"Reservation cannot be selected through another floor");
        var exit=game.Interiors.Interior.exits[0];game.SetTravelPose(exit.position,0);game.paused=false;game.SetUiCursor(false);
        check(game.Interiors.TryExit(),"Leave library through real exit");while(game.Interiors.Busy)yield return null;
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);game.paused=false;
        check(game.Interiors.TryEnter(entry),"Reenter library");while(game.Interiors.Busy)yield return null;yield return null;yield return null;
        check(reg.All.Count==192,"Reentry registers exactly 192 seats without duplicates");
        check(booking.ReservedSeatId==first.id,"Existing reservation survives scene reload");
        File.WriteAllText("QA/LibrarySeat/result.txt",(failed==0?"PASS":"FAIL")+": "+passed+" checks passed, "+failed+" failed.\n"+log);
        Debug.Log("LIBRARY_SEAT_ACCESS_COMPLETE "+passed+" passed, "+failed+" failed");

#if UNITY_EDITOR
        UnityEditor.SessionState.SetBool("CampusLibrarySeatQA",false);
        UnityEditor.EditorApplication.isPlaying=false;
#endif
    }
}
