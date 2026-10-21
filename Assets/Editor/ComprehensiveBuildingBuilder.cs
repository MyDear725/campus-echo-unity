using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Facade coordinates are metres: u goes right when facing it, d goes inward.
// These map to local -X and -Z respectively.
// The imported campus has a reflected X axis; the new group cancels it once.
public static class ComprehensiveBuildingBuilder
{
    public const string Group = "综合楼 · 通高玻璃与石材门廊";
    const string Folder = "Assets/ComprehensiveRevision";
    const string ScenePath = "Assets/Scenes/CampusLiving.unity";
    const string Output = "output/综合楼改造";
    static Transform holder;
    static CampusGame game;
    static Dictionary<string, DormitoryBuilder.Geometry> batches;
    static Dictionary<string, Material> materials;
    static int serial;
    static float clipFront;
    static Vector3 V(float u,float y,float d) => new Vector3(-u,y,-d);
    static T Save<T>(T value,string path) where T:Object {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(value,path);return value;
    }
    public static void RestoreSources(Transform campus) {
        var old=campus.Find(Group);if(!old)return;
        var marker=old.GetComponent<CourtyardRevision>();
        if(marker)foreach(var r in marker.suppressedSources)if(r)r.enabled=true;
        Object.DestroyImmediate(old.gameObject);
    }
    [MenuItem("Campus/Apply Reference Comprehensive Building")]
    public static void Generate() {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode before rebuilding.");
        EditorSceneManager.OpenScene(ScenePath);
        var g=Object.FindFirstObjectByType<CampusGame>();
        var campus=GameObject.Find("开发区校区 · 校园环境").transform;
        var data=JsonUtility.FromJson<CampusData>(g.mapData.text);
        Apply(campus,data,g);
        Validate();
        EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
        EditorSceneManager.SaveScene(g.gameObject.scene);
        AssetDatabase.SaveAssets();Capture();
        Debug.Log("COMPREHENSIVE_BUILDING_COMPLETE");
    }
    public static void Apply(Transform campus,CampusData data,CampusGame g) {
        if(!data.features.Any(f=>f.id==77921))return;
        game=g;RestoreSources(campus);serial=0;
        foreach(string dir in new[]{Folder,Folder+"/Meshes",Folder+"/Materials",Output})Directory.CreateDirectory(dir);
        AssetDatabase.Refresh();
        holder=new GameObject(Group).transform;holder.SetParent(campus,false);
        holder.localPosition=new Vector3(-130,0,60);holder.localScale=new Vector3(-1,1,1);
        ReplaceImportedGeometry(campus);
        batches=new Dictionary<string,DormitoryBuilder.Geometry>();materials=new Dictionary<string,Material>();
        Mat("Limestone","#d7c9b2",stone:true);Mat("Pale stone","#e5dccb",stone:true);
        Mat("Granite","#a6aaa7",stone:true);Mat("Interior","#e9e3d7");Mat("Oak","#ab7950",wood:true);
        Mat("Bronze","#8c6846");Mat("Frame","#36434b");Mat("Glass","#a7bec9",glass:true);
        Mat("Warm light","#fff1cd",emission:1.4f);Mat("Soil","#4f493a");Mat("Leaf","#58723f");
        Mat("Light leaf","#819251");Mat("Flower","#cd869e");Mat("Banner","#294b70");
        Building();Forecourt();ClearEntranceTrees(campus);
        foreach(var p in batches.Where(p=>p.Value.vertices.Count>0)) {
            var mesh=Save(p.Value.Mesh("Comprehensive "+p.Key),Folder+"/Meshes/"+p.Key+".asset");
            var o=new GameObject(p.Key);o.transform.SetParent(holder,false);o.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=o.AddComponent<MeshRenderer>();r.sharedMaterial=materials[p.Key];r.shadowCastingMode=p.Key=="Glass"?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;
        }
        Sign("综\n合\n楼",14.15f,18f,-.61f,.23f);
        Sign("Comprehensive\nBuilding",14.15f,13.1f,-.62f,.061f);
        Sign("立\n德\n树\n人",-14,15.2f,-.61f,.105f);
        Sign("知\n行\n合\n一",-15.5f,12.6f,-.61f,.105f);
        Sign("综合楼  ·  COMPREHENSIVE BUILDING",0,4.1f,-.21f,.055f);
        UpdateMap(data);AssetDatabase.SaveAssets();
    }
    static void Mat(string name,string hex,bool stone=false,bool wood=false,bool glass=false,float emission=0) {
        ColorUtility.TryParseHtmlString(hex,out var c);if(glass)c.a=.19f;
        var m=new Material(Shader.Find("Campus/Library")){name="Comprehensive "+name,enableInstancing=true};
        m.SetColor("_BaseColor",c);m.SetFloat("_Stone",stone?1:0);m.SetFloat("_Wood",wood?1:0);
        m.SetFloat("_Glass",glass?1:0);m.SetFloat("_Smoothness",glass?.82f:wood?.25f:.22f);m.SetFloat("_Emission",emission);
        m.SetFloat("_CampusOutdoorSurface",1);
        if(glass){m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");}
        materials[name]=Save(m,Folder+"/Materials/"+name+".mat");batches[name]=new DormitoryBuilder.Geometry();
    }
    static void B(string m,float u,float y,float d,float w,float h,float dep,bool solid=false) {
        batches[m].Box(V(u,y,d),Vector3.right,Vector3.up,Vector3.forward,new Vector3(w,h,dep));
        if(solid)Solid(m,V(u,y,d),new Vector3(w,h,dep));
    }
    static void Solid(string name,Vector3 p,Vector3 size) {
        var go=new GameObject("Collision "+name+" "+serial++){layer=9};go.transform.SetParent(holder,false);go.transform.localPosition=p;go.AddComponent<BoxCollider>().size=size;
    }
    static void Beam(string m,Vector3 a,Vector3 b,float w)=>batches[m].Beam(a,b,w);
    static void Window(float u,float y,float d,float width,float height) {
        B("Glass",u,y,d,width,height,.035f);
        foreach(float x in new[]{u-width/2,u,u+width/2})B("Frame",x,y,d-.055f,.075f,height+.08f,.12f);
        foreach(float h in new[]{y-height/2,y+height/2})B("Frame",u,h,d-.055f,width,.075f,.12f);
    }
    static void Building() {
        const float floor=.95f,storey=5.3f;
        B("Granite",0,.51f,12,56,.88f,24,true);
        // Accessible ground-floor foyer; upper galleries are architectural scenery.
        B("Interior",0,12,23.8f,56,22,.4f,true);
        foreach(float side in new[]{-1f,1f}) {
            Solid("wing side glazing",V(side*27.8f,11.5f,12),new Vector3(.4f,22,24));
            Solid("wing front glazing",V(side*20,11.5f,0),new Vector3(16,22,.3f));
            foreach(float d in new[]{0f,6f,12f,18f,23.8f})B("Limestone",side*27.8f,11.5f,d,.5f,22,.65f);
            B("Interior",side*11.5f,11.5f,14,.35f,22,20,true);
            for(int level=0;level<4;level++) {
                float y=floor+level*storey;
                B("Interior",side*19.7f,y,12,16,.23f,23.5f,level==0);
                B("Pale stone",side*20,y+.27f,0,16.2f,.65f,.6f);
                B("Pale stone",side*27.8f,y+.27f,12,.6f,.65f,24);
                for(int bay=0;bay<4;bay++) {
                    float d=3+bay*5.9f;
                    B("Glass",side*27.78f,y+2.75f,d,.035f,4.55f,5.25f);
                    foreach(float z in new[]{d-2.65f,d,d+2.65f})B("Frame",side*27.86f,y+2.75f,z,.12f,4.6f,.075f);
                    foreach(float yy in new[]{y+.48f,y+5.025f})B("Frame",side*27.86f,yy,d,.12f,.075f,5.3f);
                }
                for(int bay=0;bay<4;bay++) {
                    float u=side*(13.7f+bay*4);
                    Window(u,y+2.75f,.03f,3.35f,4.55f);
                    B("Limestone",u+side*1.9f,y+2.75f,-.1f,.55f,4.65f,.55f);
                    foreach(float offset in new[]{-.95f,-.62f})B("Oak",u+offset,y+2.75f,-.34f,.12f,4.55f,.36f);
                }
                B("Interior",side*20,y+4.95f,10,15.5f,.12f,20);
                B("Warm light",side*20,y+4.86f,3.3f,11,.025f,.12f);
            }
            B("Limestone",side*20,22.2f,12,16.5f,.55f,24.4f);
            B("Pale stone",side*20,23,0,16.4f,1.1f,.45f);
            B("Pale stone",side*20,25.1f,11.5f,16.4f,.4f,23.4f);
            for(int i=0;i<4;i++)B("Limestone",side*(13.2f+i*4.5f),24,1,.4f,2.4f,.4f);
            for(float u=12;u<28;u+=.6f)B("Frame",side*u,23.6f,.3f,.035f,1,.035f);
            B("Frame",side*20,24.1f,.3f,16,.05f,.05f);
            // Wide stone sign panels between the wings and the glazed atrium.
            B("Limestone",side*14,12.15f,-.25f,5.6f,22.8f,.55f,true);
            B("Limestone",side*10.6f,13.2f,-.3f,1.5f,25.1f,1.4f,true);
            for(float u=8.9f;u<=9.7f;u+=.28f)B("Oak",side*u,14.6f,.15f,.12f,19.3f,.35f);
        }
        B("Limestone",0,26,-.2f,22.7f,1.1f,4.8f);
        for(float u=-10;u<=10;u+=.32f)B("Oak",u,25.4f,.05f,.15f,.2f,4.1f);
        // Continuous central curtain wall, with slender vertical mullions.
        for(int bay=0;bay<8;bay++)Window(-7.875f+bay*2.25f,15.15f,.65f,2.25f,19.3f);
        for(float y=5.5f;y<25;y+=2.65f)B("Frame",0,y,.57f,18,.065f,.12f);
        for(int f=1;f<4;f++) {
            float y=.95f+f*5.3f;
            B("Interior",0,y,19.3f,22.6f,.24f,8.8f);
            B("Interior",-9.8f,y,9.9f,3,.24f,10);
            B("Interior",9.8f,y,9.9f,3,.24f,10);
            B("Glass",0,y+.65f,14.85f,22,.95f,.035f);
            B("Frame",0,y+1.15f,14.82f,22,.055f,.07f);
            for(float u=-11;u<=11;u+=1.1f)B("Frame",u,y+.67f,14.8f,.04f,1,.04f);
            B("Warm light",0,y-.18f,16.1f,19,.035f,.12f);
        }
        B("Interior",0,25.1f,12,20.5f,.25f,22);
        // Ring pendant lights visible through the atrium, suspended from the roof.
        foreach(float d in new[]{6f,11.5f,17f}) {
            batches["Warm light"].Ring(V(0,19.5f,d),Vector3.right,Vector3.forward,2.1f,.085f,72);
            batches["Bronze"].Ring(V(0,19.6f,d),Vector3.right,Vector3.forward,2.15f,.07f,72);
            foreach(float u in new[]{-1.4f,1.4f})Beam("Frame",V(u,19.6f,d),V(u,25,d),.018f);
        }
        // Two glass side doors frame a genuinely open central entrance.
        foreach(float side in new[]{-1f,1f}) {
            Window(side*6.25f,2.65f,.1f,5.5f,3.4f);
            Solid("fixed entrance glazing",V(side*6.25f,2.7f,.1f),new Vector3(5.5f,3.5f,.15f));
            B("Frame",side*3.2f,2.7f,.05f,.12f,3.5f,.18f,true);
            B("Bronze",side*2.65f,2.6f,1,.055f,.9f,.055f);
        }
        B("Frame",0,4.5f,.1f,18,.18f,.2f);
        B("Glass",0,4.95f,.15f,18,.65f,.03f);
        B("Bronze",0,5.45f,-2.2f,24,.34f,5.7f);
        B("Oak",0,5.25f,-2.2f,23.7f,.08f,5.4f);
        foreach(float u in new[]{-10.5f,10.5f})B("Limestone",u,2.9f,-3.5f,.48f,4.6f,.48f,true);
        for(float u=-10;u<=10;u+=2)for(float d=-4;d<=0;d+=1.7f)batches["Warm light"].Disc(V(u,5.195f,d),Vector3.right,Vector3.forward,.055f,12);
        for(int i=0;i<5;i++) {
            float top=.23f+(i+1)*.144f,near=-4.5f+i*.42f,far=0;
            B("Granite",0,(top+.23f)/2,(near+far)/2,20,top-.23f,far-near,true);
        }
        // Side ramp rises 0.72 m over 9 m, terminating on the entrance landing.
        var ramp=new DormitoryBuilder.Geometry();
        ramp.Quad(V(-20,.23f,-2.5f),V(-20,.23f,-.4f),V(-11,.95f,-.4f),V(-11,.95f,-2.5f));
        var rm=Save(ramp.Mesh("Accessible entrance ramp"),Folder+"/Meshes/Ramp.asset");
        var ro=new GameObject("Accessible ramp"){layer=9};ro.transform.SetParent(holder,false);
        ro.AddComponent<MeshFilter>().sharedMesh=rm;ro.AddComponent<MeshRenderer>().sharedMaterial=materials["Granite"];ro.AddComponent<MeshCollider>().sharedMesh=rm;
        B("Granite",-10.5f,.51f,-1.45f,1,.88f,2.1f,true);
        Beam("Frame",V(-20,1.18f,-2.6f),V(-11,1.9f,-2.6f),.05f);
        for(int i=0;i<7;i++) {float u=-20+i*1.5f,y=.23f+(u+20)*.08f;B("Frame",u,y+.48f,-2.6f,.035f,.96f,.035f);}
        B("Oak",-6,1.65f,9,4.8f,1.4f,1.2f,true);B("Pale stone",-6,2.39f,9,4.9f,.08f,1.35f);
        foreach(float side in new[]{-1f,1f}){Planter(side*7.7f,2.8f,1.8f,1.8f,.95f);Planter(side*7.7f,12.5f,1.8f,1.8f,.95f);}
    }
    static void Module(string name,float u,float d,float scale=1,float yaw=0) {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CampusPlanRevision/RoadScenery/Modules/"+name+".prefab");
        if(!prefab)throw new InvalidOperationException("Missing landscape module "+name);
        var o=(GameObject)PrefabUtility.InstantiatePrefab(prefab,holder);o.name="Comprehensive "+name;
        o.transform.localPosition=V(u,.23f,d);o.transform.localRotation=Quaternion.Euler(0,yaw,0);o.transform.localScale=Vector3.one*scale;
    }
    static void ClearEntranceTrees(Transform campus) {
        // The previous road layout planted trees across the new entrance steps.
        // Relocate their prefab and matching trunk collider together, outside the portal.
        int index=0;
        foreach(var t in campus.GetComponentsInChildren<Transform>().Where(t=>!t.IsChildOf(holder)&&t.name=="路荫乔木").ToArray()) {
            var p=holder.InverseTransformPoint(t.position);
            if(Mathf.Abs(p.x)>12||p.z<0||p.z>8)continue;
            var before=t.position;var after=holder.TransformPoint(new Vector3(p.x<0?-29:29,p.y,3+index++*2));
            foreach(var c in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)) {
                if(!c.name.Contains("Tree trunk"))continue;
                var delta=c.transform.position-before;delta.y=0;
                if(delta.magnitude<.4f)c.transform.position+=after-before;
            }
            t.position=after;
        }
    }
    static void Planter(float u,float d,float w,float depth,float baseY=.23f) {
        B("Granite",u,baseY+.32f,d,w,.64f,depth,true);
        B("Soil",u,baseY+.65f,d,w-.18f,.035f,depth-.18f);
        var rng=new System.Random(Mathf.RoundToInt(u*97+d*41));
        for(int i=0;i<w*depth*24;i++) {
            float x=u+((float)rng.NextDouble()-.5f)*(w-.3f),z=d+((float)rng.NextDouble()-.5f)*(depth-.3f),y=baseY+.75f+(float)rng.NextDouble()*.4f;
            var p=V(x,y,z);float a=(float)rng.NextDouble()*Mathf.PI*2;var axis=new Vector3(Mathf.Cos(a),.3f,Mathf.Sin(a))*.12f;
            batches[i%9==0?"Flower":i%3==0?"Light leaf":"Leaf"].Quad(p-axis,p+Vector3.up*.09f,p+axis,p-Vector3.up*.07f);
        }
    }
    static void Forecourt() {
        // Thin paving overlays the existing ground without blocking the east-west road at z=70.
        B("Pale stone",0,.242f,-9.5f,56,.02f,19);
        for(float u=-28;u<=28;u+=7)B("Granite",u,.255f,-9.5f,.12f,.006f,19);
        foreach(float side in new[]{-1f,1f}) {
            Planter(side*23,-4.5f,6,3);Planter(side*23,-16.8f,7,3);
            Module("Bench",side*23,-14.5f,1,180);Module("BicycleBay",side*16.5f,-16.5f,.85f,90);
            Module("Canopy-0",side*26,-3.6f,1.1f);Module("Canopy-2",side*26,-17.5f,1.1f);
            foreach(float d in new[]{-5f,-14f}) {
                float u=side*21;Module("PathLight",u,d,1.15f);
                B("Banner",u+side*.65f,3.5f,d,1,2.4f,.035f);
                Sign("厚\n德\n笃\n学",u+side*.65f,3.5f,d-.03f,.06f,Color.white);
            }
        }
    }
    static void Sign(string text,float u,float y,float d,float size,Color? color=null) {
        var o=new GameObject(text.Replace('\n',' '));o.transform.SetParent(holder,false);o.transform.localPosition=V(u,y,d);o.transform.localRotation=Quaternion.Euler(0,180,0);
        var t=o.AddComponent<TextMesh>();t.font=Resources.Load<Font>("CampusFont");t.text=text;t.fontSize=96;t.characterSize=size;
        t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.lineSpacing=1.1f;t.fontStyle=FontStyle.Bold;t.color=color??new Color(.035f,.035f,.03f);
        t.GetComponent<Renderer>().sharedMaterial=Resources.Load<Material>("InteriorWorldText");o.AddComponent<InteriorWorldLabel>();
    }
    static void UpdateMap(CampusData data) {
        MapPoint P(float x,float z)=>new MapPoint{x=x,z=z};
        var f=data.features.Single(f=>f.id==77921);
        f.points=new[]{P(-158,36),P(-102,36),P(-102,60),P(-158,60)};
        data.obstacles=data.obstacles.Where(o=>o.name!="综合楼"&&!o.name.StartsWith("Comprehensive ")).ToArray();
        var list=data.obstacles.ToList();
        void Obstacle(string name,float u,float d,float w,float depth) {var map=game.WorldToMap(holder.TransformPoint(V(u,0,d)));list.Add(new CampusObstacle{name=name,minX=map.x-w/2,maxX=map.x+w/2,minZ=map.z-depth/2,maxZ=map.z+depth/2});}
        Obstacle("综合楼",-19.8f,12,16.4f,24);Obstacle("Comprehensive right wing",19.8f,12,16.4f,24);
        Obstacle("Comprehensive rear",0,23.8f,56,.4f);
        foreach(float side in new[]{-1f,1f}) {Obstacle("Comprehensive entrance side",side*7,0,8,1);Obstacle("Comprehensive planter",side*23,-4.5f,6,3);Obstacle("Comprehensive planter",side*23,-16.8f,7,3);}
        data.obstacles=list.ToArray();
        var walls=GameObject.Find("Camera collision surfaces");
        if(walls)foreach(var c in walls.GetComponentsInChildren<Collider>().Where(c=>c.name=="综合楼 wall"))Object.DestroyImmediate(c.gameObject);
        // Remove the old terrain-plan building collision only for this footprint.
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) {
            if(c.transform.IsChildOf(holder)||c.gameObject.layer!=9)continue;
            if(c.name.Contains("综合楼"))Object.DestroyImmediate(c.gameObject);
        }
        string path=Folder+"/CampusMap.json";File.WriteAllText(path,JsonUtility.ToJson(data,true));AssetDatabase.ImportAsset(path);game.mapData=AssetDatabase.LoadAssetAtPath<TextAsset>(path);EditorUtility.SetDirty(game);
    }
    struct Vertex { public Vector3 p,n;public Vector2 uv;public Color color;
        public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),color=Color.Lerp(a.color,b.color,t)};
    }
    static float Distance(Vertex a,int edge)=>edge==0?a.p.x+32:edge==1?32-a.p.x:edge==2?a.p.z+36:clipFront-a.p.z;
    static List<Vertex> Cut(List<Vertex> poly,int edge,bool inside) {
        var o=new List<Vertex>();for(int i=0;i<poly.Count;i++){var a=poly[i];var b=poly[(i+1)%poly.Count];float da=Distance(a,edge),db=Distance(b,edge);bool ia=inside?da>=0:da<0,ib=inside?db>=0:db<0;if(ia)o.Add(a);if(ia!=ib)o.Add(Vertex.Lerp(a,b,da/(da-db)));}return o;
    }
    static void ReplaceImportedGeometry(Transform campus) {
        var suppressed=new List<Renderer>();
        foreach(var filter in campus.GetComponentsInChildren<MeshFilter>()) {
            var r=filter.GetComponent<MeshRenderer>();if(!r||!r.enabled||!filter.sharedMesh||filter.transform.IsChildOf(holder))continue;
            string path=AssetDatabase.GetAssetPath(filter.sharedMesh);
            bool roadTreeBatch=path.Contains("/RoadClear-")&&r.bounds.size.y>1;
            if(!(path.StartsWith("Assets/LivingRevision/Meshes/")||path.StartsWith("Assets/CampusGenerated/Meshes/")||path.Contains("Retained-")||path.Contains("Preserved-")||path.Contains("Surroundings-")||roadTreeBatch))continue;
            // Older roadside trees are combined into RoadClear meshes, not individual prefabs.
            // Retain the road paving, but clear these superseded trees from the new forecourt.
            clipFront=roadTreeBatch?20:5;
            var bounds=r.bounds;var min=holder.InverseTransformPoint(bounds.min);var max=holder.InverseTransformPoint(bounds.max);
            if(max.x< -32||min.x>32||max.z< -36||min.z>clipFront||max.y<.25f)continue;
            var src=filter.sharedMesh;var p=src.vertices;var n=src.normals;var uv=src.uv;var colors=src.colors;
            var matrix=holder.worldToLocalMatrix*filter.transform.localToWorldMatrix;var nm=matrix.inverse.transpose;
            Vertex At(int i)=>new Vertex{p=matrix.MultiplyPoint3x4(p[i]),n=n.Length==p.Length?nm.MultiplyVector(n[i]).normalized:Vector3.up,uv=uv.Length==p.Length?uv[i]:Vector2.zero,color=colors.Length==p.Length?colors[i]:Color.white};
            bool changed=false;var verts=new List<Vertex>();var submeshes=new List<int[]>();
            void Append(List<Vertex> poly,List<int> indices){for(int i=1;i<poly.Count-1;i++){int start=verts.Count;verts.Add(poly[0]);verts.Add(poly[i]);verts.Add(poly[i+1]);if(matrix.determinant<0)indices.AddRange(new[]{start,start+2,start+1});else indices.AddRange(new[]{start,start+1,start+2});}}
            for(int sub=0;sub<src.subMeshCount;sub++) {
                var tris=src.GetTriangles(sub);var indices=new List<int>();
                for(int i=0;i<tris.Length;i+=3) {
                    var triangle=new List<Vertex>{At(tris[i]),At(tris[i+1]),At(tris[i+2])};
                    if(triangle.Max(v=>v.p.y)<.25f||triangle.Max(v=>v.p.x)< -32||triangle.Min(v=>v.p.x)>32||triangle.Max(v=>v.p.z)< -36||triangle.Min(v=>v.p.z)>clipFront){Append(triangle,indices);continue;}
                    var remain=triangle;var outside=new List<List<Vertex>>();
                    for(int edge=0;edge<4&&remain.Count>0;edge++){outside.Add(Cut(remain,edge,false));remain=Cut(remain,edge,true);}
                    if(remain.Count>=3){changed=true;foreach(var part in outside)Append(part,indices);}else Append(triangle,indices);
                }
                submeshes.Add(indices.ToArray());
            }
            if(!changed)continue;suppressed.Add(r);r.enabled=false;if(verts.Count==0)continue;
            var mesh=new Mesh{name="Preserved campus around comprehensive building",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(verts.Select(v=>v.p).ToList());mesh.SetNormals(verts.Select(v=>v.n).ToList());mesh.SetUVs(0,verts.Select(v=>v.uv).ToList());mesh.SetColors(verts.Select(v=>v.color).ToList());
            mesh.subMeshCount=submeshes.Count;for(int i=0;i<submeshes.Count;i++)mesh.SetTriangles(submeshes[i],i);mesh.RecalculateBounds();
            mesh=Save(mesh,Folder+"/Meshes/Preserved-"+AssetDatabase.AssetPathToGUID(path)+"-"+suppressed.Count+".asset");
            var obj=new GameObject("Preserved surroundings "+suppressed.Count);obj.transform.SetParent(holder,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var rr=obj.AddComponent<MeshRenderer>();rr.sharedMaterials=r.sharedMaterials;rr.shadowCastingMode=r.shadowCastingMode;
        }
        holder.gameObject.AddComponent<CourtyardRevision>().suppressedSources=suppressed.ToArray();
    }
    public static void Validate() {
        Physics.SyncTransforms();
        var h=GameObject.Find(Group).transform;var g=Object.FindFirstObjectByType<CampusGame>();
        var data=JsonUtility.FromJson<CampusData>(g.mapData.text);
        foreach(float d in new[]{-17f,-10f,-4.7f,-2f,1f,4f,8f,13f}) {
            var map=g.WorldToMap(h.TransformPoint(V(0,0,d)));
            if(CampusCollision.Blocked(map.x,map.z,data.obstacles))throw new Exception("Comprehensive entrance blocked at "+d);
            var ray=h.TransformPoint(V(0,4,d));if(!Physics.Raycast(ray,Vector3.down,out var hit,5,1<<9|1<<10))throw new Exception("No floor at "+d);
            if(hit.point.y>1.05f)throw new Exception("Unexpected entrance obstruction at "+d+": "+hit.collider.name);
        }
        foreach(var filter in h.GetComponentsInChildren<MeshFilter>())if(!filter.sharedMesh)throw new Exception("Missing mesh: "+filter.name);
        foreach(var r in h.GetComponentsInChildren<Renderer>())foreach(var m in r.sharedMaterials)if(!m||!m.shader||!m.shader.isSupported)throw new Exception("Missing material/shader: "+r.name);
        Walk(h,V(0,.3f,-17),V(0,.95f,8),"front steps");
        Walk(h,V(-21,.3f,-1.45f),V(-9,.95f,-1.45f),"side ramp");
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/validation.txt","PASS: ground-floor foyer and entrance route are clear in map collision.\nPASS: physical floor supports plaza, steps and foyer.\nPASS: character capsule walks from plaza over steps into foyer and up side ramp.\nPASS: all mesh/material/shader references resolved.\nFour-storey wings, 26.55 m portal, 56 m frontage; upper galleries are scenery.\n");
    }
    static void Walk(Transform h,Vector3 from,Vector3 to,string name) {
        var o=new GameObject("Temporary comprehensive walking test"){layer=2};o.transform.position=h.TransformPoint(from);
        var body=o.AddComponent<CharacterController>();body.height=1.8f;body.center=new Vector3(0,.9f,0);body.radius=.32f;body.stepOffset=.3f;body.skinWidth=.025f;body.slopeLimit=48;body.excludeLayers=~((1<<9)|(1<<10));
        var goal=h.TransformPoint(to);
        try{Physics.SyncTransforms();for(int i=0;i<1000;i++){var delta=goal-o.transform.position;delta.y=0;if(delta.magnitude<.08f)break;body.Move(Vector3.ClampMagnitude(delta,.07f)+Vector3.down*.06f);}
            var remaining=goal-o.transform.position;remaining.y=0;
            if(remaining.magnitude>.12f||Mathf.Abs(o.transform.position.y-.95f)>.15f)throw new Exception("Walking test failed: "+name+" at "+h.InverseTransformPoint(o.transform.position));
        }finally{Object.DestroyImmediate(o);}
    }
    public static void Capture() {
        if(!GameObject.Find(Group))EditorSceneManager.OpenScene(ScenePath);
        var h=GameObject.Find(Group).transform;
        var cameraObject=new GameObject("Comprehensive review camera");var cam=cameraObject.AddComponent<Camera>();cam.fieldOfView=56;cam.nearClipPlane=.1f;cam.farClipPlane=1400;cam.clearFlags=CameraClearFlags.Skybox;
        cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var temps=new List<Material>();var texts=Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
        var originals=texts.Select(t=>t.GetComponent<Renderer>().sharedMaterial).ToArray();
        // CafeteriaSign binds its depth-tested font atlas in Awake during gameplay.
        // Reproduce that binding for editor rendering, where Awake does not run.
        foreach(var t in texts){if(!t.font)t.font=Resources.Load<Font>("CampusFont");t.font.RequestCharactersInTexture(t.text,t.fontSize,t.fontStyle);}
        foreach(var t in texts){var m=new Material(Resources.Load<Material>("InteriorWorldText"));m.mainTexture=t.font.material.mainTexture;t.GetComponent<Renderer>().sharedMaterial=m;temps.Add(m);}
        var rt=new RenderTexture(1600,1100,24);rt.antiAliasing=4;cam.targetTexture=rt;var previous=RenderTexture.active;
        void Shot(Vector3 p,Vector3 target,string name){cam.transform.position=h.TransformPoint(p);cam.transform.LookAt(h.TransformPoint(target));for(int i=0;i<3;i++)cam.Render();RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();File.WriteAllBytes(Output+"/"+name+".png",t.EncodeToPNG());Object.DestroyImmediate(t);}
        try{Shot(V(0,10,-53),V(0,12,3),"01_正面");Shot(V(-39,9,-42),V(0,12,5),"02_斜侧面");Shot(V(4,2.6f,-16),V(0,5,5),"03_入口近景");}
        finally{for(int i=0;i<texts.Length;i++)texts[i].GetComponent<Renderer>().sharedMaterial=originals[i];foreach(var m in temps)Object.DestroyImmediate(m);RenderTexture.active=previous;cam.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(cameraObject);}
    }
    public static void InspectSite() {
        EditorSceneManager.OpenScene(ScenePath);var h=GameObject.Find(Group).transform;var lines=new List<string>();
        foreach(var f in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) {
            var r=f.GetComponent<Renderer>();if(!r||!r.enabled||f.transform.IsChildOf(h))continue;
            var p=h.InverseTransformPoint(r.bounds.center);var size=r.bounds.size;
            if(Mathf.Abs(p.x)>12+size.x/2||p.z+size.z/2<0||p.z-size.z/2>19||p.y>12)continue;
            string hierarchy=f.name;for(var t=f.transform.parent;t;t=t.parent)hierarchy=t.name+"/"+hierarchy;
            lines.Add(hierarchy+" | "+p+" | "+size+" | "+AssetDatabase.GetAssetPath(f.sharedMesh));
        }
        File.WriteAllLines(Output+"/site-inspection.txt",lines);
    }
}
