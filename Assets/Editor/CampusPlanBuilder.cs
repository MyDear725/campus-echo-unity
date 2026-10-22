using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

public static partial class CampusPlanBuilder
{
    const string Folder="Assets/CampusPlanRevision",Group="校园平面图修订 · 生活区与学苑桥",PhysicsGroup="Campus plan physical surfaces";
    static Transform root,holder,physicsRoot;static CampusGame game;static CampusData data;
    static Dictionary<string,DormitoryBuilder.Geometry> geometry;
    static Dictionary<string,Material> materials;
    static int colliderIndex;
    static readonly Dictionary<int,Rect> Sites=new Dictionary<int,Rect>{
        {77931,new Rect(259,-134,24,10)},{77933,new Rect(220,-134,24,10)},
        {77935,new Rect(214,-112,9,18)},{77937,new Rect(220,-87,24,12)},
        {77938,new Rect(259,-87,24,12)},{2304982,new Rect(225,-55,55,24)},
        {77943,new Rect(225,-8,55,36)},{77941,new Rect(220,55,65,15)}};
    static readonly Dictionary<int,Vector2> Doors=new Dictionary<int,Vector2>{
        {77931,new Vector2(271,-124)},{77933,new Vector2(232,-124)},{77935,new Vector2(223,-103)},
        {77937,new Vector2(232,-75)},{77938,new Vector2(271,-75)},
        {2304982,new Vector2(252.5f,-31)},{77943,new Vector2(225,10)},{77941,new Vector2(252.5f,55)}};
    static MapPoint P(float x,float z)=>new MapPoint{x=x,z=z};
    static MapPoint[] RectPoints(Rect r)=>new[]{P(r.xMin,r.yMin),P(r.xMax,r.yMin),P(r.xMax,r.yMax),P(r.xMin,r.yMax)};
    static float H(float x,float z)=>CampusPlanTerrain.Height(x,z);
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static T Save<T>(T item,string path) where T:UnityEngine.Object {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(item,old);UnityEngine.Object.DestroyImmediate(item);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(item,path);return item;
    }
    public static void GenerateAndBuild(){Generate();CampusBuilder.Build();}
    [MenuItem("Campus/Apply campus plan and covered bridge")]
    public static void Generate(){
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();root=GameObject.Find("开发区校区 · 校园环境").transform;
        ComprehensiveBuildingBuilder.RestoreSources(root);
        foreach(string dir in new[]{Folder,Folder+"/Meshes",Folder+"/Materials",Folder+"/Entrances"})Directory.CreateDirectory(dir);
        if(!File.Exists(Folder+"/SourceMap.json"))File.WriteAllText(Folder+"/SourceMap.json",game.mapData.text);
        data=JsonUtility.FromJson<CampusData>(File.ReadAllText(Folder+"/SourceMap.json"));
        var old=root.Find(Group);if(old){var marker=old.GetComponent<CampusPlanState>();if(marker){foreach(var r in marker.suppressedRenderers)if(r)r.enabled=true;foreach(var c in marker.suppressedColliders)if(c)c.enabled=true;}UnityEngine.Object.DestroyImmediate(old.gameObject);}
        var oldPhysics=GameObject.Find(PhysicsGroup);if(oldPhysics)UnityEngine.Object.DestroyImmediate(oldPhysics);
        // Rebuild the curved B envelope before clipping/replacing landscape batches.
        data.routes=Array.Empty<CampusRoute>();
        TeachingBuilder.Apply(root,data,game,false);
        AssetDatabase.Refresh();holder=new GameObject(Group).transform;holder.SetParent(root,false);physicsRoot=new GameObject(PhysicsGroup).transform;colliderIndex=0;
        geometry=new Dictionary<string,DormitoryBuilder.Geometry>();materials=new Dictionary<string,Material>();
        Mat("Paving","#c1bcb0",1);Mat("Asphalt","#505a5b",0);Mat("Grass","#708252",2);Mat("Stone","#c9c6b9",3);Mat("Wood","#9f805d",4);Mat("Metal","#4a5b60",3);Mat("Leaf","#567341",5);Mat("Flower","#d9bd8b",5);Mat("Paint","#e1d9b9",3);
        Mat("Glass","#a3c4cc",3,true);Mat("Roof","#798b83",3);Mat("Light","#f5dfb0",3);
        InitRoadScenery();ResetGroundSurfaces();
        MoveCafeteria();ReplaceOldLife();UpdateMap();BuildDorms();BuildLandscape();BuildPlanRoads();BuildBridge();BuildOutdoorCollision();BuildRoadScenery();ResolveGroundSurfaces();
        SavePlanGeometry();
        UpdateEntrances();if(!game.GetComponent<CampusOutdoorTraversal>())game.gameObject.AddComponent<CampusOutdoorTraversal>();if(!game.GetComponent<CampusPlanQA>())game.gameObject.AddComponent<CampusPlanQA>();
        ComprehensiveBuildingBuilder.Apply(root,data,game);
        File.WriteAllText(Folder+"/CampusMap.json",JsonUtility.ToJson(data,true));AssetDatabase.ImportAsset(Folder+"/CampusMap.json");game.mapData=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/CampusMap.json");
        ValidateResolvedPlanEntrances();
        EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);AssetDatabase.SaveAssets();
        BuildSeventhHall();AssetDatabase.SaveAssets();Directory.CreateDirectory("QA");File.WriteAllText("QA/campus-plan-generation.txt","Life zone rebuilt from supplied north-up plan. North low/south high grade; dorm 7 north LG and south 1F entrances. Straight covered bridge stairs aligned with west-facing cafeteria entrance. Curved B wing and connected plan-based primary road network. Covered physical bridge at z=10, deck 6.5 m. Existing west-campus architecture and interiors retained. Design elevations, not a survey.\n");
        Debug.Log("CAMPUS_PLAN_GENERATED");
    }
    static void ValidateResolvedPlanEntrances(){
        foreach(var entry in game.GetComponent<BuildingInteriorTravel>().entrances){
            foreach(var p in new[]{entry.approachMapPosition,entry.returnMapPosition})if(CampusCollision.Blocked(p.x,p.z,data.obstacles))throw new Exception("Resolved plan doorway blocked: "+entry.EntranceId);
            var delta=entry.approachMapPosition-entry.returnMapPosition;delta.y=0;
            var reached=CampusCollision.Move(entry.returnMapPosition,delta,data.obstacles);
            if(Vector2.Distance(new Vector2(reached.x,reached.z),new Vector2(entry.approachMapPosition.x,entry.approachMapPosition.z))>.05f)throw new Exception("Resolved plan approach blocked: "+entry.EntranceId);
        }
        Debug.Log("RESOLVED_PLAN_ENTRANCES_PASS: planar geometry validated after relocation; physical elevation checked by runtime QA");
    }
    static void SavePlanGeometry(){
        // Large vegetation/ground batches are split spatially, so distant unseen parts can be culled.
        foreach(var pair in geometry.Where(k=>k.Value.vertices.Count>0)){
            if(pair.Value.vertices.Count<60000){var mesh=Save(pair.Value.Mesh("Plan "+pair.Key),Folder+"/Meshes/"+pair.Key+".asset");MeshObject(pair.Key,mesh,materials[pair.Key],holder);continue;}
            var chunks=new Dictionary<Vector2Int,DormitoryBuilder.Geometry>();var vertices=pair.Value.vertices;
            for(int i=0;i<vertices.Count;i+=3){var c=(vertices[i]+vertices[i+1]+vertices[i+2])/3;var key=new Vector2Int(Mathf.FloorToInt(c.x/48),Mathf.FloorToInt(c.z/48));if(!chunks.TryGetValue(key,out var batch)){batch=new DormitoryBuilder.Geometry();chunks.Add(key,batch);}batch.Triangle(vertices[i],vertices[i+1],vertices[i+2]);}
            foreach(var chunk in chunks){string name=pair.Key+"-chunk-"+chunk.Key.x+"-"+chunk.Key.y;var mesh=Save(chunk.Value.Mesh(name),Folder+"/Meshes/"+name+".asset");MeshObject(name,mesh,materials[pair.Key],holder);}
        }
    }
    public static void FinalizeLandscapeAndBuild(){
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        foreach(var filter in filters){
            var m=filter.sharedMesh;if(!m||!AssetDatabase.GetAssetPath(m).StartsWith(Folder+"/Meshes/DarkBand"))continue;
            var v=m.vertices;bool changed=false;
            for(int i=0;i<v.Length;i++)if(v[i].x>6&&v[i].x<47&&v[i].z>84&&v[i].z<125&&Mathf.Abs(v[i].y-.315f)<.002f){v[i].y=.334f;changed=true;}
            if(changed){m.vertices=v;m.RecalculateBounds();EditorUtility.SetDirty(m);}
        }
        // Remove superseded generated monoliths only when the scene uses their spatial chunks.
        foreach(string name in new[]{"Leaf","ShrubLight","Bark","Grass","Stone","Paving","Metal","Wood","Aggregate","GrassDetail"}){
            string path=Folder+"/Meshes/"+name+".asset";
            bool chunked=filters.Any(f=>f.sharedMesh&&AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(Folder+"/Meshes/"+name+"-chunk-"));
            if(chunked&&!filters.Any(f=>AssetDatabase.GetAssetPath(f.sharedMesh)==path))AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.SaveAssets();CampusBuilder.Build();
    }
    static void Mat(string name,string hex,int surface,bool glass=false){
        ColorUtility.TryParseHtmlString(hex,out var c);var m=new Material(Shader.Find(glass?"Campus/Dormitory":"Campus/Courtyard")){name="Plan "+name};if(glass){c.a=.27f;m.SetFloat("_Glass",1);m.SetFloat("_Smoothness",.8f);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");}else m.SetFloat("_Surface",surface);m.SetColor("_BaseColor",c);m.SetFloat("_Cull",0);materials[name]=Save(m,Folder+"/Materials/"+name+".mat");geometry[name]=new DormitoryBuilder.Geometry();
    }
    static void MeshObject(string name,Mesh mesh,Material mat,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=name=="Glass"?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;}
    static void Box(string mat,Vector3 c,Vector3 size,bool solid=false){geometry[mat].Box(c,Vector3.right,Vector3.up,Vector3.forward,size);if(solid)ColliderBox(mat,c,size);}
    static void Beam(string mat,Vector3 a,Vector3 b,float size,bool solid=false){geometry[mat].Beam(a,b,size);if(solid){var go=new GameObject("Rail "+colliderIndex++){layer=9};go.transform.SetParent(physicsRoot,false);a=game.MapToWorld(a);b=game.MapToWorld(b);go.transform.position=(a+b)/2;go.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);go.AddComponent<BoxCollider>().size=new Vector3(size,(b-a).magnitude,size);}}
    static void ColliderBox(string name,Vector3 c,Vector3 size){var go=new GameObject(name+" "+colliderIndex++){layer=9};go.transform.SetParent(physicsRoot,false);go.transform.position=game.MapToWorld(c);go.AddComponent<BoxCollider>().size=size;}
    static void Label(string text,Vector3 at,float size,float yaw=180){
        var go=new GameObject(text);go.transform.SetParent(holder,false);go.transform.localPosition=at;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=new Vector3(-1,1,1);var t=go.AddComponent<TextMesh>();t.text=text;t.fontSize=96;t.characterSize=size/10;t.anchor=TextAnchor.MiddleCenter;t.color=new Color(.17f,.22f,.24f);go.AddComponent<CafeteriaSign>().surface=AssetDatabase.LoadAssetAtPath<Material>("Assets/CafeteriaRevision/Materials/Wall lettering.mat");
    }
    static void MoveCafeteria(){
        var source=root.Find("第六食堂 · 石材与玻璃立面");if(!source)throw new Exception("Missing cafeteria source");
        var moved=new GameObject("第六食堂 · 新址").transform;moved.SetParent(holder,false);moved.localPosition=new Vector3(225-183*2.75f,2.9f,2);moved.localRotation=Quaternion.identity;moved.localScale=new Vector3(2.75f,1,1);
        foreach(Transform child in source){if(child.name.StartsWith("Preserved"))continue;var copy=UnityEngine.Object.Instantiate(child.gameObject,moved,false);copy.name=child.name;foreach(var r in copy.GetComponentsInChildren<Renderer>())r.enabled=true;}
    }
    static void UpdateMap(){
        var ids=Sites.Keys.ToHashSet();var oldNames=data.features.Where(f=>ids.Contains(f.id)).Select(f=>f.name).ToHashSet();
        data.obstacles=data.obstacles.Where(o=>!oldNames.Contains(o.name)&&!ObstacleInLife(o)).ToArray();
        foreach(var f in data.features.Where(f=>ids.Contains(f.id))){f.points=RectPoints(Sites[f.id]);if(f.id==2304982)f.name="第七学生公寓";if(f.id==77935)f.name="第三学生公寓";}
        data.features=data.features.Where(f=>f.id!=2304919).ToArray();var lifeGate=data.features.FirstOrDefault(f=>f.id==81823);if(lifeGate!=null){lifeGate.kind="gate";lifeGate.name="生活区入口";lifeGate.points=RectPoints(new Rect(200.5f,35,1,8));}
        data.obstacles=data.obstacles.Concat(data.features.Where(f=>ids.Contains(f.id)).Select(f=>new CampusObstacle{name=f.name,points=f.points,minX=f.points.Min(p=>p.x),maxX=f.points.Max(p=>p.x),minZ=f.points.Min(p=>p.z),maxZ=f.points.Max(p=>p.z)})).ToArray();
        var roads=new List<CampusRoad>();foreach(var r in data.roads)for(int i=1;i<r.points.Length;i++){var a=r.points[i-1];var b=r.points[i];if(!CampusPlanTerrain.InLiving((a.x+b.x)/2,(a.z+b.z)/2))roads.Add(new CampusRoad{name=r.name,kind=r.kind,width=r.width,points=new[]{a,b}});}roads.AddRange(NewRoads());var bridgeRoad=Road("学苑桥",5.2f,91.8f,10,105.5f,10,194.5f,10,202.1f,10);bridgeRoad.kind="bridge";roads.Add(bridgeRoad);data.roads=roads.ToArray();
        data.routes=data.routes.Where(r=>!r.points.Any(p=>CampusPlanTerrain.InLiving(p.x,p.z))).Concat(new[]{new CampusRoute{name="living-north-south",points=new[]{P(209,75),P(209,-142)}}}).ToArray();
        foreach(var s in data.spots.Where(s=>CampusPlanTerrain.InLiving(s.x,s.z))){s.x=251.5f;s.z=-101;}
        for(int i=0;i<data.spots.Length&&i<game.memories.Length;i++){var s=data.spots[i];if(CampusPlanTerrain.InLiving(s.x,s.z)&&game.memories[i])game.memories[i].transform.position=game.MapToWorld(V(s.x,H(s.x,s.z)+1.15f,s.z));}
    }
    static bool ObstacleInLife(CampusObstacle o){var p=o.points?.Length>0?o.points:o.segment?.Length>0?o.segment:new[]{P((o.minX+o.maxX)/2,(o.minZ+o.maxZ)/2)};return p.Any(v=>CampusPlanTerrain.InLiving(v.x,v.z));}
    static CampusRoad Road(string name,float width,params float[] p)=>new CampusRoad{name=name,kind=width>=4?"main":"secondary",width=width,points=Enumerable.Range(0,p.Length/2).Select(i=>P(p[i*2],p[i*2+1])).ToArray()};
    static CampusRoad[] NewRoads()=>new[]{
        Road("生活区西侧坡道主路",4,209,78,209,-142),Road("生活区北环路",4,209,-142,294,-142),Road("生活区东环路",4,294,-142,294,78,209,78),
        Road("公寓花园中轴",3,251.5f,-142,251.5f,-62),Road("北庭院横轴",3,223,-103,294,-103),
        Road("一二舍入户步道",3,209,-119,294,-119),Road("四五舍南侧步道",3,209,-69,294,-69),
        Road("二舍入口",3,232,-124,232,-119),Road("一舍入口",3,271,-124,271,-119),Road("四舍入口",3,232,-75,232,-69),Road("五舍入口",3,271,-75,271,-69),
        Road("七舍北门LG引道",3,209,-62,252.5f,-62,252.5f,-55),Road("七舍南门1F入口",3,209,-22,252.5f,-22,252.5f,-31),
        Road("食堂南广场",4,209,39,294,39),Road("食堂西正门广场",4.2f,202.1f,10,225,10),Road("六舍入口",3,252.5f,55,252.5f,39),
        Road("食堂西侧步道",3,219,39,219,-16,209,-16)};
    static void BuildDorms(){
        int index=0;foreach(var pair in Sites.Where(p=>p.Key!=77943)){
            var f=data.features.First(p=>p.id==pair.Key);var h=new GameObject(f.name).transform;h.SetParent(holder,false);float baseY=pair.Key==77941?2.85f:0;h.localPosition=Vector3.up*baseY;
            var synthetic=new CampusFeature{id=900001+index++,name=f.name,kind="building",points=f.points};DormitoryBuilder.BuildMappedDorm(synthetic,h,Doors[pair.Key],Folder+"/Dorms");if(pair.Key==2304982){CutNorthernDoor(h);h.localRotation=Quaternion.Euler(0,180,0);h.localPosition=V(505,0,-86);}
            var d=Doors[pair.Key];if(pair.Key==2304982)Label("7 舍 · LG 北门",V(252.5f,3.7f,-55.55f),.65f,0);else Label(pair.Key==77935?"3 舍":f.name,V(d.x,baseY+3.7f,d.y+.55f),.65f);
        }
        // South 1F pavilion; the mirrored native ground entrance now opens north into LG.
        Box("Stone",V(252.5f,3.0f,-28.5f),V(9,.16f,5),true);
        foreach(float x in new[]{248.5f,256.5f})Box("Stone",V(x,4.55f,-29.7f),V(.4f,3.1f,2.4f),true);
        Box("Metal",V(252.5f,6.15f,-29.5f),V(9,.22f,3.4f));Box("Glass",V(252.5f,4.53f,-30.65f),V(7.5f,2.9f,.1f));
        for(float x=249;x<=256;x+=1.5f)Box("Metal",V(x,4.53f,-30.56f),V(.065f,2.9f,.08f));
        Label("7 舍 · 1F 南门",V(252.5f,5.7f,-27.75f),.65f,180);
    }
}
