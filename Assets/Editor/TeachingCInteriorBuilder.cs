using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

// Metres, +Z towards the back of the building. Each flight has twelve actual
// 175 mm risers and 420 mm treads. No ramp collider or floor-change trigger.
public static partial class TeachingCInteriorBuilder
{
    static string Building="C";
    static string Folder=>"Assets/Teaching"+Building+"Interior";
    static float[] RoomStarts=>Building=="A"?new[]{2f}:new[]{2f,10f};
    const float Storey=4.2f;
    static Transform root,level;
    static Dictionary<string,Material> materials;
    static Dictionary<string,DormitoryBuilder.Geometry> meshes;
    static Material lettering;
    static int floor;
    static float y;
    static Vector3 V(float x,float h,float z)=>new Vector3(x,h,z);
    static T Save<T>(T value,string path) where T:UnityEngine.Object {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old){EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(value,path);return value;
    }
    static void Mat(string name,string hex,float smooth=.25f,float stone=0,float wood=0,float emission=0,bool glass=false){
        ColorUtility.TryParseHtmlString(hex,out var c);if(glass)c.a=.13f;
        var m=new Material(Shader.Find(glass?"Campus/Cafeteria":"Campus/TeachingInterior")){name=name};m.SetColor("_BaseColor",c);
        m.SetFloat("_Smoothness",smooth);m.SetFloat("_Stone",stone);m.SetFloat("_Wood",wood);m.SetFloat("_Emission",emission);
        if(glass)m.SetFloat("_Glass",1);else m.SetFloat("_Metallic",name=="Steel"?.65f:name=="Mirror"?.28f:name=="Metal"?.25f:0);
        if(glass){m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.renderQueue=3000;}
        materials[name]=Save(m,Folder+"/Materials/"+name+".mat");
    }
    static void Box(string mat,Vector3 p,Vector3 size,string solid=null){
        meshes[mat].Box(p,Vector3.right,Vector3.up,Vector3.forward,size);
        if(solid!=null)Collider(solid,p,size);
    }
    static void B(string mat,float x,float h,float z,float w,float height,float depth,string solid=null)=>Box(mat,V(x,h,z),V(w,height,depth),solid);
    static void Collider(string name,Vector3 p,Vector3 size){var c=new GameObject(name).AddComponent<BoxCollider>();c.transform.SetParent(level,false);c.center=p;c.size=size;}
    static void Beam(string mat,Vector3 a,Vector3 b,float width)=>meshes[mat].Beam(a,b,width);
    // Small edge radii catch light without changing the clear collision envelope.
    static void SoftBox(string mat,Vector3 center,Vector3 size,float radius,string solid=null){
        var h=size*.5f;float r=Mathf.Min(radius,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.8f);var inner=h-Vector3.one*r;
        float[] Corners(float extent)=>new[]{-extent,-extent+r,extent-r,extent};
        Vector3 Round(Vector3 p){var q=V(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));return center+q+(p-q).normalized*r;}
        for(int axis=0;axis<3;axis++)foreach(float sign in new[]{-1f,1f}){
            int u=(axis+1)%3,v=(axis+2)%3;var a=Corners(h[u]);var b=Corners(h[v]);
            Vector3 P(float s,float t){var p=Vector3.zero;p[axis]=h[axis]*sign;p[u]=s;p[v]=t;return Round(p);}
            for(int i=0;i<3;i++)for(int j=0;j<3;j++){
                var p0=P(a[i],b[j]);var p1=P(a[i+1],b[j]);var p2=P(a[i+1],b[j+1]);var p3=P(a[i],b[j+1]);
                if(sign>0)meshes[mat].Quad(p0,p1,p2,p3);else meshes[mat].Quad(p3,p2,p1,p0);
            }
        }
        if(solid!=null)Collider(solid,center,size);
    }
    static void Tube(string mat,Vector3 a,Vector3 b,float radius,int segments=12){
        var axis=(b-a).normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up).normalized;var v=Vector3.Cross(axis,u);
        for(int i=0;i<segments;i++){float t=i*2*Mathf.PI/segments,s=(i+1)*2*Mathf.PI/segments;var p=(u*Mathf.Cos(t)+v*Mathf.Sin(t))*radius;var q=(u*Mathf.Cos(s)+v*Mathf.Sin(s))*radius;meshes[mat].Quad(a+p,a+q,b+q,b+p);meshes[mat].Triangle(a,a+q,a+p);meshes[mat].Triangle(b,b+p,b+q);}
    }
    static void Bowl(string mat,Vector3 center,float rx,float rz,float depth){
        // Concave ceramic basin with a thick rim and a recessed drain.
        var heights=new[]{0f,.035f,.035f,-depth*.15f,-depth};var scales=new[]{1.05f,1f,.87f,.8f,.50f};
        for(int ring=0;ring<4;ring++)for(int i=0;i<32;i++){
            float a=i*2*Mathf.PI/32,b=(i+1)*2*Mathf.PI/32;
            Vector3 P(float t,int j)=>center+V(Mathf.Cos(t)*rx*scales[j],heights[j],Mathf.Sin(t)*rz*scales[j]);
            meshes[mat].Quad(P(a,ring),P(b,ring),P(b,ring+1),P(a,ring+1));
        }
        for(int i=0;i<32;i++){float a=i*2*Mathf.PI/32,b=(i+1)*2*Mathf.PI/32;meshes[mat].Triangle(center+Vector3.down*depth,center+V(Mathf.Cos(a)*rx*.5f,-depth,Mathf.Sin(a)*rz*.5f),center+V(Mathf.Cos(b)*rx*.5f,-depth,Mathf.Sin(b)*rz*.5f));}
        Tube("Steel",center+Vector3.down*(depth-.003f),center+Vector3.down*(depth-.008f),.032f,20);
    }
    static void BasinSurround(Vector3 center){
        // Fill every rectangular cutout corner up to the oval rim. Slight rim
        // overlap prevents cracks from floating point/triangulation differences.
        var angles=new List<float>();for(int i=0;i<32;i++)angles.Add(i*2*Mathf.PI/32);
        float corner=Mathf.Atan2(.36f*.51f,.57f*.30f);angles.Add(corner);angles.Add(Mathf.PI-corner);angles.Add(Mathf.PI+corner);angles.Add(2*Mathf.PI-corner);angles.Sort();
        Vector3 P(float a,bool outer){var d=V(Mathf.Cos(a)*.51f,0,Mathf.Sin(a)*.30f);if(outer)d*=Mathf.Min(.57f/Mathf.Max(.00001f,Mathf.Abs(d.x)),.36f/Mathf.Max(.00001f,Mathf.Abs(d.z)));return center+d;}
        for(int i=0;i<angles.Count;i++){float a=angles[i],b=angles[(i+1)%angles.Count];meshes["Ceramic"].Quad(P(a,false),P(a,true),P(b,true),P(b,false));}
    }
    static void Sign(string text,Vector3 p,Vector3 normal,float size,Color color){
        var go=new GameObject(text.Replace('\n',' '));go.transform.SetParent(level,false);go.transform.position=p;go.transform.rotation=Quaternion.LookRotation(-normal);
        var tm=go.AddComponent<TextMesh>();tm.text=text;tm.fontSize=96;tm.characterSize=size/10;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=color;
        go.AddComponent<CafeteriaSign>().surface=lettering;
    }
    static readonly Color Ink=new Color(.025f,.035f,.043f),White=new Color(.94f,.93f,.86f);
    static void BeginFloor(int f){floor=f;y=f*Storey;level=new GameObject((f+1)+"F · "+(Building=="A"?"阶梯教室":Building=="B"?"综合教室 自习区":"教室 机房")+" 卫生间 楼梯").transform;level.SetParent(root,false);meshes=new Dictionary<string,DormitoryBuilder.Geometry>();foreach(var k in materials.Keys)meshes[k]=new DormitoryBuilder.Geometry();}
    static void EndFloor(){
        foreach(var pair in meshes){
            if(pair.Value.vertices.Count==0)continue;
            var mesh=pair.Value.Mesh(pair.Key);SmoothEdges(mesh);
            var obj=new GameObject(pair.Key);obj.transform.SetParent(level,false);
            obj.AddComponent<MeshFilter>().sharedMesh=Save(mesh,Folder+"/Meshes/F"+(floor+1)+"-"+pair.Key+".asset");
            var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=materials[pair.Key];
            if(pair.Key=="Glass"||pair.Key=="Light"||pair.Key=="Bridge Glass"||pair.Key=="Bridge Lamp")r.shadowCastingMode=ShadowCastingMode.Off;
        }
    }
    static void SmoothEdges(Mesh mesh){
        // Weld normals only across gentle bevels. Keep 90-degree wall corners sharp.
        var vertices=mesh.vertices;var normals=mesh.normals;var output=new Vector3[normals.Length];
        var groups=new Dictionary<Vector3Int,List<int>>();
        for(int i=0;i<vertices.Length;i++){
            var p=vertices[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000),Mathf.RoundToInt(p.z*10000));
            if(!groups.TryGetValue(key,out var ids)){ids=new List<int>();groups.Add(key,ids);}ids.Add(i);
        }
        foreach(var ids in groups.Values)foreach(var i in ids){var sum=Vector3.zero;foreach(var j in ids)if(Vector3.Dot(normals[i],normals[j])>.55f)sum+=normals[j];output[i]=sum.normalized;}
        mesh.normals=output;
    }
    static void Slab(float x0,float x1,float z0,float z1,float top,string mat="Floor")=>B(mat,(x0+x1)/2,top-.12f,(z0+z1)/2,x1-x0,.24f,z1-z0,"Floor slab");
    [MenuItem("Campus/Interiors/Generate Teaching C")]
    public static void Generate()=>GenerateBuilding("C");
    public static void GenerateBuilding(string building){
        if(building!="A"&&building!="B"&&building!="C")throw new ArgumentException("Unknown teaching building");
        Building=building;
        Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();
        materials=new Dictionary<string,Material>();
        Mat("Floor","#bcbdb7",.4f,1);Mat("Stone","#d8d4c9",.22f,1);Mat("Wall","#e7e4db",.06f);Mat("Ceiling","#e5e5df",.02f);Mat("Dado","#b3b8b4",.2f,1);
        Mat("Dark tile","#697477",.3f,1);Mat("Wood","#997b58",.3f,0,1);Mat("Oak pale","#bfa57d",.3f,0,1);
        Mat("Metal","#35434b",.7f);Mat("Steel","#a8b4b9",.85f);Mat("Glass","#a2becb",.8f,0,0,0,true);
        Mat("Light","#fff2cf",.3f,0,0,1.6f);Mat("Screen","#457786",.4f,0,0,.35f);Mat("Board","#243c36");
        Mat("Projection","#e2ece9",.03f,0,0,.3f);
        Mat("Seat","#617b80",.22f);Mat("Ceramic","#f2f0e7",.65f);Mat("Mirror","#bccdd0",.85f,0,0,.045f);
        Mat("Trim","#85908f",.4f);Mat("Cubicle","#718482",.2f);Mat("Cabinet","#c3c8bf",.25f);Mat("Basin shadow","#a7b3ae",.15f);
        Mat("Leaf","#416247");Mat("Leaf light","#718856");Mat("Soil","#4b4035");Mat("Curtain","#aebbc0");
        if(Building=="A"){Mat("Seat","#49647d",.24f);Mat("Wood","#826449",.25f,0,1);Mat("Dark tile","#53616b",.25f,1);}
        if(Building=="B"){Mat("Seat","#799185",.22f);Mat("Wood","#ad8e66",.25f,0,1);Mat("Dado","#bec8bc",.2f,1);}
        foreach(string key in new[]{"Floor","Metal","Glass","Wood","Lamp","Limestone","Trim"})materials["Bridge "+key]=TeachingSkybridgeBuilder.BridgeMaterial(key,Folder+"/Materials");
        lettering=Save(new Material(Shader.Find("Campus/WallLettering")){name="Interior lettering"},Folder+"/Materials/Lettering.mat");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        root=new GameObject(Building+"楼 · 五层教学空间").transform;var contract=root.gameObject.AddComponent<InteriorSceneRoot>();
        contract.buildingId="teaching-"+Building.ToLowerInvariant();contract.isPlaceholder=false;contract.backgroundColor=new Color(.65f,.78f,.86f);
        var spawn=new GameObject("main · 大厅出生点").transform;spawn.SetParent(root,false);spawn.position=V(0,.06f,-7.5f);
        var exit=new GameObject("出口 · 返回"+Building+"楼正门").transform;exit.SetParent(root,false);exit.position=V(0,.06f,-11);
        contract.spawnPoints=new[]{new InteriorSpawnPoint{id="main",anchor=spawn}};contract.exits=new[]{exit};
        var presentation=root.gameObject.AddComponent<TeachingCInteriorPresentation>();presentation.rendererIndex=ConfigureRenderer();
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();
        profile.Add<ColorAdjustments>(true).contrast.Override(6);profile.TryGet<ColorAdjustments>(out var color);color.postExposure.Override(.25f);color.saturation.Override(-3);
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.06f);bloom.threshold.Override(1.25f);
        var volume=new GameObject(Building+"楼 · 色调与柔和灯光").AddComponent<Volume>();volume.transform.SetParent(root,false);volume.isGlobal=true;volume.priority=20;
        // Save volume components as subassets, so the profile survives reopening.
        string profilePath=Folder+"/InteriorVolume.asset";var existing=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if(existing){foreach(var c in existing.components)UnityEngine.Object.DestroyImmediate(c,true);existing.components.Clear();foreach(var c in profile.components){AssetDatabase.AddObjectToAsset(c,existing);existing.components.Add(c);}profile.components.Clear();UnityEngine.Object.DestroyImmediate(profile);EditorUtility.SetDirty(existing);volume.sharedProfile=existing;}
        else {AssetDatabase.CreateAsset(profile,profilePath);foreach(var c in profile.components)AssetDatabase.AddObjectToAsset(c,profile);volume.sharedProfile=profile;}
        for(int f=0;f<5;f++){BeginFloor(f);Structure();if(Building=="B")StudyCommons();else Atrium();if(Building=="A")LectureRooms();else if(Building=="B")MixedRooms();else Rooms();Toilets();if(f<4)Stairs();Ceilings();if(f==1)SkybridgeConnections(contract);EndFloor();}
        // Sunlight and scene-specific ambient lighting are switched by the travel system.
        var sun=new GameObject("日光 · 中庭天窗").AddComponent<Light>();sun.transform.SetParent(root,false);sun.type=LightType.Directional;sun.color=new Color(1,.95f,.85f);sun.intensity=1.0f;sun.transform.rotation=Quaternion.Euler(52,-38,0);sun.shadows=LightShadows.Soft;sun.shadowBias=.025f;sun.shadowNormalBias=.12f;
        RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.66f,.72f,.77f);RenderSettings.ambientEquatorColor=new Color(.54f,.56f,.56f);RenderSettings.ambientGroundColor=new Color(.32f,.34f,.35f);RenderSettings.fog=false;
        var probe=new GameObject("中庭材质反射").AddComponent<ReflectionProbe>();probe.transform.SetParent(root,false);probe.transform.position=V(0,5,-3);probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;probe.resolution=128;probe.size=V(44,44,90);probe.boxProjection=true;probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=contract.backgroundColor;probe.nearClipPlane=.15f;probe.farClipPlane=60;probe.intensity=.65f;
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,"Assets/Interiors/Scenes/teaching-"+Building.ToLowerInvariant()+".unity");
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();if(!game.GetComponent<TeachingCInteriorQA>())game.gameObject.AddComponent<TeachingCInteriorQA>();
        if(Building!="C"&&!game.GetComponent<TeachingABInteriorQA>())game.gameObject.AddComponent<TeachingABInteriorQA>();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("TEACHING_INTERIOR_GENERATED: "+Building+", five floors, ten washrooms, 96 physical treads");
    }
    public static void GenerateAndBuild(){Generate();CampusBuilder.Build();}
    static int ConfigureRenderer(){
        string path="Assets/TeachingCInterior/InteriorRenderer.asset";var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if(!renderer){renderer=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/CampusRenderer.asset"));renderer.name="C Interior Renderer";renderer.rendererFeatures.Clear();AssetDatabase.CreateAsset(renderer,path);}
        ScreenSpaceAmbientOcclusion ao=null;foreach(var feature in renderer.rendererFeatures)if(feature is ScreenSpaceAmbientOcclusion item)ao=item;
        if(!ao){ao=ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();ao.name="Interior contact shadows";AssetDatabase.AddObjectToAsset(ao,renderer);renderer.rendererFeatures.Add(ao);}
        var settings=new SerializedObject(ao);var s=settings.FindProperty("m_Settings");s.FindPropertyRelative("Source").enumValueIndex=0;s.FindPropertyRelative("NormalSamples").enumValueIndex=2;
        s.FindPropertyRelative("AOMethod").enumValueIndex=1;s.FindPropertyRelative("Intensity").floatValue=.8f;s.FindPropertyRelative("Radius").floatValue=.18f;s.FindPropertyRelative("Falloff").floatValue=55;
        s.FindPropertyRelative("Downsample").boolValue=false;s.FindPropertyRelative("AfterOpaque").boolValue=true;s.FindPropertyRelative("Samples").enumValueIndex=0;settings.ApplyModifiedPropertiesWithoutUndo();ao.SetActive(true);renderer.SetDirty();EditorUtility.SetDirty(renderer);
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/CampusURP.asset");var serialized=new SerializedObject(pipeline);var list=serialized.FindProperty("m_RendererDataList");int index=-1;
        for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==renderer)index=i;
        if(index<0){index=list.arraySize;list.InsertArrayElementAtIndex(index);list.GetArrayElementAtIndex(index).objectReferenceValue=renderer;serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);}
        return index;
    }
    static void Structure(){
        if(floor==0)Slab(-20,20,-12,30,0);
        else {
            Slab(-20,-7,-12,30,y);Slab(7,20,-12,30,y);Slab(-7,7,-12,-10,y);
            Slab(-7,7,2,21.7f,y);Slab(-7,-4,21.7f,30,y);Slab(4,7,21.7f,30,y);Slab(-4,4,29.8f,30,y);
            if(Building=="B")Slab(-7,7,-10,2,y);
        }
        // Window bays with solid sill, top lintel, mullions and sealed glazing.
        foreach(float x in new[]{-20f,20f}){
            if(BridgeSide(x)){BridgeSideWall(x);continue;}
            B("Dado",x,y+.5f,9,.28f,1,42.2f,"Exterior sill");B("Wall",x,y+3.85f,9,.28f,.7f,42.2f,"Exterior lintel");
            for(float z=-12;z<=30;z+=3.5f){B("Stone",x,y+2.25f,z,.38f,2.6f,.22f,"Window pier");if(z<29.9f)B("Metal",x,y+2.25f,z+1.75f,.12f,2.6f,.055f);}
            B("Glass",x,y+2.25f,9,.06f,2.6f,42.2f);Collider("Window glazing",V(x,y+2.25f,9),V(.16f,2.6f,42.2f));
            B("Metal",x,y+2.35f,9,.12f,.05f,42);B("Metal",x,y+1,9,.16f,.07f,42);
        }
        B("Stone",0,y+3.85f,-12,40,.7f,.3f,"Front lintel");B("Glass",0,y+1.8f,-12,40,3.6f,.06f);
        Collider("Front glazing boundary",V(0,y+2,-12),V(40,4,.15f));
        for(float x=-20;x<=20;x+=2.5f)B("Metal",x,y+2,-11.94f,.08f,4,.12f);
        B("Metal",0,y+2.5f,-11.94f,40,.08f,.12f);
        foreach(float x in new[]{-12f,12f})B("Wall",x,y+2.1f,30,16,4.2f,.3f,"Rear wall");
        B("Dado",0,y+.5f,30,8,1,.3f,"Stair window sill");B("Wall",0,y+3.95f,30,8,.5f,.3f,"Stair window lintel");
        B("Glass",0,y+2.35f,30,8,2.7f,.05f,"Stair window glazing");
        foreach(float x in new[]{-4f,-2f,0,2f,4f})B("Metal",x,y+2.35f,29.94f,.08f,2.7f,.12f);
        B("Metal",0,y+2.1f,29.94f,8,.07f,.12f);
        foreach(float x in new[]{-19.5f,-7.4f,7.4f,19.5f})foreach(float z in new[]{-10f,2f,19f}){if(z==19&&Mathf.Abs(x)>19&&BridgeSide(x))continue;B("Stone",x,y+2.1f,z,.55f,4.2f,.55f,"Structural column");}
        // Classroom and laboratory entrance walls with actual door openings.
        foreach(float x in new[]{-4f,4f})foreach(float start in RoomStarts){
            float door=start+3.5f;
            B("Wall",x,y+2.1f,start+1.3f,.24f,4.2f,2.6f,"Room wall");B("Wall",x,y+2.1f,start+5.7f,.24f,4.2f,2.6f,"Room wall");
            B("Wall",x,y+3.55f,door,.24f,1.3f,2.1f,"Door lintel");
            B("Dado",x+(x<0?.13f:-.13f),y+.55f,start+1.3f,.035f,1.1f,2.6f);B("Dado",x+(x<0?.13f:-.13f),y+.55f,start+5.7f,.035f,1.1f,2.6f);
            DoorX(x,door,(floor+1)*100+(Building=="A"?(x<0?1:2):(x<0?(start==2?1:2):(start==2?3:4))),RoomType(x,start));
        }
        foreach(float side in new[]{-1f,1f})foreach(float z in Building=="A"?new[]{2f,17.2f}:new[]{2f,9.5f,17.2f}){
            B("Wall",side*12,y+2.1f,z,16.1f,4.2f,.22f,"Room partition");
            foreach(float dz in new[]{-.14f,.14f})B("Trim",side*12,y+.065f,z+dz,16.1f,.13f,.04f);
        }
        foreach(float side in new[]{-1f,1f}){
            if(Building=="A")B("Wall",side*4,y+2.1f,13.1f,.24f,4.2f,8.4f,"Lecture hall corridor wall");
            B("Wall",side*4,y+2.1f,9.5f,.24f,4.2f,1.15f,"Corridor wall joint");
            B("Wall",side*4,y+2.1f,17.1f,.24f,4.2f,.3f,"Rear classroom corner");
            foreach(float start in new[]{2f,10f})foreach(float sign in new[]{-1f,1f}){
                float xx=side*4+sign*.15f;B("Trim",xx,y+.065f,start+1.3f,.04f,.13f,2.6f);B("Trim",xx,y+.065f,start+5.7f,.04f,.13f,2.6f);
            }
            B("Wood",side*12,y+1.9f,17.34f,3.2f,1.4f,.08f);B("Ceramic",side*12,y+1.9f,17.39f,3,1.2f,.025f);
            Sign("教学公告   /   CAMPUS NOTICE\n课程安排     文明课堂     安全须知",V(side*12,y+1.9f,17.42f),Vector3.forward,.16f,Ink);
        }
        foreach(float x in new[]{-4f,4f})B("Wall",x,y+2.1f,25.8f,.22f,4.2f,8.4f,"Stair enclosure");
        Rail(V(-.5f,y,21.73f),V(.5f,y,21.73f),false);
        if(floor==4)Rail(V(-3.9f,y,21.73f),V(-.5f,y,21.73f),false);
        B("Dark tile",-3.77f,y+.006f,9.7f,.16f,.012f,15.6f);B("Dark tile",3.77f,y+.006f,9.7f,.16f,.012f,15.6f);
        Sign((floor+1)+"F   "+(Building=="A"?"← 阶梯教室 01     阶梯教室 02 →":Building=="B"?"← 普通 / 多媒体     语音 / 研讨 →":"← 教室 01–02     机房 03–04 →")+"\n楼梯 / 卫生间  ↑",V(0,y+3.05f,19.5f),Vector3.back,.18f,Ink);
        B("Oak pale",0,y+3.08f,19.57f,7,.85f,.1f);
    }
    static void DoorX(float x,float z,int number,string type){
        float side=Mathf.Sign(x);
        foreach(float dz in new[]{-.93f,.93f})B("Wood",x,y+1.45f,z+dz,.32f,2.9f,.12f);
        B("Wood",x,y+2.92f,z,.32f,.12f,1.98f);
        // Fixed open leaf lies inside the room, leaving 1.8 m clear circulation.
        B("Wood",x+side*.83f,y+1.4f,z-.86f,1.65f,2.8f,.08f,"Open classroom door");
        B("Glass",x+side*.9f,y+1.95f,z-.91f,.48f,.9f,.02f);
        B("Steel",x+side*1.48f,y+1.03f,z-.94f,.06f,.22f,.06f);
        B("Metal",x-side*.15f,y+2.05f,z+1.52f,.08f,.66f,.92f);
        Sign(Building+number+"\n"+type,V(x-side*.20f,y+2.05f,z+1.52f),new Vector3(-side,0,0),.145f,White);
    }
    static void Rail(Vector3 a,Vector3 b,bool glass){
        Tube("Wood",a+Vector3.up*1.15f,b+Vector3.up*1.15f,.045f);
        Tube("Steel",a+Vector3.up*.22f,b+Vector3.up*.22f,.018f);
        int count=Mathf.CeilToInt(Vector3.Distance(a,b)/.75f);
        for(int i=0;i<=count;i++){var p=Vector3.Lerp(a,b,(float)i/count);Tube("Metal",p,p+Vector3.up*1.12f,.023f);SoftBox("Steel",p+Vector3.up*.02f,V(.13f,.04f,.13f),.01f);}
        var d=b-a;var horizontal=new Vector3(d.x,0,d.z);float length=horizontal.magnitude;
        if(glass){var u=horizontal.normalized;var n=Vector3.Cross(u,Vector3.up);meshes["Glass"].Box((a+b)/2+Vector3.up*.65f,u,Vector3.up,n,V(length,.85f,.025f));}
        // Vertical rail volumes stop capsules falling through the gaps. Sloped
        // flights get short segments that follow the same stepped elevation.
        int sections=Mathf.Max(1,Mathf.CeilToInt(length/.35f));
        for(int i=0;i<sections;i++){var p=Vector3.Lerp(a,b,(i+.5f)/sections);Collider("Guard rail",p+Vector3.up*.62f,V(Mathf.Abs(d.x)/sections+.07f,1.24f,Mathf.Abs(d.z)/sections+.07f));}
    }
    static void Atrium(){
        if(floor>0){Rail(V(-7,y,-10),V(-7,y,2),true);Rail(V(7,y,-10),V(7,y,2),true);Rail(V(-7,y,2),V(7,y,2),true);Rail(V(-7,y,-10),V(7,y,-10),true);}
        else {
            for(int i=0;i<4;i++){B("Dark tile",0,.003f,-9.5f+i*3.6f,13.7f,.006f,.12f);}
            B("Wood",-6.72f,6,-3, .3f,12,9,"C atrium feature wall");
            for(float z=-7.4f;z<1.5f;z+=.45f)B("Oak pale",-6.54f,6,z,.04f,12,.025f);
            Sign(Building,V(-6.49f,6.8f,-3),Vector3.right,2.1f,Ink);
            Sign("教学楼\nTEACHING BUILDING",V(-6.48f,4.9f,-3),Vector3.right,.34f,Ink);
            Sign("博学笃行   致思明理",V(-6.47f,3.3f,-3),Vector3.right,.32f,Ink);
            Sign("KNOWLEDGE FOR A BETTER TOMORROW",V(-6.47f,2.6f,-3),Vector3.right,.13f,Ink);
            foreach(float z in new[]{-7.4f,1.3f})B("Light",-6.47f,6,z,.04f,12,.04f);
            Plant(V(-7.5f,0,-8.8f),1.6f);Plant(V(6.4f,0,-8.8f),1.5f);Plant(V(6.4f,0,.3f),1.8f);
            B("Metal",6.9f,2.1f,-2.8f,.16f,2.8f,2.5f,"Directory");
            Sign(DirectoryText(),V(6.79f,2.15f,-2.8f),Vector3.left,.16f,White);
            foreach(float x in new[]{-1.5f,1.5f}){B("Metal",x,1.45f,-11.82f,.07f,2.9f,.12f);B("Steel",x*.3f,1.2f,-11.7f,.05f,.65f,.08f);}
            B("Metal",0,2.95f,-11.8f,3.1f,.1f,.13f);Sign("出口  EXIT",V(0,3.35f,-11.72f),Vector3.forward,.24f,new Color(.12f,.37f,.23f));
        }
        if(floor>0)Sign((floor+1)+"F   "+Building+(floor+1)+"01–"+(floor+1)+(Building=="A"?"02":"04"),V(0,y-.43f,1.83f),Vector3.back,.25f,Ink);
        foreach(float x in new[]{-10f,10f}){B("Light",x,y+3.98f,-4,.06f,.035f,10);}
    }
    static void Desk(float x,float z,bool pc){
        SoftBox("Oak pale",V(x,y+.77f,z),V(1.6f,.065f,.68f),.025f,"Desk top");
        foreach(float dx in new[]{-.69f,.69f})foreach(float dz in new[]{-.25f,.25f})Tube("Metal",V(x+dx,y+.035f,z+dz),V(x+dx,y+.74f,z+dz),.022f);
        B("Metal",x,y+.66f,z-.2f,1.4f,.055f,.035f);
        SoftBox("Seat",V(x,y+.46f,z+.75f),V(.52f,.065f,.5f),.025f,"Chair seat");
        SoftBox("Seat",V(x,y+.85f,z+.97f),V(.52f,.36f,.055f),.024f);
        foreach(float dx in new[]{-.22f,.22f}){
            Tube("Metal",V(x+dx,y+.03f,z+.54f),V(x+dx,y+.45f,z+.54f),.017f);Tube("Metal",V(x+dx,y+.03f,z+.94f),V(x+dx,y+.96f,z+.96f),.017f);
        }
        if(pc){
            SoftBox("Metal",V(x,y+1.14f,z-.12f),V(.68f,.43f,.05f),.018f);B("Screen",x,y+1.15f,z-.091f,.625f,.37f,.009f);
            B("Metal",x,y+.9f,z-.15f,.08f,.27f,.08f);B("Metal",x,y+.83f,z-.13f,.32f,.025f,.22f);
            B("Metal",x-.09f,y+.835f,z+.19f,.46f,.025f,.15f);B("Steel",x+.37f,y+.85f,z+.2f,.065f,.035f,.1f);
            B("Metal",x+.59f,y+.35f,z,.21f,.5f,.43f);B("Light",x+.59f,y+.5f,z+.22f,.02f,.02f,.01f);
            for(int r=0;r<3;r++)for(int c=0;c<11;c++)B("Seat",x-.28f+c*.037f,y+.853f,z+.145f+r*.043f,.025f,.009f,.025f);
            B("Light",x-.18f,y+1.27f,z-.083f,.15f,.008f,.005f);B("Seat",x,y+1.12f,z-.083f,.48f,.18f,.005f);
            for(int line=0;line<4;line++)B("Ceramic",x-.05f,y+1.18f-line*.036f,z-.079f,.3f-line*.035f,.006f,.003f);
        } else {B("Ceramic",x+.3f,y+.819f,z,.3f,.012f,.22f);B("Screen",x+.3f,y+.83f,z,.28f,.01f,.025f);}
    }
    static void Rooms(){
        foreach(float side in new[]{-1f,1f})foreach(float start in new[]{2f,10f}){
            float cx=side*12,front=start==2?2:9.5f;bool pc=side>0;
            // Four generous desk aisles, a clear doorway lane at either end.
            for(int row=0;row<3;row++)for(int col=0;col<5;col++)Desk(cx-4.8f+col*2.4f,start+2.1f+row*1.43f,pc);
            B("Metal",cx,y+2.1f,front+.17f,9.4f,1.75f,.12f);B("Board",cx,y+2.1f,front+.25f,9.2f,1.6f,.035f);
            B("Projection",cx,y+2.1f,front+.29f,4.4f,1.62f,.025f);
            Sign(pc?"计算机实验教学\nCOMPUTER LAB":"探索 · 求知 · 成长\nMULTIMEDIA CLASSROOM",V(cx,y+2.13f,front+.32f),Vector3.forward,.19f,Ink);
            B("Wood",cx-3,y+.54f,start+.95f,1.45f,1.08f,.6f,"Lectern");B("Oak pale",cx-3,y+1.12f,start+.95f,1.55f,.1f,.7f);
            B("Metal",cx,y+3.28f,front+.15f,1,.32f,.08f);Sign("10:24",V(cx,y+3.28f,front+.2f),Vector3.forward,.15f,new Color(.9f,.25f,.13f));
            foreach(float dx in new[]{-5.8f,5.8f})B("Metal",cx+dx,y+3.1f,front+.23f,.28f,.55f,.25f);
            B("Metal",cx,y+3.6f,start+4,.05f,.9f,.05f);B("Ceramic",cx,y+3.2f,start+4,.55f,.18f,.38f);
            B("Screen",cx,y+3.2f,start+3.8f,.12f,.1f,.03f);
            for(float z=start+.5f;z<start+7;z+=2.8f){B("Curtain",side*19.7f,y+2.2f,z,.15f,2.3f,.4f);}
            for(int i=0;i<2;i++){
                float z=start+1.3f+i*3.3f;B("Metal",side*4.16f,y+2.2f,z,.03f,.82f,.62f);
                Sign(i==0?"知行合一":"学无止境",V(side*4.19f,y+2.2f,z),new Vector3(side,0,0),.13f,White);
            }
        }
    }
    static void Toilets(){
        foreach(float side in new[]{-1f,1f}){
            float cx=side*12,lo=cx-8,hi=cx+8;
            // One shared footprint for walls, ceiling, tile and services. The
            // front wall extends INTO both perimeter walls, with only a door gap.
            float leftEnd=cx-.95f,rightStart=cx+.95f;
            B("Wall",(lo+leftEnd)/2,y+2.1f,21.5f,leftEnd-lo+.12f,4.2f,.26f,"Washroom front wall");
            B("Wall",(rightStart+hi)/2,y+2.1f,21.5f,hi-rightStart+.12f,4.2f,.26f,"Washroom front wall");
            B("Wall",cx,y+3.6f,21.5f,2,1.2f,.26f,"Washroom lintel");
            foreach(float dx in new[]{-1f,1f})B("Trim",cx+dx,y+1.5f,21.5f,.12f,3,.32f);
            B("Trim",cx,y+3,21.5f,2.12f,.12f,.32f);
            SoftBox("Metal",V(cx,y+3.4f,21.32f),V(2,.48f,.055f),.02f);
            Sign(side<0?"男卫生间   MEN":"女卫生间   WOMEN",V(cx,y+3.4f,21.28f),Vector3.back,.15f,White);
            B("Stone",cx,y-.018f,25.72f,16.1f,.06f,8.65f,"Washroom floor finish");
            B("Dark tile",cx,y+.012f,21.5f,1.9f,.012f,.28f);
            // Continuous tiled walls and baseboards. Mirrors are fixed to the
            // solid stair-side wall, not suspended in front of a window.
            B("Stone",side*4.14f,y+1.8f,25.75f,.08f,3.6f,8.5f);
            B("Stone",cx,y+1.8f,29.8f,16,.036f+3.56f,.08f);
            foreach(float x in new[]{lo+.16f,hi-.16f})B("Trim",x,y+.075f,25.75f,.04f,.15f,8.5f);
            B("Trim",cx,y+.075f,29.72f,15.7f,.15f,.045f);
            foreach(float x in new[]{(lo+leftEnd)/2,(rightStart+hi)/2})B("Trim",x,y+.075f,21.67f,7.1f,.15f,.04f);
            B("Ceiling",cx,y+3.85f,25.75f,16.1f,.12f,8.6f,"Washroom ceiling");
            foreach(float x in new[]{lo+.18f,hi-.18f})B("Ceramic",x,y+3.76f,25.75f,.08f,.06f,8.5f);
            SoftBox("Cabinet",V(cx,y+1.25f,24.2f),V(3.2f,2.5f,.16f),.035f,"Washroom privacy screen");
            B("Wood",cx,y+2.52f,24.2f,3.25f,.035f,.18f);
            // Four aligned side panels, three doorways, closed terminal sides.
            for(int i=0;i<=3;i++){
                float x=cx-6+i*3;
                SoftBox("Cubicle",V(x,y+1.27f,28.05f),V(.085f,2.3f,3.7f),.018f,"Toilet partition");
                foreach(float z in new[]{26.4f,29.5f})Tube("Steel",V(x,y+.01f,z),V(x,y+.18f,z),.034f);
            }
            Tube("Steel",V(cx-6,y+2.46f,26.25f),V(cx+3,y+2.46f,26.25f),.025f);
            for(int i=0;i<3;i++){
                float x=cx-4.5f+i*3;
                foreach(float dx in new[]{-1.1f,1.1f})SoftBox("Cubicle",V(x+dx,y+1.27f,26.25f),V(.8f,2.3f,.085f),.018f,"Cubicle front panel");
                SoftBox("Cubicle",V(x-.65f,y+1.27f,26.94f),V(.07f,2.26f,1.33f),.018f,"Open cubicle door");
                Tube("Steel",V(x-.59f,y+1.12f,27.46f),V(x-.59f,y+1.3f,27.46f),.018f);
                SoftBox("Ceramic",V(x,y+.73f,29.44f),V(.64f,.72f,.24f),.07f,"Cistern");
                SoftBox("Ceramic",V(x,y+.21f,28.98f),V(.43f,.42f,.61f),.12f,"Toilet pedestal");
                Bowl("Ceramic",V(x,y+.48f,28.93f),.34f,.45f,.16f);
                SoftBox("Ceramic",V(x,y+.53f,29.32f),V(.6f,.08f,.2f),.035f);
                B("Steel",x+.17f,y+.95f,29.3f,.1f,.025f,.025f);
                SoftBox("Ceramic",V(x+1.38f,y+.85f,28.9f),V(.16f,.22f,.26f),.035f);
            }
            float sx=side*4.92f;
            foreach(float x in new[]{sx-.7f,sx+.7f})SoftBox("Cabinet",V(x,y+.43f,24.12f),V(.08f,.7f,3.92f),.02f);
            foreach(float z in new[]{22.2f,26.04f})B("Cabinet",sx,y+.43f,z,1.48f,.7f,.08f);
            B("Cabinet",sx,y+.14f,24.12f,1.48f,.1f,3.92f);Collider("Vanity cabinet",V(sx,y+.43f,24.12f),V(1.48f,.7f,3.92f));
            B("Metal",sx,y+.06f,24.12f,1.24f,.12f,3.65f);
            // Counter is assembled around real basin openings; bowls do not
            // intersect a solid slab through their centres.
            foreach(float x in new[]{sx-.66f,sx+.66f})B("Ceramic",x,y+.82f,24.12f,.18f,.08f,4.08f);
            for(int i=0;i<4;i++)B("Ceramic",sx,y+.82f,22.32f+i*1.2f,1.25f,.08f,.48f);
            for(int i=0;i<3;i++){
                float z=22.92f+i*1.2f;
                BasinSurround(V(sx,y+.86f,z));Bowl("Ceramic",V(sx,y+.861f,z),.5f,.30f,.12f);
                float wallX=side*4.22f;
                SoftBox("Trim",V(wallX,y+1.91f,z),V(.08f,1.48f,1.13f),.018f);
                B("Mirror",wallX+side*.048f,y+1.91f,z,.025f,1.38f,1.03f);
                B("Light",wallX+side*.06f,y+2.68f,z,.03f,.035f,1.12f);
                Tube("Steel",V(sx-side*.57f,y+.85f,z),V(sx-side*.57f,y+1.12f,z),.026f);
                Tube("Steel",V(sx-side*.57f,y+1.12f,z),V(sx-side*.28f,y+1.12f,z),.026f);
                B("Trim",sx+side*.746f,y+.43f,z-.58f,.014f,.58f,.014f);
                Tube("Metal",V(sx+side*.75f,y+.64f,z-.2f),V(sx+side*.75f,y+.64f,z+.2f),.012f);
            }
            SoftBox("Ceramic",V(side*4.31f,y+1.25f,26.38f),V(.24f,.34f,.23f),.035f);
            SoftBox("Metal",V(cx+5.8f,y+.35f,22.2f),V(.4f,.7f,.4f),.045f,"Waste bin");
            B("Steel",cx+4.5f,y+.006f,25.5f,.16f,.012f,.16f);
        }
    }
    static void Stairs(){
        const float start=21.7f,tread=.42f,rise=.175f;
        for(int i=0;i<12;i++){
            float top=y+(i+1)*rise,z=start+(i+.5f)*tread;
            B("Dark tile",-2,top-.12f,z,2.8f,.24f,tread,"Stair ascending tread "+i);
            B("Steel",-2,top+.006f,z-tread/2+.028f,2.75f,.012f,.045f);
            float top2=y+2.1f+(i+1)*rise,z2=start+(11.5f-i)*tread;
            B("Dark tile",2,top2-.12f,z2,2.8f,.24f,tread,"Stair return tread "+i);
            B("Steel",2,top2+.006f,z2+tread/2-.028f,2.75f,.012f,.045f);
        }
        Slab(-3.9f,3.9f,26.74f,30,y+2.1f,"Dark tile");
        foreach(float x in new[]{-3.43f,-.57f})Rail(V(x,y+.175f,21.7f),V(x,y+2.1f,26.74f),false);
        foreach(float x in new[]{.57f,3.43f})Rail(V(x,y+4.2f,21.7f),V(x,y+2.275f,26.74f),false);
        Rail(V(-3.9f,y+2.1f,29.7f),V(3.9f,y+2.1f,29.7f),false);
        // Closed risers/support cheeks and a broad turn landing.
        Sign((floor+1)+"F  →  "+(floor+2)+"F",V(-3.84f,y+3.1f,25),Vector3.right,.32f,Ink);
        Sign("上下楼梯  靠右通行",V(-3.84f,y+2.4f,25),Vector3.right,.14f,Ink);
    }
    static void Ceilings(){
        // Upper slab undersides and acoustic panels in occupied rooms.
        foreach(float side in new[]{-1f,1f})foreach(float start in RoomStarts){
            float front=start==2?2:9.5f,back=Building=="A"?17.2f:start==2?9.5f:17.2f;
            B("Ceiling",side*12,y+3.85f,(front+back)/2,16.1f,.12f,back-front+.22f,"Classroom ceiling");
            foreach(float z in new[]{front+.15f,back-.15f})B("Ceramic",side*12,y+3.76f,z,15.8f,.06f,.07f);
            for(float x=side*12-6.5f;x<side*12+7;x+=1.2f)B("Dado",x,y+3.783f,(front+back)/2,.009f,.012f,back-front-.3f);
            for(float z=start+.7f;z<back-.2f;z+=1.2f)B("Dado",side*12,y+3.783f,z,15.8f,.012f,.009f);
            for(float z=start+1.2f;z<back-.3f;z+=2.1f)foreach(float x in new[]{side*12-4,side*12,side*12+4}){SoftBox("Trim",V(x,y+3.76f,z),V(2.1f,.06f,.2f),.015f);B("Light",x,y+3.725f,z,2,.018f,.13f);}
            FillLight(V(side*12,y+3.55f,start+3.6f),1.8f,9);if(Building=="A")FillLight(V(side*12,y+3.55f,13),1.8f,9);
        }
        B("Ceiling",0,y+3.85f,11.8f,8.1f,.12f,19.6f,"Corridor ceiling");
        foreach(float x in new[]{-3.84f,3.84f}){B("Ceramic",x,y+3.76f,11.8f,.08f,.06f,19.6f);B("Light",x,y+3.73f,11.8f,.035f,.025f,19.6f);}
        for(float z=3;z<21;z+=3){SoftBox("Trim",V(0,y+3.76f,z),V(2.4f,.06f,.22f),.015f);B("Light",0,y+3.725f,z,2.3f,.018f,.16f);}
        foreach(float x in new[]{-12f,12f})foreach(float z in new[]{23f,26f,29f}){B("Trim",x,y+3.76f,z,2.5f,.05f,.25f);B("Light",x,y+3.725f,z,2.4f,.018f,.18f);}
        FillLight(V(0,y+3.6f,10),1.5f,11);FillLight(V(0,y+3.5f,26),1.6f,8);
        foreach(float x in new[]{-12f,12f})FillLight(V(x,y+3.6f,25),1.5f,9);
        if(floor==4){
            B("Glass",0,21.1f,-4,14,.08f,12);for(float z=-10;z<=2;z+=2)B("Metal",0,21,z,14,.12f,.09f);
            B("Wall",-13.5f,21.05f,9,13,.18f,42,"Roof");B("Wall",13.5f,21.05f,9,13,.18f,42,"Roof");B("Wall",0,21.05f,16,14,.18f,28,"Roof");
        }
    }
    static void FillLight(Vector3 p,float intensity,float range){var l=new GameObject("室内照明").AddComponent<Light>();l.transform.SetParent(level,false);l.transform.position=p;l.transform.rotation=Quaternion.Euler(90,0,0);l.type=LightType.Spot;l.spotAngle=135;l.innerSpotAngle=95;l.color=new Color(1,.96f,.88f);l.intensity=intensity*2;l.range=range;l.shadows=LightShadows.None;}
    static void Plant(Vector3 p,float h){
        Box("Stone",p+Vector3.up*.3f,V(.75f,.6f,.75f),"Plant pot");Box("Soil",p+Vector3.up*.61f,V(.65f,.035f,.65f));
        for(int i=0;i<17;i++){
            float a=i*2.39996f;var tip=p+V(Mathf.Cos(a)*.55f,.6f+h*(.4f+(i%4)*.18f),Mathf.Sin(a)*.55f);
            Beam("Wood",p+Vector3.up*.6f,tip,.025f);var u=new Vector3(Mathf.Cos(a),.15f,Mathf.Sin(a));var v=Vector3.Cross(u,Vector3.up).normalized;
            meshes[i%2==0?"Leaf":"Leaf light"].Quad(tip-u*.38f,tip+v*.14f,tip+u*.13f,tip-v*.14f);
            for(int j=0;j<5;j++){
                var leaf=Vector3.Lerp(p+Vector3.up*.8f,tip,.45f+j*.14f);var axis=new Vector3(Mathf.Cos(a+j),.5f,Mathf.Sin(a+j)).normalized;var cross=Vector3.Cross(axis,Vector3.up).normalized;
                meshes[j%2==0?"Leaf":"Leaf light"].Quad(leaf-axis*.08f,leaf+cross*.13f,leaf+axis*.42f,leaf-cross*.13f);
            }
        }
    }
}
