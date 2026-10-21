using UnityEngine;
using System;
using System.Linq;
using System.Collections;

// Serialized map-space centres used by scenery QA and future sports interactions.
public sealed class OutdoorBasketballLandmark : MonoBehaviour
{
    public Vector3[] courtCentres;
    IEnumerator Start()
    {
        if(!Environment.GetCommandLineArgs().Contains("-courtPreview"))yield break;
        yield return null;var game=FindFirstObjectByType<CampusGame>();
        if(game){game.SetTravelPose(game.MapToWorld(new Vector3(135.5f,.36f,-45)),180,10);game.Notify("这里是教学楼旁的室外篮球场，已连接教学区主路。田径场已恢复。按 M 查看地图。");}
    }
}
