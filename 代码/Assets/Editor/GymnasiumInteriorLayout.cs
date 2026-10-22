using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static partial class GymnasiumInteriorBuilder
{
    [MenuItem("Campus/Interiors/Generate Modern Gymnasium")]
    public static void Generate(){
        Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Meshes");AssetDatabase.Refresh();
        materials=new Dictionary<string,Material>();
        Mat("Floor","#b3b6b4",.4f,1);Mat("Wall","#e1ded4",.15f);Mat("Stone","#cfc8b8",.3f,1);
        Mat("Wood","#a87f4c",.38f,0,1);Mat("Oak pale","#ceac78",.48f,0,1);Mat("Walnut","#64442e",.35f,0,1);
        Mat("Metal","#4d5d69",.55f);Mat("Steel","#a5b5be",.8f);Mat("Glass","#afcad7",.8f,0,0,0,true);
        Mat("Mirror","#a0bac5",.97f);materials["Mirror"].SetFloat("_Metallic",.6f);
        Mat("Light","#fff0d1",.25f,0,0,2);Mat("Cool light","#dcefff",.3f,0,0,1);
        Mat("Navy","#24485f",.28f);Mat("Teal","#327e7e",.28f);Mat("Green","#267b60",.2f);
        Mat("Blue","#285c9b",.32f);Mat("White","#eeeadd",.28f);Mat("Rubber","#464c50",.08f,1);
        Mat("Black","#141c23",.18f);Mat("Orange","#cd7039",.22f);Mat("Red","#ba4d40",.3f);Mat("Yellow","#d2b346",.3f);
        Mat("Felt","#22684b",.03f);Mat("Screen","#4b91a5",.4f,0,0,.4f);
        Mat("Leaf","#3e6347");Mat("Leaf light","#6b8856");Mat("Soil","#443b2d");
        lettering=Save(new Material(Shader.Find("Campus/WallLettering")),Folder+"/Materials/Lettering.mat");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        root=new GameObject("体育馆 · MOVE PLAY CONNECT").transform;
        var contract=root.gameObject.AddComponent<InteriorSceneRoot>();contract.buildingId="gymnasium";contract.isPlaceholder=false;
        contract.backgroundColor=new Color(.64f,.77f,.87f);contract.showExitMarkers=false;contract.fallRecoveryHeight=-8;
        var spawn=new GameObject("main · 接待大厅").transform;spawn.SetParent(root,false);spawn.position=V(0,.06f,-19);
        var exit=new GameObject("正门 · E 返回校园").transform;exit.SetParent(root,false);exit.position=V(0,.06f,-23);
        contract.spawnPoints=new[]{new InteriorSpawnPoint{id="main",anchor=spawn}};contract.exits=new[]{exit};
        root.gameObject.AddComponent<TeachingCInteriorPresentation>();root.gameObject.AddComponent<GymnasiumLighting>();
        AssetDatabase.StartAssetEditing();
        try { for(int f=0;f<3;f++){
            floor=f;y=f*Storey;level=new GameObject((f+1)+"F · "+new[]{"活力 / 排球 健身 舞蹈","竞技 / 羽毛球 篮球","交流 / 乒乓球 台球"}[f]).transform;level.SetParent(root,false);
            meshes=new Dictionary<string,DormitoryBuilder.Geometry>();foreach(var k in materials.Keys)meshes[k]=new DormitoryBuilder.Geometry();
            Structure();Lobby();if(f<2)Stairs();else Rail(V(-3.8f,y,16),V(.5f,y,16));
            if(f==0){Volleyball();Fitness();Dance();}else if(f==1){Badminton();Basketball();}else{TableTennis();Billiards();}
            FlushMeshes();
            foreach(float x in new[]{-23f,23f}){
                var probe=new GameObject("Hall reflection").AddComponent<ReflectionProbe>();probe.transform.SetParent(root,false);probe.transform.position=V(x,y+2.6f,7);
                probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.resolution=128;probe.size=V(37,8.2f,43);probe.boxProjection=true;probe.nearClipPlane=.2f;probe.farClipPlane=65;probe.intensity=.7f;
            }
        }
        } finally {AssetDatabase.StopAssetEditing();}
        var sun=new GameObject("日光").AddComponent<Light>();sun.transform.SetParent(root,false);sun.type=LightType.Directional;sun.color=new Color(1,.95f,.85f);sun.intensity=1.15f;sun.transform.rotation=Quaternion.Euler(38,-65,0);sun.shadows=LightShadows.Soft;sun.shadowBias=.02f;sun.shadowNormalBias=.12f;
        RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.72f,.79f);RenderSettings.ambientEquatorColor=new Color(.47f,.50f,.52f);RenderSettings.ambientGroundColor=new Color(.28f,.28f,.27f);RenderSettings.fog=false;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();var color=profile.Add<ColorAdjustments>(true);color.contrast.Override(7);color.postExposure.Override(.2f);color.saturation.Override(-4);
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.09f);bloom.threshold.Override(1.3f);
        var old=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/Volume.asset");if(old){foreach(var c in old.components)Object.DestroyImmediate(c,true);old.components.Clear();foreach(var c in profile.components){AssetDatabase.AddObjectToAsset(c,old);old.components.Add(c);}profile.components.Clear();Object.DestroyImmediate(profile);profile=old;EditorUtility.SetDirty(old);}else{AssetDatabase.CreateAsset(profile,Folder+"/Volume.asset");foreach(var c in profile.components)AssetDatabase.AddObjectToAsset(c,profile);}
        var volume=root.gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=22;volume.sharedProfile=profile;
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,"Assets/Interiors/Scenes/gymnasium.unity");
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");var game=Object.FindFirstObjectByType<CampusGame>();if(!game.GetComponent<GymnasiumInteriorQA>())game.gameObject.AddComponent<GymnasiumInteriorQA>();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("GYM_INTERIOR_GENERATED: three floors, seven sports spaces, 96 physical risers");
    }
    public static void GenerateAndBuild(){Generate();CampusBuilder.Build();}
    static void Structure(){
        Slab(-42,-4,-24,30,y);Slab(4,42,-24,30,y);Slab(-4,4,-24,16,y);if(floor==0)Slab(-4,4,16,30,y);
        // Continuous envelope. All windows retain collision even though glazing is transparent.
        foreach(float x in new[]{-42f,42f}){
            B("Stone",x,y+.45f,3,.35f,.9f,54,"Window sill wall");B("Wall",x,y+7.65f,3,.35f,1.5f,54,"Window lintel");
            for(float z=-21;z<30;z+=6){B("Wall",x,y+4.2f,z-3,.5f,8.4f,.45f,"Outer pier");
                B("Glass",x,y+3.9f,z,.08f,5.8f,5.55f,"Sealed window");
                for(float h=1;h<7;h+=1.9f)B("Metal",x,y+h,z,.14f,.07f,5.6f);
                foreach(float dz in new[]{-2.8f,0f,2.8f})B("Metal",x,y+3.9f,z+dz,.14f,5.85f,.075f);
            }
        }
        foreach(float z in new[]{-24f,30f}){
            B("Wall",0,y+4.2f,z,84,8.4f,.35f,"Front or rear wall");
            for(float x=-39;x<42;x+=6){B("Stone",x,y+2.2f,z+(z<0?.2f:-.2f),5.9f,4.4f,.12f);}
        }
        B("Glass",0,y+2.2f,-23.76f,5.4f,4.4f,.06f);foreach(float x in new[]{-2.7f,-.9f,.9f,2.7f})B("Metal",x,y+2.2f,-23.69f,.065f,4.4f,.12f);
        // Corridor boundaries with generous clear openings centred at z=-4 and z=12.
        foreach(float x in new[]{-4.5f,4.5f}){
            foreach(var s in new[]{new Vector2(-14,-6),new Vector2(-2,10),new Vector2(14,30)}){
                float z=(s.x+s.y)/2,d=s.y-s.x;B("Wall",x,y+4.2f,z,.36f,8.4f,d,"Hall wall");
                B("Wood",x+(x>0?-.2f:.2f),y+1.15f,z,.05f,2.3f,d);B("Metal",x+(x>0?-.23f:.23f),y+.08f,z,.04f,.16f,d);
            }
            foreach(float z in new[]{-4f,12f}){
                B("Wall",x,y+5.85f,z,.36f,5.1f,4,"Door lintel");
                foreach(float dz in new[]{-2.04f,2.04f})B("Metal",x,y+1.65f,z+dz,.5f,3.3f,.09f);
                B("Metal",x,y+3.3f,z,.5f,.09f,4.15f);
                // Fixed narrow glazing beside the open passage, no door collider across the route.
                Sign(x<0?(floor==0?"01  排球馆":floor==1?"04  羽毛球馆":"06  乒乓球馆"):(floor==0?(z<0?"02  健身房":"03  舞蹈室"):floor==1?"05  篮球馆":"07  台球馆"),V(x+(x>0?-.26f:.26f),y+3.7f,z),x<0?Vector3.right:Vector3.left,.32f,Ink);
            }
        }
        // Hall front walls stop at lobby; continuous open central spine.
        foreach(float x in new[]{-23.25f,23.25f}){
            B("Wall",x,y+4.2f,-14,37.5f,8.4f,.3f,"Hall front wall");
            B("Stone",x,y+1.3f,-13.8f,37.5f,2.6f,.08f);
        }
        B("Wall",0,y+4.2f,29.9f,8,8.4f,.25f,"Stair rear wall");
        // Sports trusses and acoustic ceilings. Lobby has its own lowered soffit.
        foreach(float cx in new[]{-23.3f,23.3f}){
            B("Wall",cx,y+8.05f,8,37.4f,.18f,43.8f,"Hall ceiling");
            for(float z=-10;z<30;z+=7){
                Beam("Metal",V(cx-18,y+7.85f,z),V(cx+18,y+7.85f,z),.12f);Beam("Metal",V(cx-18,y+7.1f,z),V(cx+18,y+7.1f,z),.10f);
                for(float x=cx-18;x<cx+17;x+=3){Beam("Metal",V(x,y+7.1f,z),V(x+1.5f,y+7.85f,z),.055f);Beam("Metal",V(x+1.5f,y+7.85f,z),V(x+3,y+7.1f,z),.055f);}
                foreach(float x in new[]{cx-11,cx,cx+11}){B("Metal",x,y+6.98f,z,3,.12f,.35f);B("Light",x,y+6.9f,z,2.85f,.025f,.26f);Fill(V(x,y+6.7f,z),3.5f,13);}
            }
            for(float z=-10;z<29;z+=6){B("Navy",cx<0?-41.7f:41.7f,y+1.1f,z,.16f,1.6f,5.7f);}
        }
        B("Wall",0,y+4.3f,-19,83.6f,.2f,9.6f,"Lobby soffit");B("Wall",0,y+4.3f,1,8.5f,.2f,30,"Spine ceiling");
        foreach(float x in new[]{-3.7f,3.7f})B("Light",x,y+4.15f,1,.06f,.04f,29);
        for(float z=-19;z<17;z+=7){B("Light",0,y+4.15f,z,3,.04f,.12f);Fill(V(0,y+3.8f,z),2.7f,9);}
        if(floor==2)B("Wall",0,y+8.05f,23,8,.18f,14,"Stair roof");
    }
    static void Lobby(){
        string accent=floor==0?"Teal":floor==1?"Blue":"Walnut";
        B(accent,0,y+.008f,-17,7.8f,.015f,.6f);
        B("Navy",-8,y+2.1f,-14.2f,5.8f,3.7f,.15f);
        Sign((floor+1)+"F",V(-8,y+3,-14.31f),Vector3.back,1.25f,White);
        Sign(new[]{"MOVE · 活力","PLAY · 竞技","CONNECT · 交流"}[floor],V(-8,y+1.95f,-14.31f),Vector3.back,.36f,White);
        Sign(new[]{"排球  /  健身  /  舞蹈","羽毛球  /  篮球","乒乓球  /  台球"}[floor],V(-8,y+1.3f,-14.31f),Vector3.back,.26f,White);
        B("Stone",8,y+2,-14.2f,6,3.5f,.15f);
        Sign("CAMPUS ATHLETICS",V(8,y+2.7f,-14.31f),Vector3.back,.38f,Ink);
        Sign("3F   乒乓球 · 台球\n2F   羽毛球 · 篮球\n1F   排球 · 健身 · 舞蹈",V(8,y+1.8f,-14.31f),Vector3.back,.28f,Ink);
        B("Navy",0,y+3.8f,15.7f,7.9f,.7f,.1f);Sign("楼梯  ↑   STAIRS",V(0,y+3.8f,15.63f),Vector3.back,.35f,White);
        foreach(float x in new[]{-12f,12f}){Plant(V(x,y,-15.1f),1.25f);Bench(x,y,-21,3.6f);}
        if(floor==0){
            SoftBox("Stone",V(-20,y+.55f,-16),V(6,1.1f,1.4f),.08f,"Reception desk");B("Wood",-20,y+1.13f,-16,6.1f,.08f,1.5f);
            B("Screen",-20,y+1.4f,-16,.55f,.35f,.08f);B("Metal",-20,y+1.2f,-16,.08f,.3f,.08f);
            Sign("运动服务  /  RECEPTION",V(-20,y+.6f,-16.73f),Vector3.back,.3f,Ink);
            Sign("出口  EXIT · E 返回校园",V(0,y+3.5f,-23.48f),Vector3.forward,.36f,Ink);
        }
        for(int i=0;i<12;i++){
            float x=18+i*1.35f;SoftBox(accent,V(x,y+1.1f,-14.65f),V(1.28f,2.2f,.7f),.035f,"Locker");
            foreach(float h in new[]{.6f,1.65f}){B("Metal",x+.42f,y+h,-15.02f,.045f,.19f,.04f);B("Black",x,y+h+.26f,-15.015f,.35f,.025f,.02f);}
        }
    }
    static void Stairs(){
        const float start=16,tread=.36f,rise=.175f;
        for(int i=0;i<24;i++){
            float top=y+(i+1)*rise,z=start+(i+.5f)*tread;B("Floor",-2,top-.12f,z,2.9f,.24f,tread,"Gym stair ascending "+i);
            B("Metal",-2,top+.003f,z-tread/2+.03f,2.85f,.006f,.045f);
            float t=y+4.2f+(i+1)*rise,z2=start+(23.5f-i)*tread;B("Floor",2,t-.12f,z2,2.9f,.24f,tread,"Gym stair return "+i);
            B("Metal",2,t+.003f,z2+tread/2-.03f,2.85f,.006f,.045f);
        }
        Slab(-4,4,24.64f,29.7f,y+4.2f);
        foreach(float x in new[]{-3.48f,-.52f})Rail(V(x,y+.175f,16),V(x,y+4.2f,24.64f));
        foreach(float x in new[]{.52f,3.48f})Rail(V(x,y+8.4f,16),V(x,y+4.375f,24.64f));
        Rail(V(-3.8f,y+4.2f,29.5f),V(3.8f,y+4.2f,29.5f));
        Sign((floor+1)+"F → "+(floor+2)+"F",V(0,y+5.8f,29.72f),Vector3.back,.75f,Ink);
        Fill(V(0,y+7.5f,27),3.2f,12);
    }
    static void Rail(Vector3 a,Vector3 b){
        Tube("Wood",a+Vector3.up*1.1f,b+Vector3.up*1.1f,.045f);Tube("Metal",a+Vector3.up*.15f,b+Vector3.up*.15f,.025f);
        int n=Mathf.CeilToInt(Vector3.Distance(a,b)/.65f);for(int i=0;i<=n;i++){var p=Vector3.Lerp(a,b,i/(float)n);Tube("Metal",p,p+Vector3.up*1.08f,.022f);}
        var go=new GameObject("Stair guard collider");go.transform.SetParent(level,false);go.transform.position=(a+b)/2+Vector3.up*.57f;
        go.transform.rotation=Quaternion.FromToRotation(Vector3.forward,(b-a).normalized);var c=go.AddComponent<BoxCollider>();c.size=V(.12f,1.14f,Vector3.Distance(a,b)+.08f);
    }
    static void Fill(Vector3 p,float intensity,float range,bool warm=false){var l=new GameObject("Architectural light").AddComponent<Light>();l.transform.SetParent(level,false);l.transform.position=p;l.transform.rotation=Quaternion.Euler(90,0,0);l.type=LightType.Spot;l.spotAngle=125;l.innerSpotAngle=90;l.intensity=intensity*7;l.range=range;l.color=warm?new Color(1,.79f,.53f):new Color(1,.96f,.89f);l.shadows=LightShadows.None;l.cullingMask=(1<<(26+floor))|(1<<2);}
    static void Bench(float x,float h,float z,float width){
        SoftBox("Wood",V(x,h+.48f,z),V(width,.12f,.55f),.04f,"Bench seat");foreach(float dx in new[]{-width*.38f,width*.38f})B("Metal",x+dx,h+.22f,z,.08f,.44f,.42f);
    }
    static void FlushMeshes(){
        foreach(var pair in meshes){
            if(pair.Value.vertices.Count==0)continue;
            var whole=pair.Value.Mesh(pair.Key);SmoothEdges(whole);var vertices=whole.vertices;var normals=whole.normals;var indices=whole.triangles;
            var buckets=new Dictionary<Vector3Int,List<int>>();
            for(int i=0;i<indices.Length;i+=3){var center=(vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3;
                var key=new Vector3Int(Mathf.FloorToInt(center.x/12),Mathf.FloorToInt(center.y/8.4f),Mathf.FloorToInt(center.z/12));
                if(!buckets.TryGetValue(key,out var list)){list=new List<int>();buckets.Add(key,list);}list.Add(indices[i]);list.Add(indices[i+1]);list.Add(indices[i+2]);
            }
            foreach(var bucket in buckets){
                var vs=new List<Vector3>();var ns=new List<Vector3>();var ts=new List<int>();foreach(var i in bucket.Value){ts.Add(vs.Count);vs.Add(vertices[i]);ns.Add(normals[i]);}
                var mesh=new Mesh{name=pair.Key,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vs);mesh.SetNormals(ns);mesh.SetTriangles(ts,0);mesh.RecalculateBounds();
                string name="F"+(floor+1)+"-"+pair.Key+"-"+bucket.Key.x+"_"+bucket.Key.y+"_"+bucket.Key.z;
                var obj=new GameObject(name);obj.layer=26+floor;obj.transform.SetParent(level,false);obj.AddComponent<MeshFilter>().sharedMesh=Save(mesh,Folder+"/Meshes/"+name+".asset");
                var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=materials[pair.Key];if(pair.Key.Contains("Light")||pair.Key=="Glass")r.shadowCastingMode=ShadowCastingMode.Off;
            }
            Object.DestroyImmediate(whole);
        }
    }

}
