using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public static partial class CampusPlanBuilder
{
    struct CutVertex {public Vector3 p,n;public Vector2 uv;public Color c;public static CutVertex Lerp(CutVertex a,CutVertex b,float t)=>new CutVertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),c=Color.Lerp(a.c,b.c,t)};}
    static float Distance(CutVertex v,int side)=>side==0?v.p.x-CampusPlanTerrain.West(v.p.z):side==1?303-v.p.x:side==2?v.p.z+150:95-v.p.z;
    static List<CutVertex> Cut(List<CutVertex> p,int side,bool inside){var result=new List<CutVertex>();for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];float da=Distance(a,side),db=Distance(b,side);bool ia=inside?da>=0:da<0,ib=inside?db>=0:db<0;if(ia)result.Add(a);if(ia!=ib)result.Add(CutVertex.Lerp(a,b,da/(da-db)));}return result;}
    static void ReplaceOldLife(){
        var suppressed=new List<Renderer>();int count=0;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>().Where(f=>!f.transform.IsChildOf(holder)).ToArray()){
            var r=filter.GetComponent<MeshRenderer>();if(!r||!r.enabled||!filter.sharedMesh)continue;
            var mesh=filter.sharedMesh;var matrix=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            var bounds=r.bounds;var c=root.InverseTransformPoint(bounds.center);if(c.x+bounds.extents.x<151||c.x-bounds.extents.x>303||c.z+bounds.extents.z< -150||c.z-bounds.extents.z>95)continue;
            var p=mesh.vertices;var n=mesh.normals;var uv=mesh.uv;var col=mesh.colors;
            var output=new List<CutVertex>();var indices=new List<int>[mesh.subMeshCount];bool changed=false;
            CutVertex Vertex(int i)=>new CutVertex{p=matrix.MultiplyPoint3x4(p[i]),n=matrix.MultiplyVector(n.Length==p.Length?n[i]:Vector3.up).normalized,uv=uv.Length==p.Length?uv[i]:Vector2.zero,c=col.Length==p.Length?col[i]:Color.white};
            void Append(List<CutVertex> poly,List<int> ids){for(int j=1;j<poly.Count-1;j++){int at=output.Count;output.Add(poly[0]);output.Add(poly[j]);output.Add(poly[j+1]);ids.AddRange(new[]{at,at+1,at+2});}}
            for(int s=0;s<mesh.subMeshCount;s++){indices[s]=new List<int>();var tri=mesh.GetTriangles(s);for(int i=0;i<tri.Length;i+=3){var original=new List<CutVertex>{Vertex(tri[i]),Vertex(tri[i+1]),Vertex(tri[i+2])};var remain=original;var pieces=new List<List<CutVertex>>();for(int side=0;side<4&&remain.Count>0;side++){pieces.Add(Cut(remain,side,false));remain=Cut(remain,side,true);}if(remain.Count>=3){changed=true;foreach(var piece in pieces)Append(piece,indices[s]);}else Append(original,indices[s]);}}
            if(!changed)continue;r.enabled=false;suppressed.Add(r);if(output.Count==0)continue;
            var result=new Mesh{name="Plan retained surroundings",indexFormat=IndexFormat.UInt32};result.SetVertices(output.Select(v=>v.p).ToList());result.SetNormals(output.Select(v=>v.n).ToList());result.SetUVs(0,output.Select(v=>v.uv).ToList());result.SetColors(output.Select(v=>v.c).ToList());result.subMeshCount=indices.Length;for(int s=0;s<indices.Length;s++)result.SetTriangles(indices[s],s);result.RecalculateBounds();result=Save(result,Folder+"/Meshes/Retained-"+(count++)+".asset");
            var go=new GameObject("Retained west-campus surroundings");go.transform.SetParent(holder,false);go.AddComponent<MeshFilter>().sharedMesh=result;var rr=go.AddComponent<MeshRenderer>();rr.sharedMaterials=r.sharedMaterials;rr.shadowCastingMode=r.shadowCastingMode;
        }
        foreach(var t in root.GetComponentsInChildren<TextMesh>().Where(t=>!t.transform.IsChildOf(holder))){var p=root.InverseTransformPoint(t.transform.position);if(CampusPlanTerrain.InLiving(p.x,p.z)){var r=t.GetComponent<Renderer>();if(r&&r.enabled){r.enabled=false;suppressed.Add(r);}}}
        var colliders=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>c.enabled&&c.gameObject.layer==8&&CampusPlanTerrain.InLiving(game.WorldToMap(c.bounds.center).x,game.WorldToMap(c.bounds.center).z)).ToArray();foreach(var c in colliders)c.enabled=false;
        var state=holder.gameObject.AddComponent<CampusPlanState>();state.suppressedRenderers=suppressed.ToArray();state.suppressedColliders=colliders;
    }
    // Remove the existing balcony, rear wall and AC details at the raised door bay.
    // Clip faces instead of deleting by centroid, so full-height columns remain above it.
    static void CutNorthernDoor(Transform building){
        float Plane(CutVertex v,int i)=>i==0?v.p.x-248:i==1?257-v.p.x:i==2?v.p.y-3.06f:i==3?6.16f-v.p.y:i==4?v.p.z+56:-52.4f-v.p.z;
        List<CutVertex> Split(List<CutVertex> p,int side,bool inside){var output=new List<CutVertex>();for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];float da=Plane(a,side),db=Plane(b,side);bool ia=inside?da>=0:da<0,ib=inside?db>=0:db<0;if(ia)output.Add(a);if(ia!=ib)output.Add(CutVertex.Lerp(a,b,da/(da-db)));}return output;}
        foreach(var f in building.GetComponentsInChildren<MeshFilter>()){
            var source=f.sharedMesh;var p=source.vertices;var n=source.normals;var uv=source.uv;var colors=source.colors;var tri=source.triangles;var output=new List<CutVertex>();bool changed=false;
            CutVertex Vertex(int i)=>new CutVertex{p=p[i],n=n.Length==p.Length?n[i]:Vector3.up,uv=uv.Length==p.Length?uv[i]:Vector2.zero,c=colors.Length==p.Length?colors[i]:Color.white};
            void Append(List<CutVertex> polygon){for(int j=1;j<polygon.Count-1;j++){output.Add(polygon[0]);output.Add(polygon[j]);output.Add(polygon[j+1]);}}
            for(int i=0;i<tri.Length;i+=3){var original=new List<CutVertex>{Vertex(tri[i]),Vertex(tri[i+1]),Vertex(tri[i+2])};var remain=original;var outside=new List<List<CutVertex>>();for(int plane=0;plane<6&&remain.Count>0;plane++){outside.Add(Split(remain,plane,false));remain=Split(remain,plane,true);}if(remain.Count>=3){changed=true;foreach(var poly in outside)Append(poly);}else Append(original);}
            if(!changed)continue;var m=new Mesh{name=source.name,indexFormat=IndexFormat.UInt32};m.SetVertices(output.Select(v=>v.p).ToList());m.SetNormals(output.Select(v=>v.n).ToList());m.SetUVs(0,output.Select(v=>v.uv).ToList());m.SetColors(output.Select(v=>v.c).ToList());m.SetTriangles(Enumerable.Range(0,output.Count).ToArray(),0);m.RecalculateBounds();Save(m,AssetDatabase.GetAssetPath(source));
        }
    }
    public static void RepairNorthDoorAndBuild(){
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");var campus=GameObject.Find("校园环境").transform;CutNorthernDoor(campus.Find(Group).Find("第七学生公寓"));AssetDatabase.SaveAssets();CampusBuilder.Build();
    }
    static void GroundRect(string mat,float x0,float x1,float z0,float z1,float offset=.035f){
        int nx=Mathf.CeilToInt(x1-x0),nz=Mathf.CeilToInt(z1-z0);for(int i=0;i<nx;i++)for(int j=0;j<nz;j++){
            float x=Mathf.Lerp(x0,x1,(float)i/nx),xx=Mathf.Lerp(x0,x1,(float)(i+1)/nx),z=Mathf.Lerp(z0,z1,(float)j/nz),zz=Mathf.Lerp(z0,z1,(float)(j+1)/nz);
            var a=V(x,H(x,z)+offset,z);var b=V(x,H(x,zz)+offset,zz);var c=V(xx,H(xx,zz)+offset,zz);var d=V(xx,H(xx,z)+offset,z);
            if(mat=="Grass")geometry[mat].Quad(a,b,c,d);else GroundPolygon(mat,mat=="DarkBand"?60:10,a,b,c,d);
        }
    }
    static void Path(CampusRoad road)=>RoadRibbon(road,true);
    static void BuildLandscape(){
        // Polygonal living parcel, sampled to the same terrain profile as collision.
        for(float z=-150;z<95;z+=1)for(float x=151;x<303;x+=1){float left=Mathf.Max(x,Mathf.Max(CampusPlanTerrain.West(z),CampusPlanTerrain.West(z+1)));if(left<x+1)GroundRect("Grass",left,x+1,z,z+1,0);}
        foreach(var p in Sites){var r=p.Value;GroundRect("Paving",r.xMin-1.4f,r.xMax+1.4f,r.yMin-1.4f,r.yMax+1.4f,.025f);}
        // Draw the whole perimeter as one closed ribbon so its corners share vertices.
        var ringNames=new[]{"生活区西侧坡道主路","生活区北环路","生活区东环路"};
        foreach(var road in NewRoads().Where(r=>!ringNames.Contains(r.name)))Path(road);
        RoadRibbon(Road("生活区连续环路",4,209,78,209,-142,294,-142,294,78,209,78),true,ringNames);
        GroundRect("Paving",246,259,-62,-55);GroundRect("Paving",245,260,-31,-22);GroundRect("Paving",226,279,29,45);
        // Geometric courtyard garden and compact planting beds keep doors and paths clear.
        foreach(var p in new[]{new Vector2(236,-110),new Vector2(266,-110),new Vector2(237,-94),new Vector2(267,-94),new Vector2(233,47),new Vector2(275,47),new Vector2(287,-43),new Vector2(216,-43)}){Bed(p.x,p.y,5,4);Tree(p.x,p.y);}
        for(float z=-137;z<75;z+=16){Tree(299,z);if((z< -118||z> -65)&&Mathf.Abs(z-10)>8)Tree(202,z);Lamp(213,z);}
        foreach(var p in new[]{new Vector2(241,-107),new Vector2(260,-98),new Vector2(236,43),new Vector2(269,43)})Bench(p.x,p.y);
        // Low perimeter fences distinguish public road from the residence paths.
        void Fence(Vector2 a,Vector2 b){float length=Vector2.Distance(a,b);int n=Mathf.CeilToInt(length/2.5f);for(int i=0;i<n;i++){var p=Vector2.Lerp(a,b,(float)i/n);var q=Vector2.Lerp(a,b,(float)(i+1)/n);if(p.x<202&&((p.y>34&&p.y<43)||(Mathf.Min(p.y,q.y)<13.3f&&Mathf.Max(p.y,q.y)>6.7f)))continue;float y=H(p.x,p.y);Beam("Metal",V(p.x,y,p.y),V(p.x,y+1.5f,p.y),.075f);for(float yy=.55f;yy<1.5f;yy+=.65f)Beam("Metal",V(p.x,y+yy,p.y),V(q.x,H(q.x,q.y)+yy,q.y),.045f);Wall("Living perimeter fence",P(p.x,p.y),P(q.x,q.y),1.5f,.12f);}}
        Fence(new Vector2(201,-146),new Vector2(201,90));Fence(new Vector2(201,-146),new Vector2(302,-146));Fence(new Vector2(302,-146),new Vector2(302,90));Fence(new Vector2(201,90),new Vector2(302,90));
        GroundRect("Paving",195,209,35,43,.04f);foreach(float z in new[]{34f,44f})Box("Stone",V(201,H(201,z)+1.25f,z),V(.7f,2.5f,.7f),true);
        // The cafeteria now faces west, aligned with the straight bridge approach.
        for(int step=0;step<6;step++){float y=.18f+(step+1)*.095f,d=-1.8f+step*.3f;ColliderBox("Cafeteria tread",V(225+2.75f*(d+.05f)/2,2.9f+(y+.14f)/2,10),V((.05f-d)*2.75f,y-.14f,13.9f));}
        // South 1F terrace sits on the upper grade; north LG stays at the lower grade.
        Box("Stone",V(252.5f,1.54f,-27.6f),V(10,3.08f,6.8f));
        GroundRect("Paving",247.5f,257.5f,-31,-24,.08f);
        GroundRect("Paving",203,224,3,17,.035f);
        Label("学苑桥",V(156,8.8f,12.78f),.65f);
    }
    static void Bed(float x,float z,float w,float d)=>SceneryBed(x,z,w,d);
    static void Tree(float x,float z)=>SceneryTree(x,z);
    static void Lamp(float x,float z)=>SceneryLamp(x,z);
    static void Bench(float x,float z)=>SceneryBench(x,z);
    static void RoofPanel(Vector3 a,Vector3 b,Vector3 c,Vector3 d){
        var down=Vector3.down*.085f;geometry["Roof"].Quad(a,b,c,d);geometry["Roof"].Quad(d+down,c+down,b+down,a+down);
        foreach(var edge in new[]{new[]{a,b},new[]{b,c},new[]{c,d},new[]{d,a}})geometry["Metal"].Quad(edge[0],edge[0]+down,edge[1]+down,edge[1]);
    }
    static void BuildBridge(){
        const float deck=6.5f,west=105.5f,east=194.5f,center=150,length=east-west;
        Box("Stone",V(center,deck-.22f,10),V(length,.44f,5.4f),true);
        Box("Paving",V(center,deck+.02f,10),V(length,.04f,5.2f));
        foreach(float x in new[]{119f,132f,164f,177f,190f})foreach(float z in new[]{7.9f,12.1f})Box("Stone",V(x,(deck+H(x,z))/2,z),V(.6f,deck-H(x,z),.6f),true);
        for(float x=west;x<=east;x+=4){foreach(float z in new[]{7.4f,12.6f})Box("Metal",V(x,8.65f,z),V(.12f,4.3f,.12f));Beam("Metal",V(x,10.75f,7.1f),V(x,10.95f,10),.12f);Beam("Metal",V(x,10.95f,10),V(x,10.75f,12.9f),.12f);}
        foreach(float z in new[]{7.4f,12.6f}){
            Box("Glass",V(center,8.6f,z),V(length,3.95f,.06f),true);
            Beam("Metal",V(west,7.55f,z),V(east,7.55f,z),.065f);Beam("Metal",V(west,6.65f,z),V(east,6.65f,z),.1f);
        }
        RoofPanel(V(west-.2f,10.75f,6.95f),V(east+.2f,10.75f,6.95f),V(east+.2f,11f,10),V(west-.2f,11f,10));
        RoofPanel(V(west-.2f,11f,10),V(east+.2f,11f,10),V(east+.2f,10.75f,13.05f),V(west-.2f,10.75f,13.05f));
        for(float x=108;x<east;x+=5)Box("Light",V(x,10.69f,10),V(1.2f,.04f,.09f));
        // Both flights follow the deck's X axis, with no right-angle landings.
        BridgeStairs(west,-1,deck);BridgeStairs(east,1,deck);
    }
    static void BridgeStairs(float topX,float direction,float top){
        const float tread=.36f,half=2.2f;
        int steps=40;for(int k=0;k<8;k++)steps=Mathf.CeilToInt((top-H(topX+direction*steps*tread,10))/.17f);
        float end=topX+direction*steps*tread,bottom=H(end,10),rise=(top-bottom)/steps;
        for(int i=0;i<steps;i++){
            float x=topX+direction*(i+.5f)*tread,y=top-i*rise;
            Box("Stone",V(x,y-.12f,10),V(tread+.015f,.24f,half*2),true);
            Box("Metal",V(x+direction*.14f,y+.005f,10),V(.035f,.018f,half*2-.08f));
        }
        foreach(float side in new[]{-1f,1f}){
            float z=10+side*(half+.06f);
            Beam("Metal",V(topX,top+1.12f,z),V(end,bottom+1.12f,z),.07f,true);
            Beam("Metal",V(topX,top+.14f,z),V(end,bottom+.14f,z),.1f,true);
            for(int i=0;i<=steps;i+=3){float x=topX+direction*i*tread,y=top-i*rise;Beam("Metal",V(x,y,z),V(x,y+1.12f,z),.05f);}
            for(int i=0;i<=steps;i+=7){float x=topX+direction*i*tread,y=top-i*rise;Beam("Metal",V(x,y,z),V(x,y+4.3f,z),.11f);}
            Beam("Metal",V(topX,top+4.3f,z),V(end,bottom+4.3f,z),.14f);
            geometry["Glass"].Quad(V(topX,top+.15f,z),V(end,bottom+.15f,z),V(end,bottom+4.15f,z),V(topX,top+4.15f,z));
            // A continuous solid side plane follows the slope and keeps the player on the flight.
            var a=V(topX,top+1.4f,z);var b=V(end,bottom+1.4f,z);
            var go=new GameObject("Straight stair side barrier "+colliderIndex++){layer=9};go.transform.SetParent(physicsRoot,false);
            go.transform.position=game.MapToWorld((a+b)/2);go.transform.rotation=Quaternion.LookRotation(game.MapToWorld(b-a),Vector3.up);
            go.AddComponent<BoxCollider>().size=V(.08f,2.7f,Vector3.Distance(a,b));
        }
        RoofPanel(V(topX-direction*.2f,top+4.4f,7.45f),V(topX-direction*.2f,top+4.4f,12.55f),V(end+direction*.7f,bottom+4.4f,12.55f),V(end+direction*.7f,bottom+4.4f,7.45f));
        GroundRect("Paving",Mathf.Min(end,end+direction*2.5f),Mathf.Max(end,end+direction*2.5f),7.5f,12.5f,.04f);
    }
    static void BuildOutdoorCollision(){
        var terrain=new DormitoryBuilder.Geometry();
        for(float z=-180;z<235;z+=2)for(float x=-290;x<310;x+=2){float zz=Mathf.Min(235,z+2);terrain.Quad(V(x,H(x,z),z),V(x,H(x,zz),zz),V(x+2,H(x+2,zz),zz),V(x+2,H(x+2,z),z));}
        var mesh=terrain.Mesh("Outdoor terrain collision");var p=mesh.vertices;for(int i=0;i<p.Length;i++)p[i]=game.MapToWorld(p[i]);mesh.vertices=p;var tri=mesh.triangles;for(int i=0;i<tri.Length;i+=3){int t=tri[i];tri[i]=tri[i+2];tri[i+2]=t;}mesh.triangles=tri;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=Save(mesh,Folder+"/Meshes/TerrainCollision.asset");var ground=new GameObject("Outdoor terrain"){layer=10};ground.transform.SetParent(physicsRoot,false);ground.AddComponent<MeshCollider>().sharedMesh=mesh;
        var buildings=data.features.Where(f=>f.kind=="building").Select(f=>f.name).ToHashSet();
        foreach(var o in data.obstacles){float height=buildings.Contains(o.name)?32:2.6f;if(o.points?.Length>2){for(int i=0;i<o.points.Length;i++)Wall(o.name,o.points[i],o.points[(i+1)%o.points.Length],height,.2f);}else if(o.segment?.Length==2)Wall(o.name,o.segment[0],o.segment[1],height,Mathf.Max(.15f,o.thickness));else ColliderBox(o.name,V((o.minX+o.maxX)/2,H((o.minX+o.maxX)/2,(o.minZ+o.maxZ)/2)+height/2,(o.minZ+o.maxZ)/2),V(Mathf.Max(.1f,o.maxX-o.minX),height,Mathf.Max(.1f,o.maxZ-o.minZ)));}
        Wall("West boundary",P(-290,-180),P(-290,235),20,.5f);Wall("East boundary",P(310,-180),P(310,235),20,.5f);Wall("North boundary",P(-290,-180),P(310,-180),20,.5f);Wall("South boundary",P(-290,235),P(310,235),20,.5f);
    }
    static void Wall(string name,MapPoint a,MapPoint b,float h,float width){if(Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))<.01f)return;var pa=game.MapToWorld(V(a.x,0,a.z));var pb=game.MapToWorld(V(b.x,0,b.z));var go=new GameObject(name+" collision"){layer=9};go.transform.SetParent(physicsRoot,false);go.transform.position=(pa+pb)/2+Vector3.up*(h/2+Mathf.Min(H(a.x,a.z),H(b.x,b.z)));go.transform.rotation=Quaternion.LookRotation(pb-pa);go.AddComponent<BoxCollider>().size=V(width,h,(pb-pa).magnitude);}
}
