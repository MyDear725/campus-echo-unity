using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class CampusRemoteCompatibilityQA : MonoBehaviour
{
    CampusGame game;float vertical;string folder;bool done;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot(){if(Environment.GetCommandLineArgs().Contains("-remoteCompatibilityQA"))new GameObject("Remote comprehensive compatibility QA").AddComponent<CampusRemoteCompatibilityQA>();}
    IEnumerator Start()
    {
        folder=Path.Combine(Application.dataPath,"../QA/RemoteCompatibility");Directory.CreateDirectory(folder);
        var routines=new Stack<IEnumerator>();routines.Push(Run());
        while(routines.Count>0){bool next;object current=null;try{next=routines.Peek().MoveNext();if(next)current=routines.Peek().Current;}catch(Exception error){File.WriteAllText(folder+"/result.txt","FAIL: "+error);Debug.LogException(error);Application.Quit(1);yield break;}if(!next){routines.Pop();continue;}if(current is IEnumerator nested)routines.Push(nested);else yield return current;}
    }
    void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    void Pose(float x,float y,float z){game.SetTravelPose(game.MapToWorld(new Vector3(x,y,z)),180,12);vertical=0;}
    IEnumerator Walk(float x,float z)
    {
        var goal=game.MapToWorld(new Vector3(x,0,z));
        for(int i=0;i<1800;i++){var delta=goal-game.player.position;delta.y=0;if(delta.magnitude<.1f)yield break;game.Outdoor.Move(Vector3.ClampMagnitude(delta,.11f),.02f,ref vertical,false);if(i%12==0)yield return null;}
        throw new Exception("Comprehensive route blocked at "+game.WorldToMap(game.player.position)+" target "+x+","+z);
    }
    IEnumerator Shot(string name,Vector3 position,Vector3 target)
    {game.enabled=false;game.followCamera.transform.position=game.MapToWorld(position);game.followCamera.transform.LookAt(game.MapToWorld(target));yield return new WaitForSecondsRealtime(.6f);DormitoryPreview.SaveCamera(game.followCamera,folder+"/"+name+".png");game.enabled=true;}
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(4);game=FindFirstObjectByType<CampusGame>();Check(game&&game.Automated,"Automated game missing");game.paused=false;
        var root=GameObject.Find("开发区校区 · 校园环境").transform;var comprehensive=root.Find("综合楼 · 通高玻璃与石材门廊");
        Check(comprehensive&&comprehensive.GetComponentsInChildren<MeshRenderer>().Length>10,"Remote comprehensive building missing");
        Check(root.Find("信息楼 · 石材格栅与玻璃中庭")&&root.Find("室外篮球场 · 主路接驳"),"Local building revisions lost");
        Check(FindFirstObjectByType<OutdoorBasketballLandmark>().courtCentres.All(c=>c.x>120),"Basketball moved to athletics grounds");
        var data=JsonUtility.FromJson<CampusData>(game.mapData.text);Check(data.features.Single(f=>f.id==39327169).kind=="track","Athletics site changed");
        Check(data.obstacles.Any(o=>o.name=="Comprehensive rear")&&data.roads.Any(r=>r.name=="教学区篮球场南入口连接路"),"Combined map lost remote collision or local roads");
        Pose(-130,.35f,77);yield return Walk(-130,52);Check(Mathf.Abs(game.player.position.y-.95f)<.15f,"Front steps did not reach foyer");
        yield return Walk(-130,77);Check(game.player.position.y<.5f,"Front steps did not return to plaza");
        Pose(-151,.35f,61.45f);yield return Walk(-139,61.45f);Check(Mathf.Abs(game.player.position.y-.95f)<.15f,"Side ramp did not reach landing");yield return Walk(-151,61.45f);
        Pose(-176,.35f,70);yield return Walk(-85,70);Check(game.player.position.y<.5f,"Comprehensive forecourt blocks main road");
        var catalog=game.guideCharacterCatalog;Check(catalog&&catalog.ResolveVisual(PlayerCharacterChoice.Male)&&catalog.ResolveVisual(PlayerCharacterChoice.Female),"VRoid catalogue was overwritten");
        Check(game.player.GetComponent<PlayerCharacterSelector>().PrimaryRenderer.sharedMesh.vertexCount>1000,"Player visual failed after scene merge");
        yield return Shot("01-comprehensive-facade",new Vector3(-175,12,105),new Vector3(-130,12,48));
        yield return Shot("02-comprehensive-entrance",new Vector3(-130,2.4f,85),new Vector3(-130,7,53));
        yield return Shot("03-foyer",new Vector3(-130,2.5f,59),new Vector3(-130,6,45));
        yield return Shot("04-teaching-basketball-retained",new Vector3(177,45,-38),new Vector3(130,0,-91));
        yield return Shot("05-athletics-retained",new Vector3(-194,62,-42),new Vector3(-135,0,-104));
        foreach(var r in comprehensive.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)Check(m&&m.shader&&m.shader.isSupported,"Unsupported remote building material");
        done=true;File.WriteAllText(folder+"/result.txt","PASS: remote comprehensive facade/materials loaded; real player walks front steps, ground-floor foyer, side ramp and forecourt main road; local information building, corrected basketball/athletics sites, road metadata and VRoid catalogue retained.\n");Debug.Log("REMOTE_COMPATIBILITY_QA_PASS");Application.Quit(0);
    }
    void Update(){if(game)game.paused=false;if(!done&&Time.realtimeSinceStartup>150){Debug.LogError("Remote compatibility QA timeout");Application.Quit(1);}}
}
