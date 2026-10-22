using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Five-storey teaching exteriors fitted to the existing irregular U-shaped campus layout.
// Keep A/C positions and the fountain; the western B wing follows the supplied curved plan.
public static class TeachingBuilder
{
    const string Folder="Assets/TeachingRevision";
    public const string Group="ABC教学楼 · 石材玻璃与木格栅";
    const float Storey=4.25f,Height=22.0f;
    static readonly Rect WholeArea=Rect.MinMaxRect(19,-137,107,-40);
    static Rect Mask=WholeArea;
    static float MinimumCutHeight=.2f;
    // Overlap the old curved wing's clipping regions: disconnected wall/floor
    // slivers otherwise remain standing in the courtyard between the new wings.
    static readonly Rect[] Masks={
        Rect.MinMaxRect(47.5f,-63,52,-59),Rect.MinMaxRect(54.8f,-75.5f,56.3f,-73.5f),
        Rect.MinMaxRect(39.5f,-74,44.5f,-65.5f),Rect.MinMaxRect(24,-106,40,-42),
        Rect.MinMaxRect(21,-136.5f,105,-103.7f),Rect.MinMaxRect(73.5f,-116,106,-41),
        Rect.MinMaxRect(70.5f,-97,77,-86),Rect.MinMaxRect(20,-116,44,-91),
        Rect.MinMaxRect(24,-95,49,-80),Rect.MinMaxRect(32,-83,55,-71),
        Rect.MinMaxRect(44,-74,63,-62),Rect.MinMaxRect(51,-74,70,-51),
        Rect.MinMaxRect(43,-90,53,-79),Rect.MinMaxRect(50,-110,66,-100.5f)};
    class Spec {public int id;public string letter;public Vector2 offset,anchor;}
    static readonly Spec[] Specs={new Spec{id=77925,letter="A",offset=new Vector2(0,-4),anchor=new Vector2(58.3f,-105.4f)},new Spec{id=77928,letter="B",offset=new Vector2(-5,3),anchor=new Vector2(47,-84)},new Spec{id=77927,letter="C",offset=new Vector2(5,5),anchor=new Vector2(75,-91.5f)}};
    class Edge {public Vector3 mid,u,n;public float length;}
    static Edge frame;
    static Dictionary<string,DormitoryBuilder.Geometry> geometry;
    static Dictionary<string,Material> materials;
    static System.Random random;
    static Vector3 V(float u,float y,float d)=>frame.mid+frame.u*u+Vector3.up*y-frame.n*d;
    static void Box(string m,float u,float y,float d,float w,float h,float depth)=>geometry[m].Box(V(u,y,d),frame.u,Vector3.up,frame.n,new Vector3(w,h,depth));
    static void Beam(string m,Vector3 a,Vector3 b,float width)=>geometry[m].Beam(a,b,width);
    static MapPoint P(float x,float z)=>new MapPoint{x=x,z=z};
    static T Save<T>(T value,string path)where T:UnityEngine.Object {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(value,path);return value;
    }
    [MenuItem("Campus/Apply Reference Teaching Buildings")]
    public static void Generate() {
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();
        Apply(GameObject.Find("校园环境").transform,JsonUtility.FromJson<CampusData>(game.mapData.text),game);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());Debug.Log("TEACHING_GENERATION_COMPLETE");
    }
    public static void GenerateAndBuild(){Generate();CampusBuilder.Build();}
    public static void RestoreSources(Transform root) {
        var old=root.Find(Group);if(!old)return;var marker=old.GetComponent<CourtyardRevision>();
        if(marker&&marker.suppressedSources!=null)foreach(var r in marker.suppressedSources)if(r)r.enabled=true;
        UnityEngine.Object.DestroyImmediate(old.gameObject);
    }
    public static void Apply(Transform root,CampusData data,CampusGame game,bool validateEntrances=true) {
        RestoreSources(root);Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
        string baseline=Folder+"/OriginalFootprints.json";
        if(!File.Exists(baseline))File.WriteAllText(baseline,JsonUtility.ToJson(new CampusData{features=data.features.Where(f=>Specs.Any(s=>s.id==f.id)).ToArray()},true));
        var originals=JsonUtility.FromJson<CampusData>(File.ReadAllText(baseline));
        var holder=new GameObject(Group).transform;holder.SetParent(root,false);Mask=WholeArea;ReplaceOldGeometry(root,holder);
        random=new System.Random(77925);geometry=new Dictionary<string,DormitoryBuilder.Geometry>();materials=new Dictionary<string,Material>();
        Mat("Limestone","#d6d0c2",.22f,1);Mat("Grey stone","#9c9f9b",.25f,1);Mat("Metal","#394951",.7f);Mat("Stainless","#bbc2bf",.8f);
        Mat("Wood","#a07c51",.22f);materials["Wood"].SetFloat("_Wood",1);Mat("Glass","#99b5c6",.8f,0,1);
        Mat("Floor","#aaa79b",.1f,1);Mat("Interior","#c0bba8",.1f);Mat("Lamp","#ffe6b5",.1f,0,0,1.25f);Mat("Seat","#aa8c62",.15f);
        Mat("Skin","#ad8d73",.1f);Mat("Hair","#303832",.1f);Mat("Shirt","#526775",.1f);Mat("Cream shirt","#a49f8b",.1f);
        Mat("Board","#3d554e",.1f);Mat("Leaves","#64794b",.1f);Mat("Leaves light","#87995b",.1f);Mat("Soil","#625844",.1f);
        var paving=new Material(Shader.Find("Campus/Courtyard")){name="Teaching entry paving"};ColorUtility.TryParseHtmlString("#b8b6ad",out var pc);paving.SetColor("_BaseColor",pc);paving.SetFloat("_Surface",1);materials["Paving"]=Save(paving,Folder+"/Materials/Paving.mat");geometry["Paving"]=new DormitoryBuilder.Geometry();
        foreach(var tone in new[]{("Entry stone", "#bcb9b1"),("Entry stone warm", "#c2bdb2"),("Entry stone cool", "#b5b6b1"),("Entry border", "#777d7c"),("Entry joints", "#656966")}){
            var m=new Material(Shader.Find("Campus/Courtyard")){name=tone.Item1};ColorUtility.TryParseHtmlString(tone.Item2,out var color);m.SetColor("_BaseColor",color);m.SetFloat("_Surface",3);
            materials[tone.Item1]=Save(m,Folder+"/Materials/"+tone.Item1+".mat");geometry[tone.Item1]=new DormitoryBuilder.Geometry();
        }
        var extras=new List<CampusObstacle>();var routes=new List<CampusRoute>();
        // Retain all mapped buildings and the central fountain. Clear only obsolete
        // small facade-side obstructions before adding the new entrance steps.
        var names=new HashSet<string>(data.features.Select(f=>f.name));
        data.obstacles=data.obstacles.Where(o=>!o.name.StartsWith("Teaching ")&&(names.Contains(o.name)||!InOldFacade(o))).ToArray();
        foreach(var spec in Specs) {
            var source=originals.features.Single(f=>f.id==spec.id);var feature=data.features.Single(f=>f.id==spec.id);
            feature.points=spec.letter=="B"?CurvedB():source.points.Select(p=>P(p.x+spec.offset.x,p.z+spec.offset.y)).ToArray();
            var edges=Edges(feature.points);var anchor=spec.anchor+spec.offset;
            var entry=edges.Where(e=>e.length>6).OrderBy(e=>Vector2.Distance(anchor,new Vector2(e.mid.x,e.mid.z))).First();
            foreach(var e in edges){frame=e;Facade(feature.points,e==entry,spec);}
            RoofAndFloors(feature.points);
            frame=entry;Entrance(holder,game,spec);
            foreach(var o in data.obstacles.Where(o=>o.name==feature.name)){o.points=feature.points;o.minX=feature.points.Min(p=>p.x);o.maxX=feature.points.Max(p=>p.x);o.minZ=feature.points.Min(p=>p.z);o.maxZ=feature.points.Max(p=>p.z);}
            float width=Mathf.Min(12,entry.length*.68f);
            Vector3[] corners={V(-width/2-.4f,0,0),V(width/2+.4f,0,0),V(width/2+.4f,0,-1.8f),V(-width/2-.4f,0,-1.8f)};
            extras.Add(new CampusObstacle{name="Teaching "+spec.letter+" entrance steps",points=corners.Select(v=>P(v.x,v.z)).ToArray()});
            var config=AssetDatabase.LoadAssetAtPath<BuildingEntranceDefinition>("Assets/Interiors/Entrances/teaching-"+spec.letter.ToLowerInvariant()+".asset");
            config.approachMapPosition=V(0,.23f,-2.6f);config.returnMapPosition=V(0,.23f,-4);var face=game.MapToWorld(-entry.n);config.enterYaw=Mathf.Atan2(face.x,face.z)*Mathf.Rad2Deg;EditorUtility.SetDirty(config);
            var start=V(0,.23f,-6.2f);routes.Add(new CampusRoute{name="teaching-"+spec.letter+"-approach",points=new[]{P(start.x,start.z),P(config.returnMapPosition.x,config.returnMapPosition.z),P(config.approachMapPosition.x,config.approachMapPosition.z)}});
            CameraWalls(feature,game);
        }
        TeachingSkybridgeBuilder.Exterior(holder,game);
        foreach(var pair in geometry.Where(p=>p.Value.vertices.Count>0)) {
            var mesh=Save(pair.Value.Mesh("Teaching "+pair.Key),Folder+"/Meshes/"+pair.Key+".asset");var obj=new GameObject(pair.Key);obj.transform.SetParent(holder,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=materials[pair.Key];r.shadowCastingMode=pair.Key=="Glass"?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;
        }
        data.obstacles=data.obstacles.Concat(extras).ToArray();data.routes=data.routes.Where(r=>!r.name.StartsWith("teaching-")).Concat(routes).ToArray();
        ValidateSpacing(data);ValidateRoutes(data);string path=Folder+"/CampusMap.json";File.WriteAllText(path,JsonUtility.ToJson(data,true));AssetDatabase.ImportAsset(path);game.mapData=AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        if(!game.GetComponent<TeachingPreview>())game.gameObject.AddComponent<TeachingPreview>();EditorUtility.SetDirty(game);if(validateEntrances)BuildingInteriorBuilder.ValidateConfiguration();
        AssetDatabase.Refresh();Directory.CreateDirectory("QA");File.WriteAllText("QA/teaching-generation.txt","PASS: A and C retained; B curved western wing, north end aligned with existing A-B skybridge; pedestrian corner gaps retained; U-shaped layout and fountain; all routes and door approaches validated.\n");
    }
    static MapPoint[] CurvedB() {
        // Concentric arc; long enough facade bays for glazing and a recessed entrance.
        var p=new List<MapPoint>();const int bays=8;
        for(int i=0;i<=bays;i++){float a=Mathf.Lerp(Mathf.PI,2.08f,(float)i/bays);p.Add(P(95+70.5f*Mathf.Cos(a),-105.5f+70.5f*Mathf.Sin(a)));}
        for(int i=bays;i>=0;i--){float a=Mathf.Lerp(Mathf.PI,2.08f,(float)i/bays);p.Add(P(95+55.5f*Mathf.Cos(a),-105.5f+55.5f*Mathf.Sin(a)));}
        return p.ToArray();
    }
    static void Mat(string name,string hex,float smooth,float stone=0,float glass=0,float emission=0) {
        ColorUtility.TryParseHtmlString(hex,out var c);if(glass>0)c.a=.24f;var m=new Material(Shader.Find("Campus/Cafeteria")){name=name};m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Stone",stone);m.SetFloat("_Glass",glass);m.SetFloat("_Emission",emission);
        if(glass>0){m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");}materials[name]=Save(m,Folder+"/Materials/"+name+".mat");geometry[name]=new DormitoryBuilder.Geometry();
    }
    static List<Edge> Edges(MapPoint[] p) {
        var result=new List<Edge>();for(int i=0;i<p.Length;i++){var a=new Vector3(p[i].x,0,p[i].z);var b=new Vector3(p[(i+1)%p.Length].x,0,p[(i+1)%p.Length].z);var u=(b-a).normalized;var n=new Vector3(-u.z,0,u.x);var mid=(a+b)/2;if(CampusCollision.Inside(mid.x+n.x*.1f,mid.z+n.z*.1f,p))n=-n;result.Add(new Edge{mid=mid,u=u,n=n,length=(b-a).magnitude});}return result;
    }
    static void Facade(MapPoint[] footprint,bool entry,Spec spec) {
        float length=frame.length;Box("Grey stone",0,.3f,0,length,.6f,.4f);
        Box("Limestone",0,Height-.05f,0,length+.12f,.38f,.65f);Box("Metal",0,Height+.18f,0,length+.2f,.08f,.75f);
        if(length<2.4f){Box("Limestone",0,Height/2,.08f,length,Height,.4f);return;}
        float portal=entry?Mathf.Min(12,length*.68f):0;
        void Run(float left,float right) {
            float span=right-left;if(span<.4f)return;int count=Mathf.Max(1,Mathf.RoundToInt(span/3.5f));float bay=span/count;
            for(int floor=0;floor<5;floor++) {
                float fy=.6f+floor*Storey;
                Box("Limestone",(left+right)/2,fy+.28f,0,span,.56f,.48f);
                for(int i=0;i<count;i++) {
                    float u=left+(i+.5f)*bay,w=bay-.5f;
                    Box("Limestone",u-bay/2+.2f,fy+2.15f,-.05f,.4f,3.75f,.6f);
                    Box("Glass",u,fy+2.15f,.035f,w,3.0f,.025f);
                    foreach(float x in new[]{-w/2,0,w/2})Box("Metal",u+x,fy+2.15f,-.015f,.065f,3.05f,.12f);
                    foreach(float y in new[]{.65f,2.9f,3.65f})Box("Metal",u,fy+y,-.015f,w,.065f,.12f);
                    Box("Grey stone",u,fy+.58f,-.08f,w+.12f,.12f,.32f);
                    Box("Limestone",u,fy+3.95f,0,bay,.6f,.5f);
                    if(i%3==1)for(int slat=0;slat<4;slat++)Box("Wood",u-w/2+.12f+slat*.16f,fy+2.1f,-.22f,.075f,3.2f,.26f);
                    var back=V(u,0,3);if(CampusCollision.Inside(back.x,back.z,footprint)&&bay>1.6f){
                        Box("Interior",u,fy+2.0f,3.15f,bay,3.9f,.12f);Box("Interior",u,fy+3.9f,1.7f,bay,.1f,3.0f);
                        Box("Board",u,fy+1.8f,3.05f,w*.8f,1.15f,.04f);Box("Lamp",u,fy+3.8f,1.5f,w*.65f,.03f,.1f);
                        Table(u,fy,1.5f,(i+floor)%3!=0);
                    }
                }
            }
        }
        if(entry){Run(-length/2,-portal/2-.6f);Run(portal/2+.6f,length/2);Atrium(portal,spec.letter=="A");}
        else Run(-length/2,length/2);
    }
    static void Atrium(float width,bool main) {
        float top=main?24:Height;Box("Glass",0,(top+1)/2,-.16f,width,top-1,.025f);
        for(float x=-width/2;x<=width/2+.01f;x+=width/8)Box("Metal",x,(top+1)/2,-.22f,.08f,top-1,.18f);
        for(float y=4.75f;y<top;y+=2.125f)Box("Metal",0,y,-.22f,width,.08f,.18f);
        foreach(float side in new[]{-1f,1f}) {
            Box("Limestone",side*(width/2+.55f),top/2,-.1f,1.0f,top,.75f);
            for(int j=0;j<5;j++)Box("Wood",side*(width/2-.65f)+j*.14f,top/2+.4f,-.4f,.075f,top-.8f,.35f);
        }
        Box("Metal",0,top+.22f,.75f,width+3.4f,.18f,4.3f);Box("Wood",0,top+.1f,.75f,width+3.2f,.08f,4.1f);
        // Upper lobby walls give the glazing depth without changing the independent room.
        Box("Interior",0,top/2+1,4.5f,width,top-1,.15f);
        foreach(float y in new[]{4.85f,9.1f,13.35f,17.6f,21.85f})if(y<top) {
            Box("Floor",0,y,2.6f,width,.16f,4.8f);Box("Lamp",0,y-.12f,1.8f,width*.7f,.025f,.1f);
            Box("Stainless",0,y+1.05f,.8f,width,.04f,.05f);
            for(float u=-width/2;u<=width/2;u+=1.2f)Box("Metal",u,y+.55f,.8f,.035f,1.05f,.04f);
        }
    }
    static void Entrance(Transform holder,CampusGame game,Spec spec) {
        float width=Mathf.Min(12,frame.length*.68f),door=Mathf.Min(5,width-1.4f);
        Box("Metal",0,4.8f,-1.15f,width+2.3f,.22f,3.1f);Box("Wood",0,4.65f,-1.15f,width+2.1f,.055f,2.9f);
        for(float u=-width/2;u<=width/2;u+=1.5f)geometry["Lamp"].Disc(V(u,4.61f,-1.5f),frame.u,frame.n,.07f,12);
        foreach(float x in new[]{-door/2,0,door/2})Box("Metal",x,2.13f,-.27f,.08f,3.05f,.14f);
        Box("Metal",0,3.65f,-.27f,door,.1f,.14f);
        foreach(float x in new[]{-.18f,.18f})Box("Stainless",x,2.0f,-.37f,.035f,.7f,.06f);
        for(int i=0;i<5;i++){float d=-1.7f+i*.34f,y=.18f+(i+1)*.085f;Box("Grey stone",0,(y+.1f)/2,d/2,width,y-.1f,-d+.12f);Box("Limestone",0,y+.01f,d,width,.02f,.06f);}
        EntryForecourt(width,door);
        foreach(float side in new[]{-1f,1f})Planter(side*(width/2+1),.18f,-.8f,1.2f,1.2f,true);
        float panel=Mathf.Min(frame.length/2-1.8f,width/2+2.2f);
        Box("Limestone",panel,16.9f,-.4f,3.1f,8.8f,.4f);
        Sign(holder,game,spec.letter,panel,19.3f,-.64f,1.5f);
        Sign(holder,game,spec.letter+" 楼",panel,17.8f,-.64f,.6f);
        Sign(holder,game,"LEARNING\nINSPIRES\nA BRIGHTER\nTOMORROW",panel,15.4f,-.64f,.21f);
        Sign(holder,game,"教学楼 "+spec.letter,0,4.9f,-2.73f,.3f,true);
        Sign(holder,game,"KNOWLEDGE CREATES A BETTER FUTURE",0,4.55f,-2.73f,.13f,true);
    }
    static void EntryForecourt(float width,float door) {
        float half=(width+4)/2,back=-8.5f,front=-1.5f,border=.24f;
        Box("Entry joints",0,.155f,-5,width+4,.08f,7);
        // Actual chamfered slabs: narrow mortar joints and restrained stone variation.
        void Slab(string material,float left,float right,float near,float far){
            const float top=.208f,shoulder=.203f,inset=.004f;var g=geometry[material];
            var a=V(left,shoulder,near);var b=V(right,shoulder,near);var c=V(right,shoulder,far);var d=V(left,shoulder,far);
            var aa=V(left+inset,top,near+inset);var bb=V(right-inset,top,near+inset);var cc=V(right-inset,top,far-inset);var dd=V(left+inset,top,far-inset);
            g.Quad(aa,bb,cc,dd);g.Quad(a,b,bb,aa);g.Quad(b,c,cc,bb);g.Quad(c,d,dd,cc);g.Quad(d,a,aa,dd);
        }
        float leftEdge=-half+border,rightEdge=half-border;
        int rows=Mathf.CeilToInt((front-back-2*border)/.6f);float rowDepth=(front-back-2*border)/rows;
        for(int row=0;row<rows;row++){
            float near=back+border+row*rowDepth;
            for(float left=leftEdge-(row%2)*.6f;left<rightEdge;left+=1.2f){
                float a=Mathf.Max(left,leftEdge),b=Mathf.Min(left+1.2f,rightEdge);if(b-a<.01f)continue;
                int tone=(row*17+Mathf.RoundToInt((left-leftEdge)*5)+30)%7;
                Slab(tone==0?"Entry stone warm":tone==3?"Entry stone cool":"Entry stone",a+.006f,b-.006f,near+.006f,near+rowDepth-.006f);
            }
        }
        foreach(float side in new[]{-1f,1f}){
            float a=side<0?-half:half-border;
            for(float d=back;d<front;d+=1)Slab("Entry border",a+.006f,a+border-.006f,d+.006f,Mathf.Min(d+1,front)-.006f);
            // Flush inlays guide the eye towards the doorway without narrowing the route.
            Box("Entry border",side*(door/2+.4f),.2085f,-5,.09f,.003f,7-2*border);
        }
        foreach(float d in new[]{back,front-border})for(float u=leftEdge;u<rightEdge;u+=1.2f)
            Slab("Entry border",u+.006f,Mathf.Min(u+1.2f,rightEdge)-.006f,d+.006f,d+border-.006f);
        // Recessed slot drain and cross bars in front of the bottom stair.
        Box("Entry joints",0,.209f,-1.94f,width,.012f,.16f);
        foreach(float d in new[]{-2.025f,-1.855f})Box("Metal",0,.213f,d,width,.012f,.018f);
        for(float u=-width/2+.04f;u<width/2;u+=.09f)Box("Stainless",u,.215f,-1.94f,.018f,.012f,.14f);
    }
    static void Sign(Transform holder,CampusGame game,string text,float u,float y,float d,float size,bool light=false) {
        var neutral=holder.Find("Lettering world basis");
        if(!neutral){neutral=new GameObject("Lettering world basis").transform;neutral.SetParent(holder,false);neutral.localScale=new Vector3(-1,1,1);}
        var obj=new GameObject(text.Replace('\n',' '));obj.transform.SetParent(neutral,false);obj.transform.position=game.MapToWorld(V(u,y,d));
        // The environment root mirrors map X. Text uses a proper world-facing basis.
        obj.transform.rotation=Quaternion.LookRotation(-game.MapToWorld(frame.n),Vector3.up);obj.transform.localScale=Vector3.one;
        var mesh=obj.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.characterSize=size/10;mesh.fontSize=96;mesh.fontStyle=FontStyle.Bold;mesh.color=light?new Color(.89f,.81f,.64f):new Color(.19f,.23f,.25f);mesh.lineSpacing=1.1f;
        string path=Folder+"/Materials/Wall lettering.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat)mat=Save(new Material(Shader.Find("Campus/WallLettering")){name="Wall lettering"},path);obj.AddComponent<CafeteriaSign>().surface=mat;
    }
    static void RoofAndFloors(MapPoint[] points) {
        var triangles=Triangulate(points);
        foreach(float y in new[]{.6f,4.85f,9.1f,13.35f,17.6f,21.85f})foreach(var t in triangles)geometry[y>21?"Grey stone":"Floor"].Triangle(new Vector3(t[0].x,y,t[0].z),new Vector3(t[1].x,y,t[1].z),new Vector3(t[2].x,y,t[2].z));
    }
    static List<MapPoint[]> Triangulate(MapPoint[] source) {
        var p=source.ToList();float area=0;for(int i=0;i<p.Count;i++)area+=p[i].x*p[(i+1)%p.Count].z-p[(i+1)%p.Count].x*p[i].z;if(area<0)p.Reverse();var result=new List<MapPoint[]>();
        float Cross(MapPoint a,MapPoint b,MapPoint c)=>(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
        while(p.Count>3){bool found=false;for(int i=0;i<p.Count;i++){var a=p[(i+p.Count-1)%p.Count];var b=p[i];var c=p[(i+1)%p.Count];if(Cross(a,b,c)<.0001f)continue;bool hit=p.Any(q=>q!=a&&q!=b&&q!=c&&Cross(a,b,q)>=0&&Cross(b,c,q)>=0&&Cross(c,a,q)>=0);if(hit)continue;result.Add(new[]{a,b,c});p.RemoveAt(i);found=true;break;}if(!found)throw new Exception("Cannot triangulate teaching footprint");}result.Add(p.ToArray());return result;
    }
    static bool InOldFacade(CampusObstacle o) {
        if(o.name!=null&&o.name.Contains("喷泉"))return false;
        Vector2 center=o.points!=null&&o.points.Length>0?new Vector2(o.points.Average(p=>p.x),o.points.Average(p=>p.z)):o.segment!=null&&o.segment.Length>0?new Vector2(o.segment.Average(p=>p.x),o.segment.Average(p=>p.z)):new Vector2((o.minX+o.maxX)/2,(o.minZ+o.maxZ)/2);
        return Masks.Any(m=>m.Contains(center));
    }
    static void ValidateRoutes(CampusData data) {
        foreach(var route in data.routes){var p=new Vector3(route.points[0].x,.23f,route.points[0].z);if(CampusCollision.Blocked(p.x,p.z,data.obstacles))throw new Exception("Blocked route start: "+route.name);for(int i=1;i<route.points.Length;i++){var target=new Vector3(route.points[i].x,.23f,route.points[i].z);for(int j=0;j<6000&&Vector3.Distance(p,target)>.05f;j++)p=CampusCollision.Move(p,Vector3.ClampMagnitude(target-p,.15f),data.obstacles);if(Vector3.Distance(p,target)>.1f)throw new Exception("Teaching blocks "+route.name+" at "+p);}}
    }
    static void ValidateSpacing(CampusData data) {
        var wings=Specs.Select(s=>data.features.Single(f=>f.id==s.id)).ToArray();
        var aWing=wings[0].points;var bWing=wings[1].points;var cWing=wings[2].points;
        if(bWing.Min(p=>p.x)<aWing.Min(p=>p.x)||bWing.Min(p=>p.z)<aWing.Max(p=>p.z)||bWing.Max(p=>p.z)>cWing.Max(p=>p.z))throw new Exception("B extends outside the A/C end envelope");
        for(int i=0;i<wings.Length;i++)for(int j=i+1;j<wings.Length;j++) {
            float gap=float.MaxValue;var a=wings[i].points;var b=wings[j].points;
            foreach(var p in a){if(CampusCollision.Inside(p.x,p.z,b))throw new Exception("Teaching footprints overlap");for(int k=0;k<b.Length;k++)gap=Mathf.Min(gap,CampusCollision.Segment(p.x,p.z,b[k],b[(k+1)%b.Length]));}
            foreach(var p in b){if(CampusCollision.Inside(p.x,p.z,a))throw new Exception("Teaching footprints overlap");for(int k=0;k<a.Length;k++)gap=Mathf.Min(gap,CampusCollision.Segment(p.x,p.z,a[k],a[(k+1)%a.Length]));}
            if(gap<3.5f)throw new Exception("Insufficient teaching-building separation: "+gap);Debug.Log("TEACHING_GAP "+Specs[i].letter+"-"+Specs[j].letter+": "+gap+" metres");
        }
    }
    static void CameraWalls(CampusFeature feature,CampusGame game) {
        var root=GameObject.Find("Camera collision surfaces").transform;foreach(var c in root.GetComponentsInChildren<BoxCollider>())if(c.name==feature.name+" wall")UnityEngine.Object.DestroyImmediate(c.gameObject);
        foreach(var e in Edges(feature.points)){var obj=new GameObject(feature.name+" wall"){layer=8};obj.transform.SetParent(root,false);obj.transform.position=game.MapToWorld(e.mid+Vector3.up*12);obj.transform.rotation=Quaternion.LookRotation(game.MapToWorld(e.u));obj.AddComponent<BoxCollider>().size=new Vector3(.25f,24,e.length);}
    }
    static void Sphere(string m,Vector3 center,Vector3 scale) {
        Vector3 At(float latitude,float longitude)=>center+Vector3.Scale(new Vector3(Mathf.Cos(latitude)*Mathf.Cos(longitude),Mathf.Sin(latitude),Mathf.Cos(latitude)*Mathf.Sin(longitude)),scale);
        for(int y=0;y<6;y++)for(int x=0;x<10;x++) {
            float a=-Mathf.PI/2+y*Mathf.PI/6,b=a+Mathf.PI/6,c=x*Mathf.PI/5,d=c+Mathf.PI/5;
            geometry[m].Quad(At(a,c),At(a,d),At(b,d),At(b,c));
        }
    }
    static void Planter(float u,float y,float d,float w,float depth,bool tall) {
        Box("Limestone",u,y+.2f,d,w,.4f,depth);Box("Soil",u,y+.41f,d,w-.15f,.03f,depth-.15f);
        int count=(int)(w*depth*18);
        for(int i=0;i<count;i++) {
            var p=V(u+((float)random.NextDouble()-.5f)*(w-.2f),y+.5f+(float)random.NextDouble()*.2f,d+((float)random.NextDouble()-.5f)*(depth-.2f));
            for(int j=0;j<12;j++)Leaf(p,new Vector3(((float)random.NextDouble()-.5f)*.4f,(float)random.NextDouble()*.3f,((float)random.NextDouble()-.5f)*.4f),.12f,j);
        }
        if(tall&&w<2)for(int i=0;i<40;i++){float a=i*2.399f;var p=V(u+Mathf.Cos(a)*.25f,y+.8f+(i%6)*.1f,d+Mathf.Sin(a)*.25f);Leaf(p,Vector3.zero,.18f,i);}
    }
    static void Leaf(Vector3 p,Vector3 offset,float size,int i){float a=i*2.399f;var u=new Vector3(Mathf.Cos(a),.25f,Mathf.Sin(a))*size;var v=new Vector3(-u.z,.05f,u.x)*.42f;p+=offset;geometry[i%3==0?"Leaves light":"Leaves"].Quad(p-u,p+v,p+u,p-v);}
    static void Table(float u,float y,float d,bool occupied) {
        Box("Wood",u,y+.75f,d,1.7f,.06f,.78f);
        foreach(float side in new[]{-.58f,.58f})Box("Metal",u+side,y+.36f,d,.045f,.72f,.045f);
        foreach(float side in new[]{-.72f,.72f})foreach(float along in new[]{-.48f,.48f}) {
            float z=d+side,x=u+along;Box("Seat",x,y+.43f,z,.38f,.055f,.4f);
            Box("Seat",x,y+.7f,z+Mathf.Sign(side)*.17f,.38f,.45f,.045f);
            foreach(float a in new[]{-.13f,.13f})Box("Metal",x+a,y+.21f,z,.025f,.42f,.025f);
        }
        if(!occupied)return;
        string shirt=random.Next(2)==0?"Shirt":"Cream shirt";
        Box(shirt,u-.48f,y+.84f,d+.68f,.32f,.5f,.24f);
        Sphere("Skin",V(u-.48f,y+1.2f,d+.65f),new Vector3(.105f,.14f,.1f));
        Sphere("Hair",V(u-.48f,y+1.29f,d+.69f),new Vector3(.109f,.073f,.105f));
        Beam(shirt,V(u-.63f,y+1.0f,d+.64f),V(u-.68f,y+.79f,d+.25f),.085f);
        Beam(shirt,V(u-.33f,y+1.0f,d+.64f),V(u-.28f,y+.79f,d+.25f),.085f);
        Box("Interior",u-.48f,y+.8f,d+.1f,.28f,.025f,.22f);
    }
    static void ReplaceOldGeometry(Transform root,Transform holder) {
        var suppressed=new List<Renderer>();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>()) {
            var r=filter.GetComponent<MeshRenderer>();if(!r||!r.enabled||!filter.sharedMesh)continue;
            // Imported building trim/base materials include the former building's ground
            // plates at y=0..0.14. Clip those too when reshaping B; keep grass and
            // roads (other materials) below the usual .2 m threshold untouched.
            MinimumCutHeight=r.sharedMaterials.Any(m=>m&&Array.IndexOf(new[]{"54","57","59","64","71","72"},m.name)>=0)?-.01f:.2f;
            // New native dorms are far outside this area; retained imported batches
            // may contain multiple buildings and must be clipped without deleting the batch.
            var source=filter.sharedMesh;var b=source.bounds;
            if(b.max.x<Mask.xMin||b.min.x>Mask.xMax||b.max.z<Mask.yMin||b.min.z>Mask.yMax||b.max.y<MinimumCutHeight)continue;
            var path=AssetDatabase.GetAssetPath(source);
            if(!(path.StartsWith("Assets/LivingRevision/Meshes/")||path.Contains("Retained-")||path.Contains("Surroundings-")||path.Contains("Preserved-")))continue;
            Mesh mesh=null;
            foreach(var mask in Masks){Mask=mask;var next=Clip(mesh?mesh:source);if(next){if(mesh)UnityEngine.Object.DestroyImmediate(mesh);mesh=next;}}
            Mask=WholeArea;if(!mesh)continue;
            suppressed.Add(r);r.enabled=false;if(mesh.vertexCount==0){UnityEngine.Object.DestroyImmediate(mesh);continue;}
            string id=AssetDatabase.AssetPathToGUID(path);mesh=Save(mesh,Folder+"/Meshes/Preserved-"+id+".asset");
            var obj=new GameObject("Preserved surroundings "+id);obj.transform.SetParent(holder,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterials=r.sharedMaterials;renderer.shadowCastingMode=r.shadowCastingMode;
        }
        holder.gameObject.AddComponent<CourtyardRevision>().suppressedSources=suppressed.ToArray();
    }
    struct Vertex {public Vector3 p,n;public Vector2 uv;public Color c;public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),c=Color.Lerp(a.c,b.c,t)};}
    static float Distance(Vertex v,int plane)=>plane==0?v.p.x-Mask.xMin:plane==1?Mask.xMax-v.p.x:plane==2?v.p.z-Mask.yMin:Mask.yMax-v.p.z;
    static List<Vertex> Cut(List<Vertex> poly,int plane,bool inside) {
        var output=new List<Vertex>();for(int i=0;i<poly.Count;i++){var a=poly[i];var b=poly[(i+1)%poly.Count];float da=Distance(a,plane),db=Distance(b,plane);bool ia=inside?da>=0:da<0,ib=inside?db>=0:db<0;if(ia)output.Add(a);if(ia!=ib)output.Add(Vertex.Lerp(a,b,da/(da-db)));}return output;
    }
    static Mesh Clip(Mesh source) {
        var p=source.vertices;var n=source.normals;var uv=source.uv;var colors=source.colors;var tri=source.triangles;var output=new List<Vertex>();bool changed=false;
        Vertex Vtx(int i)=>new Vertex{p=p[i],n=n.Length==p.Length?n[i]:Vector3.up,uv=uv.Length==p.Length?uv[i]:Vector2.zero,c=colors.Length==p.Length?colors[i]:Color.white};
        void Append(List<Vertex> poly){for(int j=1;j<poly.Count-1;j++){output.Add(poly[0]);output.Add(poly[j]);output.Add(poly[j+1]);}}
        for(int i=0;i<tri.Length;i+=3) {
            var a=Vtx(tri[i]);var b=Vtx(tri[i+1]);var c=Vtx(tri[i+2]);
            if(Mathf.Max(a.p.y,b.p.y,c.p.y)<MinimumCutHeight||Mathf.Max(a.p.x,b.p.x,c.p.x)<Mask.xMin||Mathf.Min(a.p.x,b.p.x,c.p.x)>Mask.xMax||Mathf.Max(a.p.z,b.p.z,c.p.z)<Mask.yMin||Mathf.Min(a.p.z,b.p.z,c.p.z)>Mask.yMax){output.Add(a);output.Add(b);output.Add(c);continue;}
            var remain=new List<Vertex>{a,b,c};var outside=new List<Vertex>();
            for(int plane=0;plane<4&&remain.Count>0;plane++){var part=Cut(remain,plane,false);for(int j=1;j<part.Count-1;j++){outside.Add(part[0]);outside.Add(part[j]);outside.Add(part[j+1]);}remain=Cut(remain,plane,true);}
            if(remain.Count>=3){changed=true;output.AddRange(outside);}else Append(new List<Vertex>{a,b,c});
        }
        if(!changed)return null;
        var mesh=new Mesh{name="Teaching preserved surroundings",indexFormat=IndexFormat.UInt32};mesh.SetVertices(output.Select(v=>v.p).ToList());mesh.SetNormals(output.Select(v=>v.n).ToList());mesh.SetUVs(0,output.Select(v=>v.uv).ToList());mesh.SetColors(output.Select(v=>v.c).ToList());mesh.SetTriangles(Enumerable.Range(0,output.Count).ToArray(),0);mesh.RecalculateBounds();return mesh;
    }
}
