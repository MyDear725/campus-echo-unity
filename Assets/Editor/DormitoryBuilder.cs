using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Native Unity geometry. Footprints stay in source-map coordinates, under the
// existing reflected campus root. No browser exporter or external model is needed.
public static class DormitoryBuilder
{
    const string Folder = "Assets/DormitoryRevision";
    const string Scene = "Assets/Scenes/CampusLiving.unity";
    const string Group = "宿舍 · 参考照片外立面";
    const int Floors = 8;
    const float Storey = 2.85f, Ground = .18f;
    static readonly int[] Ids = {77931,77933,77935,77937,77938,77941};
    static readonly HashSet<int> ArchitectureMaterials = new HashSet<int> {5,6,7,8,9,10,11,75,76,77,131,132,133,134,135,136,137,138,139};
    static readonly Dictionary<int,Vector2> Entries = new Dictionary<int,Vector2> {
        {77931,new Vector2(269,-119)}, {77933,new Vector2(236,-119)},
        {77935,new Vector2(211.24f,-99.81f)}, {77937,new Vector2(214.47f,-77.2f)},
        {77938,new Vector2(241.45f,-72.07f)}, {77941,new Vector2(187,59)}
    };
    static Dictionary<string,Material> materials;
    static Dictionary<string,Geometry> batches;
    static int balconies, airUnits, garments;

    [MenuItem("Campus/Apply Reference Dormitories")]
    public static void Generate()
    {
        EditorSceneManager.OpenScene(Scene);
        var root = GameObject.Find("开发区校区 · 校园环境").transform;
        CafeteriaBuilder.RestoreSources(root);
        ResidenceCourtyardBuilder.RestoreSources(root);
        var previous = root.Find(Group);
        if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var holder = new GameObject(Group).transform;
        holder.SetParent(root, false);
        Directory.CreateDirectory(Folder + "/Meshes");
        Directory.CreateDirectory(Folder + "/Materials");
        AssetDatabase.Refresh();
        var game = UnityEngine.Object.FindFirstObjectByType<CampusGame>();
        // Always start from the original survey, so rebuilding is deterministic.
        string baseline=File.ReadAllText("Assets/LivingRevision/CampusMap.json");
        var data = JsonUtility.FromJson<CampusData>(baseline);
        var export = JsonUtility.FromJson<ExportData>(baseline);
        var dorms = data.features.Where(f => Ids.Contains(f.id)).ToArray();
        if (dorms.Length != 6) throw new Exception("Expected six existing dormitory footprints.");
        int trimmed = ReplaceOldFacades(root, holder, dorms, export);
        ResidenceCourtyardBuilder.Reposition(data);
        MakeMaterials();
        balconies = airUnits = garments = 0;
        foreach (var dorm in dorms) BuildDorm(dorm, holder);
        ResidenceCourtyardBuilder.Generate(root,holder,data,game);
        CafeteriaBuilder.Apply(root,data,game);
        // Raise camera occluders with the new roof; retain every horizontal footprint.
        foreach (var wall in GameObject.Find("Camera collision surfaces").GetComponentsInChildren<BoxCollider>())
            if (dorms.Any(f => wall.name == f.name + " wall")) {
                var size = wall.size; size.y = Floors * Storey + 1.2f; wall.size = size;
                var p = wall.transform.position; p.y = size.y / 2; wall.transform.position = p;
            }
        var preview = game.GetComponent<DormitoryPreview>();
        if (!preview) game.gameObject.AddComponent<DormitoryPreview>();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Scene);
        Directory.CreateDirectory("QA");
        File.WriteAllText("QA/dormitory-generation.txt", $"Five northern dormitories form an open U-shaped court; sixth dormitory retained. {Floors} storeys; roof {Ground+Floors*Storey:F2} m.\n{trimmed} old facade triangles replaced.\n{balconies} recessed balcony bays; {airUnits} AC units; {garments} garments.\nGenerated meshes: {holder.GetComponentsInChildren<MeshFilter>().Length}.\n");
        Debug.Log("DORMITORY_GENERATION_COMPLETE");
    }

    public static void GenerateAndBuild() { Generate(); CampusBuilder.Build(); }

    static int ReplaceOldFacades(Transform root, Transform holder, CampusFeature[] dorms, ExportData export)
    {
        var candidates = new HashSet<int>(export.meshes.Where(m => ArchitectureMaterials.Contains(m.material)).Select(m => m.id));
        int removed = 0;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>()) {
            string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
            if (!path.StartsWith("Assets/LivingRevision/Meshes/") || !int.TryParse(Path.GetFileNameWithoutExtension(path), out int id) || !candidates.Contains(id)) continue;
            var mesh = filter.sharedMesh;
            var bounds = mesh.bounds;
            if (!dorms.Any(f => f.points.Max(p=>p.x)+3 > bounds.min.x && f.points.Min(p=>p.x)-3 < bounds.max.x && f.points.Max(p=>p.z)+3 > bounds.min.z && f.points.Min(p=>p.z)-3 < bounds.max.z)) continue;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var keep = new List<int>();
            for (int t=0;t<triangles.Length;t+=3) {
                var c=(vertices[triangles[t]]+vertices[triangles[t+1]]+vertices[triangles[t+2]])/3;
                bool replace = c.y > .06f && dorms.Any(f => NearFootprint(c, f.points, 2.8f));
                if (replace) removed++; else { keep.Add(triangles[t]);keep.Add(triangles[t+1]);keep.Add(triangles[t+2]); }
            }
            if (keep.Count == triangles.Length) continue;
            var originalRenderer=filter.GetComponent<MeshRenderer>(); originalRenderer.enabled=false;
            if (keep.Count == 0) continue;
            // Compact the retained mesh, never edit the original imported asset.
            var indexMap = new Dictionary<int,int>(); var remap = new List<int>();
            for(int t=0;t<keep.Count;t++) {int old=keep[t]; if(!indexMap.TryGetValue(old,out int next)){next=remap.Count;indexMap.Add(old,next);remap.Add(old);}keep[t]=next;}
            var copy=new Mesh {name="Retained architecture "+id,indexFormat=IndexFormat.UInt32};
            copy.vertices=remap.Select(i=>vertices[i]).ToArray();
            var normals=mesh.normals; var uv=mesh.uv;var colors=mesh.colors;
            if(normals.Length==vertices.Length)copy.normals=remap.Select(i=>normals[i]).ToArray();
            if(uv.Length==vertices.Length)copy.uv=remap.Select(i=>uv[i]).ToArray();
            if(colors.Length==vertices.Length)copy.colors=remap.Select(i=>colors[i]).ToArray();
            copy.triangles=keep.ToArray();copy.RecalculateBounds();
            copy=Save(copy,$"{Folder}/Meshes/Retained-{id}.asset");
            var obj=new GameObject(copy.name);obj.transform.SetParent(holder,false);
            obj.AddComponent<MeshFilter>().sharedMesh=copy;
            var r=obj.AddComponent<MeshRenderer>();r.sharedMaterials=originalRenderer.sharedMaterials;r.shadowCastingMode=originalRenderer.shadowCastingMode;
        }
        return removed;
    }

    static bool NearFootprint(Vector3 p, MapPoint[] points, float margin)
    {
        if(CampusCollision.Inside(p.x,p.z,points))return true;
        for(int i=0;i<points.Length;i++) {
            var a=new Vector2(points[i].x,points[i].z);var b=new Vector2(points[(i+1)%points.Length].x,points[(i+1)%points.Length].z);
            var delta=b-a;float t=Mathf.Clamp01(Vector2.Dot(new Vector2(p.x,p.z)-a,delta)/delta.sqrMagnitude);
            if(Vector2.Distance(new Vector2(p.x,p.z),a+delta*t)<margin)return true;
        }
        return false;
    }

    static T Save<T>(T value,string path) where T:UnityEngine.Object
    {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(value,path);return value;
    }

    static void MakeMaterials()
    {
        materials=new Dictionary<string,Material>();
        Mat("Tile","#d8d5c9",.2f,1);
        Mat("Stone","#bcbcaf",.25f);
        Mat("Roof","#7c817c",.15f);
        Mat("Metal","#414c4e",.65f);
        Mat("Aluminum","#b2bab7",.8f);
        Mat("Interior","#424742",.1f);
        Mat("Curtain","#c8c0ac",.1f);
        Mat("AC","#c2c7bf",.35f);
        Mat("Glass","#91adb8",.85f,0,true);
        Mat("White cloth","#e2ded2",.05f);
        Mat("Blue cloth","#607b89",.05f);
        Mat("Rose cloth","#a7837f",.05f);
        Mat("Dark cloth","#485357",.05f);
    }
    static void Mat(string name,string hex,float smooth,float tile=0,bool glass=false)
    {
        // Color shader properties are converted by Unity in the linear project.
        // Supplying Color.linear here would apply that conversion a second time.
        ColorUtility.TryParseHtmlString(hex,out var color);color.a=glass?.32f:1;
        var m=new Material(Shader.Find("Campus/Dormitory")){name=name};
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Tile",tile);m.SetFloat("_Glass",glass?1:0);
        m.SetFloat("_Cull",0);m.SetFloat("_SrcBlend",glass?5:1);m.SetFloat("_DstBlend",glass?10:0);m.SetFloat("_ZWrite",glass?0:1);
        if(glass){m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");}
        materials[name]=Save(m,$"{Folder}/Materials/{name}.mat");
    }

    class Edge
    {
        public Vector3 a,u,n;public float length;
        public Vector3 At(float x,float y,float depth)=>a+u*x+Vector3.up*y+n*depth;
    }

    static void BuildDorm(CampusFeature f,Transform holder)
    {
        batches=materials.Keys.ToDictionary(k=>k,k=>new Geometry());
        var edges=new List<Edge>();
        for(int i=0;i<f.points.Length;i++) {
            var p=f.points[i];var q=f.points[(i+1)%f.points.Length];var a=new Vector3(p.x,0,p.z);var d=new Vector3(q.x-p.x,0,q.z-p.z);
            var u=d.normalized;var n=new Vector3(-u.z,0,u.x);var test=a+d*.5f+n*.05f;
            if(CampusCollision.Inside(test.x,test.z,f.points))n=-n;
            edges.Add(new Edge{a=a,u=u,n=n,length=d.magnitude});
        }
        Vector2 anchor=ResidenceCourtyardBuilder.Entry(f.id,Entries[f.id]);
        var entry=edges.Where(e=>e.length>6).OrderBy(e=>Vector2.Distance(new Vector2(e.At(e.length*.5f,0,0).x,e.At(e.length*.5f,0,0).z),anchor)).First();
        foreach(var edge in edges) Elevation(edge,edge==entry,f.id);
        Roof(f.points,Ground+Floors*Storey);
        var building=new GameObject(f.id==77935?"西侧长条公寓（名称待确认）":f.name).transform;building.SetParent(holder,false);
        foreach(var pair in batches.Where(b=>b.Value.vertices.Count>0)) {
            var mesh=pair.Value.Mesh(f.id+" "+pair.Key);
            mesh=Save(mesh,$"{Folder}/Meshes/{f.id}-{pair.Key}.asset");
            var obj=new GameObject(pair.Key);obj.transform.SetParent(building,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=materials[pair.Key];r.shadowCastingMode=pair.Key=="Glass"?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;
        }
    }

    static void Part(Edge e,string mat,float x,float y,float w,float h,float d,float offset)
    {batches[mat].Box(e.At(x,y,offset),e.u,Vector3.up,e.n,new Vector3(w,h,d));}

    static void Elevation(Edge e,bool entrance,int seed)
    {
        float length=e.length, height=Ground+Floors*Storey;
        bool balconySide=length>14;
        int count=Mathf.Max(1,Mathf.FloorToInt(length/(balconySide?3.05f:3.4f)));
        if(entrance && count%2==0)count--;
        float bay=length/count;int stair=entrance?count/2:-1;
        float entryCenter=(stair+.5f)*bay,entryWidth=Mathf.Min(length*.55f,7.6f);
        // The rear wall is behind the glazing: balcony depth is real geometry.
        Part(e,balconySide?"Interior":"Tile",length/2,height/2,length,height,.25f,balconySide?-2.05f:-.18f);
        HorizontalOutsideDoor(e,"Stone",length,.22f,.42f,.36f,.03f,entrance,entryCenter,entryWidth);
        Part(e,"Tile",length/2,height+.38f,length+.06f,.76f,.4f,-.02f);
        Part(e,"Stone",length/2,height+.8f,length+.14f,.09f,.48f,-.02f);
        for(int j=0;j<=count;j++) {
            float x=Mathf.Clamp(j*bay,.18f,length-.18f);
            float bottom=entrance&&Mathf.Abs(x-entryCenter)<entryWidth/2?3.5f:0;
            if(balconySide)Part(e,"Tile",x,(height+bottom)/2,.36f,height-bottom,1.65f,-.65f);
            if(j%2==0)Part(e,"Metal",x,height+.96f,.025f,.3f,.025f,-.1f);
        }
        Part(e,"Metal",length/2,height+1.1f,length,.035f,.035f,-.1f);
        for(int floor=0;floor<Floors;floor++) {
            float y=Ground+floor*Storey;
            if(balconySide)HorizontalOutsideDoor(e,"Tile",length,y+.19f,.38f,1.9f,-.72f,entrance&&floor==0,entryCenter,entryWidth);
            for(int j=0;j<count;j++) {
                float x=(j+.5f)*bay,w=bay-.48f;
                if(j==stair) {
                    if(floor==0) {
                        Part(e,"Tile",x-w/2-.1f,(height+3.5f)/2,.27f,height-2.3f,.6f,.17f);
                        Part(e,"Tile",x+w/2+.1f,(height+3.5f)/2,.27f,height-2.3f,.6f,.17f);
                    }
                    if(floor==0)continue;
                    Window(e,x,y+1.55f,w,2.37f,-.28f,false,seed+j+floor,true);
                    Part(e,"Tile",x,y+.26f,w,.45f,.5f,-.08f);
                    continue;
                }
                if(floor==0&&entrance&&Mathf.Abs(x-entryCenter)<entryWidth/2)continue;
                float windowWidth=balconySide?w-.18f:Mathf.Min(1.28f,w*.65f);
                Window(e,x,y+1.55f,windowWidth,1.98f,balconySide?-1.39f:.07f,true,seed+j+floor,false);
                if(balconySide) {
                    Part(e,"Tile",x,y+.5f,w,.62f,.16f,-1.42f);
                    if(floor>0) {
                        balconies++;
                        Rail(e,x,w,y+.42f,.08f);
                        if((seed+j+floor)%4!=0)Laundry(e,x,y+2.48f,w,seed+floor*13+j*7);
                        if(j%2==0)AirConditioner(e,j*bay+.22f,y+1.2f);
                    }
                } else Part(e,"Stone",x,y+.5f,windowWidth+.18f,.1f,.38f,.16f);
            }
        }
        // Rainwater pipe and brackets on each end pier.
        foreach(float x in new[]{.16f,length-.16f}) {
            Part(e,"AC",x,height/2,.065f,height,.065f,.24f);
            for(int k=1;k<Floors;k++)Part(e,"Metal",x,k*Storey,.13f,.035f,.1f,.22f);
        }
        if(entrance)Entrance(e,entryCenter,entryWidth);
    }

    static void HorizontalOutsideDoor(Edge e,string mat,float length,float y,float h,float depth,float offset,bool door,float center,float width)
    {
        if(!door){Part(e,mat,length/2,y,length,h,depth,offset);return;}
        float left=center-width/2,right=center+width/2;
        if(left>0)Part(e,mat,left/2,y,left,h,depth,offset);
        if(right<length)Part(e,mat,(right+length)/2,y,length-right,h,depth,offset);
    }

    static void Window(Edge e,float x,float y,float w,float h,float offset,bool curtains,int seed,bool stair)
    {
        if(offset>-.5f)Part(e,"Interior",x,y,w,h,.04f,offset-.08f);
        batches["Glass"].Quad(e.At(x-w/2,y-h/2,offset),e.At(x+w/2,y-h/2,offset),e.At(x+w/2,y+h/2,offset),e.At(x-w/2,y+h/2,offset));
        foreach(float dx in new[]{-w/2,0,w/2})Part(e,"Aluminum",x+dx,y,.045f,h+.07f,.065f,offset+.035f);
        foreach(float dy in new[]{-h/2,h*.22f,h/2})Part(e,"Aluminum",x,y+dy,w,.045f,.065f,offset+.035f);
        if(stair){Part(e,"Aluminum",x-w*.25f,y,.035f,h,.055f,offset+.04f);Part(e,"Aluminum",x+w*.25f,y,.035f,h,.055f,offset+.04f);}
        if(curtains&&offset<-.5f) {
            for(int side=-1;side<=1;side+=2) {
                float cw=w*((seed+side)%3==0?.3f:.17f);
                for(int k=0;k<5;k++)Part(e,"Curtain",x+side*(w/2-cw/2)+cw*(k-2)/5,y,cw/5+.01f,h-.06f,.035f,offset-.18f-(k%2)*.035f);
            }
        }
    }

    static void Rail(Edge e,float x,float w,float y,float depth)
    {
        foreach(float rise in new[]{.12f,.64f,.91f})Part(e,"Metal",x,y+rise,w,.035f,.035f,depth);
        int bars=Mathf.CeilToInt(w/.17f);
        for(int k=0;k<=bars;k++)Part(e,"Metal",x-w/2+w*k/bars,y+.47f,.022f,.91f,.022f,depth);
    }

    static void AirConditioner(Edge e,float x,float y)
    {
        airUnits++;
        Part(e,"AC",x,y,.62f,.5f,.32f,.37f);
        Part(e,"Metal",x,y-.29f,.66f,.045f,.48f,.3f);
        // Circular grille, central motor and radial blades, rather than a black square.
        Vector3 center=e.At(x-.06f,y,.538f);
        batches["Metal"].Disc(center,e.u,Vector3.up,.19f,24);
        for(int ring=1;ring<=4;ring++)batches["Aluminum"].Ring(center+e.n*.008f,e.u,Vector3.up,ring*.044f,.006f,24);
        for(int k=0;k<6;k++){float a=k*Mathf.PI/3;batches["AC"].Beam(center,center+(e.u*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a))*.18f,.012f);}
        for(int k=0;k<5;k++)Part(e,"Metal",x+.23f,y-.13f+k*.06f,.1f,.018f,.02f,.54f);
        Part(e,"AC",x+.35f,y-.25f,.025f,.58f,.025f,.23f);
    }

    static void Laundry(Edge e,float x,float y,float w,int seed)
    {
        Part(e,"Aluminum",x,y+.18f,w-.14f,.022f,.022f,-.47f);
        int n=2+Math.Abs(seed)%3;string[] palette={"White cloth","Blue cloth","White cloth","Rose cloth","Dark cloth"};
        for(int i=0;i<n;i++) {
            garments++;float xx=x+(i-(n-1)/2f)*.43f;float drop=(seed+i)%2==0?.65f:.82f;
            var geom=batches[palette[Math.Abs(seed+i*3)%palette.Length]];
            // Shaped shoulders and sleeves; hem folds sit at different depths.
            Vector2[] silhouette={new Vector2(-.10f,0),new Vector2(.10f,0),new Vector2(.25f,-.14f),new Vector2(.21f,-.30f),new Vector2(.14f,-.25f),new Vector2(.15f,-drop),new Vector2(-.15f,-drop),new Vector2(-.14f,-.25f),new Vector2(-.21f,-.30f),new Vector2(-.25f,-.14f)};
            if((seed+i)%4==0) {
                // Two separately draped trouser legs leave a visible gap.
                for(int leg=-1;leg<=1;leg+=2)geom.Quad(e.At(xx+leg*.09f-.065f,y,-.47f),e.At(xx+leg*.09f+.065f,y,-.47f),e.At(xx+leg*.12f+.055f,y-.92f,-.42f),e.At(xx+leg*.12f-.055f,y-.92f,-.43f));
                silhouette=new[]{new Vector2(-.16f,0),new Vector2(.16f,0),new Vector2(.16f,-.18f),new Vector2(-.16f,-.18f)};
                drop=.22f;
            } else if((seed+i)%5==0) {
                silhouette=new[]{new Vector2(-.2f,0),new Vector2(.2f,0),new Vector2(.19f,-.78f),new Vector2(-.21f,-.81f)};
            }
            var center=e.At(xx,y-drop*.38f,-.49f);
            for(int k=0;k<silhouette.Length;k++) {
                var a=silhouette[k];var b=silhouette[(k+1)%silhouette.Length];
                geom.Triangle(center,e.At(xx+a.x,y+a.y,-.46f+Mathf.Sin(k+seed)*.028f),e.At(xx+b.x,y+b.y,-.46f+Mathf.Sin(k+1+seed)*.028f));
            }
            batches["Metal"].Beam(e.At(xx,y+.18f,-.47f),e.At(xx-.13f,y-.01f,-.47f),.012f);
            batches["Metal"].Beam(e.At(xx,y+.18f,-.47f),e.At(xx+.13f,y-.01f,-.47f),.012f);
        }
    }

    static void Entrance(Edge e,float x,float w)
    {
        Part(e,"Interior",x,1.55f,w-1.1f,2.8f,.15f,-.8f);
        Window(e,x,1.53f,w-1.25f,2.65f,-.35f,false,0,true);
        foreach(float dx in new[]{-.16f,.16f})Part(e,"Aluminum",x+dx,1.4f,.035f,.52f,.12f,-.23f);
        Part(e,"Tile",x,3.23f,w+.5f,.46f,2.8f,.8f);
        Part(e,"Stone",x,3.5f,w+.62f,.09f,2.9f,.8f);
        Part(e,"Metal",x,2.98f,w-.1f,.035f,2.45f,.8f);
        foreach(float dx in new[]{-w/2+.3f,w/2-.3f}) {
            // Piers remain at the footprint edge, leaving the existing approach clear.
            Part(e,"Tile",x+dx,1.55f,.48f,3.1f,.56f,.05f);
            Part(e,"Metal",x+dx,1.72f,.2f,.52f,.045f,.35f);
        }
        // Thin threshold and steps inside the collision footprint, not across walk routes.
        for(int k=0;k<3;k++)Part(e,"Stone",x,.045f+k*.055f,w-.8f,.09f,.24f,-.1f-k*.24f);
        Part(e,"Metal",x+w/2-.7f,1.75f,.32f,.55f,.025f,.34f);
    }

    static void Roof(MapPoint[] p,float y)
    {
        // Ear clipping retains the concave outlines of the west and sixth buildings.
        var indices=Enumerable.Range(0,p.Length).ToList();float area=0;
        for(int i=0;i<p.Length;i++)area+=p[i].x*p[(i+1)%p.Length].z-p[(i+1)%p.Length].x*p[i].z;
        float sign=Mathf.Sign(area);int limit=p.Length*p.Length;
        while(indices.Count>2&&limit-->0) {
            bool found=false;
            for(int i=0;i<indices.Count;i++) {
                int ai=indices[(i+indices.Count-1)%indices.Count],bi=indices[i],ci=indices[(i+1)%indices.Count];
                var a=new Vector2(p[ai].x,p[ai].z);var b=new Vector2(p[bi].x,p[bi].z);var c=new Vector2(p[ci].x,p[ci].z);
                if(Cross(b-a,c-b)*sign<=.00001f)continue;
                if(indices.Any(k=>k!=ai&&k!=bi&&k!=ci&&InTriangle(new Vector2(p[k].x,p[k].z),a,b,c,sign)))continue;
                batches["Roof"].Triangle(new Vector3(a.x,y,a.y),new Vector3(b.x,y,b.y),new Vector3(c.x,y,c.y));indices.RemoveAt(i);found=true;break;
            }
            if(!found)throw new Exception("Cannot triangulate dormitory roof.");
        }
    }
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static bool InTriangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c,float sign)=>Cross(b-a,p-a)*sign>=0&&Cross(c-b,p-b)*sign>=0&&Cross(a-c,p-c)*sign>=0;

    internal class Geometry
    {
        public readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector3> normals=new List<Vector3>();readonly List<int> indices=new List<int>();
        public void Triangle(Vector3 a,Vector3 b,Vector3 c){int i=vertices.Count;vertices.AddRange(new[]{a,b,c});var n=Vector3.Cross(b-a,c-a).normalized;normals.AddRange(new[]{n,n,n});indices.AddRange(new[]{i,i+1,i+2});}
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
        public void Box(Vector3 c,Vector3 u,Vector3 v,Vector3 n,Vector3 size) {
            u*=size.x/2;v*=size.y/2;n*=size.z/2;
            Quad(c-u-v+n,c+u-v+n,c+u+v+n,c-u+v+n);Quad(c+u-v-n,c-u-v-n,c-u+v-n,c+u+v-n);
            Quad(c-u-v-n,c-u-v+n,c-u+v+n,c-u+v-n);Quad(c+u-v+n,c+u-v-n,c+u+v-n,c+u+v+n);
            Quad(c-u+v+n,c+u+v+n,c+u+v-n,c-u+v-n);Quad(c-u-v-n,c+u-v-n,c+u-v+n,c-u-v+n);
        }
        public void Beam(Vector3 a,Vector3 b,float width){var v=(b-a).normalized;var u=Vector3.Cross(v,Vector3.forward).normalized;if(u.sqrMagnitude<.1f)u=Vector3.right;Box((a+b)/2,u,v,Vector3.Cross(u,v),new Vector3(width,(b-a).magnitude,width));}
        public void Disc(Vector3 c,Vector3 u,Vector3 v,float radius,int count){for(int i=0;i<count;i++){float a=i*2*Mathf.PI/count,b=(i+1)*2*Mathf.PI/count;Triangle(c,c+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius,c+(u*Mathf.Cos(b)+v*Mathf.Sin(b))*radius);}}
        public void Ring(Vector3 c,Vector3 u,Vector3 v,float radius,float width,int count){for(int i=0;i<count;i++){float a=i*2*Mathf.PI/count,b=(i+1)*2*Mathf.PI/count;var d=u*Mathf.Cos(a)+v*Mathf.Sin(a);var e=u*Mathf.Cos(b)+v*Mathf.Sin(b);Quad(c+d*radius,c+e*radius,c+e*(radius-width),c+d*(radius-width));}}
        public Mesh Mesh(string name){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(vertices);m.SetNormals(normals);m.SetTriangles(indices,0);m.RecalculateBounds();return m;}
    }
}
