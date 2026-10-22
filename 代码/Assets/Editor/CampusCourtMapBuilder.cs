using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Additive revision only: keep the hand-authored campus and every interior intact.
public static class CampusCourtMapBuilder
{
    const string Folder="Assets/OutdoorBasketball",Group="室外篮球场 · 主路接驳";
    static Transform root,holder,physics;
    static Dictionary<string,DormitoryBuilder.Geometry> geo;
    static Dictionary<string,Material> mats;
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static T Save<T>(T value,string path)where T:Object
    {var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,path);return value;}
    static void Mat(string name,string hex,float kind=0,float metal=0)
    {
        ColorUtility.TryParseHtmlString(hex,out var c);var m=new Material(Shader.Find(name=="Paint"?"Campus/CourtPaint":"Campus/RefinedInterior")){name="Outdoor basketball · "+name};m.SetColor("_BaseColor",c);if(m.HasProperty("_Kind"))m.SetFloat("_Kind",kind);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",metal>0?.38f:.19f);if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metal);m.enableInstancing=true;
        mats[name]=Save(m,Folder+"/Materials/"+name+".mat");geo[name]=new DormitoryBuilder.Geometry();
    }
    static void Box(string m,Vector3 c,Vector3 size)=>geo[m].Box(c,Vector3.right,Vector3.up,Vector3.forward,size);
    static void Tube(string m,Vector3 a,Vector3 b,float radius,int sides=8)
    {
        var dir=(b-a).normalized;var u=Vector3.Cross(dir,Vector3.up).normalized;if(u.sqrMagnitude<.1f)u=Vector3.right;var v=Vector3.Cross(dir,u);
        for(int i=0;i<sides;i++){float aa=2*Mathf.PI*i/sides,bb=2*Mathf.PI*(i+1)/sides;var p=(u*Mathf.Cos(aa)+v*Mathf.Sin(aa))*radius;var q=(u*Mathf.Cos(bb)+v*Mathf.Sin(bb))*radius;geo[m].Quad(a+p,b+p,b+q,a+q);}
        geo[m].Disc(a,-u,v,radius,sides);geo[m].Disc(b,u,v,radius,sides);
    }
    static void Collider(string name,Vector3 at,Vector3 size)
    {var t=new GameObject(name){layer=9};t.transform.SetParent(physics,false);t.transform.localPosition=at;t.AddComponent<BoxCollider>().size=size;}
    [MenuItem("Campus/Apply basketball, cleanup and map")]
    public static void Apply()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");root=GameObject.Find("校园环境").transform;var game=Object.FindFirstObjectByType<CampusGame>();
        foreach(var dir in new[]{Folder,Folder+"/Meshes",Folder+"/Materials","Assets/Resources/CampusMap"})Directory.CreateDirectory(dir);AssetDatabase.Refresh();
        var old=root.Find(Group);if(old){var marker=old.GetComponent<CourtyardRevision>();if(marker)foreach(var r in marker.suppressedSources)if(r)r.enabled=true;Object.DestroyImmediate(old.gameObject);}
        holder=new GameObject(Group).transform;holder.SetParent(root,false);physics=new GameObject("Court physical surfaces").transform;physics.SetParent(holder,false);
        var report=new List<string>();CleanLegacy(report);
        geo=new Dictionary<string,DormitoryBuilder.Geometry>();mats=new Dictionary<string,Material>();
        Mat("Paving","#bab8af",1);Mat("Edge","#696c68",1);Mat("Runoff","#997b66",5);Mat("Court","#5e8583",5);Mat("Key","#486c7b",5);Mat("Paint","#ede9d9");Mat("Metal","#394c53",4,.45f);Mat("Padding","#36596a",3);Mat("White","#e2e2d9");Mat("Orange","#c87930",5);Mat("Seam","#312d28");Mat("Wood","#a1845b",2);Mat("Lens","#cbdfdf",4,.18f);
        ParkPaving();Collider("Basketball park ground",V(135.5f,.10f,-90.5f),V(23,.304f,75));
        foreach(float z in new[]{-109f,-73f})Court(135.5f,z);
        Fence();Approach();Facilities();
        foreach(var pair in geo.Where(p=>p.Value.vertices.Count>0))
        {
            var mesh=Save(pair.Value.Mesh(pair.Key),Folder+"/Meshes/"+pair.Key+".asset");var go=new GameObject("Court · "+pair.Key);go.transform.SetParent(holder,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mats[pair.Key];r.shadowCastingMode=pair.Key=="Paint"?ShadowCastingMode.Off:ShadowCastingMode.On;
        }
        var data=JsonUtility.FromJson<CampusData>(game.mapData.text);
        // Restore the original athletics feature exactly; only the teaching-side court is revised.
        var track=data.features.First(f=>f.id==39327169);track.name="校园运动场";track.kind="track";
        track.points=new[]{P(-166.57f,-55.38f),P(-165.40f,-153.75f),P(-104.01f,-153.66f),P(-102.27f,-54.06f)};
        var field=data.features.First(f=>f.id==39327816);field.name="室外篮球场";field.kind="court";
        field.points=new[]{P(124,-128),P(147,-128),P(147,-53),P(124,-53)};
        var added=new[]{Road("教学区篮球场南入口连接路",4.6f,118,-40,135.5f,-40,135.5f,-53),Road("教学区篮球场中入口连接路",3.6f,118,-91,124,-91),Road("教学区篮球场北入口连接路",4.6f,118,-137,135.5f,-137,135.5f,-128)};
        data.roads=data.roads.Where(r=>!r.name.StartsWith("篮球场")&&!r.name.StartsWith("教学区篮球场")).Concat(added).ToArray();
        File.WriteAllText(AssetDatabase.GetAssetPath(game.mapData),JsonUtility.ToJson(data,true));AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(game.mapData));
        BakeMap(data);
        var marker2=holder.gameObject.AddComponent<OutdoorBasketballLandmark>();marker2.courtCentres=new[]{V(135.5f,.256f,-109),V(135.5f,.256f,-73)};
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("QA");report.Add("CORRECTION: athletics feature 39327169 and its original renderers restored. Basketball revision targets feature 39327816 beside teaching building C only: two 15 x 28 metre courts fitted to its narrow footprint, 4 baskets with woven 3D nets and balls. Three gateways connect to the existing teaching-area east main road at z=-137, -91 and -40. Interiors, teaching buildings and the athletics grounds are preserved.");
        report.Add("Map is an in-game plan in existing map metres, north = -Z / east = +map X. All building features have live selectable labels and index entries, with bridge and new access roads shown. Source drawings constrain relative placement; not a surveyed map. Basketballs are scene props; no sports rules or shooting controls added.");
        File.WriteAllLines("QA/court-map-revision.txt",report);Debug.Log("COURT_MAP_APPLIED");
    }
    static MapPoint P(float x,float z)=>new MapPoint{x=x,z=z};
    static CampusRoad Road(string name,float width,params float[] xz)=>new CampusRoad{name=name,kind="secondary",width=width,points=Enumerable.Range(0,xz.Length/2).Select(i=>P(xz[i*2],xz[i*2+1])).ToArray()};
    static void Floor(string m,float x,float z,float w,float d)
    {const float y=.26f;geo[m].Quad(V(x-w/2,y,z-d/2),V(x-w/2,y,z+d/2),V(x+w/2,y,z+d/2),V(x+w/2,y,z-d/2));}
    static void ParkPaving()
    {
        // Disjoint surface patches, no stacked paving under court paint.
        Floor("Paving",124.25f,-90.5f,.5f,75);Floor("Paving",146.75f,-90.5f,.5f,75);
        Floor("Paving",135.5f,-126.5f,22,3);Floor("Paving",135.5f,-91,22,4);Floor("Paving",135.5f,-55,22,4);
    }
    static void Line(float x,float z,float xx,float zz,float y=.273f,float width=.05f)
    {var a=V(x,y,z);var b=V(xx,y,zz);var direction=(b-a).normalized;var side=Vector3.Cross(Vector3.up,direction)*width/2;geo["Paint"].Quad(a-side,b-side,b+side,a+side);}
    static void Arc(float x,float z,float radius,float a,float b,float width=.05f)
    {int n=Mathf.CeilToInt((b-a)/3);for(int i=0;i<n;i++){float aa=Mathf.Lerp(a,b,(float)i/n)*Mathf.Deg2Rad,bb=Mathf.Lerp(a,b,(float)(i+1)/n)*Mathf.Deg2Rad;Line(x+Mathf.Cos(aa)*radius,z+Mathf.Sin(aa)*radius,x+Mathf.Cos(bb)*radius,z+Mathf.Sin(bb)*radius,.273f,width);}}
    static void Court(float x,float z)
    {
        foreach(int s in new[]{-1,1}){Floor("Runoff",x+s*9.25f,z,3.5f,32);Floor("Runoff",x,z+s*15,15,2);Floor("Court",x+s*4.975f,z-11.1f,5.05f,5.8f);Floor("Court",x+s*4.975f,z+11.1f,5.05f,5.8f);}
        Floor("Court",x,z,15,16.4f);
        Line(x-7.5f,z-14,x+7.5f,z-14);Line(x-7.5f,z+14,x+7.5f,z+14);Line(x-7.5f,z-14,x-7.5f,z+14);Line(x+7.5f,z-14,x+7.5f,z+14);Line(x-7.5f,z,x+7.5f,z);Arc(x,z,1.8f,0,360);
        foreach(int side in new[]{-1,1})
        {
            float baseline=z+side*14,free=baseline-side*5.8f,basket=baseline-side*1.575f;
            Floor("Key",x,baseline-side*2.9f,4.9f,5.8f);
            Line(x-2.45f,baseline,x-2.45f,free);Line(x+2.45f,baseline,x+2.45f,free);Line(x-2.45f,free,x+2.45f,free);Arc(x,free,1.8f,side<0?0:180,side<0?180:360);
            // Three-point arc joins straight corner lines 0.90 m from the sidelines.
            Arc(x,basket,6.75f,side<0?12.5f:192.5f,side<0?167.5f:347.5f);
            float join=Mathf.Sin(12.5f*Mathf.Deg2Rad)*6.75f;Line(x-6.59f,baseline,x-6.59f,basket-side*join);Line(x+6.59f,baseline,x+6.59f,basket-side*join);
            Hoop(x,baseline,side);
        }
        Ball(V(x+6.3f,.383f,z+4.7f));
    }
    static void Hoop(float x,float baseline,int side)
    {
        const float y=.265f;float baseZ=baseline+side*1.0f,board=baseline-side*1.20f,rim=baseline-side*1.575f;
        Box("Padding",V(x,y+.4f,baseZ),V(1.25f,.8f,1.4f));Collider("Padded basket base",V(x,y+.4f,baseZ),V(1.25f,.8f,1.4f));
        Tube("Metal",V(x,y+.7f,baseZ),V(x,y+3.9f,baseZ-side*.35f),.105f,12);
        Tube("Metal",V(x,y+3.9f,baseZ-side*.35f),V(x,y+3.9f,board+side*.15f),.085f,12);
        Tube("Metal",V(x,y+2.7f,baseZ),V(x,y+3.45f,board+side*.12f),.055f,8);
        Box("Lens",V(x,y+3.575f,board),V(1.80f,1.05f,.055f));
        foreach(float dx in new[]{-.9f,.9f})Box("White",V(x+dx,y+3.575f,board-side*.035f),V(.035f,1.08f,.03f));
        foreach(float h in new[]{3.05f,4.1f})Box("White",V(x,y+h,board-side*.035f),V(1.83f,.035f,.03f));
        foreach(float dx in new[]{-.295f,.295f})Box("White",V(x+dx,y+3.3f,board-side*.038f),V(.04f,.45f,.03f));
        foreach(float h in new[]{3.075f,3.525f})Box("White",V(x,y+h,board-side*.038f),V(.63f,.04f,.03f));
        Tube("Orange",V(x,y+3.05f,board),V(x,y+3.05f,rim),.017f,8);
        for(int i=0;i<40;i++){float a=i*2*Mathf.PI/40,b=(i+1)*2*Mathf.PI/40;Tube("Orange",V(x+Mathf.Cos(a)*.225f,y+3.05f,rim+Mathf.Sin(a)*.225f),V(x+Mathf.Cos(b)*.225f,y+3.05f,rim+Mathf.Sin(b)*.225f),.01f,6);}
        // Crossing strands form actual diamond netting, with a free lower opening.
        for(int row=0;row<5;row++)for(int i=0;i<12;i++)foreach(int direction in new[]{-1,1})
        {
            float a=(i+(row%2)*.5f)*Mathf.PI/6,b=a+direction*Mathf.PI/12,r=.22f-row*.017f,rr=.22f-(row+1)*.017f;
            Tube("White",V(x+Mathf.Cos(a)*r,y+3.025f-row*.085f,rim+Mathf.Sin(a)*r),V(x+Mathf.Cos(b)*rr,y+3.025f-(row+1)*.085f,rim+Mathf.Sin(b)*rr),.0035f,4);
        }
    }
    static void Ball(Vector3 c)
    {
        // Share one smooth sphere mesh across both balls. Seams stay true 3D tubes.
        var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.name="Basketball · 24 cm";ball.layer=9;ball.transform.SetParent(holder,false);ball.transform.localPosition=c;ball.transform.localScale=Vector3.one*.24f;ball.GetComponent<Renderer>().sharedMaterial=mats["Orange"];
        foreach(var axis in new[]{Vector3.up,Vector3.right,Vector3.forward})
        {var u=axis==Vector3.up?Vector3.right:Vector3.up;var v=Vector3.Cross(axis,u);for(int i=0;i<48;i++){float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;Tube("Seam",c+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*.1205f,c+(u*Mathf.Cos(b)+v*Mathf.Sin(b))*.1205f,.0022f,4);}}
    }
    static void Fence()
    {
        void Segment(Vector3 a,Vector3 b)
        {
            int n=Mathf.CeilToInt(Vector3.Distance(a,b)/3);for(int i=0;i<=n;i++){var at=Vector3.Lerp(a,b,(float)i/n);Tube("Metal",at,at+Vector3.up*4,.045f,8);}
            foreach(float y in new[]{.25f,2f,4f})Tube("Metal",a+Vector3.up*y,b+Vector3.up*y,.022f,6);
            // Wide-spaced mesh, readable at walking distance without thousands of objects.
            float length=Vector3.Distance(a,b);var d=(b-a).normalized;
            for(float t=.13f;t<length;t+=.24f){var at=a+d*t;Tube("Metal",at+Vector3.up*.25f,at+Vector3.up*3.98f,.004f,4);}
            for(float y=.48f;y<4;y+=.24f)Tube("Metal",a+Vector3.up*y,b+Vector3.up*y,.004f,4);
            var middle=(a+b)/2;var go=new GameObject("Ball-stop fence"){layer=9};go.transform.SetParent(physics,false);go.transform.localPosition=middle+Vector3.up*2;go.transform.localRotation=Quaternion.LookRotation(b-a);go.AddComponent<BoxCollider>().size=V(.10f,4,length);
        }
        // South, west and east gates remain open. No collider spans these openings.
        Segment(V(147,.252f,-128),V(147,.252f,-53));
        foreach(var a in new[]{new[]{V(124,.252f,-128),V(124,.252f,-92.8f)},new[]{V(124,.252f,-89.2f),V(124,.252f,-53)},new[]{V(124,.252f,-128),V(133,.252f,-128)},new[]{V(138,.252f,-128),V(147,.252f,-128)},new[]{V(124,.252f,-53),V(133,.252f,-53)},new[]{V(138,.252f,-53),V(147,.252f,-53)}})Segment(a[0],a[1]);
    }
    static void Approach()
    {
        void Patch(string name,float x,float z,float w,float d){Floor("Paving",x,z,w,d);Collider(name,V(x,.1f,z),V(w,.32f,d));}
        Patch("South road join",129.4f,-40,16.8f,4.6f);Patch("South gate approach",135.5f,-47.65f,4.6f,10.7f);
        Patch("Middle main-road connector",122.5f,-91,3,3.6f);
        Patch("North road join",129.4f,-137,16.8f,4.6f);Patch("North gate approach",135.5f,-131.35f,4.6f,6.7f);
        foreach(float x in new[]{133.13f,137.87f}){Box("Edge",V(x,.25f,-47.65f),V(.12f,.06f,10.7f));Box("Edge",V(x,.25f,-131.35f),V(.12f,.06f,6.7f));}
    }
    static void Facilities()
    {
        foreach(float z in new[]{-119f,-101f,-83f,-65f})foreach(float x in new[]{145.7f})
        {
            foreach(float zz in new[]{-.8f,.8f})Box("Metal",V(x,.48f,z+zz),V(.55f,.40f,.13f));
            for(int s=0;s<5;s++)Box("Wood",V(x-.26f+s*.13f,.71f,z),V(.105f,.07f,2.4f));
        }
        foreach(float z in new[]{-126f,-91f,-55f})foreach(float x in new[]{148.2f})
        {Tube("Metal",V(x,.23f,z),V(x,6.2f,z),.06f,10);Box("Metal",V(x,6.2f,z),V(1.3f,.15f,.4f));Box("Lens",V(x,6.10f,z),V(1.15f,.03f,.32f));}
    }
    struct Vertex {public Vector3 p,n;public Vector2 uv;public Color c;public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),c=Color.Lerp(a.c,b.c,t)};}
    static List<Vertex> Cut(List<Vertex> p,int axis,float value,bool positive)
    {
        var output=new List<Vertex>();for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];float da=(a.p[axis]-value)*(positive?1:-1),db=(b.p[axis]-value)*(positive?1:-1);if(da>=0)output.Add(a);if((da>=0)!=(db>=0))output.Add(Vertex.Lerp(a,b,da/(da-db)));}return output;
    }
    static void CleanLegacy(List<string> report)
    {
        // No cutout or new surface lies anywhere near the west-side athletics grounds.
        var zones=new[]{new Rect(124,-128,23,75),new Rect(121,-42.3f,16.8f,4.6f),new Rect(133.2f,-53,4.6f,10.7f),new Rect(121,-92.8f,3,3.6f),new Rect(-16.5f,127.9f,86,45.1f),new Rect(121,-139.3f,16.8f,4.6f),new Rect(133.2f,-134.7f,4.6f,6.7f)};
        var suppressed=new List<Renderer>();int index=0,removed=0;
        foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>!f.transform.IsChildOf(holder)).ToArray())
        {
            var r=f.GetComponent<MeshRenderer>();if(!r||!r.enabled||!f.sharedMesh||f.GetComponentInParent<LODGroup>())continue;
            bool information=f.transform.IsChildOf(root.Find("信息楼 · 石材格栅与玻璃中庭"));
            if(information&&f.name!="Information preserved surroundings")continue;
            var center=root.InverseTransformPoint(r.bounds.center);var bounds=new Bounds(center,r.bounds.size);
            var relevant=zones.Select((v,i)=>new{v,i}).Where(z=>bounds.max.x>=z.v.xMin&&bounds.min.x<=z.v.xMax&&bounds.max.z>=z.v.yMin&&bounds.min.z<=z.v.yMax).ToArray();if(relevant.Length==0)continue;
            var mesh=f.sharedMesh;bool legacyInformation=information||AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/LivingRevision/",StringComparison.Ordinal);var m=root.worldToLocalMatrix*f.transform.localToWorldMatrix;var pp=mesh.vertices;var nn=mesh.normals;var uu=mesh.uv;var cc=mesh.colors;var vertices=new List<Vertex>();var indices=new List<int>[mesh.subMeshCount];bool changed=false;
            Vertex At(int i)=>new Vertex{p=m.MultiplyPoint3x4(pp[i]),n=m.MultiplyVector(nn.Length==pp.Length?nn[i]:Vector3.up).normalized,uv=uu.Length==pp.Length?uu[i]:Vector2.zero,c=cc.Length==pp.Length?cc[i]:Color.white};
            void Append(List<Vertex> p,List<int> ids){for(int i=1;i<p.Count-1;i++){int at=vertices.Count;vertices.Add(p[0]);vertices.Add(p[i]);vertices.Add(p[i+1]);ids.Add(at);ids.Add(at+1);ids.Add(at+2);}}
            for(int s=0;s<indices.Length;s++)
            {
                indices[s]=new List<int>();var tris=mesh.GetTriangles(s);
                for(int i=0;i<tris.Length;i+=3)
                {
                    var pieces=new List<List<Vertex>>{new List<Vertex>{At(tris[i]),At(tris[i+1]),At(tris[i+2])}};
                    foreach(var zone in relevant)
                    {
                        var next=new List<List<Vertex>>();foreach(var original in pieces)
                        {
                            // Preserve all road/soil planes. Clear old track and cut-off objects above them.
                            if(zone.i==4)
                            {
                                // The modern road meshes are 0.30 m high: protect them explicitly.
                                if(!legacyInformation||original.Max(v=>v.p.y)<=.05f){next.Add(original);continue;}
                            }
                            var remain=original;var outside=new List<List<Vertex>>();
                            foreach(var plane in new[]{(0,zone.v.xMin,true),(0,zone.v.xMax,false),(2,zone.v.yMin,true),(2,zone.v.yMax,false),(1,-.1f,true),(1,50f,false)})
                            {if(remain.Count<3)break;outside.Add(Cut(remain,plane.Item1,plane.Item2,!plane.Item3));remain=Cut(remain,plane.Item1,plane.Item2,plane.Item3);}
                            if(remain.Count>=3){changed=true;removed++;next.AddRange(outside.Where(p=>p.Count>=3));}else next.Add(original);
                        }pieces=next;
                    }
                    foreach(var p in pieces)Append(p,indices[s]);
                }
            }
            if(!changed)continue;suppressed.Add(r);r.enabled=false;
            report.Add("Clipped legacy mesh: "+f.name+" / "+AssetDatabase.GetAssetPath(mesh));if(vertices.Count==0)continue;
            var result=new Mesh{name="Court/map retained surroundings",indexFormat=IndexFormat.UInt32};result.SetVertices(vertices.Select(v=>v.p).ToList());result.SetNormals(vertices.Select(v=>v.n).ToList());result.SetUVs(0,vertices.Select(v=>v.uv).ToList());result.SetColors(vertices.Select(v=>v.c).ToList());result.subMeshCount=indices.Length;for(int s=0;s<indices.Length;s++)result.SetTriangles(indices[s],s);result.RecalculateBounds();
            result=Save(result,Folder+"/Meshes/Retained-"+(index++)+".asset");var go=new GameObject("Court/map preserved surroundings");go.transform.SetParent(holder,false);go.AddComponent<MeshFilter>().sharedMesh=result;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=r.sharedMaterials;renderer.shadowCastingMode=r.shadowCastingMode;
        }
        holder.gameObject.AddComponent<CourtyardRevision>().suppressedSources=suppressed.ToArray();report.Add("Removed/clipped legacy faces: "+removed+". Information cleanup only targets residual old meshes; the new facade, entry, trees with LOD, roads and street furniture remain.");
    }
    public static void ApplyAndBuild(){Apply();CampusPerformanceBuild.OptimizeAndBuild();}
    public static void Audit()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");var root=GameObject.Find("校园环境").transform;var rows=new List<string>();
        var filters=root.GetComponentsInChildren<MeshFilter>();
        foreach(var f in filters)
        {
            var r=f.GetComponent<MeshRenderer>();if(!r||!f.sharedMesh||f.GetComponentInParent<LODGroup>())continue;
            var c=root.InverseTransformPoint(r.bounds.center);var b=new Bounds(c,r.bounds.size);if(b.max.x< -16.5f||b.min.x>69.5f||b.max.z<127.9f||b.min.z>173||b.min.y>1)continue;
            rows.Add((r.enabled?"ON ":"OFF ")+f.name+" | "+AssetDatabase.GetAssetPath(f.sharedMesh)+" | "+string.Join(" / ",r.sharedMaterials.Select(m=>m?m.name:"NULL")));
            var mesh=f.sharedMesh;var p=mesh.vertices;var transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix;
            for(int s=0;s<mesh.subMeshCount;s++){var tri=mesh.GetTriangles(s);var heights=new SortedDictionary<float,int>();for(int i=0;i<tri.Length;i+=3){var a=transform.MultiplyPoint3x4(p[tri[i]]);var bb=transform.MultiplyPoint3x4(p[tri[i+1]]);var cc=transform.MultiplyPoint3x4(p[tri[i+2]]);var mid=(a+bb+cc)/3;if(mid.x< -16.5f||mid.x>69.5f||mid.z<127.9f||mid.z>173||mid.y>1)continue;float key=Mathf.Round(mid.y*100)/100;if(!heights.ContainsKey(key))heights[key]=0;heights[key]++;}if(heights.Count>0)rows.Add("  sub "+s+": "+string.Join(",",heights.Select(k=>k.Key+"="+k.Value)));}
        }
        File.WriteAllLines("QA/information-leftover-audit.txt",rows);
    }

    public static void RefreshMap(CampusData data)=>BakeMap(data);
    static void BakeMap(CampusData data)
    {
        const int w=1944,h=1397;var pixels=Enumerable.Repeat(new Color32(230,234,220,255),w*h).ToArray();float scale=w/CampusMapUI.Width;
        Color32 C(string hex){ColorUtility.TryParseHtmlString(hex,out var c);return c;}
        Vector2 Pixel(float x,float z)=>new Vector2((x-CampusMapUI.West)*scale,(z-CampusMapUI.North)*scale);
        void Put(int x,int y,Color32 c){if(x>=0&&x<w&&y>=0&&y<h)pixels[(h-1-y)*w+x]=c;}
        void Disc(float x,float z,float radius,Color32 color)
        {var p=Pixel(x,z);float r=radius*scale;for(int yy=Mathf.Max(0,(int)(p.y-r));yy<Mathf.Min(h,p.y+r);yy++)for(int xx=Mathf.Max(0,(int)(p.x-r));xx<Mathf.Min(w,p.x+r);xx++){float a=Mathf.Clamp01(r-Vector2.Distance(new Vector2(xx+.5f,yy+.5f),p));if(a>0){int i=(h-1-yy)*w+xx;pixels[i]=Color.Lerp(pixels[i],color,a);}}}
        void Stroke(MapPoint a,MapPoint b,float width,Color32 color)
        {
            var p=Pixel(a.x,a.z);var q=Pixel(b.x,b.z);float r=width*scale/2;
            for(int yy=Mathf.Max(0,(int)(Mathf.Min(p.y,q.y)-r-1));yy<Mathf.Min(h,Mathf.Max(p.y,q.y)+r+1);yy++)for(int xx=Mathf.Max(0,(int)(Mathf.Min(p.x,q.x)-r-1));xx<Mathf.Min(w,Mathf.Max(p.x,q.x)+r+1);xx++)
            {float t=Mathf.Clamp01(Vector2.Dot(new Vector2(xx+.5f,yy+.5f)-p,q-p)/Mathf.Max(.001f,(q-p).sqrMagnitude));float alpha=Mathf.Clamp01(r+.5f-Vector2.Distance(new Vector2(xx+.5f,yy+.5f),Vector2.Lerp(p,q,t)));if(alpha>0){int i=(h-1-yy)*w+xx;pixels[i]=Color.Lerp(pixels[i],color,alpha);}}
        }
        void Poly(MapPoint[] p,Color32 c)
        {int x0=Mathf.Max(0,(int)Pixel(p.Min(a=>a.x),p.Min(a=>a.z)).x),x1=Mathf.Min(w-1,(int)Pixel(p.Max(a=>a.x),p.Max(a=>a.z)).x);int y0=Mathf.Max(0,(int)Pixel(p.Min(a=>a.x),p.Min(a=>a.z)).y),y1=Mathf.Min(h-1,(int)Pixel(p.Max(a=>a.x),p.Max(a=>a.z)).y);for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)if(CampusCollision.Inside(CampusMapUI.West+(x+.5f)/scale,CampusMapUI.North+(y+.5f)/scale,p))Put(x,y,c);}
        var living=new[]{P(200,-150),P(303,-150),P(303,95),P(151,95)};Poly(living,C("#e5e5d8"));
        foreach(var f in data.features.Where(f=>f.kind=="hill"||f.kind=="water"||f.kind=="plaza"))
        {Poly(f.points,C(f.kind=="hill"?"#c4d3b5":f.kind=="water"?"#a3c9cf":"#ded9cb"));for(int i=0;i<f.points.Length;i++)Stroke(f.points[i],f.points[(i+1)%f.points.Length],.75f,C(f.kind=="water"?"#78a8b4":"#b7c6a7"));}
        var rng=new System.Random(217);for(int i=0;i<1000;i++){float x=-224+(float)rng.NextDouble()*530,z=-160+(float)rng.NextDouble()*362;if(data.features.Any(f=>f.kind!="hill"&&CampusCollision.Inside(x,z,f.points))||data.roads.Any(r=>CampusPlanRoadNetwork.Distance(x,z,r)<r.width/2+2))continue;Disc(x,z,1.0f+(float)rng.NextDouble()*1.5f,C(i%5==0?"#c9cfaa":"#becfaf"));}
        foreach(var road in data.roads)for(int i=1;i<road.points.Length;i++)Stroke(road.points[i-1],road.points[i],Mathf.Max(2,road.width)+1.1f,C("#b5bbb3"));
        foreach(var road in data.roads)for(int i=1;i<road.points.Length;i++)Stroke(road.points[i-1],road.points[i],Mathf.Max(2,road.width),C(road.kind=="public"?"#b0b9b9":"#f4f1e8"));
        // Independent street below bridge; the bridge is drawn last above it.
        Stroke(P(183,-168),P(155,220),18,C("#aeb8b6"));Stroke(P(183,-168),P(155,220),.6f,C("#e7e4d7"));
        foreach(var f in data.features.Where(f=>f.kind=="building"||f.kind=="court"||f.kind=="track"))
        {
            bool dorm=f.name.Contains("公寓")||f.name.Contains("宿舍");Color32 color=C(f.kind!="building"?"#a0b6a5":f.id==77943?"#d4b38e":dorm?"#cbbfa5":"#acc1c5");
            if(f.kind=="building")Poly(f.points.Select(p=>P(p.x+1.0f,p.z+1.4f)).ToArray(),C("#b4bcb0"));
            Poly(f.points,color);for(int i=0;i<f.points.Length;i++)Stroke(f.points[i],f.points[(i+1)%f.points.Length],.65f,C(f.kind=="building"?"#728c90":"#849c88"));
        }
        // Athletics symbol stays on the west; basketball details belong east of teaching C.
        foreach(float diameter in new[]{56f,53.6f,51.2f,48.8f,46.4f,44f,41.6f})
        {Stroke(P(-135,-125),P(-135,-83),diameter,C("#f3e8d8"));Stroke(P(-135,-125),P(-135,-83),diameter-.35f,C("#c98d7b"));}
        Stroke(P(-135,-125),P(-135,-83),38.5f,C("#a6bc91"));
        foreach(float x in new[]{135.5f})foreach(float z in new[]{-109f,-73f})
        {var p=new[]{P(x-7.5f,z-14),P(x+7.5f,z-14),P(x+7.5f,z+14),P(x-7.5f,z+14)};Poly(p,C("#7c9e9c"));for(int i=0;i<4;i++)Stroke(p[i],p[(i+1)%4],.28f,C("#f8f4e8"));Stroke(P(x-7.5f,z),P(x+7.5f,z),.28f,C("#f8f4e8"));}
        Stroke(P(91.8f,10),P(202,10),7.1f,C("#668982"));Stroke(P(91.8f,10),P(202,10),5.0f,C("#e5ece0"));
        var texture=new Texture2D(w,h,TextureFormat.RGB24,false);texture.SetPixels32(pixels);texture.Apply();string path="Assets/Resources/CampusMap/Plan.png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
    }
}
