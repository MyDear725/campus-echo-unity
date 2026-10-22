using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Searches the same 2D collision map as the outdoor controller, or the actual indoor
// floors/colliders. No straight-line fallback through walls when a route is unavailable.
public interface ICampusRouteGrid
{
    int Closest(Vector3 point);
    Vector3 Position(int id);
    void Neighbors(int id, List<int> output);
    bool Clear(Vector3 from, Vector3 to);
}
public sealed class CampusOutdoorRouteGrid : ICampusRouteGrid
{
    const float Cell=1.5f, Bin=12;
    const int Width=401, Height=278;
    readonly Dictionary<Vector2Int,CampusObstacle[]> bins=new Dictionary<Vector2Int,CampusObstacle[]>();
    readonly CampusGame game;
    readonly List<(Vector2 a,Vector2 b,float half)> streets=new List<(Vector2,Vector2,float)>();
    readonly Dictionary<int,bool> walkable=new Dictionary<int,bool>();
    static readonly CampusObstacle[] Empty=Array.Empty<CampusObstacle>();
    public CampusOutdoorRouteGrid(CampusData data,CampusGame campus=null)
    {
        game=campus;
        if(game&&game.Outdoor)foreach(var road in data.roads.Where(r=>r.kind=="external"&&r.name=="学府大街"))
            for(int i=1;i<road.points.Length;i++)streets.Add((new Vector2(road.points[i-1].x,road.points[i-1].z),new Vector2(road.points[i].x,road.points[i].z),road.width*.5f+.5f));
        var staging=new Dictionary<Vector2Int,List<CampusObstacle>>();
        foreach(var o in data.obstacles)
        {
            var points=o.points!=null&&o.points.Length>0?o.points:o.segment;
            if(points!=null&&points.Length==0)points=null;
            float minX=points!=null?points.Min(p=>p.x):o.minX,maxX=points!=null?points.Max(p=>p.x):o.maxX;
            float minZ=points!=null?points.Min(p=>p.z):o.minZ,maxZ=points!=null?points.Max(p=>p.z):o.maxZ;
            float pad=.8f+o.thickness;
            for(int x=Mathf.FloorToInt((minX-pad)/Bin);x<=Mathf.FloorToInt((maxX+pad)/Bin);x++)
            for(int z=Mathf.FloorToInt((minZ-pad)/Bin);z<=Mathf.FloorToInt((maxZ+pad)/Bin);z++)
            {
                var key=new Vector2Int(x,z);if(!staging.TryGetValue(key,out var list))staging[key]=list=new List<CampusObstacle>();list.Add(o);
            }
        }
        foreach(var pair in staging)bins[pair.Key]=pair.Value.ToArray();
    }
    float SurfaceHeight(float x,float z)
    {
        if(!game||!game.Outdoor)return .23f;
        float floor=CampusPlanTerrain.Height(x,z)+.065f;
        if(Mathf.Abs(z-10)>1.75f)return floor;
        if(x>=105.5f&&x<=194.5f)return 6.54f;
        if(x>=91.82f&&x<105.5f)return Mathf.Lerp(.295f,6.54f,Mathf.InverseLerp(91.82f,105.5f,x));
        if(x>194.5f&&x<=202.06f)return Mathf.Lerp(6.54f,3.145f,Mathf.InverseLerp(194.5f,202.06f,x));
        return floor;
    }
    bool Free(float x,float z)
    {
        if(streets.Count>0&&SurfaceHeight(x,z)<CampusPlanTerrain.Height(x,z)+2)
        {
            var p=new Vector2(x,z);
            foreach(var street in streets){var d=street.b-street.a;float t=Mathf.Clamp01(Vector2.Dot(p-street.a,d)/Mathf.Max(.001f,d.sqrMagnitude));if(Vector2.Distance(p,street.a+d*t)<street.half)return false;}
        }
        bins.TryGetValue(new Vector2Int(Mathf.FloorToInt(x/Bin),Mathf.FloorToInt(z/Bin)),out var obstacles);
        if(CampusCollision.Blocked(x,z,obstacles??Empty,.48f))return false;
        if(game&&game.Outdoor)
        {
            var p=game.MapToWorld(new Vector3(x,SurfaceHeight(x,z),z));
            if(Physics.CheckCapsule(p+Vector3.up*.5f,p+Vector3.up*1.5f,.28f,1<<9,QueryTriggerInteraction.Ignore))return false;
        }
        return true;
    }
    bool Walk(int id)
    {
        if(id<0||id>=Width*Height)return false;
        if(!walkable.TryGetValue(id,out bool value)){var p=Position(id);walkable[id]=value=Free(p.x,p.z);}return value;
    }
    public Vector3 Position(int id){float x=-290+(id%Width)*Cell,z=-180+(id/Width)*Cell;return new Vector3(x,SurfaceHeight(x,z),z);}
    public int Closest(Vector3 point)
    {
        int gx=Mathf.RoundToInt((point.x+290)/Cell),gz=Mathf.RoundToInt((point.z+180)/Cell),best=-1;float distance=float.MaxValue;
        for(int x=gx-3;x<=gx+3;x++)for(int z=gz-3;z<=gz+3;z++)
        {
            if(x<0||x>=Width||z<0||z>=Height)continue;int id=x+z*Width;if(!Walk(id))continue;
            float sq=(Position(id)-point).sqrMagnitude;if(sq>=distance||!Clear(point,Position(id)))continue;distance=sq;best=id;
        }
        return best;
    }
    public void Neighbors(int id,List<int> output)
    {
        output.Clear();int x=id%Width,z=id/Width;
        for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)
        {
            if(dx==0&&dz==0)continue;int nx=x+dx,nz=z+dz;
            if(nx<0||nz<0||nx>=Width||nz>=Height)continue;int next=nx+nz*Width;
            if(Walk(next)&&Clear(Position(id),Position(next)))output.Add(next);
        }
    }
    public bool Clear(Vector3 from,Vector3 to)
    {
        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/.45f));
        for(int i=0;i<=steps;i++){var p=Vector3.Lerp(from,to,(float)i/steps);if(!Free(p.x,p.z)||(game&&game.Outdoor&&Mathf.Abs(p.y-SurfaceHeight(p.x,p.z))>.4f))return false;}return true;
    }
}
public sealed class CampusIndoorRouteGrid : ICampusRouteGrid
{
    const float Cell=.65f;
    readonly Scene scene;
    readonly Bounds bounds;
    readonly Dictionary<Vector2Int,List<int>> cells=new Dictionary<Vector2Int,List<int>>();
    readonly List<Vector3> positions=new List<Vector3>();
    readonly List<Vector2Int> indices=new List<Vector2Int>();
    public CampusIndoorRouteGrid(InteriorSceneRoot root)
    {
        scene=root.gameObject.scene;bounds=new Bounds(root.transform.position,Vector3.zero);
        foreach(var c in root.GetComponentsInChildren<Collider>())if(c.enabled&&!c.isTrigger)bounds.Encapsulate(c.bounds);
    }
    public Vector3 Position(int id)=>positions[id];
    List<int> Sample(Vector2Int cell)
    {
        if(cells.TryGetValue(cell,out var result))return result;
        result=new List<int>();cells[cell]=result;
        float x=cell.x*Cell,z=cell.y*Cell;
        if(x<bounds.min.x||x>bounds.max.x||z<bounds.min.z||z>bounds.max.z)return result;
        var hits=Physics.RaycastAll(new Vector3(x,bounds.max.y+2,z),Vector3.down,bounds.size.y+5,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        foreach(var h in hits.OrderBy(h=>h.point.y))
        {
            if(h.collider.gameObject.scene!=scene||h.normal.y<.65f)continue;
            var p=h.point+Vector3.up*.045f;
            if(result.Any(id=>Mathf.Abs(positions[id].y-p.y)<.1f))continue;
            if(Physics.OverlapCapsule(p+Vector3.up*.35f,p+Vector3.up*1.5f,.27f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(c=>c.gameObject.scene==scene))continue;
            result.Add(positions.Count);positions.Add(p);indices.Add(cell);
        }
        return result;
    }
    public int Closest(Vector3 point)
    {
        var cell=new Vector2Int(Mathf.RoundToInt(point.x/Cell),Mathf.RoundToInt(point.z/Cell));int best=-1;float distance=float.MaxValue;
        for(int dx=-3;dx<=3;dx++)for(int dz=-3;dz<=3;dz++)foreach(int id in Sample(cell+new Vector2Int(dx,dz)))
        {
            var p=positions[id];if(Mathf.Abs(p.y-point.y)>.7f)continue;
            float sq=(p-point).sqrMagnitude;if(sq<distance&&Clear(point,p)){distance=sq;best=id;}
        }
        return best;
    }
    public void Neighbors(int id,List<int> output)
    {
        output.Clear();var cell=indices[id];var from=positions[id];
        for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)
        {
            if(dx==0&&dz==0)continue;
            foreach(int next in Sample(cell+new Vector2Int(dx,dz)))if(Mathf.Abs(positions[next].y-from.y)<=.45f&&Clear(from,positions[next]))output.Add(next);
        }
    }
    public bool Clear(Vector3 from,Vector3 to)
    {
        Vector3 delta=to-from;float length=delta.magnitude;
        if(length>.01f&&Physics.CapsuleCastAll(from+Vector3.up*.65f,from+Vector3.up*1.5f,.25f,delta/length,length,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(h=>h.collider.gameObject.scene==scene))return false;
        int steps=Mathf.Max(1,Mathf.CeilToInt(length/.4f));
        for(int i=0;i<=steps;i++)
        {
            var p=Vector3.Lerp(from,to,(float)i/steps);
            if(!Physics.RaycastAll(p+Vector3.up*.3f,Vector3.down,.7f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).Any(h=>h.collider.gameObject.scene==scene&&h.normal.y>.65f))return false;
        }
        return true;
    }
}
public static class CampusRoutePlanner
{
    struct Entry : IComparable<Entry>
    {
        public float score;public int id,order;
        public int CompareTo(Entry other){int c=score.CompareTo(other.score);return c!=0?c:order.CompareTo(other.order);}
    }
    public static IEnumerator Find(ICampusRouteGrid grid,Vector3 start,Vector3 goal,Action<List<Vector3>> completed)
    {
        int source=grid.Closest(start),target=grid.Closest(goal);
        if(source<0||target<0){completed(new List<Vector3>());yield break;}
        var queue=new SortedSet<Entry>();int sequence=0,expanded=0;
        var scores=new Dictionary<int,float>{{source,0}};var parent=new Dictionary<int,int>();var closed=new HashSet<int>();var neighbors=new List<int>();
        queue.Add(new Entry{id=source,score=Vector3.Distance(start,goal),order=sequence++});
        while(queue.Count>0&&expanded<55000)
        {
            var entry=queue.Min;queue.Remove(entry);if(!closed.Add(entry.id))continue;
            int node=entry.id;
            if(node==target)
            {
                var path=new List<Vector3>{grid.Position(node)};
                while(parent.TryGetValue(node,out int previous)){node=previous;path.Add(grid.Position(node));}
                path.Reverse();
                if(grid.Clear(start,path[0]))path.Insert(0,start);
                if(grid.Clear(path[path.Count-1],goal))path.Add(goal);
                // Outdoor string pulling is safe against the exact collision map. Indoors retain
                // sampled floor heights so stairs are represented in the rendered line.
                if(grid is CampusOutdoorRouteGrid)
                {
                    var smooth=new List<Vector3>{path[0]};int current=0;
                    while(current<path.Count-1){int next=path.Count-1;while(next>current+1&&!grid.Clear(path[current],path[next]))next--;smooth.Add(path[next]);current=next;}path=smooth;
                }
                completed(path);yield break;
            }
            grid.Neighbors(node,neighbors);
            foreach(int next in neighbors)
            {
                if(closed.Contains(next))continue;
                float score=scores[node]+Vector3.Distance(grid.Position(node),grid.Position(next));
                if(scores.TryGetValue(next,out float old)&&old<=score)continue;
                scores[next]=score;parent[next]=node;
                queue.Add(new Entry{id=next,score=score+Vector3.Distance(grid.Position(next),grid.Position(target)),order=sequence++});
            }
            expanded++;if(expanded%160==0)yield return null;
        }
        completed(new List<Vector3>());
    }
}
