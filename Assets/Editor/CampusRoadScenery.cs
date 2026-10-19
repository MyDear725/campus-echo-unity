using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static partial class CampusPlanBuilder
{
    static bool FreeScenery(float x,float z,float radius=1){
        if(x< -280||x>301||z< -175||z>207)return false;
        if(Vector2.Distance(new Vector2(x,z),new Vector2(35,10))<7.5f+radius)return false;
        if(x>87&&x<205&&Mathf.Abs(z-10)<4+radius)return false;
        if(data.roads.Any(r=>r.kind!="bridge"&&CampusPlanRoadNetwork.Distance(x,z,r)<r.width/2+radius+.25f))return false;
        foreach(var f in data.features.Where(f=>f.kind=="building"||f.kind=="water"||f.kind=="track"||f.kind=="court")){
            if(CampusCollision.Inside(x,z,f.points))return false;
            for(int i=0;i<f.points.Length;i++)if(CampusCollision.Segment(x,z,f.points[i],f.points[(i+1)%f.points.Length])<radius+.65f)return false;
        }
        if(game.Interiors!=null&&game.Interiors.entrances.Any(e=>e&&Vector2.Distance(new Vector2(x,z),new Vector2(e.approachMapPosition.x,e.approachMapPosition.z))<5+radius))return false;
        // Interior travel is not initialized in edit mode; use the serialized component too.
        if(game.GetComponent<BuildingInteriorTravel>().entrances.Any(e=>e&&Vector2.Distance(new Vector2(x,z),new Vector2(e.approachMapPosition.x,e.approachMapPosition.z))<5+radius))return false;
        return true;
    }
    static CampusRoad FindRoad(string name)=>data.roads.First(r=>r.name==name);
    static void DressRoute(string name,float spacing=11,bool low=false,bool both=true){
        var r=FindRoad(name);float along=spacing*.35f;int index=0;
        for(int i=1;i<r.points.Length;i++){
            var a=new Vector2(r.points[i-1].x,r.points[i-1].z);var b=new Vector2(r.points[i].x,r.points[i].z);var delta=b-a;var dir=delta.normalized;var n=new Vector2(-dir.y,dir.x);
            while(along<delta.magnitude){var p=a+dir*along;
                foreach(float side in both?new[]{-1f,1f}:new[]{-1f}){
                    var t=p+n*side*(r.width/2+2.15f);
                    if(FreeScenery(t.x,t.y,1.2f))SceneryTree(t.x,t.y,.92f+(index%4)*.06f);
                    var l=p+n*side*(r.width/2+.65f)+dir*2.4f;
                    if(index%2==0&&FreeScenery(l.x,l.y,.22f))SceneryLamp(l.x,l.y,low);
                    var bench=p+n*side*(r.width/2+1.6f)-dir*3.7f;
                    float yaw=Mathf.Atan2(-n.x*side,-n.y*side)*Mathf.Rad2Deg+180;
                    if(index%3==1&&FreeScenery(bench.x,bench.y,1.35f)){
                        GroundRect("Paving",bench.x-1.7f,bench.x+1.7f,bench.y-1.2f,bench.y+1.2f,.06f);
                        SceneryBench(bench.x,bench.y,yaw);
                    }
                    for(int j=0;j<4;j++){var s=p+n*side*(r.width/2+1f)+dir*(j*1.3f-1.4f);if(FreeScenery(s.x,s.y,.55f)){Shrubs(s.x,s.y,.55f);if(j%2==0)OrnamentalGrass(s.x+n.x*.4f,s.y+n.y*.4f);}}
                }
                index++;along+=spacing;
            }along-=delta.magnitude;
        }
    }
    static void Rail(Vector3 a,Vector3 b,bool collision=true){
        float length=Vector3.Distance(a,b);int count=Mathf.CeilToInt(length/1.8f);
        for(int i=0;i<=count;i++){var q=Vector3.Lerp(a,b,(float)i/count);Box("Metal",q+Vector3.up*.56f,V(.065f,1.12f,.065f));Box("Metal",q+Vector3.up*.025f,V(.16f,.05f,.16f));}
        for(float h=.24f;h<1;h+=.23f)Beam("Metal",a+Vector3.up*h,b+Vector3.up*h,.018f);
        Beam("Wood",a+Vector3.up*1.1f,b+Vector3.up*1.1f,.085f);
        if(collision){var q=new GameObject("Lakeside safety railing"){layer=9};q.transform.SetParent(physicsRoot,false);q.transform.position=game.MapToWorld((a+b)/2+Vector3.up*.55f);q.transform.rotation=Quaternion.LookRotation(game.MapToWorld(b-a));q.AddComponent<BoxCollider>().size=V(.1f,1.1f,length);}
    }
    static void LakeEdges(string roadName,int featureId){
        var road=FindRoad(roadName);var lake=data.features.First(f=>f.id==featureId);
        var centre=new Vector2(lake.points.Average(p=>p.x),lake.points.Average(p=>p.z));
        for(int i=1;i<road.points.Length;i++){
            var a=new Vector2(road.points[i-1].x,road.points[i-1].z);var b=new Vector2(road.points[i].x,road.points[i].z);var n=new Vector2(-(b-a).y,(b-a).x).normalized;
            if(Vector2.Dot(n,centre-(a+b)/2)<0)n=-n;
            int steps=Mathf.CeilToInt(Vector2.Distance(a,b)/2);
            for(int k=0;k<steps;k++){
                var p=Vector2.Lerp(a,b,(float)k/steps)+n*(road.width/2+.3f);var q=Vector2.Lerp(a,b,(float)(k+1)/steps)+n*(road.width/2+.3f);var mid=(p+q)/2;
                if(data.roads.Any(r=>r!=road&&CampusPlanRoadNetwork.Distance(mid.x,mid.y,r)<r.width/2+.55f))continue;
                Rail(V(p.x,H(p.x,p.y)+.08f,p.y),V(q.x,H(q.x,q.y)+.08f,q.y));
                // Sloping stone/soil shoulder, with a low toe toward the water. Keep the walk surface level.
                var pp=p+n*1.1f;var qq=q+n*1.1f;
                geometry["Stone"].Quad(V(p.x,H(p.x,p.y)+.02f,p.y),V(q.x,H(q.x,q.y)+.02f,q.y),V(qq.x,-.05f,qq.y),V(pp.x,-.05f,pp.y));
                if(k%2==0){var s=mid-n*.15f;OrnamentalGrass(s.x,s.y);}
            }
        }
    }
    static void ViewingDeck(){
        // A side pocket on the west shore; the loop remains clear and the deck has an open approach.
        const float x=-78,z=23;float y=H(x,z)+.095f;
        GroundRect("Aggregate",-83,-74,19.5f,27,.055f);
        Box("Metal",V(-81,y-.08f,z),V(4,.14f,6.4f));
        for(int i=0;i<30;i++)Box("Wood",V(-81,y,z-3+i*.207f),V(4,.08f,.193f));
        ColliderBox("Viewing deck",V(-81,y-.06f,z),V(4,.14f,6.4f));
        Rail(V(-83,y,19.8f),V(-83,y,26.2f));Rail(V(-83,y,19.8f),V(-79,y,19.8f));Rail(V(-83,y,26.2f),V(-79,y,26.2f));
        SceneryBench(-82,23,270);
    }
    static void PlazaArc(float cx,float cz,float radius,float start,float end){
        for(float a=start;a<end;a+=3){float aa=a*Mathf.Deg2Rad,b=Mathf.Min(end,a+3)*Mathf.Deg2Rad;Vector3 Q(float t,float r)=>V(cx+Mathf.Cos(t)*r,H(cx+Mathf.Cos(t)*r,cz+Mathf.Sin(t)*r)+.104f,cz+Mathf.Sin(t)*r);GroundPolygon("DarkBand",60,Q(aa,radius),Q(b,radius),Q(b,radius+.18f),Q(aa,radius+.18f));}
    }
    static void BuildRoadScenery(){
        // 01: separate pedestrian approach next to the retained campus vehicle loop.
        DressRoute("入口林荫步行带",10);
        // 02: the existing curved B, northern A and eastern C stay at their mapped positions.
        DressRoute("教学内庭南入口",10,false,true);
        foreach(var p in new[]{new Vector2(66,-68),new Vector2(65,-91)})if(FreeScenery(p.x,p.y,2.1f))ReadingTable(p.x,p.y,90);
        // 03: keep the library forecourt and doorway clear, with an arc motif in the reading edges.
        DressRoute("湖畔至书韵广场",12,true);
        DressRoute("图书馆北侧路",12);
        // Unify the existing gold-coloured circular plaza without moving its perimeter or entries.
        var plaza=data.features.First(f=>f.id==2304850);var centre=V(plaza.points.Average(p=>p.x),.325f,plaza.points.Average(p=>p.z));
        for(int i=0;i<plaza.points.Length;i++){var a=plaza.points[i];var b=plaza.points[(i+1)%plaza.points.Length];GroundPolygon("Paving",20,centre,V(a.x,.325f,a.z),V(b.x,.325f,b.z));}
        PlazaArc(26,104,18,25,150);PlazaArc(26,104,19,25,150);PlazaArc(26,104,17,200,310);
        foreach(var p in new[]{new Vector2(8,112),new Vector2(17,119),new Vector2(36,117)})if(FreeScenery(p.x,p.y,2.2f)){ReadingTable(p.x,p.y);SceneryTree(p.x-3,p.y+2);}
        // 04: both existing lakes, continuous aggregate paths, shore rails and a reachable deck.
        DressRoute("思源湖环湖步道",14,true,false);DressRoute("涌泉湖环湖步道",12,true,false);
        LakeEdges("思源湖环湖步道",2304775);LakeEdges("涌泉湖环湖步道",39328846);ViewingDeck();
        // 05: no turning flights; decorate only the sides of the existing straight approaches.
        BuildBridgeArrivalGarden();
        foreach(float x in new[]{87f,207f})foreach(float z in new[]{3f,17f}){
            if(FreeScenery(x,z,1.5f)){SceneryBed(x,z,3,1.7f);SceneryLamp(x-2,z);}
        }
        foreach(var p in new[]{new Vector2(86,5),new Vector2(205,17)})if(FreeScenery(p.x,p.y,1.35f))SceneryBench(p.x,p.y,90);
        // Structural fascia and stair stringers make the roofed bridge readable from below.
        foreach(float z in new[]{7.65f,12.35f}){Box("Metal",V(150,5.98f,z),V(89,.38f,.24f));}
        foreach(var end in new[]{new Vector2(105.5f,91.82f),new Vector2(194.5f,202.06f)})foreach(float z in new[]{7.9f,12.1f})Beam("Metal",V(end.x,6.2f,z),V(end.y,H(end.y,10)-.08f,z),.22f);
        // 06: human-scale unit pavers, shaded entrances, dedicated cycle bays.
        DressRoute("生活区西侧坡道主路",14);DressRoute("公寓花园中轴",10);DressRoute("一二舍入户步道",12);DressRoute("四五舍南侧步道",12);
        if(FreeScenery(288,-119,2.3f)){GroundRect("Paving",285,291,-121,-117,.07f);CycleParking(288,-119,0);}
        if(FreeScenery(284,-67,2.1f))CycleParking(284,-67,0,4);
        // 07: open central approach on z=10, side seating, planting, cycle parking and bins.
        GroundRect("Paving",203,219.8f,2,18,.061f);
        foreach(float z in new[]{2.1f,17.9f}){GroundRect("DarkBand",203,220,z,z+.16f,.072f);}
        foreach(var p in new[]{new Vector2(215,-2),new Vector2(215,23)})if(FreeScenery(p.x,p.y,2.2f)){ReadingTable(p.x,p.y,90);SceneryTree(p.x-2,p.y+3);}
        if(FreeScenery(214.5f,28,2.3f))CycleParking(214.5f,28,0,5);
        if(FreeScenery(214.5f,33,1.4f))WasteStation(214.5f,33);
        DressRoute("食堂南广场",13);
        // 08: independent brick-red sport ribbon alongside the grey gym walk, never in the track itself.
        DressRoute("体育馆至教学区主路",16,false,false);
        for(float x=-182;x< -122;x+=16){if(FreeScenery(x,9,1.35f))SceneryBench(x,9);if(FreeScenery(x+5,9,.3f))SceneryLamp(x+5,9);}
        float fy=H(-174,9);Box("Metal",V(-174,fy+.48f,9),V(.32f,.96f,.32f),true);Box("Stone",V(-174,fy+.98f,9),V(.52f,.08f,.44f));Beam("Metal",V(-174,fy+1,9.1f),V(-174,fy+1.17f,9.1f),.025f);Beam("Metal",V(-174,fy+1.17f,9.1f),V(-174,fy+1.17f,8.95f),.025f);
        for(float x=-180;x< -120;x+=15){GroundPolygon("Paint",60,V(x,.31f,5),V(x+1,.31f,5.4f),V(x,.31f,5.8f));}
        PolishDaylight();ExportSceneryReport();
    }
    static void PolishDaylight(){
        var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
        if(sun){sun.color=new Color(1,.96f,.88f);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.shadowBias=.035f;sun.shadowNormalBias=.25f;}
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.57f,.64f,.72f);RenderSettings.ambientEquatorColor=new Color(.41f,.44f,.40f);RenderSettings.ambientGroundColor=new Color(.23f,.25f,.21f);
        RenderSettings.fogColor=new Color(.69f,.76f,.8f);RenderSettings.fogDensity=.0011f;
        var sky=Save(new Material(Shader.Find("Campus/DaySky")){name="Soft campus daylight"},Folder+"/Materials/DaySky.mat");RenderSettings.skybox=sky;game.followCamera.clearFlags=CameraClearFlags.Skybox;
        var pipeline=UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if(pipeline){pipeline.shadowCascadeCount=4;pipeline.shadowDistance=105;var so=new SerializedObject(pipeline);so.FindProperty("m_SoftShadowsSupported").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();pipeline.msaaSampleCount=4;EditorUtility.SetDirty(pipeline);}
        // Depth reconstruction works with the existing custom architectural shaders too.
        var renderer=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/CampusRenderer.asset");
        if(renderer){
            string path=SceneryFolder+"/CampusSSAO.asset";var ao=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion>(path);
            if(!ao){ao=ScriptableObject.CreateInstance<UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion>();ao.name="Campus contact shading";AssetDatabase.CreateAsset(ao,path);}
            var settings=new SerializedObject(ao);var p=settings.FindProperty("m_Settings");p.FindPropertyRelative("Source").enumValueIndex=0;p.FindPropertyRelative("AfterOpaque").boolValue=true;p.FindPropertyRelative("Downsample").boolValue=true;
            p.FindPropertyRelative("Intensity").floatValue=.65f;p.FindPropertyRelative("Radius").floatValue=.35f;p.FindPropertyRelative("Falloff").floatValue=70;p.FindPropertyRelative("Samples").enumValueIndex=1;
            settings.ApplyModifiedPropertiesWithoutUndo();ao.SetActive(true);if(!renderer.rendererFeatures.Contains(ao))renderer.rendererFeatures.Add(ao);EditorUtility.SetDirty(renderer);renderer.SetDirty();
        }
    }
    static void ExportSceneryReport(){
        Directory.CreateDirectory("QA");File.WriteAllText("QA/road-landscape-generation.txt","Eight connected landscape nodes; metre-scale modules.\nTrees (LOD instances): "+sceneryTrees.Count+"\nFurniture collision volumes: "+sceneryProps+"\nBridge deck=6.5m; north dorm7 LG=.23m, south1F=3.08m.\nMain traffic routes retained. Gym route paved for pedestrian priority. West entrance sidewalk and gym jogging ribbon appended to shared route data.\n");
    }
}
