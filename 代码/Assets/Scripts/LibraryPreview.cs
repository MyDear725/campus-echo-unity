using System;
using System.Collections;
using System.IO;
using UnityEngine;

// Reproducible exterior views; normal play still enters the independent library scene.
public class LibraryPreview : MonoBehaviour
{
    CampusGame game;
    void Start() {
        game=GetComponent<CampusGame>();
        var args=Environment.GetCommandLineArgs();
        if(Array.IndexOf(args,"-libraryPreview")>=0){game.SetTravelPose(game.MapToWorld(new Vector3(26,.23f,92)),270,12);game.Notify("图书馆：向中央玻璃门走近，按 E 进入。");}
        if(Array.IndexOf(args,"-libraryQA")>=0)StartCoroutine(Capture());
    }
    IEnumerator Capture() {
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        yield return new WaitForSecondsRealtime(3);
        game.enabled=false;game.player.gameObject.SetActive(false);Cursor.visible=false;
        var camera=game.followCamera;
        string folder=Path.Combine(Application.dataPath,"../QA/Library");Directory.CreateDirectory(folder);
        var positions=new[]{new Vector3(12,5.5f,92),new Vector3(17,7,58),new Vector3(39,3.1f,92),new Vector3(43,11.7f,79),new Vector3(18,68,136),new Vector3(89,2.4f,92)};
        var targets=new[]{new Vector3(52,10,92),new Vector3(62,9,92),new Vector3(50,4.6f,92),new Vector3(54,11.5f,79),new Vector3(56,0,92),new Vector3(80,3.1f,92)};
        string[] names={"facade","oblique","entrance","windows","site","rear-entrance"};
        for(int i=0;i<names.Length;i++){
            camera.fieldOfView=i==5?85:i==2?72:55;camera.transform.position=game.MapToWorld(positions[i]);camera.transform.LookAt(game.MapToWorld(targets[i]));
            yield return new WaitForSecondsRealtime(1);DormitoryPreview.SaveCamera(camera,Path.Combine(folder,names[i]+".png"));
        }
        foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))foreach(var m in r.sharedMaterials)
            if(!m||!m.shader||!m.shader.isSupported)throw new Exception("Missing/unsupported material: "+r.name);
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: six library exterior views rendered; scene materials supported.\n");
        Application.Quit(0);
    }
}
