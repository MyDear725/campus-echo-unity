using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed class VroidPresentationQA : MonoBehaviour
{
    CampusGame game;string folder;bool finished;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot(){if(Environment.GetCommandLineArgs().Contains("-vroidPresentationQA")){var go=new GameObject("VRoid presentation QA");DontDestroyOnLoad(go);go.AddComponent<VroidPresentationQA>();}}
    void Awake(){folder=Path.Combine(Application.dataPath,"../QA/VRoid");Directory.CreateDirectory(folder);Application.logMessageReceived+=Log;}
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string value,string stack,LogType type){if(!finished&&(type==LogType.Error||type==LogType.Exception)){finished=true;File.WriteAllText(folder+"/result.txt","FAIL: "+value+"\n"+stack);Application.Quit(1);}}
    void Check(bool ok,string detail){if(!ok)throw new Exception(detail);Debug.Log("VROID_QA_CHECK "+detail);}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(3);game=FindFirstObjectByType<CampusGame>();game.paused=false;
        var selector=game.player.GetComponent<PlayerCharacterSelector>();
        foreach(var choice in new[]{PlayerCharacterChoice.Male,PlayerCharacterChoice.Female})
        {
            var location=game.player.position;Check(selector.ApplyChoice(choice,false),"Select "+choice);yield return new WaitForSecondsRealtime(.5f);
            Check(Vector3.Distance(game.player.position,location)<.03f,"Selection preserves gameplay root");
            var visual=game.player.Find("VRoid · "+choice);Check(visual,"VRoid instance active");
            var animator=visual.GetComponent<Animator>();Check(animator&&animator.isHuman&&animator.avatar.isValid,"Valid humanoid retargeting");
            var meshes=visual.GetComponentsInChildren<SkinnedMeshRenderer>();Check(meshes.Length>=2,"Complete body face and hair meshes");
            Check(meshes.SelectMany(m=>m.sharedMaterials).All(m=>m&&m.shader.name=="Campus/VRoid URP"&&m.shader.isSupported&&m.GetTexture("_BaseMap")),"Every material has supported shader and texture");
            Check(meshes.Any(m=>m.sharedMesh.blendShapeCount>10),"Facial expressions retained");
            Check(visual.GetComponent<VroidAvatarExpression>(),"Blink driver attached");
            game.enabled=false;
            selector.SetMotion(0,0,true);yield return new WaitForSecondsRealtime(.35f);
            var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var footBefore=foot.position;
            for(float t=0;t<.6f;t+=Time.unscaledDeltaTime){selector.SetMotion(6,6,true);yield return null;}
            Check(Vector3.Distance(footBefore,foot.position)>.01f,"Locomotion changes leg pose");
            for(float t=0;t<.6f;t+=Time.unscaledDeltaTime){selector.SetMotion(0,0,true);yield return null;}
            game.player.rotation=Quaternion.identity;
            var cam=game.followCamera;cam.transform.position=game.player.position+new Vector3(0,1.25f,3.2f);cam.transform.LookAt(game.player.position+Vector3.up*.95f);cam.fieldOfView=43;
            DormitoryPreview.SaveCamera(cam,folder+"/"+choice+"-front.png");
            cam.transform.position=game.player.position+new Vector3(2.7f,1.65f,-2.6f);cam.transform.LookAt(game.player.position+Vector3.up*1.0f);
            DormitoryPreview.SaveCamera(cam,folder+"/"+choice+"-back.png");game.enabled=true;
        }
        Check(game.guide.GetComponentsInChildren<VroidAvatarExpression>().Length==1,"Guide uses VRoid too");
        finished=true;File.WriteAllText(folder+"/result.txt","PASS: both VRoid selections, complete textured meshes, supported URP materials, valid humanoid locomotion, retained facial expressions and blink drivers, unchanged gameplay roots; guide replacement; four rendered views.");
        Debug.Log("VROID_PRESENTATION_QA_PASS");Application.Quit(0);
    }
}
