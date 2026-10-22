using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Reference-inspired exterior, in existing campus map metres. No new interior.
public static class InformationBuildingBuilder
{
    const string Folder="Assets/InformationRevision",Group="信息楼 · 石材格栅与玻璃中庭";
    static readonly Vector3 Origin=new Vector3(-3,0,159.9f),Tangent=new Vector3(1,0,-.145f).normalized,Back=new Vector3(-.145f,0,-1).normalized;
    static readonly Rect Mask=Rect.MinMaxRect(-16.5f,127.9f,69.5f,173);
    static Dictionary<string,DormitoryBuilder.Geometry> geometry;
    static Dictionary<string,Material> materials;
    static Transform physical;
    static Vector3 V(float u,float y,float d)=>Origin+Tangent*u+Vector3.up*y+Back*d;
    static void Box(string m,float u,float y,float d,float w,float h,float depth)=>geometry[m].Box(V(u,y,d),Tangent,Vector3.up,Back,new Vector3(w,h,depth));
    static void Beam(string m,Vector3 a,Vector3 b,float width)=>geometry[m].Beam(a,b,width);
    static T Save<T>(T value,string path)where T:Object
    {var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,path);return value;}
    static void Mat(string name,string hex,float stone=0,float glass=0,float wood=0,float emission=0)
    {
        ColorUtility.TryParseHtmlString(hex,out var color);if(glass>0)color.a=.22f;
        var m=new Material(Shader.Find("Campus/Library")){name="Information · "+name};m.SetColor("_BaseColor",color);m.SetFloat("_Stone",stone);m.SetFloat("_Glass",glass);m.SetFloat("_Wood",wood);m.SetFloat("_Smoothness",glass>0?.85f:.28f);m.SetFloat("_Emission",emission);m.enableInstancing=true;
        if(glass>0){m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");}
        materials[name]=Save(m,Folder+"/Materials/"+name+".mat");geometry[name]=new DormitoryBuilder.Geometry();
    }
    static void Glass(float u,float y,float d,float w,float h)
    {geometry["Glass"].Quad(V(u-w/2,y-h/2,d),V(u+w/2,y-h/2,d),V(u+w/2,y+h/2,d),V(u-w/2,y+h/2,d));}
    [MenuItem("Campus/Apply Reference Information Building")]
    public static void Apply()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");var game=Object.FindFirstObjectByType<CampusGame>();var root=GameObject.Find("开发区校区 · 校园环境").transform;
        foreach(string dir in new[]{Folder,Folder+"/Meshes",Folder+"/Materials"})Directory.CreateDirectory(dir);AssetDatabase.Refresh();
        var old=root.Find(Group);if(old){var state=old.GetComponent<CourtyardRevision>();if(state)foreach(var r in state.suppressedSources)if(r)r.enabled=true;Object.DestroyImmediate(old.gameObject);}
        var holder=new GameObject(Group).transform;holder.SetParent(root,false);ClearLegacy(root,holder);
        physical=new GameObject("Information entrance physical surfaces").transform;physical.SetParent(holder,false);
        geometry=new Dictionary<string,DormitoryBuilder.Geometry>();materials=new Dictionary<string,Material>();
        Mat("Limestone","#d5cebf",1);Mat("Stone edge","#a5a59d",1);Mat("Graphite","#35444c");Mat("Bronze","#8b7051");Mat("Oak","#ab8159",0,0,1);Mat("Interior","#d7ccba",0,0,0,.10f);Mat("Room backdrop","#59666c");Mat("Glass","#a3bac7",0,1);Mat("LED","#ffe8c4",0,0,0,1.2f);Mat("Leaf","#647a4e");Mat("Soil","#5b5645");Mat("Banner","#344e6a");
        Structure();Entrance();Forecourt();
        foreach(var pair in geometry.Where(p=>p.Value.vertices.Count>0))
        {
            var mesh=Save(pair.Value.Mesh("Information · "+pair.Key),Folder+"/Meshes/"+pair.Key+".asset");var go=new GameObject(pair.Key);go.transform.SetParent(holder,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[pair.Key];renderer.shadowCastingMode=pair.Key=="Glass"||pair.Key=="LED"?ShadowCastingMode.Off:ShadowCastingMode.On;
        }
        Sign(holder,game,"信\n息\n楼",62,17.5f,-.73f,1.15f);Sign(holder,game,"Information\nBuilding",62,14.5f,-.74f,.33f);
        Sign(holder,game,"信息楼  ·  INFORMATION BUILDING",34,5.65f,-4.78f,.30f);
        var marker=holder.gameObject.AddComponent<InformationBuildingLandmark>();marker.entranceMap=V(34,.23f,-9);marker.lookMap=V(34,9,1);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("QA");File.WriteAllText("QA/information-exterior.txt","Reference-inspired information building: existing map parcel and angled main wing / west return retained approximately. Five-storey limestone facade, transparent central atrium, vertical bronze/oak fins, projecting canopy, glazed doors, exterior steps/ramp and simple glazed interior scenery. Real 3D geometry in shared material batches; original campus map, road coordinates, building collision and other entrance contracts untouched. No playable information-building interior added.\n");
        Debug.Log("INFORMATION_EXTERIOR_APPLIED");
    }
    static void Structure()
    {
        // Main angled bar and its short west return follow the existing campus parcel.
        Box("Stone edge",34.5f,.52f,10,70,1.04f,20);Box("Limestone",34.5f,22.5f,10,70,.42f,20.5f);
        Box("Limestone",34.5f,11.6f,19.8f,70,21.6f,.32f);
        Box("Room backdrop",34,11.6f,19.57f,24,21,.10f);
        foreach(float u in new[]{-.5f,69.5f})Box("Limestone",u,11.6f,10,.40f,21.6f,20);
        // Five floors of believable windows, sill bands, soffits and recessed reveals.
        for(int floor=0;floor<5;floor++)
        {
            float y=.9f+floor*4.25f;
            foreach(float u in new[]{9.2f,55.6f})Box("Limestone",u,y+.2f,.13f,19.5f,.45f,.5f);
            foreach(float u in new[]{2.8f,8.4f,14f,52f,57.6f,66.6f})
            {
                Glass(u,y+2.18f,.06f,4.2f,3.25f);Box("Room backdrop",u,y+2.1f,1.20f,4.25f,3.4f,.10f);
                Box("Graphite",u,y+2.18f,-.04f,.055f,3.25f,.09f);
                foreach(float edge in new[]{-1f,1f})Box("Graphite",u+edge*2.1f,y+2.18f,-.04f,.07f,3.3f,.11f);
                foreach(float yy in new[]{.56f,2.95f,3.8f})Box("Graphite",u,y+yy,-.04f,4.25f,.06f,.11f);
            }
            // Genuine depth in the atrium: slabs, rear glazing, balcony edges and lights.
            Box("Interior",34,y-.10f,11.8f,24,.22f,15.3f);
            Box("Stone edge",34,y+.13f,4.4f,24,.22f,.45f);
            for(float u=23;u<46;u+=2.2f)Box("Graphite",u,y+.70f,4.25f,.045f,1.15f,.045f);
            Box("Bronze",34,y+1.28f,4.25f,24,.05f,.06f);
            for(int i=0;i<5;i++)Box("LED",25+i*4.5f,y+3.9f,7,2.6f,.035f,.10f);
            // Upper side walls/windows visible from campus paths and rear viewpoints.
            foreach(float u in new[]{-.73f,69.73f})for(int bay=0;bay<4;bay++)
            {
                float d=2.6f+bay*4.7f;
                Box("Glass",u,y+2.1f,d,.025f,3.15f,3.5f);Box("Graphite",u,y+2.1f,d,.07f,3.2f,.055f);
                foreach(float side in new[]{-1f,1f})Box("Limestone",u,y+2.1f,d+side*2.05f,.5f,4.15f,.5f);
            }
            for(float u=3;u<68;u+=5.6f)
            {
                Box("Glass",u,y+2.1f,20,4.2f,3.1f,.025f);Box("Stone edge",u,y+.4f,20.05f,5.4f,.6f,.25f);
                Box("Graphite",u,y+2.1f,20.05f,.055f,3.15f,.06f);
                foreach(float edge in new[]{-1f,1f})Box("Graphite",u+edge*2.1f,y+2.1f,20.05f,.07f,3.2f,.07f);
            }
        }
        foreach(float u in new[]{-.15f,18.6f,49.1f,62.0f,69.4f})
        {
            float w=u==62?5.4f:1.35f;Box("Limestone",u,11.65f,-.20f,w,22.2f,.95f);
            Box("Stone edge",u,22.85f,-.12f,w+.18f,.22f,1.15f);
        }
        foreach(float center in new[]{19.9f,47.8f,67.6f})
            for(int i=0;i<11;i++)Box("Oak",center-1.05f+i*.21f,11.8f,-.28f,.065f,21.0f,.36f);
        Glass(34,12,.025f,24,20.8f);
        for(float u=22;u<=46;u+=2.4f)Box("Graphite",u,12,-.08f,.085f,20.9f,.16f);
        for(float y=1.6f;y<22.2f;y+=2.12f)Box("Graphite",34,y,-.08f,24,.065f,.16f);
        Box("Limestone",34,23.25f,2.1f,29,.30f,6.4f);Box("Oak",34,23.05f,1.9f,28.8f,.08f,6.2f);
        // Decorative visible stair flights inside glazing; not an indoor travel route.
        for(int floor=0;floor<4;floor++)for(int i=0;i<16;i++)Box("Interior",25+i*.46f,.9f+floor*4.25f+i*.25f,7.3f,.48f,.13f,2.1f);
        // Short return wing: full side/rear structures, rather than a backdrop plane.
        Box("Limestone",-5.2f,10.5f,13.0f,8.6f,21,23);
        for(int f=0;f<5;f++)for(int b=0;b<4;b++)
        {
            Box("Glass",-9.55f,2.5f+f*4.1f,4.2f+b*5.3f,.03f,2.9f,3.6f);
            Box("Graphite",-9.6f,2.5f+f*4.1f,4.2f+b*5.3f,.06f,2.9f,.06f);
        }
    }
    static void Entrance()
    {
        Box("Stone edge",34,5.65f,-1.9f,29,.35f,5.7f);Box("Oak",34,5.43f,-1.8f,28.7f,.07f,5.55f);
        foreach(float u in new[]{21.8f,46.2f})Box("Limestone",u,2.9f,-1.75f,.85f,5.1f,.85f);
        for(float u=23;u<46;u+=3.6f)Box("LED",u,5.37f,-2.6f,.22f,.025f,.22f);
        foreach(float u in new[]{29.7f,31.85f,34f,36.15f,38.3f})Box("Graphite",u,2.85f,-.45f,.10f,4.1f,.20f);
        Glass(34,2.85f,-.39f,8.6f,4.0f);Box("Bronze",34,4.87f,-.52f,9.0f,.12f,.16f);
        for(float u=30.3f;u<38.5f;u+=2.15f)Box("Bronze",u,2.2f,-.56f,.045f,.65f,.08f);
        for(int i=0;i<6;i++)
        {
            float y=.23f+(i+1)*.11f/2,d=-5.7f+i*.43f,h=(i+1)*.11f;
            Box("Stone edge",34,y,d,22,h,.43f);Collider("Entry tread "+i,V(34,y,d),new Vector3(22,h,.43f));
        }
        Box("Stone edge",34,.56f,-1.68f,30,.66f,3.3f);Collider("Entrance landing",V(34,.56f,-1.68f),new Vector3(30,.66f,3.3f));
        Vector3 a=V(47.6f,.23f,-6),b=V(47.6f,.89f,-.5f);var side=Tangent;
        geometry["Stone edge"].Quad(a-side,b-side,b+side,a+side);
        geometry["Stone edge"].Quad(a+side-Vector3.up*.18f,b+side-Vector3.up*.18f,b+side,a+side);
        geometry["Stone edge"].Quad(a-side,b-side,b-side-Vector3.up*.18f,a-side-Vector3.up*.18f);
        Collider("Accessible ramp",(a+b)/2-Vector3.up*.08f,new Vector3(2,.16f,Vector3.Distance(a,b)),Quaternion.LookRotation(b-a,Vector3.up));
        // A continuous accessible apron beside the steps, not a decorative floating lip.
        foreach(float u in new[]{22.4f,45.6f})
        {
            Box("Limestone",u,.47f,-4.3f,.65f,.48f,4.2f);
            Beam("Bronze",V(u,1.4f,-6),V(u,1.4f,-2.4f),.055f);
        }
    }
    static void Forecourt()
    {
        foreach(float u in new[]{16f,52f})
        {
            Box("Limestone",u,.55f,-4.2f,5,.70f,2.2f);Box("Soil",u,.91f,-4.2f,4.8f,.04f,2);
            for(int i=0;i<18;i++)
            {float xx=u-2.1f+(i%9)*.52f,d=-4.7f+(i/9)*.8f;Box("Leaf",xx,1.17f,d,.55f,.5f,.65f);}
            foreach(float baseU in new[]{u-1.05f,u+1.05f})Box("Stone edge",baseU,.43f,-6.35f,.5f,.4f,.65f);
            for(int slat=0;slat<5;slat++)Box("Oak",u,.70f,-6.60f+slat*.12f,3.1f,.075f,.095f);
            for(int slat=0;slat<3;slat++)Box("Oak",u,.99f+slat*.13f,-6.0f,3.1f,.085f,.055f);
        }
        foreach(float u in new[]{10f,58f})
        {
            Box("Graphite",u,2.1f,-7.3f,.11f,3.8f,.11f);Box("Graphite",u,4.05f,-7.3f,1.05f,.08f,.32f);
            Box("LED",u,4.00f,-7.3f,.85f,.015f,.22f);Box("Banner",u+.42f,2.8f,-7.3f,.60f,1.5f,.035f);
        }
    }
    static void Sign(Transform holder,CampusGame game,string text,float u,float y,float d,float size)
    {
        var go=new GameObject(text);go.transform.SetParent(holder,false);go.transform.localPosition=V(u,y,d);
        go.transform.localRotation=Quaternion.LookRotation(Back);go.transform.localScale=new Vector3(-1,1,1);
        var tm=go.AddComponent<TextMesh>();tm.text=text;tm.font=game.interfaceFont;tm.fontSize=96;tm.characterSize=size/10;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.fontStyle=FontStyle.Bold;tm.lineSpacing=1.05f;tm.color=new Color(.055f,.045f,.035f);
        go.AddComponent<CafeteriaSign>().surface=AssetDatabase.LoadAssetAtPath<Material>("Assets/CafeteriaRevision/Materials/Wall lettering.mat");
    }
    static void Collider(string name,Vector3 at,Vector3 size,Quaternion? rotation=null)
    {var go=new GameObject(name){layer=9};go.transform.SetParent(physical,false);go.transform.localPosition=at;go.transform.localRotation=rotation??Quaternion.LookRotation(Back);go.AddComponent<BoxCollider>().size=size;}
    struct Vertex {public Vector3 p,n;public Vector2 uv;public Color c;public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),c=Color.Lerp(a.c,b.c,t)};}
    static float Plane(Vector3 v,int side)=>side==0?v.x-Mask.xMin:side==1?Mask.xMax-v.x:side==2?v.z-Mask.yMin:side==3?Mask.yMax-v.z:side==4?v.y-.5f:50-v.y;
    static List<Vertex> Cut(List<Vertex> poly,int side,bool inside)
    {var list=new List<Vertex>();for(int i=0;i<poly.Count;i++){var a=poly[i];var b=poly[(i+1)%poly.Count];float da=Plane(a.p,side),db=Plane(b.p,side);bool ia=inside?da>=0:da<0,ib=inside?db>=0:db<0;if(ia)list.Add(a);if(ia!=ib)list.Add(Vertex.Lerp(a,b,da/(da-db)));}return list;}
    static void ClearLegacy(Transform root,Transform holder)
    {
        var suppressed=new List<Renderer>();
        foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>!f.transform.IsChildOf(holder)).ToArray())
        {
            var r=f.GetComponent<MeshRenderer>();if(!r||!r.enabled||!f.sharedMesh||f.GetComponentInParent<LODGroup>())continue;
            var c=root.InverseTransformPoint(r.bounds.center);var bounds=new Bounds(c,r.bounds.size);
            if(bounds.max.x<Mask.xMin||bounds.min.x>Mask.xMax||bounds.max.z<Mask.yMin||bounds.min.z>Mask.yMax||bounds.max.y<.5f)continue;
            var source=f.sharedMesh;var matrix=root.worldToLocalMatrix*f.transform.localToWorldMatrix;var p=source.vertices;var n=source.normals;var uv=source.uv;var colors=source.colors;
            Vertex Vtx(int i)=>new Vertex{p=matrix.MultiplyPoint3x4(p[i]),n=matrix.MultiplyVector(n.Length==p.Length?n[i]:Vector3.up).normalized,uv=uv.Length==p.Length?uv[i]:Vector2.zero,c=colors.Length==p.Length?colors[i]:Color.white};
            var output=new List<Vertex>();var indices=new List<int>[source.subMeshCount];bool changed=false;
            void Append(List<Vertex> poly,List<int> ids){for(int i=1;i<poly.Count-1;i++){int at=output.Count;output.Add(poly[0]);output.Add(poly[i]);output.Add(poly[i+1]);ids.AddRange(new[]{at,at+1,at+2});}}
            for(int s=0;s<indices.Length;s++)
            {
                indices[s]=new List<int>();var triangles=source.GetTriangles(s);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var original=new List<Vertex>{Vtx(triangles[i]),Vtx(triangles[i+1]),Vtx(triangles[i+2])};var remain=original;var outside=new List<List<Vertex>>();
                    for(int plane=0;plane<6&&remain.Count>0;plane++){outside.Add(Cut(remain,plane,false));remain=Cut(remain,plane,true);}
                    if(remain.Count>=3){changed=true;foreach(var poly in outside)Append(poly,indices[s]);}else Append(original,indices[s]);
                }
            }
            if(!changed)continue;r.enabled=false;suppressed.Add(r);if(output.Count==0)continue;
            var mesh=new Mesh{name="Information retained surroundings",indexFormat=IndexFormat.UInt32};mesh.SetVertices(output.Select(v=>v.p).ToList());mesh.SetNormals(output.Select(v=>v.n).ToList());mesh.SetUVs(0,output.Select(v=>v.uv).ToList());mesh.SetColors(output.Select(v=>v.c).ToList());mesh.subMeshCount=indices.Length;for(int s=0;s<indices.Length;s++)mesh.SetTriangles(indices[s],s);mesh.RecalculateBounds();
            mesh=Save(mesh,Folder+"/Meshes/Retained-"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source))+".asset");
            var go=new GameObject("Information preserved surroundings");go.transform.SetParent(holder,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var rr=go.AddComponent<MeshRenderer>();rr.sharedMaterials=r.sharedMaterials;rr.shadowCastingMode=r.shadowCastingMode;
        }
        holder.gameObject.AddComponent<CourtyardRevision>().suppressedSources=suppressed.ToArray();
    }
    public static void ApplyOptimizeAndBuild(){Apply();CampusPerformanceBuild.OptimizeAndBuild();}
    public static void FinishAndBuild()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var root=GameObject.Find("开发区校区 · 校园环境").transform.Find(Group);
        foreach(var text in root.GetComponentsInChildren<TextMesh>())text.color=new Color(.055f,.045f,.035f);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);CampusPerformanceBuild.OptimizeAndBuild();
    }
}
