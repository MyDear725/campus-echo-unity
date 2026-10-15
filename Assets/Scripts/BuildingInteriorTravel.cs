using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CampusGame))]
public class BuildingInteriorTravel : MonoBehaviour
{
    public BuildingEntranceDefinition[] entrances=Array.Empty<BuildingEntranceDefinition>();
    public enum TravelState { Outside, Loading, Inside, Leaving }
    public TravelState State {get;private set;}
    public bool Inside=>State==TravelState.Inside||State==TravelState.Leaving;
    public bool Busy=>State==TravelState.Loading||State==TravelState.Leaving;
    public BuildingEntranceDefinition Current {get;private set;}
    public InteriorSceneRoot Interior {get;private set;}
    public string LastError {get;private set;}
    public event Action<BuildingEntranceDefinition> Entered;
    public event Action<BuildingEntranceDefinition> Exited;
    public CharacterController IndoorController {get;private set;}
    CampusGame game;
    Scene campusScene,loadedScene;
    readonly List<GameObject> hiddenCampusRoots=new List<GameObject>();
    int originalPlayerLayer;
    Vector3 fallbackOutsidePosition;
    float fade;
    CameraClearFlags outsideClearFlags;
    Color outsideBackground;

    void Awake(){game=GetComponent<CampusGame>();campusScene=gameObject.scene;}
    void Start() {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-entrancePreview");
        if(at>=0){string id=at+1<args.Length?args[at+1]:"cafeteria";var entry=entrances.FirstOrDefault(e=>e&&e.EntranceId==id);if(entry){game.SetTravelPose(game.MapToWorld(entry.returnMapPosition),entry.enterYaw,10);game.Notify("靠近"+entry.displayName+"门口，按 E 进入。");}}
    }
    void Update(){
        fade=Mathf.MoveTowards(fade,Busy?1:0,Time.unscaledDeltaTime*5);
        if(State==TravelState.Inside&&Interior&&game.player.position.y<Interior.fallRecoveryHeight)PlaceInside(Interior.FindSpawn(Current.spawnId));
    }
    public BuildingEntranceDefinition NearbyEntrance() {
        if(State!=TravelState.Outside)return null;
        var p=game.WorldToMap(game.player.position);
        return entrances.Where(e=>e&&Mathf.Abs(e.approachMapPosition.y-p.y)<2&&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(e.approachMapPosition.x,e.approachMapPosition.z))<=e.interactionRadius)
            .OrderBy(e=>(e.approachMapPosition-p).sqrMagnitude).FirstOrDefault();
    }
    public string InteractionPrompt {
        get {
            if(Busy)return State==TravelState.Loading?"正在进入…":"正在返回校园…";
            if(Inside)return Interior&&Interior.NearExit(game.player.position)?"[ E ] 返回校园":"";
            var entry=NearbyEntrance();return entry?"[ E ] 进入 "+entry.displayName:"";
        }
    }
    // Called once by CampusGame's E-key dispatcher, before outdoor NPC/quest actions.
    public bool TryInteract() {
        if(Busy)return true;
        if(Inside){if(Interior.NearExit(game.player.position))TryExit();return true;}
        var entry=NearbyEntrance();if(!entry)return false;TryEnter(entry);return true;
    }
    public bool TryEnter(BuildingEntranceDefinition entry) {
        if(State!=TravelState.Outside||game.paused||!entry||NearbyEntrance()!=entry)return false;
        LastError=null;
        if(string.IsNullOrWhiteSpace(entry.interiorScenePath)||!Application.CanStreamedLevelBeLoaded(entry.interiorScenePath)) {Fail("室内场景尚未接入或未加入构建列表");return false;}
        if(entry.interiorScenePath==campusScene.path||SceneManager.GetSceneByPath(entry.interiorScenePath).isLoaded){Fail("入口不能指向校园或已经加载的场景");return false;}
        State=TravelState.Loading;Current=entry;StartCoroutine(Enter());return true;
    }
    public bool TryExit() {
        if(State!=TravelState.Inside||game.paused||!Interior||!Interior.NearExit(game.player.position))return false;
        State=TravelState.Leaving;StartCoroutine(Leave());return true;
    }
    void Fail(string reason){LastError=reason;game.Notify("暂时无法进入："+reason+"。你仍在校园中。");Debug.LogWarning("Interior transition: "+reason);}
    AsyncOperation BeginLoad(string path,out string error) {
        try {error=null;return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);}catch(Exception e){error=e.Message;return null;}
    }
    IEnumerator Enter() {
        yield return new WaitForSecondsRealtime(.2f);
        var operation=BeginLoad(Current.interiorScenePath,out string error);
        if(operation==null){State=TravelState.Outside;Current=null;Fail(error??"场景加载未启动");yield break;}
        while(!operation.isDone)yield return null;
        loadedScene=SceneManager.GetSceneByPath(Current.interiorScenePath);
        var roots=loadedScene.IsValid()&&loadedScene.isLoaded?loadedScene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<InteriorSceneRoot>(true)).ToArray():Array.Empty<InteriorSceneRoot>();
        error=roots.Length==1?roots[0].ValidateContract(Current.buildingId,Current.spawnId):"室内需要且只能有一个 InteriorSceneRoot";
        if(error==null) {
            var spawn=roots[0].FindSpawn(Current.spawnId);Physics.SyncTransforms();
            if(!roots[0].gameObject.activeInHierarchy||!spawn.gameObject.activeInHierarchy)error="室内根节点或出生点被禁用";
            else if(!Physics.RaycastAll(spawn.position+Vector3.up*.15f,Vector3.down,3,Physics.AllLayers,QueryTriggerInteraction.Ignore).Any(h=>h.collider.gameObject.scene==loadedScene))error="出生点下方缺少地面 Collider";
            else if(Physics.OverlapCapsule(spawn.position+Vector3.up*.34f,spawn.position+Vector3.up*1.46f,.3f,Physics.AllLayers,QueryTriggerInteraction.Ignore).Any(c=>c.gameObject.scene==loadedScene))error="出生点与墙体或其他碰撞体重叠";
        }
        if(error==null&&loadedScene.GetRootGameObjects().Any(r=>r.GetComponentsInChildren<Camera>(true).Length>0||r.GetComponentsInChildren<AudioListener>(true).Length>0||r.GetComponentsInChildren<CampusGame>(true).Length>0))error="室内不能重复放置玩家控制器、相机或 AudioListener";
        if(error!=null){if(loadedScene.IsValid()&&loadedScene.isLoaded)yield return SceneManager.UnloadSceneAsync(loadedScene);loadedScene=default;State=TravelState.Outside;Current=null;Fail(error);yield break;}
        Interior=roots[0];fallbackOutsidePosition=game.player.position;
        outsideClearFlags=game.followCamera.clearFlags;outsideBackground=game.followCamera.backgroundColor;
        hiddenCampusRoots.Clear();
        foreach(var root in campusScene.GetRootGameObjects()) {
            if(root==game.transform.root.gameObject||root==game.player.root.gameObject||root==game.followCamera.transform.root.gameObject||!root.activeSelf)continue;
            hiddenCampusRoots.Add(root);root.SetActive(false);
        }
        SceneManager.SetActiveScene(loadedScene);
        game.followCamera.clearFlags=CameraClearFlags.SolidColor;game.followCamera.backgroundColor=new Color(.18f,.21f,.24f);
        originalPlayerLayer=game.player.gameObject.layer;game.player.gameObject.layer=2; // IgnoreRaycast keeps camera off the shared player capsule.
        IndoorController=game.player.GetComponent<CharacterController>()??game.player.gameObject.AddComponent<CharacterController>();
        IndoorController.enabled=false;IndoorController.height=1.8f;IndoorController.radius=.32f;IndoorController.center=new Vector3(0,.9f,0);IndoorController.stepOffset=.3f;IndoorController.slopeLimit=45;IndoorController.skinWidth=.025f;IndoorController.minMoveDistance=0;
        State=TravelState.Inside;PlaceInside(Interior.FindSpawn(Current.spawnId));
        game.Notify(Current.displayName+(Interior.isPlaceholder?" · 空白室内。转身找到绿色出口，靠近后按 E 返回。":" · 靠近出口按 E 返回校园。"));
        try{Interior.onPlayerEntered.Invoke();Entered?.Invoke(Current);}catch(Exception e){Debug.LogException(e);}
    }
    void PlaceInside(Transform anchor) {
        if(!anchor)return;IndoorController.enabled=false;
        game.SetTravelPose(anchor.position,anchor.eulerAngles.y,12);Physics.SyncTransforms();IndoorController.enabled=true;
    }
    IEnumerator Leave() {
        yield return new WaitForSecondsRealtime(.2f);
        var previous=Current;
        try{Interior.onPlayerLeaving.Invoke();}catch(Exception e){Debug.LogException(e);}
        IndoorController.enabled=false;game.player.gameObject.layer=originalPlayerLayer;
        SceneManager.SetActiveScene(campusScene);
        foreach(var root in hiddenCampusRoots)if(root)root.SetActive(true);hiddenCampusRoots.Clear();
        game.followCamera.clearFlags=outsideClearFlags;game.followCamera.backgroundColor=outsideBackground;
        var destination=game.IsMapWalkable(previous.returnMapPosition)?game.MapToWorld(previous.returnMapPosition):fallbackOutsidePosition;
        game.SetTravelPose(destination,previous.enterYaw+180,12);Physics.SyncTransforms();
        if(loadedScene.IsValid()&&loadedScene.isLoaded)yield return SceneManager.UnloadSceneAsync(loadedScene);
        loadedScene=default;Interior=null;Current=null;State=TravelState.Outside;
        game.Notify("已回到"+previous.displayName+"门外。");
        try{Exited?.Invoke(previous);}catch(Exception e){Debug.LogException(e);}
    }
    public CollisionFlags MoveCharacter(Vector3 displacement)=>IndoorController&&IndoorController.enabled?IndoorController.Move(displacement):CollisionFlags.None;
    void OnGUI() {
        if(fade<=.001f)return;
        var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;GUI.matrix=Matrix4x4.identity;GUI.depth=-100;
        GUI.color=new Color(.025f,.035f,.05f,fade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
        if(Busy){GUI.color=Color.white;GUI.Label(new Rect(Screen.width/2-140,Screen.height/2-20,280,40),State==TravelState.Leaving?"正在返回校园…":"正在进入建筑…",new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,font=game.interfaceFont,fontSize=22});}GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;
    }
}
