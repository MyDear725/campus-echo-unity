using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Preferences are independent of semester snapshots; the tracked task belongs to the
// current semester. Routing transparently changes from campus entrance to indoor point.
public sealed class CampusRouteGuide : MonoBehaviour
{
    public const string PreferenceKey="CampusRouteLineEnabledV1";
    public static string PreferenceKeyOverride;
    public bool LineEnabled => PlayerPrefs.GetInt(PreferenceKeyOverride??PreferenceKey,1)!=0;
    public string Target => owner?.State.trackedLocation??"";
    public string Status { get; private set; }="";
    public bool Searching { get; private set; }
    public IReadOnlyList<Vector3> Path => path;
    public LineRenderer Line => line;
    CampusSemester owner;
    LineRenderer line;Material material;
    ICampusRouteGrid grid;
    List<Vector3> path=new List<Vector3>();
    Coroutine search;
    string context="";
    int gridScene=-1,gridFloor=-1;
    bool gridRoom;
    float refresh;
    Vector3 lastGoal;
    public void Initialize(CampusSemester semester)
    {
        owner=semester;
        line=gameObject.AddComponent<LineRenderer>();line.useWorldSpace=true;line.widthMultiplier=.11f;
        line.numCornerVertices=4;line.numCapVertices=4;line.alignment=LineAlignment.View;
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
        material=new Material(Resources.Load<Shader>("CampusRoute"));line.sharedMaterial=material;line.enabled=false;
    }
    void OnDestroy(){if(material)Destroy(material);}
    public void SetEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(PreferenceKeyOverride??PreferenceKey,enabled?1:0);PlayerPrefs.Save();
        if(!enabled){CancelSearch();line.enabled=false;}
        else context="";
    }
    public bool Resolve(out Vector3 destination,out string instruction)
    {
        destination=Vector3.zero;instruction="";var game=owner?owner.Game:null;
        if(!game||!game.player||string.IsNullOrEmpty(Target)||game.Interiors.Busy)return false;
        bool inside=game.Interiors.Inside;
        if(inside)
        {
            var root=game.Interiors.Interior;var dorm=DormitoryCorridor.Current(game);
            if(dorm&&dorm.InsideRoom)
            {
                var bedroom=dorm.GetComponent<DormitoryRoomInterior>();
                destination=bedroom.ExitApproach;instruction="先从寝室门按 E 返回走廊";return true;
            }
            if(dorm&&dorm.Floor>1)
            {
                float side=root.transform.InverseTransformPoint(game.player.position).x<0?-1:1;
                destination=root.transform.TransformPoint(new Vector3(side*(dorm.Layout.stairX-.2f),.06f,-.8f));instruction="先到楼梯口按 E 下至一楼";return true;
            }
            var point=FindObjectsByType<CampusSemesterPoint>(FindObjectsSortMode.None).FirstOrDefault(p=>p.location==Target&&p.gameObject.scene==root.gameObject.scene);
            if(point){destination=point.transform.position;instruction=CampusSemesterPoint.PlaceName(Target);return true;}
            var exit=root.exits?.Where(e=>e&&e.gameObject.activeInHierarchy).OrderBy(e=>(e.position-game.player.position).sqrMagnitude).FirstOrDefault();
            if(!exit)return false;
            destination=exit.position;instruction="先到出口按 E 返回校园";return true;
        }
        var outdoor=FindObjectsByType<CampusSemesterPoint>(FindObjectsSortMode.None).FirstOrDefault(p=>p.location==Target&&p.gameObject.scene==game.gameObject.scene);
        if(outdoor){destination=outdoor.transform.position;instruction=CampusSemesterPoint.PlaceName(Target);return true;}
        string building=Target=="a"?"teaching-a":Target=="b"?"teaching-b":Target=="c"?"teaching-c":Target=="dorm"?"dormitory-1":Target;
        var entrance=game.Interiors.entrances.FirstOrDefault(e=>e&&e.buildingId==building);
        if(!entrance)return false;
        destination=game.MapToWorld(entrance.approachMapPosition);instruction="前往"+entrance.displayName+"，按 E 进入";return true;
    }
    void CancelSearch(){if(search!=null)StopCoroutine(search);search=null;Searching=false;}
    void Update()
    {
        if(!owner||!owner.Game||!line)return;
        var game=owner.Game;
        if(!LineEnabled||owner.Restoring||!Resolve(out Vector3 target,out string instruction)){line.enabled=false;return;}
        var dorm=DormitoryCorridor.Current(game);int scene=game.Interiors.Inside?game.Interiors.Interior.gameObject.scene.handle:game.gameObject.scene.handle;
        string key=Target+":"+scene+":"+(dorm?dorm.Floor:0)+":"+(dorm&&dorm.InsideRoom)+":"+instruction;
        bool changed=context!=key||(lastGoal-target).sqrMagnitude>.25f;
        if(changed)
        {
            CancelSearch();path.Clear();line.enabled=false;context=key;lastGoal=target;
            if(gridScene!=scene||gridFloor!=(dorm?dorm.Floor:0)||gridRoom!=(dorm&&dorm.InsideRoom))
            {
                gridScene=scene;gridFloor=dorm?dorm.Floor:0;gridRoom=dorm&&dorm.InsideRoom;
                grid=game.Interiors.Inside?(ICampusRouteGrid)new CampusIndoorRouteGrid(game.Interiors.Interior):new CampusOutdoorRouteGrid(JsonUtility.FromJson<CampusData>(game.mapData.text));
            }
        }
        refresh-=Time.unscaledDeltaTime;
        if(!Searching&&(changed||refresh<=0))
        {
            refresh=path.Count==0?5:1;
            float near=path.Count>1?Vector3.Distance(NearestOnPath(game.player.position,out _),game.player.position):float.MaxValue;
            // Re-route on scene/target change and when the player leaves the current route.
            if(changed||path.Count==0||near>2)search=StartCoroutine(Plan(game.player.position,target,instruction));
        }
        if(path.Count>1&&!Searching)
        {
            Vector3 projection=NearestOnPath(game.player.position,out int segment);
            var visible=new List<Vector3>();
            if(grid.Clear(game.Interiors.Inside?game.player.position:game.WorldToMap(game.player.position),game.Interiors.Inside?projection:game.WorldToMap(projection)))visible.Add(game.player.position);
            visible.Add(projection);
            for(int i=segment+1;i<path.Count;i++)visible.Add(path[i]);
            for(int i=visible.Count-1;i>0;i--)if((visible[i]-visible[i-1]).sqrMagnitude<.0025f)visible.RemoveAt(i);
            // Keep the first visible stretch ahead of the avatar, avoiding a cap at their feet.
            float trim=.65f;
            while(visible.Count>1&&trim>0)
            {
                float length=Vector3.Distance(visible[0],visible[1]);
                if(length<=trim){trim-=length;visible.RemoveAt(0);}
                else {visible[0]=Vector3.MoveTowards(visible[0],visible[1],trim);trim=0;}
            }
            line.positionCount=visible.Count;
            for(int i=0;i<visible.Count;i++){var p=visible[i];p.y+=.11f;line.SetPosition(i,p);}
            line.enabled=visible.Count>1&&!game.Interiors.Busy;
        }
    }
    Vector3 NearestOnPath(Vector3 position,out int segment)
    {
        segment=0;Vector3 best=path[0];float distance=float.MaxValue;
        for(int i=0;i<path.Count-1;i++)
        {
            Vector3 delta=path[i+1]-path[i];float t=Mathf.Clamp01(Vector3.Dot(position-path[i],delta)/Mathf.Max(.0001f,delta.sqrMagnitude));
            var point=path[i]+delta*t;float sq=(point-position).sqrMagnitude;
            if(sq<distance){distance=sq;segment=i;best=point;}
        }
        return best;
    }
    IEnumerator Plan(Vector3 start,Vector3 goal,string instruction)
    {
        Searching=true;Status="正在规划路线…";bool inside=owner.Game.Interiors.Inside;
        if(!inside){start=owner.Game.WorldToMap(start);goal=owner.Game.WorldToMap(goal);}
        yield return CampusRoutePlanner.Find(grid,start,goal,result=>path=inside?result:result.Select(owner.Game.MapToWorld).ToList());
        Searching=false;Status=path.Count>1?instruction:"暂未找到可通行路线，请参考方向提示或楼层指引。";
    }
}
