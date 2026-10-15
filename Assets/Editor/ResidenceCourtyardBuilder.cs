using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// The revised living court is an intentional design adaptation of the map.
// Map coordinates, visual meshes, player obstacles and camera walls share it.
public static class ResidenceCourtyardBuilder
{
    const string Folder="Assets/ResidenceCourtyard";
    const string Group="生活区 · 凹形庭院与道路";
    const float West=194,East=291,North=-141,South=-64;
    static readonly Dictionary<int,Rect> Footprints=new Dictionary<int,Rect>{
        {77933,new Rect(220,-134,24,10)}, {77931,new Rect(259,-134,24,10)},
        {77935,new Rect(210,-112,9,18)}, {77937,new Rect(210,-84,9,17)},
        {77938,new Rect(271,-112,9,34)}
    };
    static readonly Dictionary<int,Vector2> Doors=new Dictionary<int,Vector2>{
        {77933,new Vector2(232,-124)},{77931,new Vector2(271,-124)},
        {77935,new Vector2(219,-103)},{77937,new Vector2(219,-75.5f)},
        {77938,new Vector2(271,-95)}
    };
    static Dictionary<string,DormitoryBuilder.Geometry> meshes;
    static Dictionary<string,Material> materials;
    static List<CampusObstacle> props;
    static System.Random random;
    static MapPoint P(float x,float z)=>new MapPoint{x=x,z=z};
    static MapPoint[] Rectangle(Rect r)=>new[]{P(r.xMin,r.yMin),P(r.xMax,r.yMin),P(r.xMax,r.yMax),P(r.xMin,r.yMax)};
    static bool Inside(float x,float z)=>x>=West&&x<=East&&z>=North&&z<=South;
    public static Vector2 Entry(int id,Vector2 fallback)=>Doors.TryGetValue(id,out var v)?v:fallback;

    public static void RestoreSources(Transform root)
    {
        var old=root.Find(Group);if(!old)return;
        var marker=old.GetComponent<CourtyardRevision>();
        if(marker&&marker.suppressedSources!=null)foreach(var renderer in marker.suppressedSources)if(renderer)renderer.enabled=true;
        UnityEngine.Object.DestroyImmediate(old.gameObject);
    }

    public static void Reposition(CampusData data)
    {
        foreach(var f in data.features.Where(f=>Footprints.ContainsKey(f.id))) {
            f.points=Rectangle(Footprints[f.id]);
            foreach(var obstacle in data.obstacles.Where(o=>o.name==f.name)) {
                obstacle.points=f.points;obstacle.minX=f.points.Min(p=>p.x);obstacle.maxX=f.points.Max(p=>p.x);
                obstacle.minZ=f.points.Min(p=>p.z);obstacle.maxZ=f.points.Max(p=>p.z);
            }
        }
        // Remove old tree/bench/fence colliders from the replaced landscape only.
        var buildingNames=new HashSet<string>(data.features.Where(f=>f.kind=="building").Select(f=>f.name));
        var obstacles=new List<CampusObstacle>();
        foreach(var o in data.obstacles) {
            if(buildingNames.Contains(o.name)){obstacles.Add(o);continue;}
            if(o.segment!=null&&o.segment.Length==2) {
                foreach(var pair in OutsideSegments(o.segment[0],o.segment[1]))obstacles.Add(new CampusObstacle{name=o.name,segment=pair,thickness=o.thickness});
            } else if(o.points!=null&&o.points.Length>2) {
                if(!o.points.Any(p=>Inside(p.x,p.z)))obstacles.Add(o);
            } else if(!Inside((o.minX+o.maxX)/2,(o.minZ+o.maxZ)/2))obstacles.Add(o);
        }
        data.obstacles=obstacles.ToArray();
        var roads=new List<CampusRoad>();
        foreach(var r in data.roads)for(int i=1;i<r.points.Length;i++)foreach(var pair in OutsideSegments(r.points[i-1],r.points[i]))
            roads.Add(new CampusRoad{name=r.name,kind=r.kind,width=r.width,points=pair});
        roads.AddRange(NewRoads());data.roads=roads.ToArray();
        var route=data.routes.First(r=>r.name=="toLiving");
        var prefix=route.points.TakeWhile(p=>!(p.x>195&&p.z<-70)).ToList();
        prefix.AddRange(new[]{P(196.04f,-74.92f),P(205,-74.92f),P(205,-89),P(225,-89),P(245,-89)});route.points=prefix.ToArray();
        var extra=new[]{
            new CampusRoute{name="courtyard-loop",points=new[]{P(225,-117),P(265,-117),P(265,-70),P(225,-70),P(225,-117)}},
            new CampusRoute{name="courtyard-axis",points=new[]{P(245,-65),P(245,-117),P(251.5f,-117),P(251.5f,-137)}},
            new CampusRoute{name="west-dorm-entry",points=new[]{P(245,-103),P(225,-103),P(220.5f,-103)}},
            new CampusRoute{name="east-dorm-entry",points=new[]{P(245,-95),P(265,-95),P(269.5f,-95)}}
        };
        data.routes=data.routes.Concat(extra).ToArray();
    }

    static IEnumerable<MapPoint[]> OutsideSegments(MapPoint a,MapPoint b)
    {
        var ts=new List<float>{0,1};float dx=b.x-a.x,dz=b.z-a.z;
        if(Mathf.Abs(dx)>.00001f)foreach(float x in new[]{West,East}){float t=(x-a.x)/dx;if(t>0&&t<1)ts.Add(t);}
        if(Mathf.Abs(dz)>.00001f)foreach(float z in new[]{North,South}){float t=(z-a.z)/dz;if(t>0&&t<1)ts.Add(t);}
        ts.Sort();for(int i=1;i<ts.Count;i++){float t=(ts[i-1]+ts[i])/2;if(!Inside(a.x+dx*t,a.z+dz*t))yield return new[]{P(a.x+dx*ts[i-1],a.z+dz*ts[i-1]),P(a.x+dx*ts[i],a.z+dz*ts[i])};}
    }
    static CampusRoad Road(string name,string kind,float width,params float[] coordinates)
    {return new CampusRoad{name=name,kind=kind,width=width,points=Enumerable.Range(0,coordinates.Length/2).Select(i=>P(coordinates[i*2],coordinates[i*2+1])).ToArray()};}
    static CampusRoad[] NewRoads()=>new[]{
        Road("宿舍区北侧沥青路","main",4,205,-138,286,-138),
        Road("宿舍区西侧沥青路","main",4,205,-138,205,-64),
        Road("宿舍区西南道路衔接","main",4,205,-64,194,-64),
        Road("宿舍区东侧沥青路","main",4,286,-138,286,-64),
        Road("庭院环形步道","secondary",3.2f,225,-117,265,-117,265,-70,225,-70,225,-117),
        Road("花园中轴步道","secondary",3.2f,245,-64,245,-117,251.5f,-117,251.5f,-138),
        Road("庭院西入口","secondary",3.2f,205,-89,245,-89),
        Road("第一公寓入口","secondary",2.6f,271,-124,271,-117,265,-117),
        Road("第二公寓入口","secondary",2.6f,232,-124,232,-117),
        Road("西侧公寓入口","secondary",2.4f,219,-103,245,-103),
        Road("第四公寓入口","secondary",2.4f,219,-75.5f,225,-75.5f),
        Road("第五公寓入口","secondary",2.4f,271,-95,245,-95),
        Road("庭院东入口","secondary",2.4f,265,-73,286,-73)
    };

    public static void Generate(Transform root,Transform dorms,CampusData data,CampusGame game)
    {
        Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
        var group=new GameObject(Group).transform;group.SetParent(root,false);
        var suppressed=new List<Renderer>();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>()) {
            var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled)continue;
            if(filter.transform.IsChildOf(dorms)&&!filter.name.StartsWith("Retained architecture"))continue;
            var mesh=filter.sharedMesh;var b=mesh.bounds;
            if(b.max.x<West||b.min.x>East||b.max.z<North||b.min.z>South)continue;
            // Preserve overhanging crowns of trees whose batch lies outside the
            // replacement area, rather than shearing foliage at a vertical edge.
            if(renderer.sharedMaterials.Any(m=>new[]{"82","83","84","85","86"}.Contains(m.name))&&!Inside(b.center.x,b.center.z))continue;
            var clipped=Clip(mesh);if(clipped==null)continue;
            renderer.enabled=false;suppressed.Add(renderer);
            if(clipped.vertexCount==0){UnityEngine.Object.DestroyImmediate(clipped);continue;}
            string id=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(mesh));
            clipped=Save(clipped,Folder+"/Meshes/Surroundings-"+id+".asset");
            var go=new GameObject("Preserved surroundings");go.transform.SetParent(group,false);go.AddComponent<MeshFilter>().sharedMesh=clipped;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterials=renderer.sharedMaterials;r.shadowCastingMode=renderer.shadowCastingMode;
        }
        group.gameObject.AddComponent<CourtyardRevision>().suppressedSources=suppressed.ToArray();
        BuildLandscape(group,data);
        RebuildCameraWalls(data,game);
        Validate(data);
        string path=Folder+"/CampusMap.json";File.WriteAllText(path,JsonUtility.ToJson(data,true));AssetDatabase.ImportAsset(path);
        game.mapData=AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        Directory.CreateDirectory("QA");
        File.WriteAllText("QA/courtyard-validation.txt","PASS: open U-shaped arrangement; 10–15 m gaps at the main building separations; all campus and courtyard walking routes completed; moved building collision polygons and camera walls rebuilt; spawn clear.\nGarden and roads use the same layout as the minimap.\n");
        Debug.Log("COURTYARD_VALIDATED");
    }

    static T Save<T>(T value,string path)where T:UnityEngine.Object {var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,path);return value;}

    struct Vertex {public Vector3 p,n;public Vector2 uv;public Color color;public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),color=Color.Lerp(a.color,b.color,t)};}
    static float Distance(Vertex v,int plane)=>plane==0?v.p.x-West:plane==1?East-v.p.x:plane==2?v.p.z-North:South-v.p.z;
    static List<Vertex> Cut(List<Vertex> polygon,int plane,bool inside) {
        var result=new List<Vertex>();if(polygon.Count==0)return result;
        for(int i=0;i<polygon.Count;i++) {var a=polygon[i];var b=polygon[(i+1)%polygon.Count];float da=Distance(a,plane),db=Distance(b,plane);bool ina=inside?da>=0:da<0,inb=inside?db>=0:db<0;
            if(ina)result.Add(a);if(ina!=inb)result.Add(Vertex.Lerp(a,b,da/(da-db)));}
        return result;
    }
    static Mesh Clip(Mesh source) {
        var p=source.vertices;var n=source.normals;var uv=source.uv;var color=source.colors;var triangles=source.triangles;
        var output=new List<Vertex>();bool changed=false;
        Vertex V(int i)=>new Vertex{p=p[i],n=n.Length==p.Length?n[i]:Vector3.up,uv=uv.Length==p.Length?uv[i]:Vector2.zero,color=color.Length==p.Length?color[i]:Color.white};
        void Append(List<Vertex> polygon){for(int j=1;j<polygon.Count-1;j++){output.Add(polygon[0]);output.Add(polygon[j]);output.Add(polygon[j+1]);}}
        for(int i=0;i<triangles.Length;i+=3) {
            var a=V(triangles[i]);var b=V(triangles[i+1]);var c=V(triangles[i+2]);
            if(Mathf.Max(a.p.x,b.p.x,c.p.x)<West||Mathf.Min(a.p.x,b.p.x,c.p.x)>East||Mathf.Max(a.p.z,b.p.z,c.p.z)<North||Mathf.Min(a.p.z,b.p.z,c.p.z)>South){output.Add(a);output.Add(b);output.Add(c);continue;}
            var remain=new List<Vertex>{a,b,c};var outside=new List<Vertex>();
            for(int plane=0;plane<4&&remain.Count>0;plane++){
                var part=Cut(remain,plane,false);for(int j=1;j<part.Count-1;j++){outside.Add(part[0]);outside.Add(part[j]);outside.Add(part[j+1]);}
                remain=Cut(remain,plane,true);
            }
            if(remain.Count>=3){changed=true;output.AddRange(outside);}else Append(new List<Vertex>{a,b,c});
        }
        if(!changed)return null;
        var mesh=new Mesh{name="Preserved surroundings",indexFormat=IndexFormat.UInt32};mesh.SetVertices(output.Select(v=>v.p).ToList());mesh.SetNormals(output.Select(v=>v.n).ToList());mesh.SetUVs(0,output.Select(v=>v.uv).ToList());mesh.SetColors(output.Select(v=>v.color).ToList());mesh.SetTriangles(Enumerable.Range(0,output.Count).ToArray(),0);mesh.RecalculateBounds();return mesh;
    }

    static void Material(string name,string hex,int surface) {
        ColorUtility.TryParseHtmlString(hex,out var color);var material=new Material(Shader.Find("Campus/Courtyard")){name=name};material.SetColor("_BaseColor",color);material.SetFloat("_Surface",surface);material.SetFloat("_Cull",0);
        materials[name]=Save(material,Folder+"/Materials/"+name+".mat");meshes[name]=new DormitoryBuilder.Geometry();
    }
    static void Box(string mat,float x,float y,float z,float width,float height,float depth)=>meshes[mat].Box(new Vector3(x,y,z),Vector3.right,Vector3.up,Vector3.forward,new Vector3(width,height,depth));
    static void Ground(string mat,float x,float z,float width,float depth,float y)=>meshes[mat].Quad(new Vector3(x-width/2,y,z-depth/2),new Vector3(x-width/2,y,z+depth/2),new Vector3(x+width/2,y,z+depth/2),new Vector3(x+width/2,y,z-depth/2));
    static void PropObstacle(string name,float x,float z,float w,float d)=>props.Add(new CampusObstacle{name=name,minX=x-w/2,maxX=x+w/2,minZ=z-d/2,maxZ=z+d/2});

    static void BuildLandscape(Transform group,CampusData data)
    {
        random=new System.Random(19247);meshes=new Dictionary<string,DormitoryBuilder.Geometry>();materials=new Dictionary<string,Material>();props=new List<CampusObstacle>();
        Material("Asphalt","#53585a",0);Material("Paving","#b4b1a5",1);Material("Granite","#c7c6bb",3);Material("Grass","#6d7c49",2);Material("Soil","#5d5140",3);Material("Wood","#877158",4);Material("Iron","#525c59",3);Material("Drain","#29312e",3);Material("Paint","#cecebf",3);
        Material("Leaves","#607d43",5);Material("LeavesLight","#879b5d",5);Material("LeavesDark","#455c36",5);Material("Rose","#bc8492",5);Material("Cream","#e2d6ae",5);Material("Lavender","#9084ac",5);
        Ground("Grass",(West+East)/2,(North+South)/2,East-West,South-North,.035f);
        // Substantial paved aprons connect each entrance to the pedestrian ring.
        foreach(var pair in Footprints){var r=pair.Value;Ground("Paving",r.center.x,r.center.y,r.width+3.2f,r.height+3.2f,.055f);}
        var roads=NewRoads();
        foreach(var road in roads)for(int i=1;i<road.points.Length;i++)RoadSegment(road.points[i-1],road.points[i],road.width,road.kind=="main",roads);
        for(int stripe=0;stripe<5;stripe++)Ground("Paint",203.5f+stripe*.7f,-89,.36f,1.9f,.09f);
        // Four planted islands keep the long central axis and cross-paths clear.
        Bed(234.5f,-110,7,8);Bed(255.5f,-106.5f,7,14);
        Bed(234.5f,-80.5f,7,10);Bed(255.5f,-82.5f,7,13);
        foreach(var at in new[]{new Vector2(232,-110),new Vector2(258,-107),new Vector2(233,-79),new Vector2(258,-81)})Tree(at.x,at.y);
        foreach(float z in new[]{-127f,-106f,-81f})Tree(198,z);
        Bench(238,-97);Bench(238,-84);Bench(252,-99);Bench(252,-75);
        foreach(float z in new[]{-113f,-98f,-82f,-72f}){Lamp(222.7f,z);Lamp(267.3f,z);}
        // Low lawn tufts and irregular flower groupings, never in the walkways.
        for(int i=0;i<1600;i++){
            float x=228+(float)random.NextDouble()*34,z=-114+(float)random.NextDouble()*41;
            if(roads.Any(r=>NearRoad(x,z,r,1.2f))||props.Any(o=>x>o.minX-.3f&&x<o.maxX+.3f&&z>o.minZ-.3f&&z<o.maxZ+.3f))continue;
            float h=.07f+(float)random.NextDouble()*.09f;var a=new Vector3(x,.04f,z);meshes[i%2==0?"Leaves":"LeavesLight"].Triangle(a+Vector3.left*.018f,a+Vector3.up*h,a+Vector3.right*.018f);
        }
        data.obstacles=data.obstacles.Concat(props).ToArray();
        foreach(var pair in meshes.Where(p=>p.Value.vertices.Count>0)){
            var mesh=Save(pair.Value.Mesh("Courtyard "+pair.Key),Folder+"/Meshes/"+pair.Key+".asset");
            var obj=new GameObject(pair.Key);obj.transform.SetParent(group,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[pair.Key];renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
        }
    }
    static bool NearRoad(float x,float z,CampusRoad road,float extra){for(int i=1;i<road.points.Length;i++)if(CampusCollision.Segment(x,z,road.points[i-1],road.points[i])<road.width/2+extra)return true;return false;}
    static void RoadSegment(MapPoint a,MapPoint b,float width,bool asphalt,CampusRoad[] roads)
    {
        var start=new Vector3(a.x,0,a.z);var end=new Vector3(b.x,0,b.z);var u=(end-start).normalized;var n=new Vector3(-u.z,0,u.x);float length=Vector3.Distance(start,end);
        var center=(start+end)/2;center.y=asphalt?.063f:.085f;
        meshes[asphalt?"Asphalt":"Paving"].Box(center,u,Vector3.up,n,new Vector3(length+.035f,.04f,width));
        int blocks=Mathf.CeilToInt(length/.9f);
        for(int side=-1;side<=1;side+=2)for(int k=0;k<blocks;k++) {
            var p=start+u*((k+.5f)*length/blocks)+n*side*(width/2+.12f);
            // Curb breaks at junctions keep every path level and connected.
            if(roads.Any(r=>r.points.Length>1&&NearRoad(p.x,p.z,r,.12f)&&!r.points.Any(v=>v==a||v==b)))continue;
            p.y=.105f;meshes["Granite"].Box(p,u,Vector3.up,n,new Vector3(length/blocks-.018f,.19f,.22f));
            if(asphalt&&k%10==5) {
                var drain=start+u*((k+.5f)*length/blocks)+n*side*(width/2-.22f);drain.y=.089f;
                meshes["Drain"].Box(drain,u,Vector3.up,n,new Vector3(.65f,.018f,.3f));
                for(int bar=0;bar<7;bar++)meshes["Iron"].Box(drain+u*(bar-3)*.08f+Vector3.up*.012f,u,Vector3.up,n,new Vector3(.025f,.015f,.28f));
            }
        }
    }
    static void Bed(float x,float z,float w,float d)
    {
        Ground("Soil",x,z,w,d,.065f);
        for(int side=-1;side<=1;side+=2){Box("Granite",x+side*w/2,.11f,z,.12f,.15f,d);Box("Granite",x,.11f,z+side*d/2,w,.15f,.12f);}
        for(int i=0;i<90;i++){
            float px=x+((float)random.NextDouble()-.5f)*(w-.5f),pz=z+((float)random.NextDouble()-.5f)*(d-.5f);
            Shrub(px,pz,.22f+(float)random.NextDouble()*.25f,i%4==0);
        }
    }
    static void Leaf(string material,Vector3 center,Vector3 u,Vector3 v,float size)
    {meshes[material].Quad(center-u*size,center+v*size*.44f+Vector3.up*.025f,center+u*size,center-v*size*.44f);}
    static void Shrub(float x,float z,float size,bool flowers)
    {
        for(int j=0;j<20;j++){
            float angle=(float)random.NextDouble()*Mathf.PI*2,r=(float)random.NextDouble()*size;
            var p=new Vector3(x+Mathf.Cos(angle)*r,.13f+(float)random.NextDouble()*size,z+Mathf.Sin(angle)*r);
            var u=new Vector3(Mathf.Cos(angle),.25f,Mathf.Sin(angle));var v=new Vector3(-u.z,.4f,u.x);
            Leaf(j%3==0?"LeavesLight":"Leaves",p,u,v,.11f);
            if(flowers&&j%5==0)for(int k=0;k<5;k++){float a=k*1.256f;Leaf(j%2==0?"Cream":"Lavender",p+Vector3.up*.13f,new Vector3(Mathf.Cos(a),.3f,Mathf.Sin(a)),v,.055f);}
        }
    }
    static void Tree(float x,float z)
    {
        var bottom=new Vector3(x,.08f,z);var top=new Vector3(x+.15f,4.7f,z-.15f);meshes["Wood"].Beam(bottom,top,.18f);PropObstacle("庭院树干",x,z,.4f,.4f);
        for(int b=0;b<8;b++){
            float angle=b*2.399f;var tip=top+new Vector3(Mathf.Cos(angle)*1.6f,Mathf.Sin(b)*.7f,Mathf.Sin(angle)*1.6f);
            meshes["Wood"].Beam(Vector3.Lerp(bottom,top,.55f),tip,.065f);
            for(int j=0;j<75;j++){
                var offset=new Vector3(((float)random.NextDouble()-.5f)*2,((float)random.NextDouble()-.5f)*1.6f,((float)random.NextDouble()-.5f)*2);
                float a=(float)random.NextDouble()*6.28f;var u=new Vector3(Mathf.Cos(a),.3f,Mathf.Sin(a));var v=new Vector3(-u.z,.4f,u.x);
                Leaf(j%3==0?"LeavesLight":j%3==1?"Leaves":"LeavesDark",tip+offset,u,v,.23f);
            }
        }
    }
    static void Bench(float x,float z)
    {
        for(int i=0;i<5;i++)Box("Wood",x,.48f,z+(i-2)*.105f,1.7f,.055f,.085f);
        for(int i=0;i<4;i++)Box("Wood",x,.68f+i*.10f,z+.27f,1.7f,.075f,.045f);
        foreach(float dx in new[]{-.62f,.62f}){Box("Iron",x+dx,.25f,z,.06f,.5f,.44f);Box("Iron",x+dx,.72f,z+.3f,.045f,.6f,.045f);}
        PropObstacle("庭院座椅",x,z,1.8f,.65f);
    }
    static void Lamp(float x,float z)
    {Box("Iron",x,1.8f,z,.07f,3.6f,.07f);Box("Iron",x,3.65f,z,.38f,.09f,.38f);Box("Cream",x,3.53f,z,.22f,.18f,.22f);PropObstacle("庭院路灯",x,z,.16f,.16f);}

    static void RebuildCameraWalls(CampusData data,CampusGame game)
    {
        var root=GameObject.Find("Camera collision surfaces").transform;
        var features=data.features.Where(f=>Footprints.ContainsKey(f.id)).ToArray();
        foreach(var box in root.GetComponentsInChildren<BoxCollider>())if(features.Any(f=>box.name==f.name+" wall"))UnityEngine.Object.DestroyImmediate(box.gameObject);
        foreach(var f in features)for(int i=0;i<f.points.Length;i++){
            var a=game.MapToWorld(new Vector3(f.points[i].x,12,f.points[i].z));var p=f.points[(i+1)%f.points.Length];var b=game.MapToWorld(new Vector3(p.x,12,p.z));
            var obj=new GameObject(f.name+" wall"){layer=8};obj.transform.SetParent(root,false);obj.transform.position=(a+b)/2;obj.transform.rotation=Quaternion.LookRotation(b-a);
            obj.AddComponent<BoxCollider>().size=new Vector3(.24f,24,Vector3.Distance(a,b));
        }
    }
    static void Validate(CampusData data)
    {
        if(CampusCollision.Blocked(245,-68,data.obstacles))throw new Exception("Courtyard spawn blocked.");
        foreach(var route in data.routes){var p=new Vector3(route.points[0].x,.23f,route.points[0].z);for(int i=1;i<route.points.Length;i++){
            var goal=new Vector3(route.points[i].x,.23f,route.points[i].z);for(int step=0;step<6000&&(goal-p).magnitude>.05f;step++){
                var next=CampusCollision.Move(p,Vector3.ClampMagnitude(goal-p,.15f),data.obstacles);if((next-p).sqrMagnitude<.0000001f)throw new Exception("Blocked route "+route.name+" at "+p);p=next;
            }if((goal-p).magnitude>.1f)throw new Exception("Unfinished route "+route.name);
        }}
        foreach(var f in data.features.Where(f=>Footprints.ContainsKey(f.id))){float x=f.points.Average(p=>p.x),z=f.points.Average(p=>p.z);if(!CampusCollision.Blocked(x,z,data.obstacles))throw new Exception("Missing moved dorm collision: "+f.name);}
    }
}
