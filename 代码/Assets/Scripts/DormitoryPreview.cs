using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A convenient arrival point for inspecting the revised residence facades.
public class DormitoryPreview : MonoBehaviour
{
    CampusGame game;
    void Start() {
        game=GetComponent<CampusGame>();
        if(System.Environment.GetCommandLineArgs().Contains("-dormPreview"))Visit();
        if(System.Environment.GetCommandLineArgs().Contains("-dormQA"))StartCoroutine(Capture());
    }
    void Update(){if(Input.GetKeyDown(KeyCode.B)&&!game.paused)Visit();}
    void Visit(){game.VisitDormitory();}
    IEnumerator Capture() {
        while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
        yield return new WaitForSeconds(3);
        game.enabled=false;game.player.gameObject.SetActive(false);Cursor.visible=false;
        var camera=game.followCamera;camera.fieldOfView=56;
        string dir=Path.Combine(Application.dataPath,"../QA/Dormitories");Directory.CreateDirectory(dir);
        var positions=new[]{new Vector3(252,6,-104),new Vector3(271,2.4f,-114),new Vector3(266,9,-115),new Vector3(245,66,-45),new Vector3(245,3,-68),new Vector3(205,2,-86)};
        var targets=new[]{new Vector3(271,10,-129),new Vector3(271,1.8f,-124),new Vector3(270,10,-124),new Vector3(245,0,-104),new Vector3(245,5,-118),new Vector3(205,1.6f,-130)};
        string[] names={"facade","entrance","balconies","district","garden","road"};
        for(int i=0;i<names.Length;i++){
            camera.transform.position=game.MapToWorld(positions[i]);camera.transform.LookAt(game.MapToWorld(targets[i]));
            yield return new WaitForSeconds(1);SaveCamera(camera,Path.Combine(dir,names[i]+".png"));yield return new WaitForSeconds(1);
        }
        foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))foreach(var m in r.sharedMaterials)if(!m||!m.shader||!m.shader.isSupported)throw new System.Exception("Missing/unsupported material on "+r.name);
        File.WriteAllText(Path.Combine(dir,"result.txt"),"PASS: six rendered dormitory/courtyard views; all scene materials supported.\n");
        Application.Quit(0);
    }
    public static void SaveCamera(Camera camera,string path) {
        // Offscreen rendering also works when the Windows player is hidden.
        var target=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        var active=RenderTexture.active;float aspect=camera.aspect;
        try {
            target.Create();camera.aspect=1.6f;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            byte[] bytes=image.EncodeToPNG();if(bytes.Length<10000)throw new System.Exception("Rendered image is empty: "+path);
            File.WriteAllBytes(path,bytes);
        } finally {camera.aspect=aspect;RenderTexture.active=active;target.Release();Destroy(target);Destroy(image);}
    }
}
