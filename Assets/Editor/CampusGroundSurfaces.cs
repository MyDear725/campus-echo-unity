using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static partial class CampusPlanBuilder
{
    // One physical-height surface per XZ location. Borders are inlays, not layered decals.
    sealed class GroundPatch
    {
        public List<Vector2> polygon;
        public Rect bounds;
        public string material;
        public int priority;
    }
    static readonly List<GroundPatch> groundPatches=new List<GroundPatch>();
    static readonly Dictionary<Vector2Int,List<GroundPatch>> groundGrid=new Dictionary<Vector2Int,List<GroundPatch>>();
    static readonly Dictionary<Vector2Int,List<Vector2[]>> finishedGround=new Dictionary<Vector2Int,List<Vector2[]>>();
    const float GroundFinish=.065f;
    static float Cross2(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static float Area2(List<Vector2> p){float a=0;for(int i=1;i+1<p.Count;i++)a+=Cross2(p[i]-p[0],p[i+1]-p[0]);return a;}
    static Rect PolygonBounds(List<Vector2> p)=>Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));
    static IEnumerable<Vector2Int> GroundCells(Rect b){for(int x=Mathf.FloorToInt(b.xMin/4);x<=Mathf.FloorToInt(b.xMax/4);x++)for(int z=Mathf.FloorToInt(b.yMin/4);z<=Mathf.FloorToInt(b.yMax/4);z++)yield return new Vector2Int(x,z);}
    static void ResetGroundSurfaces(){groundPatches.Clear();groundGrid.Clear();finishedGround.Clear();}
    static void GroundPolygon(string material,int priority,params Vector3[] vertices){
        var p=vertices.Select(v=>new Vector2(v.x,v.z)).ToList();
        if(p.Count==4){
            bool positive=false,negative=false;for(int i=0;i<4;i++){float cross=Cross2(p[(i+1)%4]-p[i],p[(i+2)%4]-p[(i+1)%4]);positive|=cross>.000001f;negative|=cross<-.000001f;}
            if(positive&&negative){GroundPolygon(material,priority,vertices[0],vertices[1],vertices[2]);GroundPolygon(material,priority,vertices[0],vertices[2],vertices[3]);return;}
        }
        if(Mathf.Abs(Area2(p))<.000001f)return;
        if(Area2(p)<0)p.Reverse();groundPatches.Add(new GroundPatch{polygon=p,bounds=PolygonBounds(p),material=material,priority=priority});
    }
    static List<Vector2> HalfPlane(List<Vector2> p,Vector2 a,Vector2 b,bool inside){
        var result=new List<Vector2>();for(int i=0;i<p.Count;i++){
            var u=p[i];var v=p[(i+1)%p.Count];float du=Cross2(b-a,u-a),dv=Cross2(b-a,v-a);
            bool iu=inside?du>=0:du<=0,iv=inside?dv>=0:dv<=0;
            if(iu)result.Add(u);if(iu!=iv&&Mathf.Abs(du-dv)>1e-12f)result.Add(Vector2.LerpUnclamped(u,v,du/(du-dv)));
        }return result;
    }
    static IEnumerable<List<Vector2>> SubtractGround(List<Vector2> polygon,List<Vector2> cutter){
        var remain=polygon;
        for(int i=0;i<cutter.Count&&remain.Count>=3;i++){
            var a=cutter[i];var b=cutter[(i+1)%cutter.Count];var outside=HalfPlane(remain,a,b,false);
            if(outside.Count>=3&&Mathf.Abs(Area2(outside))>.000001f)yield return outside;
            remain=HalfPlane(remain,a,b,true);
        }
    }
    static IEnumerable<GroundPatch> GroundCandidates(Rect bounds)=>GroundCells(bounds).Where(groundGrid.ContainsKey).SelectMany(k=>groundGrid[k]).Distinct().Where(p=>p.bounds.Overlaps(bounds));
    static List<List<Vector2>> Uncovered(List<Vector2> p,IEnumerable<GroundPatch> cutters){
        var pieces=new List<List<Vector2>>{p};foreach(var cutter in cutters){
            pieces=pieces.SelectMany(piece=>PolygonBounds(piece).Overlaps(cutter.bounds)?SubtractGround(piece,cutter.polygon):new[]{piece}).ToList();if(pieces.Count==0)break;
        }return pieces;
    }
    // Match the actual two-metre terrain collider triangles, including the residence slope.
    static float GroundPlaneHeight(Vector2 p){
        float x=Mathf.Floor(p.x/2)*2,z=Mathf.Floor(p.y/2)*2,tx=(p.x-x)/2,tz=(p.y-z)/2;
        float a=H(x,z),c=H(x+2,z+2);
        return tz>=tx?a*(1-tz)+H(x,z+2)*(tz-tx)+c*tx:a*(1-tx)+c*tz+H(x+2,z)*(tx-tz);
    }
    static void EmitGround(string material,List<Vector2> polygon){
        var bounds=PolygonBounds(polygon);
        for(float x=Mathf.Floor(bounds.xMin/2)*2;x<bounds.xMax;x+=2)for(float z=Mathf.Floor(bounds.yMin/2)*2;z<bounds.yMax;z+=2){
            var a=new Vector2(x,z);var b=new Vector2(x+2,z);var c=new Vector2(x+2,z+2);var d=new Vector2(x,z+2);
            foreach(var cell in new[]{new[]{a,b,c},new[]{a,c,d}}){
                var p=polygon;for(int j=0;j<3&&p.Count>=3;j++)p=HalfPlane(p,cell[j],cell[(j+1)%3],true);
                for(int j=1;j+1<p.Count;j++){
                    if(Mathf.Abs(Cross2(p[j]-p[0],p[j+1]-p[0]))<.000001f)continue;
                    Vector3 At(Vector2 q)=>V(q.x,GroundPlaneHeight(q)+GroundFinish,q.y);
                    geometry[material].Triangle(At(p[0]),At(p[j+1]),At(p[j]));
                    var tri=new[]{p[0],p[j],p[j+1]};
                    foreach(var key in GroundCells(PolygonBounds(tri.ToList()))){if(!finishedGround.TryGetValue(key,out var list))finishedGround[key]=list=new List<Vector2[]>();list.Add(tri);}
                }
            }
        }
    }
    static void ResolveGroundSurfaces(){
        int clipped=0,pieces=0;
        foreach(var patch in groundPatches.OrderByDescending(p=>p.priority)){
            var output=Uncovered(patch.polygon,GroundCandidates(patch.bounds));
            if(output.Count!=1||Mathf.Abs(Area2(output[0])-Area2(patch.polygon))>.00001f)clipped++;
            foreach(var polygon in output){EmitGround(patch.material,polygon);pieces++;}
            foreach(var key in GroundCells(patch.bounds)){if(!groundGrid.TryGetValue(key,out var list))groundGrid[key]=list=new List<GroundPatch>();list.Add(patch);}
        }
        ValidateGroundCoverage();
        TrimPreviousGround();
        Directory.CreateDirectory("QA");File.WriteAllText("QA/ground-surface-repair.txt",$"Surface patches: {groundPatches.Count}; clipped overlapping patches: {clipped}; visible pieces: {pieces}.\nSingle flush paving height: terrain collider + {GroundFinish} m. Shared terrain triangle subdivision. Mitered living-road corners, open junction borders, teaching B/C spurs join x=74. Living west route joins south ring at z=78.\nBridge canopy raised 1.2 m, unchanged 6.5 m deck and straight stairs.\n");
        Debug.Log($"GROUND_SURFACES_RESOLVED: {clipped} overlaps clipped, {pieces} visible pieces");
    }
    static void ValidateGroundCoverage(){
        int samples=0;
        foreach(var road in data.roads.Where(r=>r.kind!="external"&&r.kind!="bridge"))for(int i=1;i<road.points.Length;i++){
            var a=new Vector2(road.points[i-1].x,road.points[i-1].z);var b=new Vector2(road.points[i].x,road.points[i].z);var side=new Vector2(-(b-a).normalized.y,(b-a).normalized.x);
            int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.65f));
            for(int k=0;k<n;k++){
                var p=Vector2.Lerp(a,b,(k+.371f)/n)+side*.0137f;var key=new Vector2Int(Mathf.FloorToInt(p.x/4),Mathf.FloorToInt(p.y/4));
                if(!finishedGround.TryGetValue(key,out var triangles))throw new Exception("Unpaved route: "+road.name+" at "+p);
                int cover=triangles.Count(t=>Cross2(t[1]-t[0],p-t[0])>=-.00001f&&Cross2(t[2]-t[1],p-t[1])>=-.00001f&&Cross2(t[0]-t[2],p-t[2])>=-.00001f);
                int strict=triangles.Count(t=>Cross2(t[1]-t[0],p-t[0])>.00001f&&Cross2(t[2]-t[1],p-t[1])>.00001f&&Cross2(t[0]-t[2],p-t[2])>.00001f);
                if(cover==0||strict>1)throw new Exception("Ground coverage="+cover+", overlapping interiors="+strict+" on "+road.name+" at "+p+" triangles="+string.Join(";",triangles.Where(t=>Cross2(t[1]-t[0],p-t[0])>.00001f&&Cross2(t[2]-t[1],p-t[1])>.00001f&&Cross2(t[0]-t[2],p-t[2])>.00001f).Select(t=>string.Join(",",t.Select(q=>q.ToString("F5"))))));
                samples++;
            }
        }
        Debug.Log("GROUND_COVERAGE_PASS: "+samples+" samples; no road gaps or duplicate surface interiors");
    }
    static void TrimPreviousGround(){
        var state=holder.GetComponent<CampusPlanState>();var suppressed=state.suppressedRenderers.ToList();int count=0,trimmed=0;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>().ToArray()){
            var renderer=filter.GetComponent<MeshRenderer>();var mesh=filter.sharedMesh;
            if(!renderer||!renderer.enabled||!mesh||filter.GetComponentInParent<LODGroup>())continue;
            var matrix=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var colors=mesh.colors;
            var world=vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();
            var output=new List<CutVertex>();var indices=new List<int>[mesh.subMeshCount];bool changed=false;
            CutVertex Vertex(int i)=>new CutVertex{p=world[i],n=matrix.MultiplyVector(normals.Length==vertices.Length?normals[i]:Vector3.up).normalized,uv=uv.Length==vertices.Length?uv[i]:Vector2.zero,c=colors.Length==vertices.Length?colors[i]:Color.white};
            void Append(CutVertex a,CutVertex b,CutVertex c,List<int> ids){int at=output.Count;output.Add(a);output.Add(b);output.Add(c);ids.Add(at);ids.Add(at+1);ids.Add(at+2);}
            for(int sub=0;sub<mesh.subMeshCount;sub++){
                indices[sub]=new List<int>();var triangles=mesh.GetTriangles(sub);
                for(int i=0;i<triangles.Length;i+=3){
                    int ai=triangles[i],bi=triangles[i+1],ci=triangles[i+2];var a=world[ai];var b=world[bi];var c=world[ci];
                    bool low=new[]{a,b,c}.All(v=>Mathf.Abs(v.y-GroundPlaneHeight(new Vector2(v.x,v.z)))<.19f);
                    var normal=Vector3.Cross(b-a,c-a).normalized;
                    bool nearFinish=new[]{a,b,c}.Any(v=>Mathf.Abs(v.y-GroundPlaneHeight(new Vector2(v.x,v.z))-GroundFinish)<.04f);
                    if(!low||!nearFinish||Mathf.Abs(normal.y)<.8f){Append(Vertex(ai),Vertex(bi),Vertex(ci),indices[sub]);continue;}
                    var p=new List<Vector2>{new Vector2(a.x,a.z),new Vector2(b.x,b.z),new Vector2(c.x,c.z)};
                    var candidates=GroundCandidates(PolygonBounds(p)).ToArray();
                    if(candidates.Length==0){Append(Vertex(ai),Vertex(bi),Vertex(ci),indices[sub]);continue;}
                    if(Area2(p)<0)p.Reverse();
                    var remain=new List<List<Vector2>>();
                    if(candidates.Length<=128)remain=Uncovered(p,candidates);
                    else foreach(var key in GroundCells(PolygonBounds(p))){
                        float x=key.x*4,z=key.y*4;var cell=new[]{new Vector2(x,z),new Vector2(x+4,z),new Vector2(x+4,z+4),new Vector2(x,z+4)};var piece=p;
                        for(int e=0;e<4&&piece.Count>=3;e++)piece=HalfPlane(piece,cell[e],cell[(e+1)%4],true);
                        if(piece.Count>=3&&Mathf.Abs(Area2(piece))>.000001f)remain.AddRange(Uncovered(piece,GroundCandidates(PolygonBounds(piece))));
                    }
                    if(Mathf.Abs(remain.Sum(v=>Mathf.Abs(Area2(v)))-Mathf.Abs(Area2(p)))<.000001f){Append(Vertex(ai),Vertex(bi),Vertex(ci),indices[sub]);continue;}
                    changed=true;trimmed++;var va=Vertex(ai);var vb=Vertex(bi);var vc=Vertex(ci);
                    var pa=new Vector2(a.x,a.z);var pb=new Vector2(b.x,b.z);var pc=new Vector2(c.x,c.z);float denominator=Cross2(pb-pa,pc-pa);
                    CutVertex Interpolate(Vector2 q){float wb=Cross2(q-pa,pc-pa)/denominator,wc=Cross2(pb-pa,q-pa)/denominator,wa=1-wb-wc;return new CutVertex{p=va.p*wa+vb.p*wb+vc.p*wc,n=(va.n*wa+vb.n*wb+vc.n*wc).normalized,uv=va.uv*wa+vb.uv*wb+vc.uv*wc,c=va.c*wa+vb.c*wb+vc.c*wc};}
                    foreach(var piece in remain)for(int j=1;j+1<piece.Count;j++){
                        var u=Interpolate(piece[0]);var v=Interpolate(piece[j]);var w=Interpolate(piece[j+1]);
                        if(denominator<0)Append(u,w,v,indices[sub]);else Append(u,v,w,indices[sub]);
                    }
                }
            }
            if(!changed)continue;
            var repaired=new Mesh{name="Non-overlapping previous ground",indexFormat=IndexFormat.UInt32};
            repaired.SetVertices(output.Select(v=>v.p).ToList());repaired.SetNormals(output.Select(v=>v.n).ToList());repaired.SetUVs(0,output.Select(v=>v.uv).ToList());repaired.SetColors(output.Select(v=>v.c).ToList());
            repaired.subMeshCount=indices.Length;for(int s=0;s<indices.Length;s++)repaired.SetTriangles(indices[s],s);repaired.RecalculateBounds();
            repaired=Save(repaired,Folder+"/Meshes/AlignedSurface-"+(count++)+".asset");
            var go=new GameObject("Ground overlap repair");go.transform.SetParent(holder,false);go.AddComponent<MeshFilter>().sharedMesh=repaired;var rr=go.AddComponent<MeshRenderer>();rr.sharedMaterials=renderer.sharedMaterials;rr.shadowCastingMode=renderer.shadowCastingMode;
            renderer.enabled=false;if(!filter.transform.IsChildOf(holder))suppressed.Add(renderer);
        }
        state.suppressedRenderers=suppressed.Distinct().ToArray();Debug.Log($"PREVIOUS_GROUND_TRIMMED: {trimmed} triangles in {count} meshes");
    }
}
