using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

// Opt-in real controller traversal and real IMGUI screenshots; isolated save keys.
public sealed class CampusCourtMapQA : MonoBehaviour
{
    CampusGame game;string folder;float vertical;bool done;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot(){if(Environment.GetCommandLineArgs().Contains("-courtMapQA"))new GameObject("Court and map QA").AddComponent<CampusCourtMapQA>();}
    void LateUpdate(){if(game){game.paused=false;Cursor.lockState=CursorLockMode.None;}}
    void Check(bool valid,string text){if(!valid)throw new Exception(text);}
    IEnumerator Start()
    {
        folder=Path.Combine(Application.dataPath,"../QA/CourtMap");Directory.CreateDirectory(folder);var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Run());
        while(stack.Count>0){bool next;object current=null;try{next=stack.Peek().MoveNext();if(next)current=stack.Peek().Current;}catch(Exception e){File.WriteAllText(folder+"/result.txt","FAIL: "+e);Debug.LogException(e);Application.Quit(1);yield break;}if(!next){stack.Pop();continue;}if(current is IEnumerator routine)stack.Push(routine);else yield return current;}
    }
    void Pose(float x,float z){game.SetTravelPose(game.MapToWorld(new Vector3(x,.35f,z)),180);vertical=0;}
    IEnumerator Walk(float x,float z)
    {
        var goal=game.MapToWorld(new Vector3(x,0,z));
        for(int i=0;i<1800;i++){var d=goal-game.player.position;d.y=0;if(d.magnitude<.12f)yield break;game.Outdoor.Move(Vector3.ClampMagnitude(d,.11f),.02f,ref vertical,false);if(i%12==0)yield return null;}
        throw new Exception("Court connector blocked at "+game.WorldToMap(game.player.position)+" goal "+x+","+z);
    }
    IEnumerator Shot(string name,Vector3 at,Vector3 target)
    {
        game.enabled=false;game.followCamera.transform.position=game.MapToWorld(at);game.followCamera.transform.LookAt(game.MapToWorld(target));yield return new WaitForSecondsRealtime(.7f);DormitoryPreview.SaveCamera(game.followCamera,folder+"/"+name+".png");game.enabled=true;
    }
    IEnumerator ScreenShot(string name)
    {yield return new WaitForSecondsRealtime(.7f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(folder+"/"+name+".png");yield return new WaitForSecondsRealtime(.5f);}
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(4);game=FindFirstObjectByType<CampusGame>();game.paused=false;
        var marker=FindFirstObjectByType<OutdoorBasketballLandmark>();Check(marker&&marker.courtCentres.Length==2&&marker.courtCentres.All(c=>c.x>124&&c.x<147),"Courts must be beside teaching C, not on the athletics grounds");
        var current=JsonUtility.FromJson<CampusData>(game.mapData.text);var track=current.features.First(f=>f.id==39327169);Check(track.kind=="track"&&track.points.All(p=>p.x< -100),"Original athletics map feature must be restored");
        Check(!current.roads.Any(r=>r.name.StartsWith("篮球场")),"Mistaken athletics access roads remain");
        Pose(118,-40);yield return Walk(135.5f,-40);yield return Walk(135.5f,-54);yield return Walk(129,-54);yield return Walk(129,-91);yield return Walk(118,-91);yield return Walk(129,-91);yield return Walk(129,-126);yield return Walk(135.5f,-126);yield return Walk(135.5f,-137);yield return Walk(118,-137);
        yield return Walk(135.5f,-137);yield return Walk(135.5f,-126);yield return Walk(129,-126);yield return Walk(129,-91);yield return Walk(118,-91);yield return Walk(129,-91);yield return Walk(129,-54);yield return Walk(135.5f,-54);yield return Walk(135.5f,-40);yield return Walk(118,-40);
        foreach(var c in marker.courtCentres){Pose(c.x,c.z);yield return Walk(c.x,c.z+8);Check(game.player.position.y>.18f&&game.player.position.y<.5f,"Court floor elevation wrong");}
        yield return Shot("01-court-overview",new Vector3(177,45,-38),new Vector3(130,0,-91));
        yield return Shot("02-hoop-and-net",new Vector3(132,2.1f,-67),new Vector3(135.5f,2.8f,-60.6f));
        yield return Shot("02b-basketball",new Vector3(142.5f,.95f,-70),new Vector3(141.8f,.38f,-68.3f));
        yield return Shot("03-main-road-entrance",new Vector3(135.5f,2.1f,-43),new Vector3(135.5f,2.2f,-80));
        yield return Shot("03b-athletics-restored",new Vector3(-194,62,-42),new Vector3(-135,0,-104));
        yield return Shot("04-information-cleanup",new Vector3(74,2.1f,178),new Vector3(31,10,150));
        game.player.gameObject.SetActive(true);Pose(118,-91);
        var data=JsonUtility.FromJson<CampusData>(game.mapData.text);Check(game.Map.BuildingCount==data.features.Count(f=>f.kind=="building"),"Every building must have a label and index entry");
        Check(game.Map.Open()&&game.UiBlocking&&!game.paused,"Map must open and own mouse and movement");
        yield return ScreenShot("05-map-1600");
        Check(game.Map.VisibleLabelRects.Length==game.Map.BuildingCount,"All building labels must be visible in full-map view");
        foreach(var r in game.Map.VisibleLabelRects)Check(game.Map.LastMapRect.Contains(r.min)&&game.Map.LastMapRect.Contains(r.max),"Building label clipped at map border");
        var labels=game.Map.VisibleLabelRects;for(int i=0;i<labels.Length;i++)for(int j=i+1;j<labels.Length;j++)Check(!labels[i].Overlaps(labels[j]),"Map building labels overlap: "+i+","+j);
        game.Map.SelectBuilding(77943);Check(game.Map.SelectedId==77943,"Selecting cafeteria must highlight correct building");yield return ScreenShot("06-map-cafeteria");
        Screen.SetResolution(1280,720,FullScreenMode.Windowed);yield return ScreenShot("07-map-1280");
        game.Map.Close();Check(!game.Map.IsOpen&&!game.UiBlocking&&!game.paused,"Closing map must release input without pausing");
        Check(game.Map.Open(),"Map must reopen");game.SetTravelPose(game.MapToWorld(new Vector3(118,.3f,-91)),180);Check(!game.Map.IsOpen&&!game.UiBlocking,"Travel closes map and releases cursor owner");
        done=true;File.WriteAllText(folder+"/result.txt","PASS: athletics feature restored, no mistaken west-campus access roads; basketball courts beside teaching C only; 3 teaching-main-road entrances walked both directions; 2 courts, 4 baskets, nets and balls rendered; map labels, selection, 2 resolutions and input ownership.\n");Debug.Log("COURT_MAP_QA_PASS");Application.Quit(0);
    }
    void Update(){if(!done&&Time.realtimeSinceStartup>150){Debug.LogError("Court/map QA timeout");Application.Quit(1);}}
}
