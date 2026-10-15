using System;
using System.Collections;
using System.IO;
using UnityEngine;

public class TeachingPreview : MonoBehaviour
{
    void Start() {
        var game=GetComponent<CampusGame>();var args=Environment.GetCommandLineArgs();
        if(Array.IndexOf(args,"-teachingPreview")>=0){game.SetTravelPose(game.MapToWorld(new Vector3(65,.23f,-72)),180,14);game.Notify("教学区：ABC 外观已更新。靠近各楼入口按 E 进入。");}
        if(Array.IndexOf(args,"-teachingQA")>=0)StartCoroutine(Capture(game));
    }
    IEnumerator Capture(CampusGame game) {
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;yield return new WaitForSecondsRealtime(3);
        game.enabled=false;game.player.gameObject.SetActive(false);Cursor.visible=false;var camera=game.followCamera;
        string folder=Path.Combine(Application.dataPath,"../QA/Teaching");Directory.CreateDirectory(folder);
        var positions=new[]{new Vector3(66,6,-73),new Vector3(58,4,-97),new Vector3(58,4,-87),new Vector3(67,4,-87),new Vector3(60,80,-52),new Vector3(49,2.4f,-78),new Vector3(55,2.4f,-52)};
        var targets=new[]{new Vector3(60,10,-110),new Vector3(58,11,-110),new Vector3(42,9,-80),new Vector3(80,9,-87),new Vector3(60,0,-100),new Vector3(41,.2f,-74),new Vector3(50,5,-76)};
        string[] names={"courtyard","a-facade","b-facade","c-facade","site","b-entry-paving","b-courtyard-cleared"};
        for(int i=0;i<names.Length;i++){camera.fieldOfView=i==0?82:i==1?92:70;camera.transform.position=game.MapToWorld(positions[i]);camera.transform.LookAt(game.MapToWorld(targets[i]));yield return new WaitForSecondsRealtime(1);DormitoryPreview.SaveCamera(camera,Path.Combine(folder,names[i]+".png"));}
        camera.orthographic=true;camera.orthographicSize=56;camera.transform.position=game.MapToWorld(new Vector3(65,160,-88));camera.transform.rotation=Quaternion.Euler(90,180,0);
        yield return new WaitForSecondsRealtime(.5f);DormitoryPreview.SaveCamera(camera,Path.Combine(folder,"aligned-footprints.png"));
        foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))foreach(var m in r.sharedMaterials)if(!m||!m.shader||!m.shader.isSupported)throw new Exception("Missing material "+r.name);
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: seven teaching exterior views and full orthographic footprint view rendered; all scene materials supported.\n");Application.Quit(0);
    }
}
