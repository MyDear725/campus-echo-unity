using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

[Serializable] public class InteriorSpawnPoint
{
    public string id="main";
    public Transform anchor;
}

[Serializable] public class InteriorConnection
{
    public string id,displayName,destinationSpawnId;
    public Transform anchor;
    public BuildingEntranceDefinition destination;
    [Min(.5f)] public float radius=1.6f;
    public bool IsNear(Vector3 p)=>anchor&&anchor.gameObject.activeInHierarchy&&Mathf.Abs(p.y-anchor.position.y)<1&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(anchor.position.x,anchor.position.z))<=radius;
}

// Keep exactly one of these in each interior scene. Artists own the remaining
// hierarchy; no CampusGame, player, camera or AudioListener should be duplicated.
[Serializable] public class InteriorExteriorExit { public Transform anchor; public string entranceId; }

public class InteriorSceneRoot : MonoBehaviour
{
    public string buildingId;
    public InteriorSpawnPoint[] spawnPoints;
    public Transform[] exits;
    public InteriorExteriorExit[] exteriorExits=Array.Empty<InteriorExteriorExit>();
    public string ExteriorExitAt(Vector3 p)=>exteriorExits?.Where(e=>e!=null&&e.anchor&&Mathf.Abs(e.anchor.position.y-p.y)<1.5f&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(e.anchor.position.x,e.anchor.position.z))<=exitRadius).OrderBy(e=>(e.anchor.position-p).sqrMagnitude).FirstOrDefault()?.entranceId;
    public InteriorConnection[] connections=Array.Empty<InteriorConnection>();
    [Min(.5f)] public float exitRadius=2.4f;
    [Tooltip("Optional. Exit i is bound to the entrance whose EntranceId is " +
             "exitEntranceIds[i]; leaving through it returns to THAT doorway outside. " +
             "Keep the array aligned with exits. Leave an entry empty (or omit the " +
             "array) to fall back to the entrance the player came in through.")]
    public string[] exitEntranceIds;
    public float fallRecoveryHeight=-10;
    public bool isPlaceholder=true;
    public bool showExitMarkers=true;
    public Color backgroundColor=new Color(.18f,.21f,.24f);
    CampusGame game;
    public UnityEvent onPlayerEntered=new UnityEvent();
    public UnityEvent onPlayerLeaving=new UnityEvent();
    void Start(){game=FindFirstObjectByType<CampusGame>();}
    void OnGUI() {
        if(!showExitMarkers||!game||game.paused||!game.Interiors||game.Interiors.Interior!=this||game.Interiors.Busy)return;
        var matrix=GUI.matrix;GUI.matrix=Matrix4x4.identity;
        foreach(var exit in exits.Where(e=>e&&e.gameObject.activeInHierarchy)) {
            var p=game.followCamera.WorldToScreenPoint(exit.position+Vector3.up*2.2f);
            if(p.z<=0||p.x<0||p.x>Screen.width||p.y<0||p.y>Screen.height)continue;
            var rect=new Rect(p.x-80,Screen.height-p.y-16,160,32);GUI.Box(rect,"");
            GUI.Label(rect,"出口 · 返回校园",new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,font=game.interfaceFont,fontSize=17});
        }
        GUI.matrix=matrix;
    }

    public Transform FindSpawn(string id)=>spawnPoints?.FirstOrDefault(p=>p!=null&&p.id==id)?.anchor;
    public InteriorConnection NearbyConnection(Vector3 position)=>connections?.Where(c=>c!=null&&c.IsNear(position)).OrderBy(c=>(c.anchor.position-position).sqrMagnitude).FirstOrDefault();

    // The exit the player is standing at, or null. NearExit() delegates to this so the
    // prompt, the E key and the return position all describe the same doorway.
    public Transform NearestExit(Vector3 position)
    {
        if(exits==null)return null;
        Transform best=null;float bestSqr=float.MaxValue;
        foreach(var e in exits)
        {
            if(!e||!e.gameObject.activeInHierarchy)continue;
            if(Mathf.Abs(e.position.y-position.y)>=2)continue;
            float sqr=(new Vector2(e.position.x,e.position.z)-new Vector2(position.x,position.z)).sqrMagnitude;
            if(sqr>exitRadius*exitRadius)continue;
            if(sqr<bestSqr){bestSqr=sqr;best=e;}
        }
        return best;
    }

    // The EntranceId bound to an exit, or null when the exit has no binding.
    public string ExitEntranceId(Transform exit)
    {
        if(exit==null||exits==null||exitEntranceIds==null)return null;
        int i=Array.IndexOf(exits,exit);
        if(i<0||i>=exitEntranceIds.Length)return null;
        return string.IsNullOrWhiteSpace(exitEntranceIds[i])?null:exitEntranceIds[i];
    }

    public bool NearExit(Vector3 position)=>NearestExit(position)!=null;
    public string ValidateContract(string expectedId,string spawnId) {
        if(buildingId!=expectedId)return "室内建筑 ID 与入口配置不一致";
        if(!FindSpawn(spawnId))return "室内缺少出生点："+spawnId;
        if(spawnPoints.Count(p=>p!=null&&p.id==spawnId)!=1)return "室内出生点 ID 重复："+spawnId;
        if(exits==null||!exits.Any(e=>e&&e.gameObject.activeInHierarchy))return "室内未设置可用出口";
        if(FindSpawn(spawnId).gameObject.scene!=gameObject.scene||exits.Where(e=>e).Any(e=>e.gameObject.scene!=gameObject.scene))return "出生点或出口不属于当前室内场景";
        if(exitEntranceIds!=null&&exitEntranceIds.Length>exits.Length)return "出口绑定数组 exitEntranceIds 比 exits 长";
        return null;
    }
    void OnDrawGizmos() {
        if(spawnPoints!=null)foreach(var p in spawnPoints)if(p?.anchor){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(p.anchor.position,.35f);Gizmos.DrawLine(p.anchor.position,p.anchor.position+p.anchor.forward*1.5f);}
        if(exits!=null)foreach(var exit in exits)if(exit){Gizmos.color=Color.green;Gizmos.DrawWireSphere(exit.position,exitRadius);}
    }
}
