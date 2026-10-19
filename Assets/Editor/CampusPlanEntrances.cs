using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static partial class CampusPlanBuilder
{
    const string SeventhScene=Folder+"/Dormitory7.unity";
    static void UpdateEntrances(){
        var travel=game.GetComponent<BuildingInteriorTravel>();
        foreach(var e in travel.entrances.Where(e=>e&&Sites.ContainsKey(e.mapFeatureId))){
            var d=Doors[e.mapFeatureId];var rect=Sites[e.mapFeatureId];var normal=d.x==rect.xMax?Vector2.right:d.x==rect.xMin?Vector2.left:d.y==rect.yMin?Vector2.down:Vector2.up;var p=d+normal*2.2f;var back=d+normal*3.8f;
            e.approachMapPosition=V(p.x,H(p.x,p.y)+.06f,p.y);e.returnMapPosition=V(back.x,H(back.x,back.y)+.06f,back.y);e.enterYaw=normal==Vector2.right?90:normal==Vector2.left?270:normal==Vector2.down?0:180;
            if(e.mapFeatureId==77943){e.approachMapPosition=V(222.8f,H(222.8f,10)+.65f,10);e.returnMapPosition=V(218,H(218,10)+.06f,10);}
            if(e.mapFeatureId==77935)e.displayName="第三学生公寓";EditorUtility.SetDirty(e);
        }
        BuildingEntranceDefinition Entry(string id,string title,string spawn,float z,float y,float yaw){var e=ScriptableObject.CreateInstance<BuildingEntranceDefinition>();e.buildingId="dormitory-7";e.entranceId=id;e.displayName=title;e.category=CampusBuildingCategory.Dormitory;e.mapFeatureId=2304982;e.approachMapPosition=V(252.5f,y,z);e.returnMapPosition=V(252.5f,y,z+(yaw==180?1.5f:-1.5f));e.enterYaw=yaw;e.interactionRadius=2.4f;e.spawnId=spawn;e.interiorScenePath=SeventhScene;return Save(e,Folder+"/Entrances/"+id+".asset");}
        var south=Entry("dormitory-7-south","七舍 · 1F 南门","upper-south",-27,3.16f,180);var north=Entry("dormitory-7-north","七舍 · LG 北门","LG",-58.5f,.3f,0);
        travel.entrances=travel.entrances.Where(e=>e&&e.buildingId!="dormitory-7").Concat(new[]{south,north}).ToArray();EditorUtility.SetDirty(travel);
        var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==SeventhScene))scenes.Add(new EditorBuildSettingsScene(SeventhScene,true));EditorBuildSettings.scenes=scenes.ToArray();
    }
    static void BuildSeventhHall(){
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var r=new GameObject("七舍 · 北门LG与南门1F交通厅");var contract=r.AddComponent<InteriorSceneRoot>();contract.buildingId="dormitory-7";contract.isPlaceholder=false;contract.showExitMarkers=false;contract.backgroundColor=new Color(.6f,.7f,.75f);
        var stone=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Dorm7 warm limestone"};stone.SetColor("_BaseColor",new Color(.72f,.72f,.67f));stone.SetFloat("_Smoothness",.25f);stone=Save(stone,Folder+"/Materials/HallStone.mat");
        var metal=new Material(stone){name="Dorm7 rail"};metal.SetColor("_BaseColor",new Color(.2f,.27f,.3f));metal=Save(metal,Folder+"/Materials/HallMetal.mat");
        var wood=new Material(stone){name="Dorm7 oak"};wood.SetColor("_BaseColor",new Color(.65f,.49f,.32f));wood=Save(wood,Folder+"/Materials/HallWood.mat");
        void Cube(string n,Vector3 p,Vector3 size,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(r.transform,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;}
        void Text(string value,Vector3 p,float yaw){var g=new GameObject(value);g.transform.SetParent(r.transform,false);g.transform.localPosition=p;g.transform.localRotation=Quaternion.Euler(0,yaw,0);var t=g.AddComponent<TextMesh>();t.text=value;t.fontSize=96;t.characterSize=.022f;t.anchor=TextAnchor.MiddleCenter;t.color=new Color(.17f,.23f,.26f);g.AddComponent<CafeteriaSign>().surface=AssetDatabase.LoadAssetAtPath<Material>("Assets/CafeteriaRevision/Materials/Wall lettering.mat");}
        Cube("LG stone floor",V(0,-.13f,0),V(16,.26f,24),stone);Cube("South 1F landing",V(0,2.72f,-8),V(16,.26f,8),stone);
        Cube("Roof",V(0,6.2f,0),V(16,.24f,24),stone);
        foreach(float x in new[]{-8f,8f})Cube("Side wall",V(x,3,0),V(.22f,6,24),stone);
        foreach(float z in new[]{-12f,12f}){foreach(float x in new[]{-5.2f,5.2f})Cube("Door surround",V(x,3,z),V(5.6f,6,.22f),stone);Cube("Door lintel",V(0,z<0?5.8f:4.5f,z),V(4.8f,z<0?.8f:3,.22f),stone);}
        for(int i=0;i<18;i++){float top=(i+1)*2.85f/18;Cube("Physical stair "+(i+1),V(0,top/2,3-(i*.4f)),V(3.6f,top,.41f),stone);Cube("Tread nosing",V(0,top+.005f,3.16f-i*.4f),V(3.56f,.012f,.035f),metal);}
        foreach(float x in new[]{-1.9f,1.9f}){for(int i=0;i<=18;i+=2)Cube("Baluster",V(x,i*2.85f/18+.55f,3.2f-i*.4f),V(.045f,1.1f,.045f),metal);var a=V(x,1.1f,3.4f);var b=V(x,3.95f,-4.1f);var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Continuous handrail";g.transform.SetParent(r.transform,false);g.transform.position=(a+b)/2;g.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);g.transform.localScale=V(.07f,(a-b).magnitude,.07f);g.GetComponent<Renderer>().sharedMaterial=wood;}
        foreach(float x in new[]{-5.1f,5.1f}){Cube("Upper guardrail",V(x,3.9f,-4),V(5.8f,.07f,.07f),metal);for(float xx=x-2.7f;xx<x+2.8f;xx+=.55f)Cube("Upper baluster",V(xx,3.4f,-4),V(.045f,1.1f,.045f),metal);}
        Text("7 舍\nLG 北门层",V(-5,2.1f,11.8f),180);Text("1F 南门  ↑",V(0,5.15f,-11.8f),0);Text("北门 / LG  ↓",V(5,4.7f,-5),180);
        foreach(float z in new[]{-9f,-3f,4f,9f}){var g=new GameObject("Hall light");g.transform.SetParent(r.transform,false);g.transform.position=V(0,5.8f,z);var l=g.AddComponent<Light>();l.type=LightType.Point;l.range=15;l.intensity=2.8f;l.color=new Color(1,.91f,.76f);Cube("Ceiling light",V(0,6.04f,z),V(3,.04f,.15f),wood);}
        Transform Anchor(string name,Vector3 p,float yaw){var t=new GameObject(name).transform;t.SetParent(r.transform,false);t.localPosition=p;t.localRotation=Quaternion.Euler(0,yaw,0);return t;}
        var n=Anchor("LG north exit",V(0,.06f,10.5f),180);var s=Anchor("1F south exit",V(0,2.91f,-10.5f),0);
        contract.exits=new[]{s,n};contract.exteriorExits=new[]{new InteriorExteriorExit{anchor=s,entranceId="dormitory-7-south"},new InteriorExteriorExit{anchor=n,entranceId="dormitory-7-north"}};
        contract.spawnPoints=new[]{new InteriorSpawnPoint{id="LG",anchor=Anchor("LG arrival",V(0,.06f,7),180)},new InteriorSpawnPoint{id="upper-south",anchor=Anchor("South arrival",V(0,2.91f,-7),0)}};
        // Rotate the whole physical hall: low entrance is north, upper landing is south.
        r.transform.rotation=Quaternion.Euler(0,180,0);
        EditorSceneManager.SaveScene(scene,SeventhScene);EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
    }
}
