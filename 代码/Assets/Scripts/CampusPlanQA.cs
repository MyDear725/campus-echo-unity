using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed class CampusPlanQA:MonoBehaviour
{
    CampusGame game;string folder;float vertical;
    IEnumerator Start(){
        var args=Environment.GetCommandLineArgs();game=GetComponent<CampusGame>();if(args.Contains("-bridgeArrivalPreview")){yield return null;game.SetTravelPose(game.MapToWorld(new Vector3(87,.35f,10)),90,12);yield break;}if(args.Contains("-roadLandscapePreview")){yield return null;game.SetTravelPose(game.MapToWorld(new Vector3(-4.8f,.35f,196)),180,10);yield break;}if(args.Contains("-campusPlanPreview")){yield return null;game.SetTravelPose(game.MapToWorld(new Vector3(208,CampusPlanTerrain.Height(208,10)+.1f,10)),270,10);yield break;}
        if(!args.Contains("-campusPlanQA"))yield break;folder=Path.Combine(Application.dataPath,"../QA/CampusPlan");Directory.CreateDirectory(folder);yield return new WaitForSeconds(1);
        game.paused=false;
        var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Run());
        while(stack.Count>0){object next=null;bool more=false;try{more=stack.Peek().MoveNext();if(more)next=stack.Peek().Current;}catch(Exception e){File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL: "+e);Debug.LogException(e);Application.Quit(1);yield break;}if(!more){stack.Pop();continue;}if(next is IEnumerator routine)stack.Push(routine);else yield return next;}
    }
    IEnumerator Walk(Vector3 mapTarget){var target=game.MapToWorld(mapTarget);for(int i=0;i<2200;i++){var delta=target-game.player.position;delta.y=0;if(delta.magnitude<.12f)yield break;var move=Vector3.ClampMagnitude(delta,.24f);game.Outdoor.Move(move,.04f,ref vertical,false);if(i%24==0)yield return null;}throw new Exception("Outdoor route blocked toward "+mapTarget+" at "+game.WorldToMap(game.player.position));}
    void Pose(float x,float z){vertical=0;game.SetTravelPose(game.MapToWorld(new Vector3(x,CampusPlanTerrain.Height(x,z)+.12f,z)),180,12);}
    IEnumerator Shot(string name,Vector3 position,Vector3 target){yield return new WaitForEndOfFrame();var camera=game.followCamera;camera.transform.position=game.MapToWorld(position);camera.transform.LookAt(game.MapToWorld(target));var rt=new RenderTexture(1600,1000,24){antiAliasing=4};camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(folder,name+".png"),tex.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=old;Destroy(rt);Destroy(tex);}
    IEnumerator Run(){
        foreach(var e in game.Interiors.entrances)if(!game.IsMapWalkable(e.returnMapPosition))throw new Exception("Blocked exterior return: "+e.EntranceId);
        Pose(209,40);yield return Walk(new Vector3(209,0,-68));if(game.player.position.y>1)throw new Exception("North LG road did not descend from south 1F");
        foreach(var road in CampusPlanRoadNetwork.Create()){
            Pose(road.points[0].x,road.points[0].z);foreach(var p in road.points)yield return Walk(new Vector3(p.x,0,p.z));Debug.Log("PLAN_ROAD_PASS: "+road.name);
        }
        // Repaired living bends, south-ring join, and B/C door spurs use actual character collision.
        foreach(var road in JsonUtility.FromJson<CampusData>(game.mapData.text).roads.Where(r=>r.name.Contains("入户步道")||r.name.Contains("入口步道")||r.name=="生活区东环路"||r.name=="生活区西侧坡道主路")){
            Pose(road.points[0].x,road.points[0].z);foreach(var p in road.points)yield return Walk(new Vector3(p.x,0,p.z));
            Debug.Log("ROAD_JOIN_WALK_PASS: "+road.name);
        }
        // One uninterrupted outdoor tour through all eight landscape nodes, with no teleports between them.
        Pose(-4.8f,203);
        var tour=new[]{new Vector2(-4.8f,180),new Vector2(-37,163),new Vector2(-28,127),new Vector2(-12,109),new Vector2(14,99),new Vector2(30,99),new Vector2(42,92),new Vector2(30,99),new Vector2(14,99),new Vector2(22,73),new Vector2(-6,62),new Vector2(-35,63),new Vector2(-49,60),new Vector2(-60,50),new Vector2(-68,34),new Vector2(-77,23),new Vector2(-75,10),new Vector2(-63,-3),new Vector2(-50,-3),new Vector2(-85,0),new Vector2(-119,0),new Vector2(-113,0),new Vector2(-119,5.4f),new Vector2(-180,5.4f),new Vector2(-119,5.4f),new Vector2(-113,0),new Vector2(-85,0),new Vector2(-50,-3),new Vector2(-1,-3),new Vector2(35,-12),new Vector2(68,-29),new Vector2(74,-47),new Vector2(74,-66),new Vector2(74,-84),new Vector2(74,-66),new Vector2(74,-47),new Vector2(68,-29),new Vector2(55,-23),new Vector2(46,-14),new Vector2(38,-2),new Vector2(35,10),new Vector2(65,10),new Vector2(87,10),new Vector2(108,10),new Vector2(192,10),new Vector2(209,10),new Vector2(209,-62),new Vector2(209,-119),new Vector2(251.5f,-119),new Vector2(251.5f,-103),new Vector2(251.5f,-69),new Vector2(209,-69),new Vector2(209,10),new Vector2(218,10)};
        foreach(var p in tour)yield return Walk(new Vector3(p.x,0,p.y));
        Debug.Log("ROAD_LANDSCAPE_CONTINUOUS_TOUR_PASS: entry, library, lake, sport, teaching, covered bridge, residence, cafeteria");
        // Begin in the residence zone, cross the physical bridge, then follow each of the three branches.
        Pose(218,10);yield return Walk(new Vector3(89,0,10));yield return Walk(new Vector3(35,0,10));
        var branchRoads=CampusPlanRoadNetwork.Create().Where(r=>r.name.Contains("方向步道")||r.name=="两湖直行步道");
        foreach(var road in branchRoads){
            foreach(var point in road.points)yield return Walk(new Vector3(point.x,0,point.z));
            if(road.name=="图书馆正门方向步道"){
                var door=game.Interiors.entrances.First(e=>e.buildingId=="library"&&e.mapFeatureId==77917&&e.EntranceId!="library-rear");
                if(Vector3.Distance(game.player.position,game.MapToWorld(door.approachMapPosition))>door.interactionRadius)throw new Exception("Library branch misses main-door interaction");
            }
            foreach(var point in road.points.Reverse())yield return Walk(new Vector3(point.x,0,point.z));
            Debug.Log("BRIDGE_ARRIVAL_BRANCH_PASS: "+road.name);
        }
        yield return Shot("arrival-01-avenue",new Vector3(86,2.1f,10),new Vector3(35,1.6f,10));
        yield return Shot("arrival-02-junction",new Vector3(48,6,12),new Vector3(30,0.6f,10));
        yield return Shot("arrival-03-layout",new Vector3(108,100,35),new Vector3(25,0,20));
        yield return Shot("arrival-04-library",new Vector3(24,2.1f,74),new Vector3(46,5,92));
        yield return Shot("arrival-05-sign",new Vector3(47,2,21.5f),new Vector3(42,1.8f,21.5f));
        Pose(89,10);yield return Walk(new Vector3(108,6.5f,10));if(game.player.position.y<6.3f)throw new Exception("Straight west bridge stair failed");
        yield return Walk(new Vector3(192,6.5f,10));if(game.player.position.y<6.3f)throw new Exception("Bridge deck failed");
        yield return Walk(new Vector3(218,3.08f,10));if(Mathf.Abs(game.player.position.y-3.08f)>.2f)throw new Exception("Straight east flight did not land at cafeteria");
        yield return Shot("07-bridge-to-cafeteria",new Vector3(205,5.2f,10),new Vector3(225,6,10));
        yield return Walk(new Vector3(89,.23f,10));if(game.player.position.y>1)throw new Exception("Return straight flights failed");
        Pose(145,25);yield return Walk(new Vector3(145,0,-5));if(game.player.position.y>1)throw new Exception("Road under bridge blocked");
        Pose(209,-62);yield return Walk(new Vector3(252.5f,.23f,-62));if(game.player.position.y>1)throw new Exception("North LG approach is not low");
        Pose(209,-22);yield return Walk(new Vector3(252.5f,3.08f,-22));if(game.player.position.y<3)throw new Exception("South 1F approach is not high");
        Pose(252.5f,-62);yield return Walk(new Vector3(252.5f,0,-58.5f));var travel=game.Interiors;var north=travel.entrances.First(e=>e.EntranceId=="dormitory-7-north");
        if(!travel.TryEnter(north))throw new Exception("North LG enter failed");while(travel.Busy)yield return null;if(!travel.Inside)throw new Exception(travel.LastError);
        if(game.player.position.y>1)throw new Exception("North entrance did not arrive at LG");
        for(int i=0;i<700&&game.player.position.z<10;i++){game.MoveIndoor(new Vector3(0,0,.12f),.022f);if(i%15==0)yield return null;}
        if(game.player.position.y<2.7f)throw new Exception("LG to south 1F physical stairs failed");if(!travel.TryExit())throw new Exception("South exit failed");while(travel.Busy)yield return null;
        if(game.player.position.y<3||game.WorldToMap(game.player.position).z< -28)throw new Exception("South exit returned to wrong door");
        var south=travel.entrances.First(e=>e.EntranceId=="dormitory-7-south");game.SetTravelPose(game.MapToWorld(south.approachMapPosition),180,12);
        if(!travel.TryEnter(south))throw new Exception("South 1F enter failed");while(travel.Busy)yield return null;
        for(int i=0;i<700&&game.player.position.z> -10;i++){game.MoveIndoor(new Vector3(0,0,-.12f),.022f);if(i%15==0)yield return null;}
        if(game.player.position.y> .25f)throw new Exception("South 1F to north LG stairs failed");if(!travel.TryExit())throw new Exception("North LG exit failed");while(travel.Busy)yield return null;
        if(game.player.position.y>1||game.WorldToMap(game.player.position).z> -58)throw new Exception("North LG returned to wrong exterior door");
        yield return Shot("01-living-plan",new Vector3(340,160,95),new Vector3(245,0,-35));yield return Shot("02-covered-bridge",new Vector3(163,23,55),new Vector3(159,6,10));yield return Shot("03-dorm7-south",new Vector3(274,12,-13),new Vector3(252,8,-35));yield return Shot("04-dorm7-north",new Vector3(249,13,-70),new Vector3(252,6,-55));
        yield return Shot("05-west-campus-roads",new Vector3(-30,330,50),new Vector3(-30,0,30));
        yield return Shot("06-curved-teaching-B",new Vector3(-10,65,-27),new Vector3(57,8,-85));
        yield return Shot("landscape-01-entry",new Vector3(-5,2.05f,199),new Vector3(-5,2,175));
        yield return Shot("landscape-02-teaching",new Vector3(74,2.05f,-48),new Vector3(70,3.2f,-91));
        yield return Shot("landscape-03-library",new Vector3(8,2.05f,106),new Vector3(50,5.6f,92));
        yield return Shot("landscape-04-lake",new Vector3(-76,2.05f,27),new Vector3(-67,2.2f,42));
        yield return Shot("landscape-05-bridge",new Vector3(88,2.05f,10),new Vector3(115,6.8f,10));
        yield return Shot("landscape-06-residence",new Vector3(209,2.05f,-115),new Vector3(209,2.7f,-72));
        yield return Shot("landscape-07-cafeteria",new Vector3(206,4.9f,10),new Vector3(230,7.5f,10));
        yield return Shot("landscape-08-sport",new Vector3(-181,2.05f,5),new Vector3(-139,2.2f,5));
        yield return Shot("repair-bridge-deck",new Vector3(110,8.3f,10),new Vector3(145,9.1f,10));
        yield return Shot("repair-residence-corner",new Vector3(216,10,86),new Vector3(209,3.1f,73));
        yield return Shot("repair-teaching-junction",new Vector3(79,8,-73),new Vector3(71,.3f,-84));
        yield return Shot("repair-lake-junction",new Vector3(-52,10,6),new Vector3(-50,.3f,-3));
        yield return Shot("repair-lake-junction-offset",new Vector3(-51.5f,10,6),new Vector3(-49.5f,.3f,-3));
        Pose(74,-48);for(int i=0;i<30;i++)yield return null;
        var timings=new System.Collections.Generic.List<float>();for(int i=0;i<120;i++){yield return null;timings.Add(Time.unscaledDeltaTime*1000);}
        timings.Sort();File.WriteAllText(Path.Combine(folder,"performance-sample.txt"),"Teaching walkway, 120 frames after warmup, 60 FPS cap. Not a full-campus benchmark.\nMean: "+timings.Average().ToString("F2")+" ms; p95: "+timings[114].ToString("F2")+" ms\nResolution: "+Screen.width+" x "+Screen.height+"\nGPU: "+SystemInfo.graphicsDeviceName+"\n");
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: bridge arrival avenue and all three destination branches including library main-door interaction; 24 road routes and uninterrupted eight-node landscape tour; north-low south-high grade; straight covered bridge flights both directions, aligned cafeteria front; dorm north LG to south 1F and reverse, correct exterior exits.\n");Debug.Log("CAMPUS_PLAN_QA_PASS");Application.Quit(0);
    }
}
