using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static partial class CampusPlanBuilder
{
    static CampusRoad[] planRoads;
    static void BuildPlanRoads(){
        planRoads=CampusPlanRoadNetwork.Create();
        // Keep the public street and the new living-zone roads; replace the obsolete west-campus routes.
        data.roads=data.roads.Where(r=>r.kind=="external"||r.kind=="bridge"||r.points.All(p=>CampusPlanTerrain.InLiving(p.x,p.z))).Concat(planRoads).ToArray();
        // Small landscaping obstacles cannot remain in the centre of a new mapped route.
        var important=data.features.Select(f=>f.name).ToHashSet();
        bool OnRoad(float x,float z,float margin=0)=>planRoads.Any(r=>CampusPlanRoadNetwork.Distance(x,z,r)<r.width/2+margin);
        var removed=new List<CampusObstacle>();
        data.obstacles=data.obstacles.Where(o=>{
            if(important.Contains(o.name)||o.segment?.Length>0||o.name.StartsWith("Teaching "))return true;
            float x=o.points?.Length>0?o.points.Average(p=>p.x):(o.minX+o.maxX)/2,z=o.points?.Length>0?o.points.Average(p=>p.z):(o.minZ+o.maxZ)/2;
            if(OnRoad(x,z,1.1f)){removed.Add(o);return false;}return true;
        }).ToArray();
        ClearRoadVegetation(removed);
        // Register all door spurs before making borders, so no kerb crosses a junction.
        var spurs=new List<CampusRoad>();
        foreach(var e in game.GetComponent<BuildingInteriorTravel>().entrances.Where(e=>e&&e.buildingId.StartsWith("teaching-"))){
            var q=e.returnMapPosition;var r=Road(e.displayName+"入户步道",3.2f,q.x,q.z,68,-99);
            if(e.buildingId=="teaching-b")r=Road("B 楼弧形入口步道",3.2f,q.x,q.z,60,-84,74,-84);
            if(e.buildingId=="teaching-c")r=Road("C 楼入口步道",3.2f,q.x,q.z,74,-84);
            spurs.Add(r);
        }
        data.roads=data.roads.Concat(spurs).ToArray();
        foreach(var road in planRoads.Concat(spurs))RoadRibbon(road);
        var routes=planRoads.Select(r=>new CampusRoute{name="plan-road: "+r.name,points=r.points}).ToArray();
        data.routes=data.routes.Concat(routes).ToArray();
        Directory.CreateDirectory("QA");File.WriteAllText("QA/campus-road-network.txt",string.Join("\n",planRoads.Select(r=>r.name+" / "+r.width+" m / "+r.points.Length+" nodes"))+"\nTerrain priority: north entrance LG=0.23; south 1F=3.08.\n");
    }
    static void RoadRibbon(CampusRoad r,bool living=false,string[] aliases=null){
        var p=r.points.Select(q=>new Vector2(q.x,q.z)).ToArray();bool main=!living&&r.kind=="main";
        bool promenade=r.name=="天桥西侧接驳步道"||r.name.Contains("方向步道")||r.name=="两湖直行步道";
        bool vehicle=main&&new[]{"教学区东侧主路","教学楼外围环路","教学区西北主路","西侧校内环路","锦屏山西侧主路"}.Contains(r.name);
        float half=r.width/2;
        Vector2 Offset(int i,float width){
            if((i==0||i==p.Length-1)&&(p[0]-p[p.Length-1]).sqrMagnitude<.000001f){
                var prev=(p[0]-p[p.Length-2]).normalized;var next=(p[1]-p[0]).normalized;
                var np=new Vector2(-prev.y,prev.x);var nn=new Vector2(-next.y,next.x);var miter=(np+nn).normalized;
                return miter*width/Mathf.Max(.25f,Vector2.Dot(miter,np));
            }
            var a=(p[i]-p[Mathf.Max(0,i-1)]).normalized;var b=(p[Mathf.Min(p.Length-1,i+1)]-p[i]).normalized;if(i==0)a=b;if(i==p.Length-1)b=a;
            var n1=new Vector2(-a.y,a.x);var n2=new Vector2(-b.y,b.x);var n=(n1+n2).normalized;return n*width/Mathf.Max(.25f,Vector2.Dot(n,n1));
        }
        void Strip(string material,float left,float right,float y,bool edge=false){
            for(int k=1;k<p.Length;k++){
                var a=p[k-1]+Offset(k-1,left);var b=p[k]+Offset(k,left);var c=p[k]+Offset(k,right);var d=p[k-1]+Offset(k-1,right);
                int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(p[k-1],p[k])));
                for(int j=0;j<n;j++){
                    float t=(float)j/n,tt=(float)(j+1)/n;
                    var aa=Vector2.Lerp(a,b,t);var bb=Vector2.Lerp(a,b,tt);var cc=Vector2.Lerp(d,c,tt);var dd=Vector2.Lerp(d,c,t);
                    var middle=(aa+bb+cc+dd)/4;
                    if(edge&&promenade&&Vector2.Distance(middle,new Vector2(35,10))<7.2f)continue;
                    if(edge&&data.roads.Any(other=>other!=r&&other.name!=r.name&&(aliases==null||!aliases.Contains(other.name))&&other.kind!="bridge"&&CampusPlanRoadNetwork.Distance(middle.x,middle.y,other)<other.width/2+.3f))continue;
                    Vector3 At(Vector2 v)=>V(v.x,H(v.x,v.y)+y,v.y);
                    int priority=edge?50:material=="Asphalt"?35:material=="Track"?34:material=="SmallPavers"?32:material=="Promenade"?33:material=="Aggregate"?31:y<.05f?15:30;
                    GroundPolygon(material,priority,At(aa),At(bb),At(cc),At(dd));
                }
            }
        }
        if(vehicle)Strip("Paving",-half-1.15f,half+1.15f,.045f);
        bool lake=r.name.Contains("环湖"),sports=r.name=="体育馆运动带";
        string surface=promenade?"Promenade":living?"SmallPavers":sports?"Track":lake?"Aggregate":vehicle?"Asphalt":"Paving";
        Strip(surface,-half,half,.065f);
        foreach(float side in new[]{-1f,1f}){
            Strip("Stone",side*half,side*(half+.14f),.065f,true);
            if(vehicle||sports)Strip("Paint",side*(half-.17f),side*(half-.11f),.065f,true);
            else if(!lake){
                Strip("BorderStone",side*(half-.40f),side*(half-.06f),.065f,true);
                Strip("DarkBand",side*(half-.47f),side*(half-.40f),.065f,true);
            }
        }
    }

    static void ClearRoadVegetation(List<CampusObstacle> removed){
        // Remove complete small props by their recorded obstacle footprints, and trim old vegetation
        // in the new walkable corridor. Buildings, water and native architectural meshes stay intact.
        var original=JsonUtility.FromJson<CampusData>(File.ReadAllText(Folder+"/SourceMap.json"));
        var oldRoads=original.roads.Where(r=>r.kind!="external").ToArray();var publicRoads=original.roads.Where(r=>r.kind=="external").ToArray();
        var marker=holder.GetComponent<CampusPlanState>();var suppressed=marker.suppressedRenderers.ToList();int number=0;
        foreach(var f in root.GetComponentsInChildren<MeshFilter>().ToArray()){
            var rr=f.GetComponent<MeshRenderer>();if(!rr||!rr.enabled||!f.sharedMesh)continue;
            string path=AssetDatabase.GetAssetPath(f.sharedMesh);
            if(!(path.Contains("Retained-")||path.Contains("Surroundings-")||path.Contains("Preserved-")||path.StartsWith("Assets/LivingRevision/Meshes/")))continue;
            var m=f.sharedMesh;var points=m.vertices;var mat=root.worldToLocalMatrix*f.transform.localToWorldMatrix;var world=points.Select(v=>mat.MultiplyPoint3x4(v)).ToArray();
            bool Near(float x,float z)=>planRoads.Any(r=>CampusPlanRoadNetwork.Distance(x,z,r)<r.width/2+.4f);
            bool Architectural(Vector3 p)=>data.features.Any(b=>(b.kind=="building"||b.kind=="water"||b.kind=="court"||b.kind=="track"||b.kind=="plaza")&&CampusCollision.Inside(p.x,p.z,b.points));
            var lists=new List<int[]>();bool changed=false;
            for(int sub=0;sub<m.subMeshCount;sub++){var tri=m.GetTriangles(sub);var output=new List<int>();for(int i=0;i<tri.Length;i+=3){var a=world[tri[i]];var b=world[tri[i+1]];var c=world[tri[i+2]];var p=(a+b+c)/3;
                bool clear=Mathf.Max(a.y,Mathf.Max(b.y,c.y))>.08f&&p.y<12&&Near(p.x,p.z)&&!Architectural(p)&&Mathf.Max((a-b).magnitude,(b-c).magnitude)<12;
                // Keep grass/soil and existing low paving, which the new surface covers.
                bool oldSurface=Mathf.Min(a.y,Mathf.Min(b.y,c.y))>.005f&&Mathf.Max(a.y,Mathf.Max(b.y,c.y))<.22f&&oldRoads.Any(road=>CampusPlanRoadNetwork.Distance(p.x,p.z,road)<road.width/2+2)&&!publicRoads.Any(road=>CampusPlanRoadNetwork.Distance(p.x,p.z,road)<road.width/2+2)&&!Architectural(p);
                if(clear||oldSurface)changed=true;else{output.Add(tri[i]);output.Add(tri[i+1]);output.Add(tri[i+2]);}}
                lists.Add(output.ToArray());}
            if(!changed)continue;
            var copy=UnityEngine.Object.Instantiate(m);copy.name="Road clearance retained surroundings";for(int sub=0;sub<lists.Count;sub++)copy.SetTriangles(lists[sub],sub);copy.RecalculateBounds();copy=Save(copy,Folder+"/Meshes/RoadClear-"+(number++)+".asset");
            var go=new GameObject("Road clearance surroundings");go.transform.SetParent(holder,false);go.transform.position=f.transform.position;go.transform.rotation=f.transform.rotation;go.transform.localScale=f.transform.lossyScale;go.transform.localScale=new Vector3(-go.transform.localScale.x,go.transform.localScale.y,go.transform.localScale.z);
            go.AddComponent<MeshFilter>().sharedMesh=copy;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=rr.sharedMaterials;renderer.shadowCastingMode=rr.shadowCastingMode;rr.enabled=false;if(!f.transform.IsChildOf(holder))suppressed.Add(rr);
        }
        marker.suppressedRenderers=suppressed.Distinct().ToArray();
    }
}
