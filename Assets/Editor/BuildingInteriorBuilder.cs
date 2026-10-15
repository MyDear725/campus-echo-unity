using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BuildingInteriorBuilder
{
    const string Folder="Assets/Interiors";
    class Spec {public int feature;public string id,label;public CampusBuildingCategory category;public Vector2? anchor;public bool dorm;}
    static readonly Spec[] Specs={
        new Spec{feature=77917,id="library",label="图书馆",category=CampusBuildingCategory.Library,anchor=new Vector2(52,104)},
        new Spec{feature=77923,id="gymnasium",label="体育馆",category=CampusBuildingCategory.Gymnasium},
        new Spec{feature=77943,id="cafeteria",label="第六食堂",category=CampusBuildingCategory.Cafeteria},
        new Spec{feature=77925,id="teaching-a",label="教学楼 A",category=CampusBuildingCategory.Teaching,anchor=new Vector2(58.3f,-105.4f)},
        new Spec{feature=77928,id="teaching-b",label="教学楼 B",category=CampusBuildingCategory.Teaching,anchor=new Vector2(47,-84)},
        new Spec{feature=77927,id="teaching-c",label="教学楼 C",category=CampusBuildingCategory.Teaching,anchor=new Vector2(75,-91.5f)},
        new Spec{feature=77931,id="dormitory-1",label="第一学生公寓",category=CampusBuildingCategory.Dormitory,anchor=new Vector2(271,-124),dorm=true},
        new Spec{feature=77933,id="dormitory-2",label="第二学生公寓",category=CampusBuildingCategory.Dormitory,anchor=new Vector2(232,-124),dorm=true},
        new Spec{feature=77935,id="dormitory-west",label="西侧学生公寓",category=CampusBuildingCategory.Dormitory,anchor=new Vector2(219,-103),dorm=true},
        new Spec{feature=77937,id="dormitory-4",label="第四学生公寓",category=CampusBuildingCategory.Dormitory,anchor=new Vector2(219,-75.5f),dorm=true},
        new Spec{feature=77938,id="dormitory-5",label="第五学生公寓",category=CampusBuildingCategory.Dormitory,anchor=new Vector2(271,-95),dorm=true},
        new Spec{feature=77941,id="dormitory-6",label="第六学生宿舍",category=CampusBuildingCategory.Dormitory,anchor=new Vector2(187,59),dorm=true}
    };
    struct Edge {public Vector3 center,normal;public MapPoint a,b;public float length;}
    [MenuItem("Campus/Interiors/Install Entrances and Missing Placeholders")]
    public static void Install() {
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var campus=SceneManager.GetActiveScene();var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();
        var data=JsonUtility.FromJson<CampusData>(game.mapData.text);
        Directory.CreateDirectory(Folder+"/Entrances");Directory.CreateDirectory(Folder+"/Scenes");Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
        var entries=new List<BuildingEntranceDefinition>();
        foreach(var spec in Specs) {
            string path=Folder+"/Entrances/"+spec.id+".asset";
            var entry=AssetDatabase.LoadAssetAtPath<BuildingEntranceDefinition>(path);
            if(!entry) {
                entry=ScriptableObject.CreateInstance<BuildingEntranceDefinition>();entry.name=spec.label;entry.buildingId=spec.id;entry.displayName=spec.label;entry.category=spec.category;entry.mapFeatureId=spec.feature;
                var feature=data.features.Single(f=>f.id==spec.feature);var edge=FindEdge(feature,spec);
                Vector3 center=spec.id=="cafeteria"?new Vector3(183,.23f,8):edge.center;center.y=.23f;
                Vector3 normal=spec.id=="cafeteria"?Vector3.left:edge.normal;
                // Stay in front of the facade and of the raised cafeteria steps.
                entry.approachMapPosition=Reachable(center,normal,data,3,5.5f);
                entry.returnMapPosition=Reachable(center,normal,data,4.4f,7);
                var facing=game.MapToWorld(-normal);entry.enterYaw=Mathf.Atan2(facing.x,facing.z)*Mathf.Rad2Deg;
                entry.interiorScenePath=Folder+"/Scenes/"+spec.id+".unity";entry.spawnId="main";
                AssetDatabase.CreateAsset(entry,path);
            }
            entries.Add(entry);
            // Never overwrite a teammate's scene or edited entrance configuration.
            if(!File.Exists(entry.interiorScenePath))CreatePlaceholder(entry);
        }
        SceneManager.SetActiveScene(campus);
        EnsureLibraryDoor(game,data);
        var travel=game.GetComponent<BuildingInteriorTravel>()??game.gameObject.AddComponent<BuildingInteriorTravel>();
        travel.entrances=entries.Concat(travel.entrances.Where(e=>e&&!entries.Contains(e))).ToArray();
        if(!game.GetComponent<BuildingInteriorQA>())game.gameObject.AddComponent<BuildingInteriorQA>();
        var build=new List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(campus.path,true)};
        build.AddRange(EditorBuildSettings.scenes.Where(s=>s.path!=campus.path));
        foreach(var entry in travel.entrances) {
            var existing=build.FirstOrDefault(s=>s.path==entry.interiorScenePath);
            if(existing!=null)existing.enabled=true;else build.Add(new EditorBuildSettingsScene(entry.interiorScenePath,true));
        }
        EditorBuildSettings.scenes=build.ToArray();EditorUtility.SetDirty(travel);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(campus);EditorSceneManager.SaveScene(campus);
        ValidateConfiguration();Debug.Log("BUILDING_INTERIORS_INSTALLED: "+travel.entrances.Length+" entrances");
    }
    public static void InstallAndBuild(){Install();CampusBuilder.Build();}
    static void EnsureLibraryDoor(CampusGame game,CampusData data) {
        // The old south portal opens into the information-building footprint.
        // Provide a visible, reachable plaza-side portal for the new interaction.
        if(GameObject.Find("图书馆 · 广场侧交互入口")||GameObject.Find("图书馆 · 石材与通高玻璃大厅"))return;
        var edge=FindEdge(data.features.Single(f=>f.id==77917),Specs.First(s=>s.id=="library"));
        var root=new GameObject("图书馆 · 广场侧交互入口").transform;root.position=game.MapToWorld(edge.center);root.rotation=Quaternion.LookRotation(game.MapToWorld(edge.normal));
        var frame=PlaceholderMaterial("Door metal",new Color(.22f,.28f,.3f));var glass=PlaceholderMaterial("Door glass",new Color(.25f,.4f,.46f));glass.SetFloat("_Smoothness",.8f);glass.SetFloat("_Metallic",.5f);EditorUtility.SetDirty(glass);
        void Part(string name,Vector3 position,Vector3 size,Material material){var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root,false);obj.transform.localPosition=position;obj.transform.localScale=size;obj.GetComponent<MeshRenderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());}
        Part("Recess",new Vector3(0,1.5f,.22f),new Vector3(4.2f,3,.18f),frame);
        foreach(float x in new[]{-1f,1f})Part("Door leaf",new Vector3(x,1.45f,.35f),new Vector3(1.85f,2.8f,.08f),glass);
        foreach(float x in new[]{-2.05f,0,2.05f})Part("Door frame",new Vector3(x,1.5f,.43f),new Vector3(.09f,3,.12f),frame);
        foreach(float x in new[]{-.22f,.22f})Part("Handle",new Vector3(x,1.35f,.53f),new Vector3(.04f,.6f,.06f),frame);
        Part("Canopy",new Vector3(0,3.25f,.85f),new Vector3(5.2f,.17f,1.9f),frame);
    }
    [MenuItem("Campus/Interiors/Validate Configuration")]
    public static void ValidateConfiguration() {
        var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();if(!game)throw new Exception("Open CampusLiving before validating.");
        var data=JsonUtility.FromJson<CampusData>(game.mapData.text);var travel=game.GetComponent<BuildingInteriorTravel>();
        if(!travel||travel.entrances.Length<12)throw new Exception("Expected twelve building entries.");
        var ids=new HashSet<string>();var report=new List<string>();
        foreach(var e in travel.entrances) {
            if(!e||string.IsNullOrEmpty(e.buildingId)||!ids.Add(e.EntranceId))throw new Exception("Null building ID or duplicate doorway ID");
            if(!File.Exists(e.interiorScenePath)||!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==e.interiorScenePath))throw new Exception("Interior is not included in build: "+e.buildingId);
            foreach(var p in new[]{e.approachMapPosition,e.returnMapPosition})if(CampusCollision.Blocked(p.x,p.z,data.obstacles))throw new Exception("Blocked exterior doorway: "+e.buildingId+" "+p);
            var arrived=CampusCollision.Move(e.returnMapPosition,e.approachMapPosition-e.returnMapPosition,data.obstacles);
            if(Vector3.Distance(arrived,e.approachMapPosition)>.05f)throw new Exception("Door approach is obstructed: "+e.buildingId);
            report.Add(e.EntranceId+" | "+e.displayName+" | approach "+e.approachMapPosition+" | "+e.interiorScenePath);
        }
        Directory.CreateDirectory("QA");File.WriteAllLines("QA/entrance-configuration.txt",new[]{"PASS: all exterior door approaches, return points, unique IDs and build entries."}.Concat(report));
    }
    static Edge FindEdge(CampusFeature f,Spec s) {
        var edges=new List<Edge>();for(int i=0;i<f.points.Length;i++) {
            var a=f.points[i];var b=f.points[(i+1)%f.points.Length];var delta=new Vector3(b.x-a.x,0,b.z-a.z);float length=delta.magnitude;
            if(length<=(s.anchor.HasValue?6:8))continue;
            var mid=new Vector3((a.x+b.x)/2,0,(a.z+b.z)/2);var normal=new Vector3(-delta.z,0,delta.x).normalized;
            var probe=mid+normal*.08f;if(CampusCollision.Inside(probe.x,probe.z,f.points))normal=-normal;
            edges.Add(new Edge{center=mid,normal=normal,a=a,b=b,length=length});
        }
        if(s.anchor.HasValue){var p=s.anchor.Value;return edges.OrderBy(e=>s.dorm?Vector2.Distance(p,new Vector2(e.center.x,e.center.z)):CampusCollision.Segment(p.x,p.y,e.a,e.b)).First();}
        return edges.OrderByDescending(e=>(e.normal.z*.75f-e.normal.x*.25f)*15+e.length*.15f).First();
    }
    static Vector3 Reachable(Vector3 center,Vector3 normal,CampusData data,float start,float limit) {
        for(float distance=start;distance<=limit;distance+=.2f){var p=center+normal*distance;if(!CampusCollision.Blocked(p.x,p.z,data.obstacles))return p;}
        throw new Exception("No reachable doorway beside "+center);
    }
    static Material PlaceholderMaterial(string name,Color color) {
        string path=Folder+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat)return mat;
        mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.05f);AssetDatabase.CreateAsset(mat,path);return mat;
    }
    static void CreatePlaceholder(BuildingEntranceDefinition entry) {
        var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        var root=new GameObject("InteriorRoot · "+entry.displayName).AddComponent<InteriorSceneRoot>();root.buildingId=entry.buildingId;
        var spawn=new GameObject("Spawn_main").transform;spawn.SetParent(root.transform);spawn.position=new Vector3(0,.06f,-4.8f);
        var exit=new GameObject("Exit_toCampus").transform;exit.SetParent(root.transform);exit.position=new Vector3(0,.06f,-8.5f);exit.rotation=Quaternion.Euler(0,180,0);
        root.spawnPoints=new[]{new InteriorSpawnPoint{id="main",anchor=spawn}};root.exits=new[]{exit};root.isPlaceholder=true;
        var shell=new GameObject("PlaceholderShell · 可替换的空房间").transform;shell.SetParent(root.transform);
        var floor=PlaceholderMaterial("Neutral floor",new Color(.42f,.45f,.48f));var wall=PlaceholderMaterial("Neutral wall",new Color(.67f,.69f,.7f));var accent=PlaceholderMaterial("Exit marker",new Color(.16f,.42f,.42f));
        void Box(string name,Vector3 at,Vector3 size,Material mat){var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(shell,false);obj.transform.position=at;obj.transform.localScale=size;obj.GetComponent<MeshRenderer>().sharedMaterial=mat;}
        Box("Floor (Collider)",new Vector3(0,-.1f,0),new Vector3(20,.2f,20),floor);
        Box("West wall",new Vector3(-10,1.75f,0),new Vector3(.2f,3.5f,20),wall);Box("East wall",new Vector3(10,1.75f,0),new Vector3(.2f,3.5f,20),wall);
        Box("North wall",new Vector3(0,1.75f,10),new Vector3(20,3.5f,.2f),wall);Box("South wall",new Vector3(0,1.75f,-10),new Vector3(20,3.5f,.2f),wall);
        Box("Exit marker panel",new Vector3(0,1.25f,-9.84f),new Vector3(1.8f,2.5f,.08f),accent);
        var light=new GameObject("Placeholder lighting").AddComponent<Light>();light.transform.SetParent(root.transform);light.type=LightType.Directional;light.intensity=.85f;light.transform.rotation=Quaternion.Euler(60,-30,0);light.shadows=LightShadows.Soft;
        RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.67f,.7f);RenderSettings.sun=light;
        if(root.ValidateContract(entry.buildingId,entry.spawnId)!=null)throw new Exception("Invalid generated interior");
        EditorSceneManager.SaveScene(scene,entry.interiorScenePath);EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);
    }
}
