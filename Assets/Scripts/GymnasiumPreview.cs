using System;
using System.Collections;
using System.IO;
using UnityEngine;

public class GymnasiumPreview : MonoBehaviour
{
    CampusGame game;
    void Start() {
        game=GetComponent<CampusGame>();var args=Environment.GetCommandLineArgs();
        if(Array.IndexOf(args,"-gymPreview")>=0){game.SetTravelPose(game.MapToWorld(new Vector3(-158,.23f,6)),180,12);game.Notify("体育馆：走到中央台阶前，按 E 进入。");}
        if(Array.IndexOf(args,"-gymQA")>=0)StartCoroutine(Capture());
    }
    IEnumerator Capture() {
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        yield return new WaitForSecondsRealtime(3);
        game.enabled=false;game.player.gameObject.SetActive(false);Cursor.visible=false;
        var camera=game.followCamera;string folder=Path.Combine(Application.dataPath,"../QA/Gymnasium");Directory.CreateDirectory(folder);
        var positions=new[]{new Vector3(-158,5,12),new Vector3(-121,10,10),new Vector3(-158,2.9f,2),new Vector3(-142,10,-4),new Vector3(-115,61,33)};
        var targets=new[]{new Vector3(-158,11,-9),new Vector3(-158,10,-16),new Vector3(-158,4.5f,-9),new Vector3(-143,10,-14),new Vector3(-158,0,-19)};
        string[] names={"facade","oblique","entrance","windows","site"};
        for(int i=0;i<names.Length;i++) {
            camera.fieldOfView=i==0?91:i==1?72:i==2?83:60;camera.transform.position=game.MapToWorld(positions[i]);camera.transform.LookAt(game.MapToWorld(targets[i]));
            yield return new WaitForSecondsRealtime(1);DormitoryPreview.SaveCamera(camera,Path.Combine(folder,names[i]+".png"));
        }
        foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))foreach(var m in r.sharedMaterials)
            if(!m||!m.shader||!m.shader.isSupported)throw new Exception("Missing/unsupported material: "+r.name);
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: five gymnasium exterior views rendered; scene materials supported.\n");
        Application.Quit(0);
    }
}
