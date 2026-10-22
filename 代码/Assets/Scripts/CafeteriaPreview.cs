using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public class CafeteriaPreview : MonoBehaviour
{
    CampusGame game;
    void Start() {
        game=GetComponent<CampusGame>();
        if(System.Environment.GetCommandLineArgs().Contains("-diningPreview"))game.VisitCafeteria();
        if(System.Environment.GetCommandLineArgs().Contains("-diningQA"))StartCoroutine(Capture());
    }
    void Update(){if(Input.GetKeyDown(KeyCode.F)&&!game.paused)game.VisitCafeteria();}
    IEnumerator Capture() {
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        yield return new WaitForSeconds(3);
        game.enabled=false;game.player.gameObject.SetActive(false);Cursor.visible=false;
        var camera=game.followCamera;camera.fieldOfView=56;
        string dir=Path.Combine(Application.dataPath,"../QA/Cafeteria");Directory.CreateDirectory(dir);
        var positions=new[]{new Vector3(173,4.2f,8),new Vector3(164,5,-15),new Vector3(171,2.8f,8),new Vector3(178,8,19),new Vector3(155,44,38)};
        var targets=new[]{new Vector3(185,9,8),new Vector3(189,8,7),new Vector3(184,4.2f,8),new Vector3(186,8,16),new Vector3(188,0,8)};
        string[] names={"facade","oblique","entrance","windows","site"};
        for(int i=0;i<names.Length;i++) {
            camera.fieldOfView=i==0?106:i==1?65:56;
            camera.transform.position=game.MapToWorld(positions[i]);camera.transform.LookAt(game.MapToWorld(targets[i]));
            yield return new WaitForSeconds(1);DormitoryPreview.SaveCamera(camera,Path.Combine(dir,names[i]+".png"));yield return new WaitForSeconds(1);
        }
        foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))foreach(var m in r.sharedMaterials)
            if(!m||!m.shader||!m.shader.isSupported)throw new System.Exception("Missing/unsupported material on "+r.name);
        File.WriteAllText(Path.Combine(dir,"result.txt"),"PASS: five offscreen cafeteria views; all scene materials supported.\n");
        Application.Quit(0);
    }
}
