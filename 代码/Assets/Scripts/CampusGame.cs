using System.Collections;
using System;
using System.Linq;
using System.IO;
using UnityEngine;

public class CampusGame : MonoBehaviour {
 public TextAsset mapData;
 public Transform player,guide;
 public PlayerCharacterCatalog guideCharacterCatalog;
 public Camera followCamera;
 public Font interfaceFont;
 public GameObject[] memories;
 public bool paused=true;
 public bool pixelStudy;
 public CampusOutdoorTraversal Outdoor {get;private set;}
 public BuildingInteriorTravel Interiors {get;private set;}
 public bool Automated {get;private set;}
 public CampusWelcomeStory Story {get;private set;}
 public CampusMapUI Map {get;private set;}
 public bool UiCursorActive {get;private set;}
 public bool UiBlocking {get=>UiCursorActive;set=>SetUiCursor(value);}
 public bool Seated;
 int uiCursorChangedFrame=-1;
 public bool ExteriorShortcutsAllowed=>Interiors==null||(!Interiors.Inside&&!Interiors.Busy);
 public float mapXSign=1;
 public Vector3 MapToWorld(Vector3 point)=>new Vector3(point.x*mapXSign,point.y,point.z);
 public Vector3 WorldToMap(Vector3 point)=>MapToWorld(point);
 float dormitoryOriginalFieldOfView; bool dormitoryCameraApplied;
 CampusData data; float yaw=180,pitch=18,targetYaw=180,targetPitch=18,distance=5.8f,vertical;
 bool grounded=true,started,complete,qa; int collected; string message="欢迎来到校园。沿路到书韵广场寻找小岚。";
 float messageUntil=10; Animation animationPlayer; SkinnedMeshRenderer face; string motion=""; GUIStyle title,body,small;
 PlayerCharacterSelector characterSelector; PlayerCharacterChoice pendingCharacterChoice; bool characterChoiceConfirmed,choosingCharacter;
 void Awake(){
  data=JsonUtility.FromJson<CampusData>(mapData.text);qa=Environment.GetCommandLineArgs().Contains("-campusQA");bool femaleQa=Environment.GetCommandLineArgs().Contains("-femaleCharacterQA");
  Interiors=GetComponent<BuildingInteriorTravel>();Automated=qa||Environment.GetCommandLineArgs().Contains("-githubIntegrationQA")||Environment.GetCommandLineArgs().Contains("-overlayRenderQA")||Environment.GetCommandLineArgs().Contains("-cafeteriaQA")||Environment.GetCommandLineArgs().Contains("-allowanceQA")||Environment.GetCommandLineArgs().Contains("-phoneQA")||Environment.GetCommandLineArgs().Contains("-libDirectQA")||Environment.GetCommandLineArgs().Contains("-femaleCharacterQA")||Environment.GetCommandLineArgs().Contains("-campusPlanQA")||Environment.GetCommandLineArgs().Contains("-gymInteriorQA")||Environment.GetCommandLineArgs().Contains("-entranceQA")||Environment.GetCommandLineArgs().Contains("-teachingCQA")||Environment.GetCommandLineArgs().Contains("-teachingABQA")||Environment.GetCommandLineArgs().Contains("-skybridgeQA");
  Automated=Automated||Environment.GetCommandLineArgs().Contains("-interiorRefinementQA")||Environment.GetCommandLineArgs().Contains("-vroidPresentationQA")||Environment.GetCommandLineArgs().Contains("-performanceQA")||Environment.GetCommandLineArgs().Contains("-informationQA")||Environment.GetCommandLineArgs().Contains("-courtMapQA")||Environment.GetCommandLineArgs().Contains("-remoteCompatibilityQA")||Environment.GetCommandLineArgs().Contains("-finalEnvironmentQA");
  Automated=Automated||Environment.GetCommandLineArgs().Contains("-frameStabilityQA");
  Outdoor=GetComponent<CampusOutdoorTraversal>();
  QualitySettings.vSyncCount=0;Application.targetFrameRate=120;
  characterSelector=player.GetComponent<PlayerCharacterSelector>();
  if(guide!=null&&guideCharacterCatalog!=null){var guideVisual=guide.GetComponent<GuideCharacterVisual>();if(guideVisual==null)guideVisual=guide.gameObject.AddComponent<GuideCharacterVisual>();try{guideVisual.Initialize(guideCharacterCatalog,player);}catch(Exception error){Debug.LogWarning("Xiaolan UMA visual failed to initialize; keeping gameplay flow active. "+error.Message,this);}}
  if(characterSelector!=null){characterSelector.VisualChanged+=RefreshCharacterVisual;var saved=characterSelector.ReadSavedChoice();pendingCharacterChoice=saved;characterChoiceConfirmed=saved==PlayerCharacterChoice.Male||saved==PlayerCharacterChoice.Female;if(Automated){pendingCharacterChoice=femaleQa?PlayerCharacterChoice.Female:PlayerCharacterChoice.Male;characterChoiceConfirmed=true;if(characterSelector.CurrentChoice!=pendingCharacterChoice)characterSelector.ApplyChoice(pendingCharacterChoice,false);}else if(characterChoiceConfirmed&&characterSelector.CurrentChoice!=saved)characterSelector.ApplyChoice(saved,false);}
  // A saved gender choice means the normal game can start immediately; the
  // first-run chooser remains paused until the player confirms a choice.
  // Show the chooser on the first actual game start, even if an editor run left a saved gender preference.
  bool hasStartedBefore=PlayerPrefs.GetInt("CampusUnityStarted",0)==1;
  if(!qa){
   if(characterSelector!=null&&!hasStartedBefore){paused=true;choosingCharacter=true;}
   else if(characterSelector==null||characterChoiceConfirmed)paused=false;
  }
  RefreshCharacterVisual();
  foreach(var actor in new[]{player,guide})foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
  if(!qa){started=PlayerPrefs.GetInt("CampusUnityStarted",0)==1;collected=PlayerPrefs.GetInt("CampusUnityMemories",0)&7;complete=started&&collected==7&&PlayerPrefs.GetInt("CampusUnityComplete",0)==1;}
  if(!started){collected=0;complete=false;}
  for(int i=0;i<memories.Length;i++)memories[i].SetActive((collected&(1<<i))==0);
  if(interfaceFont==null)interfaceFont=Resources.Load<Font>("CampusFont");
  if(interfaceFont==null)interfaceFont=Font.CreateDynamicFontFromOSFont(new string[]{"Arial Unicode MS","Hiragino Sans GB","STHeiti Medium","Microsoft YaHei","SimHei"},20);
  if(!Automated){Story=gameObject.AddComponent<CampusWelcomeStory>();Story.Initialize(this);}
  Map=gameObject.AddComponent<CampusMapUI>();Map.Initialize(this,data);if(pixelStudy&&!qa)Teleport(new Vector3(65,.23f,-72));ApplyCursor();if(qa){StartCoroutine(QA());StartCoroutine(QATimeout());}
 }
 public void SetUiCursor(bool active){UiCursorActive=active;uiCursorChangedFrame=Time.frameCount;ApplyCursor();}
 void ApplyCursor(){bool free=paused||Automated||UiCursorActive||Seated;Cursor.lockState=free?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=free;}
 public void RefreshCursorState(){ApplyCursor();}
 void OnApplicationFocus(bool focus){if(!focus&&!Automated){paused=true;ApplyCursor();}}
 void Update(){
  if(!Automated){
   if(CampusOverlayHub.InputConsumedFrame==Time.frameCount)return;
   // Modal interior menus own movement, mouse look and Escape until closed.
   // Also consume the closing frame so Escape cannot pause the game as well.
   if(UiCursorActive||uiCursorChangedFrame==Time.frameCount){Animate("Idle",0);return;}
   if(Input.GetKeyDown(KeyCode.Escape)){paused=!paused;ApplyCursor();}
   if(paused){Animate("Idle",0);return;}
   // Sitting: pause handles the toggle above, but no movement, gravity or yaw may run.
   if(Seated){Animate("Idle",0);return;}
   if(Interiors&&Interiors.Busy){Animate("Idle",0);return;}

   targetYaw=Mathf.Repeat(targetYaw+Input.GetAxisRaw("Mouse X")*2.3f,360);targetPitch=Mathf.Clamp(targetPitch-Input.GetAxisRaw("Mouse Y")*1.8f,-32,68);
   distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*.5f,2.5f,11);
   yaw=Mathf.LerpAngle(yaw,targetYaw,1-Mathf.Exp(-22*Time.deltaTime));pitch=Mathf.Lerp(pitch,targetPitch,1-Mathf.Exp(-22*Time.deltaTime));
   Vector2 input=new Vector2((Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0),(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0));
   input=Vector2.ClampMagnitude(input,1);float speed=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?10:5.4f;
   Vector3 before=player.position;
   // Movement is camera-relative, but the avatar should face the direction it
   // actually travels. Previously the root stayed locked to the camera yaw,
   // so A/D and diagonal input made the model slide or run at an angle.
   Vector3 cameraForward=Quaternion.Euler(0,yaw,0)*Vector3.forward;
   Vector3 cameraRight=Quaternion.Euler(0,yaw,0)*Vector3.right;
   Vector3 desiredMove=cameraRight*input.x+cameraForward*input.y;
   if(desiredMove.sqrMagnitude>.0001f)
    player.rotation=Quaternion.LookRotation(desiredMove,Vector3.up);
   var movement=desiredMove*speed*Mathf.Min(Time.deltaTime,.06f);
   if(grounded&&Input.GetKeyDown(KeyCode.Space)){vertical=6.5f;grounded=false;}
   if(Interiors&&Interiors.Inside)MoveIndoor(movement,Mathf.Min(Time.deltaTime,.06f));
   else if(Outdoor){var flags=Outdoor.Move(movement,Mathf.Min(Time.deltaTime,.06f),ref vertical,false);grounded=(flags&CollisionFlags.Below)!=0;}
   else {player.position=MapToWorld(CampusCollision.Move(WorldToMap(before),WorldToMap(movement),data.obstacles));
    if(!grounded){vertical-=18*Time.deltaTime;var p=player.position;p.y+=vertical*Time.deltaTime;if(p.y<=.23f){p.y=.23f;grounded=true;vertical=0;}player.position=p;}}
   float actual=Vector2.Distance(new Vector2(before.x,before.z),new Vector2(player.position.x,player.position.z))/Mathf.Max(.001f,Time.deltaTime);
   Animate(!grounded?(vertical>0?"Jump":"Fall"):actual>.15f?(speed>6?"Run":"Walk"):"Idle",actual,input.y<0);
   if(ExteriorShortcutsAllowed){
    if(Input.GetKeyDown(KeyCode.R))Teleport(new Vector3(3,.23f,194));
    if(Input.GetKeyDown(KeyCode.T)){yaw=targetYaw=180;pitch=targetPitch=18;player.rotation=Quaternion.Euler(0,yaw,0);Teleport(new Vector3(65,.23f,-72));Say("教学区：ABC 教学楼与喷泉广场。");}
    if(Input.GetKeyDown(KeyCode.L)){yaw=targetYaw=180;pitch=targetPitch=18;player.rotation=Quaternion.Euler(0,yaw,0);Teleport(Outdoor?new Vector3(209,.23f,38):new Vector3(175.14f,.23f,14));Say("生活区：沿主路向北下坡，可到七舍和公寓花园。");}
    if(Input.GetKeyDown(KeyCode.G))VisitLibrary();}
   if(Input.GetKeyDown(KeyCode.E))Interact();
  }
  if(face!=null&&face.sharedMesh!=null){for(int i=0;i<face.sharedMesh.blendShapeCount;i++){string n=face.sharedMesh.GetBlendShapeName(i);if(n.Contains("Blink"))face.SetBlendShapeWeight(i,Mathf.Pow(Mathf.Max(0,Mathf.Cos(Time.time*1.8f)),35)*100);else if(n.Contains("Smile"))face.SetBlendShapeWeight(i,complete?65:10);}}
  foreach(var item in memories)if(item.activeSelf){item.transform.Rotate(0,Time.deltaTime*32,0);}
 }
 // Shared by normal play and the stair traversal regression check. Only gravity
 // changes vertical velocity; floor transitions never reposition the character.
 public void MoveIndoor(Vector3 horizontalDisplacement,float deltaTime){
  if(!Interiors||!Interiors.Inside)return;
  deltaTime=Mathf.Clamp(deltaTime,0,.06f);horizontalDisplacement.y=0;
  // A long sprint frame must not skip two risers at once. Substeps preserve the
  // real stair colliders and give the capsule consistent low-frame-rate steps.
  int count=Mathf.Max(1,Mathf.CeilToInt(horizontalDisplacement.magnitude/.14f));
  float dt=deltaTime/count;
  for(int i=0;i<count;i++){
   vertical-=18*dt;
   var flags=Interiors.MoveCharacter(horizontalDisplacement/count+Vector3.up*vertical*dt);
   grounded=(flags&CollisionFlags.Below)!=0;
   if(grounded)vertical=-2;
   if((flags&CollisionFlags.Above)!=0&&vertical>0)vertical=0;
  }
 }
 void LateUpdate(){if(!qa)UpdateCamera();}
 void UpdateCamera(){
  // Narrow dormitory halls need a lower target and wider perspective so the
  // existing collision zoom still frames the full avatar. Restore the exact
  // incoming FOV outside this module; movement and orbit controls are unchanged.
  bool dormitoryView=Interiors&&Interiors.State==BuildingInteriorTravel.TravelState.Inside&&DormitoryCorridor.Current(this)!=null;
  if(dormitoryView&&!dormitoryCameraApplied){dormitoryOriginalFieldOfView=followCamera.fieldOfView;dormitoryCameraApplied=true;}
  if(!dormitoryView&&dormitoryCameraApplied){followCamera.fieldOfView=dormitoryOriginalFieldOfView;dormitoryCameraApplied=false;}
  Vector3 pivot=player.position+Vector3.up*(dormitoryView?1.0f:1.5f),back=Quaternion.Euler(pitch,yaw,0)*Vector3.back;
  float d=distance;int mask=Interiors&&Interiors.Inside?Physics.DefaultRaycastLayers:(1<<8)|(Outdoor?((1<<9)|(1<<10)):0);if(Physics.SphereCast(pivot,.2f,back,out RaycastHit hit,d,mask,QueryTriggerInteraction.Ignore))d=Mathf.Max(.6f,hit.distance-.15f);
  if(dormitoryView)followCamera.fieldOfView=Mathf.Lerp(dormitoryOriginalFieldOfView,84f,Mathf.Clamp01((3.2f-d)/1.8f));
  followCamera.transform.position=pivot+back*d;followCamera.transform.rotation=Quaternion.LookRotation(pivot-followCamera.transform.position);
 }
 public void Teleport(Vector3 position){if(!ExteriorShortcutsAllowed)return;if(Outdoor)position.y=CampusPlanTerrain.Height(position.x,position.z);player.position=MapToWorld(position);vertical=0;grounded=true;UpdateCamera();}
 public void VisitDormitory(){if(!ExteriorShortcutsAllowed)return;yaw=targetYaw=180;pitch=targetPitch=12;player.rotation=Quaternion.Euler(0,yaw,0);Teleport(new Vector3(245,.23f,-68));Say("宿舍花园：靠近公寓入口，按 E 进入。");}
 public void VisitCafeteria(){if(!ExteriorShortcutsAllowed)return;yaw=targetYaw=270;pitch=targetPitch=8;player.rotation=Quaternion.Euler(0,yaw,0);Teleport(Outdoor?new Vector3(218,3.08f,10):new Vector3(174.5f,.23f,8));Say("学生食堂：向正门走近，按 E 进入。");}
 // G jumps straight to the library front door, facing the entrance so E works immediately.
 public void VisitLibrary(){if(!ExteriorShortcutsAllowed)return;yaw=targetYaw=270;pitch=targetPitch=8;player.rotation=Quaternion.Euler(0,yaw,0);Teleport(new Vector3(44.6f,.23f,92));Say("图书馆：向正门走近，按 E 进入。");}
 public bool IsMapWalkable(Vector3 position)=>!CampusCollision.Blocked(position.x,position.z,data.obstacles);
 // Shared travel API uses world coordinates indoors and resets movement/camera state.
 // Turn the follow camera to a heading without moving the player. Used so sitting down
 // frames the table instead of keeping the pre-sit yaw.
 public void AlignViewToSeat(float heading){yaw=targetYaw=heading;player.rotation=Quaternion.Euler(0,heading,0);UpdateCamera();}
 public void SetTravelPose(Vector3 worldPosition,float heading,float elevation=12){if(Map&&Map.IsOpen)Map.Close();player.position=worldPosition;yaw=targetYaw=heading;pitch=targetPitch=elevation;player.rotation=Quaternion.Euler(0,yaw,0);vertical=0;grounded=true;Animate("Idle",0);UpdateCamera();}
 public void Notify(string text)=>Say(text);
 void Animate(string state,float speed,bool backwards=false){
  if(characterSelector!=null&&characterSelector.CurrentChoice!=PlayerCharacterChoice.None){characterSelector.SetMotion(speed,backwards?-speed:speed,grounded);return;}
  if(animationPlayer==null)return;
  if(motion!=state&&animationPlayer[state]!=null){animationPlayer.CrossFade(state,.16f);motion=state;}
  if(animationPlayer[state]!=null){animationPlayer[state].speed=(state=="Walk"||state=="Run")?Mathf.Clamp(speed/(state=="Run"?10:5.4f),.35f,1.3f)*(backwards?-1:1):1;}
 }
 void RefreshCharacterVisual(){
  animationPlayer=player.GetComponentInChildren<Animation>(true);
  face=characterSelector!=null&&characterSelector.PrimaryRenderer!=null?characterSelector.PrimaryRenderer:player.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.gameObject.activeInHierarchy)??player.GetComponentInChildren<SkinnedMeshRenderer>(true);
  if(face!=null)face.updateWhenOffscreen=true;
 }
 void OnDestroy(){if(characterSelector!=null)characterSelector.VisualChanged-=RefreshCharacterVisual;}
 public bool Interact(){
  // One dispatcher, one action per press. Inside the cafeteria the ordering system is
  // offered the press first; only the single counter/menu the player stands at answers
  // it, and when it does, nothing else (exit, doors, NPCs) runs on the same press.
  if(Interiors&&Interiors.Inside&&Interiors.Interior&&Interiors.Interior.buildingId=="cafeteria"){
    var ordering=CampusSystemsRegistry.FindOrdering();
    if(ordering){var ui=ordering.GetComponent<CafeteriaOrderingUI>();if(ui&&ui.InteractOnce())return true;}
  }
  if(Interiors&&Interiors.Inside&&Interiors.Interior&&Interiors.Interior.buildingId=="library"){
    var seats=LibrarySeatInteraction.Ensure();if(seats.InteractOnce())return true;
  }
  var semester=CampusSemester.Instance;
  var semesterPoint=CampusSemesterPoint.Nearby(this);
  if(semester&&semester.Ready&&semesterPoint){semester.UI.OpenAt(semesterPoint);return true;}
  var dorm=DormitoryCorridor.Current(this);if(dorm&&dorm.TryInteract())return true;
  if(Interiors&&Interiors.TryInteract())return true;
  if(Story!=null)return Story.TryInteract();
  if(Vector3.Distance(player.position,guide.position)<4){
   if(!started){started=true;Say("小岚：请在图书馆、涌泉湖畔和田径场寻找三枚校园记忆。");}
   else if(collected==7){complete=true;Say("小岚：三枚记忆都找到了！欢迎继续探索我们的校园。");}
   else Say("小岚：沿地图上的金色标记寻找剩余记忆吧。");
   Save();return true;
  }
  for(int i=0;i<memories.Length;i++)if(memories[i].activeSelf&&Vector3.Distance(player.position,memories[i].transform.position)<4){
   if(!started){Say("先到书韵广场找小岚领取探索任务。");return false;}
   collected|=1<<i;memories[i].SetActive(false);Say("已收集："+data.spots[i].area+(collected==7?"。返回小岚处交付任务。":""));Save();return true;
  }
  if(Vector2.Distance(new Vector2(player.position.x*mapXSign,player.position.z),new Vector2(-8,191))<4){Say("校园导览：WASD 移动，Shift 无限奔跑，空格跳跃，M 地图，ESC 暂停。");return true;}
  return false;
 }
 void Save(){if(Automated||Story!=null)return;PlayerPrefs.SetInt("CampusUnityStarted",started?1:0);PlayerPrefs.SetInt("CampusUnityMemories",collected);PlayerPrefs.SetInt("CampusUnityComplete",complete?1:0);PlayerPrefs.Save();}
 void Say(string text){message=text;messageUntil=Time.time+9;}
 int Count(){return (collected&1)+((collected>>1)&1)+((collected>>2)&1);}
 void OnGUI(){
  if(pixelStudy)followCamera.GetComponent<CampusPixelView>()?.DrawBackground();
  if(interfaceFont==null)interfaceFont=Resources.Load<Font>("CampusFont");
  GUI.skin.font=interfaceFont;if(GUI.skin.button!=null)GUI.skin.button.font=interfaceFont;if(GUI.skin.box!=null)GUI.skin.box.font=interfaceFont;if(GUI.skin.label!=null)GUI.skin.label.font=interfaceFont;
  if(title==null||title.font!=interfaceFont){title=new GUIStyle(GUI.skin.label){font=interfaceFont,fontSize=26,fontStyle=FontStyle.Bold};body=new GUIStyle(GUI.skin.label){font=interfaceFont,fontSize=17,wordWrap=true};small=new GUIStyle(body){fontSize=14,font=interfaceFont};}
  float scale=Mathf.Clamp(Mathf.Min(Screen.height/900f,Screen.width/1280f),.7f,1.35f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);float w=Screen.width/scale,h=Screen.height/scale;
  bool indoors=Interiors&&Interiors.Inside;
  string heading=pixelStudy?"风起 · 校园  /  像素试作":"风起 · 校园  /  UNITY";
  string summary=Story!=null?Story.Heading+"\n"+Story.Objective:indoors?Interiors.Current.displayName+"\n"+(Interiors.Interior.isPlaceholder?"室内场景待制作 · 靠近出口按 E 返回":"室内探索 · 靠近出口按 E 返回"):"风起校园\n"+(complete?"探索完成 · 自由漫游":started?"校园记忆  "+Count()+" / 3":"前往书韵广场，与小岚交谈");
  if(CampusSemester.Instance&&Story!=null&&Story.Finished)summary=SemesterRules.Chapter(CampusSemester.Instance.Clock.DayIndex)+"\n"+CampusSemester.Instance.Objective;
  bool readingStory=Story!=null&&Story.DialogueOpen;
  if(!readingStory){
   var info=summary.Split(new[]{'\n'},2);
   CampusStoryStyle.Guidance(interfaceFont,w,info[0],info.Length>1?info[1]:heading,Story!=null?Story.StageIndex:0,Story!=null&&!Story.Finished?Story.Data.stages.Length:0);
  }
  if(pixelStudy)GUI.Label(new Rect(24,h-65,w-48,26),"T 返回教学广场    F6 切换像素大小（标准 / 粗 / 细 / 原生分辨率）",small);
  if(!readingStory)GUI.Label(new Rect(24,h-37,w-48,30),indoors?"WASD 移动    鼠标转向    Shift 奔跑    空格跳跃    E 交互    K 学期手册    P 手机    J 剧情指引    ESC 暂停":"WASD 移动    鼠标转向    Shift 奔跑    空格跳跃    E 交互 / 进入建筑    M 地图    K 学期手册    P 手机    J 指引    T 教学    L 生活区    B 宿舍    F 食堂    G 图书馆    R 南门    ESC 暂停",small);
  if(Time.time<messageUntil&&!readingStory){CampusStoryStyle.Panel(new Rect(w*.5f-330,h-140,660,76));GUI.Label(new Rect(w*.5f-306,h-126,612,52),message,CampusStoryStyle.Label(interfaceFont,17,CampusStoryStyle.Paper,false,TextAnchor.MiddleLeft));}
  // The contextual "[ E ] ..." prompt is drawn once, by CampusHud.
  if(paused&&(!qa||pixelStudy)){
   bool showChoices=characterSelector!=null&&(!characterChoiceConfirmed||choosingCharacter);float panelHeight=showChoices?375:455;float top=h/2-panelHeight/2;
   GUI.Box(new Rect(w/2-230,top,460,panelHeight),"");GUI.Label(new Rect(w/2-185,top+22,400,40),"校园漫游已准备就绪",title);
   if(showChoices){GUI.Label(new Rect(w/2-185,top+70,370,48),characterChoiceConfirmed?"选择要更换的角色：":"首次开始前，请选择角色：",body);if(GUI.Button(new Rect(w/2-175,top+124,168,44),pendingCharacterChoice==PlayerCharacterChoice.Male?"✓ 男生 · 日系校园":"男生 · 日系校园")){if(characterSelector.ApplyChoice(PlayerCharacterChoice.Male,characterChoiceConfirmed)){pendingCharacterChoice=PlayerCharacterChoice.Male;if(characterChoiceConfirmed)choosingCharacter=false;}}if(GUI.Button(new Rect(w/2+7,top+124,168,44),pendingCharacterChoice==PlayerCharacterChoice.Female?"✓ 女生 · 日系校园":"女生 · 日系校园")){if(characterSelector.ApplyChoice(PlayerCharacterChoice.Female,characterChoiceConfirmed)){pendingCharacterChoice=PlayerCharacterChoice.Female;if(characterChoiceConfirmed)choosingCharacter=false;}}}
   else GUI.Label(new Rect(w/2-185,top+70,370,58),"点击开始后，鼠标控制视角。\n按 ESC 可暂停并释放鼠标。",body);
   float startY=showChoices?top+188:top+142;bool canStart=characterSelector==null||characterChoiceConfirmed||pendingCharacterChoice==PlayerCharacterChoice.Male||pendingCharacterChoice==PlayerCharacterChoice.Female;bool oldEnabled=GUI.enabled;GUI.enabled=canStart;if(GUI.Button(new Rect(w/2-175,startY,350,44),"开始 / 继续探索")){if(!characterChoiceConfirmed&&characterSelector!=null){characterChoiceConfirmed=characterSelector.PersistCurrentChoice();if(!characterChoiceConfirmed)return;}choosingCharacter=false;paused=false;PlayerPrefs.SetInt("CampusUnityStarted",1);PlayerPrefs.Save();ApplyCursor();}GUI.enabled=oldEnabled;
   if(characterChoiceConfirmed&&characterSelector!=null&&GUI.Button(new Rect(w/2-175,startY+54,350,34),choosingCharacter?"取消更换角色":"更换角色"))choosingCharacter=!choosingCharacter;
   float resetY=startY+(characterChoiceConfirmed&&characterSelector!=null?98:54);
   var semester=CampusSemester.Instance;
   if(!showChoices&&semester){
    if(GUI.Button(new Rect(w/2-175,resetY,350,34),"课表与学期进度  K"))semester.UI.Open("today");
    if(GUI.Button(new Rect(w/2-175,resetY+44,350,34),"重新开始今天"))semester.UI.RequestReset(false);
    if(GUI.Button(new Rect(w/2-175,resetY+88,350,34),"重新开始本学期 · 9月1日"))semester.UI.RequestReset(true);
    if(GUI.Button(new Rect(w/2-175,resetY+132,350,30),"保存并退出游戏")){semester.Save();Application.Quit();}
   }

  }
 }
 IEnumerator QATimeout(){yield return new WaitForSecondsRealtime(120);Debug.LogError("Campus QA timed out; inspect runtime log.");Application.Quit(1);}
 IEnumerator QA(){
  paused=false;Animate("Idle",0);while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;yield return new WaitForSeconds(3);
   string dir=Path.Combine(Application.dataPath,"../QA");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"runtime-result.txt"),"RUNNING: validation has not completed yet.\\n");
  if(pixelStudy){
   var pixel=followCamera.GetComponent<CampusPixelView>();if(pixel==null)throw new Exception("Pixel renderer missing");
   foreach(int size in new[]{1,2,4,3}){pixel.pixelSize=size;yield return null;yield return null;if(pixel.Image==null||!pixel.Image.IsCreated()||pixel.Image.width!=Mathf.Max(1,Screen.width/size)||pixel.Image.height!=Mathf.Max(1,Screen.height/size)||pixel.Image.filterMode!=FilterMode.Point)throw new Exception("Pixel resize failed: "+size);}
   foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))foreach(var mat in renderer.sharedMaterials)if(mat==null||mat.shader==null||!mat.shader.isSupported)throw new Exception("Unsupported pixel material: "+renderer.name);
  }
  foreach(var route in data.routes){var p=new Vector3(route.points[0].x,.23f,route.points[0].z);for(int i=1;i<route.points.Length;i++){var goal=new Vector3(route.points[i].x,.23f,route.points[i].z);for(int n=0;n<6000&&Vector3.Distance(p,goal)>.05f;n++){var next=CampusCollision.Move(p,Vector3.ClampMagnitude(goal-p,.15f),data.obstacles);if((next-p).sqrMagnitude<.000001f)throw new Exception("Route blocked: "+route.name);p=next;}if(Vector3.Distance(p,goal)>.1f)throw new Exception("Route did not finish: "+route.name);}}
  foreach(var f in data.features.Where(f=>f.kind=="building"||f.kind=="water")){var a=f.points[0];if(!CampusCollision.Blocked(a.x,a.z,data.obstacles))throw new Exception("Missing boundary collision: "+f.name);}
  Teleport(new Vector3(3,.23f,194));for(int angle=-1440;angle<=1440;angle+=15){yaw=angle;player.rotation=Quaternion.Euler(0,yaw,0);UpdateCamera();var back=followCamera.transform.position-player.position;back.y=0;if(Vector3.Dot(back.normalized,player.forward)>-.99f)throw new Exception("Camera left player back");}yaw=180;player.rotation=Quaternion.Euler(0,180,0);
  Teleport(new Vector3(20,.23f,109));if(!Interact()||!started)throw new Exception("Quest start failed");
  for(int i=0;i<data.spots.Length;i++){Teleport(new Vector3(data.spots[i].x,.23f,data.spots[i].z));if(!Interact())throw new Exception("Collect failed");}
  Teleport(new Vector3(20,.23f,109));Interact();if(!complete)throw new Exception("Quest finish failed");
  if(characterSelector!=null&&characterSelector.CurrentChoice!=PlayerCharacterChoice.None){RefreshCharacterVisual();var animator=player.GetComponentInChildren<Animator>(true);if(face==null||animator==null||animator.runtimeAnimatorController==null)throw new Exception("Selected UMA character did not finish building");}
  else if(animationPlayer==null||animationPlayer["Run"]==null||animationPlayer["Walk"]==null)throw new Exception("Character clips missing");
  float characterHeight=face.bounds.size.y;if(characterHeight<1||characterHeight>2.5f)throw new Exception("Incorrect character height: "+characterHeight);
  string[] names={"entrance","teaching-court","lakeside","overview","living-plan","living-courtyard","living-dining"};Vector3[] positions={new Vector3(3,2.5f,200),new Vector3(65,4,-70),new Vector3(-25,3,76),new Vector3(-150,210,255),new Vector3(229,190,-49),new Vector3(246,6,-83),new Vector3(165,7,26)};Vector3[] look={new Vector3(3,1.4f,180),new Vector3(60,3,-97),new Vector3(-39,1,28),new Vector3(20,0,0),new Vector3(229,0,-48.999f),new Vector3(240,5,-120),new Vector3(195,3,11)};
  for(int i=0;i<names.Length;i++){if(i==0)Teleport(new Vector3(3,.23f,194));followCamera.orthographic=names[i]=="living-plan";followCamera.orthographicSize=86;followCamera.transform.position=MapToWorld(positions[i]);followCamera.transform.LookAt(MapToWorld(look[i]));if(names[i]=="living-plan"&&mapXSign<0)followCamera.transform.rotation=Quaternion.Euler(90,180,0);yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(dir,names[i]+".png"));yield return new WaitForSeconds(1);}followCamera.orthographic=false;
  Teleport(new Vector3(3,.23f,194));yaw=180;player.rotation=Quaternion.Euler(0,180,0);UpdateCamera();Animate("Run",10);yield return new WaitForSeconds(.4f);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"running-character.png"));yield return new WaitForSeconds(1);
  var baked=new Mesh();face.BakeMesh(baked);var verts=baked.vertices.Select(v=>face.transform.TransformPoint(v)).ToArray();
  string visual="Player="+player.position+" bounds="+face.bounds+" visible="+face.isVisible+" world mesh Y="+verts.Min(v=>v.y)+","+verts.Max(v=>v.y)+" center="+face.transform.TransformPoint(baked.bounds.center)+" material="+face.sharedMaterial.shader.name+"\\n";Destroy(baked);
  if(pixelStudy){
   Teleport(new Vector3(65,.23f,-72));yaw=180;pitch=18;player.rotation=Quaternion.Euler(0,180,0);Animate("Idle",0);UpdateCamera();yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"pixel-player-court.png"));yield return new WaitForSeconds(1);
   paused=true;yield return new WaitForSeconds(.25f);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"pixel-pause-menu.png"));yield return new WaitForSeconds(1);paused=false;
  }
   File.WriteAllText(Path.Combine(dir,"runtime-result.txt"),"PASS: all mapped quest routes; building/lake collisions; camera rear alignment over 8 full turns; start/collect/finish; character Idle/Walk/Run; screenshots captured.\\n"+(pixelStudy?"PASS: pixel render target sizes 1/2/3/4, point sampling, supported materials; teaching courtyard player view.\\n":"")+"Character height: "+characterHeight+" m\\n"+visual);Application.Quit(0);
 }
}




