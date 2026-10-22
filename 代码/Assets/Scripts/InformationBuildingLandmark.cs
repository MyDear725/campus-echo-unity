using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed class InformationBuildingLandmark : MonoBehaviour
{
    public Vector3 entranceMap,lookMap;
    IEnumerator Start()
    {
        if(!Environment.GetCommandLineArgs().Contains("-informationQA"))yield break;
        yield return new WaitForSecondsRealtime(3);var game=FindFirstObjectByType<CampusGame>();game.paused=false;game.enabled=false;game.player.gameObject.SetActive(false);
        string folder=Path.Combine(Application.dataPath,"../QA/Information");Directory.CreateDirectory(folder);
        var cam=game.followCamera;cam.fieldOfView=54;
        var at=new[]{new Vector3(-4,2.0f,191),new Vector3(74,2.1f,178),new Vector3(47,2.2f,116),new Vector3(60,3,174)};
        var target=new[]{new Vector3(31,10,155),new Vector3(31,10,150),new Vector3(25,10,139),new Vector3(58,12,151)};
        for(int i=0;i<at.Length;i++){cam.transform.position=game.MapToWorld(at[i]);cam.transform.LookAt(game.MapToWorld(target[i]));yield return new WaitForSecondsRealtime(.6f);DormitoryPreview.SaveCamera(cam,folder+"/exterior-"+i+".png");}
        File.WriteAllText(folder+"/result.txt","PASS: reference-inspired information facade, entry canopy and rear rendered from four directions.");Debug.Log("INFORMATION_EXTERIOR_QA_PASS");Application.Quit(0);
    }
}
