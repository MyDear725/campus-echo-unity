using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public class CampusGame : MonoBehaviour {
 public TextAsset mapData;
 public Transform player,guide;
 public Camera followCamera;
 public Font interfaceFont;
 public GameObject[] memories;
 public bool paused=true;
 public bool pixelStudy;
 public float mapXSign=1;
 public Vector3 MapToWorld(Vector3 point)=>new Vector3(point.x*mapXSign,point.y,point.z);
 public Vector3 WorldToMap(Vector3 point)=>MapToWorld(point);
 CampusData data; float yaw=180,pitch=18,targetYaw=180,targetPitch=18,distance=5.8f,vertical;
 bool grounded=true,started,complete,mapOpen,qa; int collected; string message="欢迎来到开发区校区。沿路到书韵广场寻找小岚。";
 float messageUntil=10; Animation animationPlayer; SkinnedMeshRenderer face; string motion=""; GUIStyle title,body,small; Texture2D minimap;
 void Awake(){
  data=JsonUtility.FromJson<CampusData>(mapData.text);qa=Environment.GetCommandLineArgs().Contains("-campusQA");
  Application.targetFrameRate=60;
  animationPlayer=player.GetComponentInChildren<Animation>();face=player.GetComponentInChildren<SkinnedMeshRenderer>();
  foreach(var actor in new[]{player,guide})foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
  if(!qa){started=PlayerPrefs.GetInt("CampusUnityStarted",0)==1;collected=PlayerPrefs.GetInt("CampusUnityMemories",0)&7;complete=started&&collected==7&&PlayerPrefs.GetInt("CampusUnityComplete",0)==1;}
  if(!started){collected=0;complete=false;}
  for(int i=0;i<memories.Length;i++)memories[i].SetActive((collected&(1<<i))==0);
  if(interfaceFont==null)interfaceFont=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",20);
  BuildMap();if(pixelStudy&&!qa)Teleport(new Vector3(65,.23f,-72));ApplyCursor();if(qa){StartCoroutine(QA());StartCoroutine(QATimeout());}
 }
 void ApplyCursor(){Cursor.lockState=paused||qa?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=paused||qa;}
 void OnApplicationFocus(bool focus){if(!focus&&!qa){paused=true;ApplyCursor();}}
 void Update(){
  if(!qa){
   if(Input.GetKeyDown(KeyCode.Escape)){paused=!paused;ApplyCursor();}
   if(paused){Animate("Idle",0);return;}
   if(Input.GetKeyDown(KeyCode.M))mapOpen=!mapOpen;
   targetYaw=Mathf.Repeat(targetYaw+Input.GetAxisRaw("Mouse X")*2.3f,360);targetPitch=Mathf.Clamp(targetPitch-Input.GetAxisRaw("Mouse Y")*1.8f,-32,68);
   distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*.5f,2.5f,11);
   yaw=Mathf.LerpAngle(yaw,targetYaw,1-Mathf.Exp(-22*Time.deltaTime));pitch=Mathf.Lerp(pitch,targetPitch,1-Mathf.Exp(-22*Time.deltaTime));
   player.rotation=Quaternion.Euler(0,yaw,0);
   Vector2 input=new Vector2((Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)?1:0),(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0)-(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)?1:0));
   input=Vector2.ClampMagnitude(input,1);float speed=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?10:5.4f;
   Vector3 before=player.position;player.position=MapToWorld(CampusCollision.Move(WorldToMap(before),WorldToMap((player.right*input.x+player.forward*input.y)*speed*Mathf.Min(Time.deltaTime,.06f)),data.obstacles));
   float actual=Vector3.Distance(before,player.position)/Mathf.Max(.001f,Time.deltaTime);
   if(grounded&&Input.GetKeyDown(KeyCode.Space)){vertical=6.5f;grounded=false;}
   if(!grounded){vertical-=18*Time.deltaTime;var p=player.position;p.y+=vertical*Time.deltaTime;if(p.y<=.23f){p.y=.23f;grounded=true;vertical=0;}player.position=p;}
   Animate(!grounded?(vertical>0?"Jump":"Fall"):actual>.15f?(speed>6?"Run":"Walk"):"Idle",actual,input.y<0);
   if(Input.GetKeyDown(KeyCode.R))Teleport(new Vector3(3,.23f,194));
   if(Input.GetKeyDown(KeyCode.T)){yaw=targetYaw=180;pitch=targetPitch=18;player.rotation=Quaternion.Euler(0,yaw,0);Teleport(new Vector3(65,.23f,-72));Say("教学区：ABC 教学楼与喷泉广场。");}
   if(Input.GetKeyDown(KeyCode.L)){yaw=targetYaw=180;pitch=targetPitch=18;player.rotation=Quaternion.Euler(0,yaw,0);Teleport(new Vector3(175.14f,.23f,14));Say("生活区西1门：沿西侧主路向北，可到恩苑和公寓中庭。");}
   if(Input.GetKeyDown(KeyCode.E))Interact();
  }
  if(face!=null){for(int i=0;i<face.sharedMesh.blendShapeCount;i++){string n=face.sharedMesh.GetBlendShapeName(i);if(n.Contains("Blink"))face.SetBlendShapeWeight(i,Mathf.Pow(Mathf.Max(0,Mathf.Cos(Time.time*1.8f)),35)*100);else if(n.Contains("Smile"))face.SetBlendShapeWeight(i,complete?65:10);}}
  foreach(var item in memories)if(item.activeSelf){item.transform.Rotate(0,Time.deltaTime*32,0);}
 }
 void LateUpdate(){if(!qa)UpdateCamera();}
 void UpdateCamera(){
  Vector3 pivot=player.position+Vector3.up*1.5f,back=Quaternion.Euler(pitch,yaw,0)*Vector3.back;
  float d=distance;if(Physics.SphereCast(pivot,.2f,back,out RaycastHit hit,d,1<<8,QueryTriggerInteraction.Ignore))d=Mathf.Max(.6f,hit.distance-.15f);
  followCamera.transform.position=pivot+back*d;followCamera.transform.rotation=Quaternion.LookRotation(pivot-followCamera.transform.position);
 }
 public void Teleport(Vector3 position){player.position=MapToWorld(position);vertical=0;grounded=true;UpdateCamera();}
 void Animate(string state,float speed,bool backwards=false){
  if(animationPlayer==null)return;
  if(motion!=state&&animationPlayer[state]!=null){animationPlayer.CrossFade(state,.16f);motion=state;}
  if(animationPlayer[state]!=null){animationPlayer[state].speed=(state=="Walk"||state=="Run")?Mathf.Clamp(speed/(state=="Run"?10:5.4f),.35f,1.3f)*(backwards?-1:1):1;}
 }
 public bool Interact(){
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
 void Save(){if(qa)return;PlayerPrefs.SetInt("CampusUnityStarted",started?1:0);PlayerPrefs.SetInt("CampusUnityMemories",collected);PlayerPrefs.SetInt("CampusUnityComplete",complete?1:0);PlayerPrefs.Save();}
 void Say(string text){message=text;messageUntil=Time.time+9;}
 int Count(){return (collected&1)+((collected>>1)&1)+((collected>>2)&1);}
 void OnGUI(){
  if(pixelStudy)followCamera.GetComponent<CampusPixelView>()?.DrawBackground();
  GUI.skin.font=interfaceFont;
  if(title==null){title=new GUIStyle(GUI.skin.label){font=interfaceFont,fontSize=26,fontStyle=FontStyle.Bold};body=new GUIStyle(GUI.skin.label){font=interfaceFont,fontSize=17,wordWrap=true};small=new GUIStyle(body){fontSize=14};}
  float scale=Mathf.Clamp(Screen.height/900f,.7f,1.5f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);float w=Screen.width/scale,h=Screen.height/scale;
  GUI.Box(new Rect(20,20,350,125),"");GUI.Label(new Rect(38,32,320,36),pixelStudy?"风起 · 校园  /  像素试作":"风起 · 校园  /  UNITY",title);GUI.Label(new Rect(38,73,315,65),"大连理工大学 · 开发区校区\n"+(complete?"探索完成 · 自由漫游":started?"校园记忆  "+Count()+" / 3":"前往书韵广场，与小岚交谈"),body);
  if(pixelStudy)GUI.Label(new Rect(24,h-65,w-48,26),"T 返回教学广场    F6 切换像素大小（标准 / 粗 / 细 / 原生分辨率）",small);
  GUI.Label(new Rect(24,h-37,w-48,30),"WASD 移动    鼠标转向    Shift 奔跑    空格跳跃    E 交互    M 地图    L 生活区    ESC 暂停",small);
  if(Time.time<messageUntil){GUI.Box(new Rect(w*.5f-330,h-130,660,72),"");GUI.Label(new Rect(w*.5f-312,h-119,624,60),message,body);}
  bool near=Vector3.Distance(player.position,guide.position)<4||memories.Any(m=>m.activeSelf&&Vector3.Distance(player.position,m.transform.position)<4);
  if(near&&!paused)GUI.Label(new Rect(w*.5f-75,h*.64f,180,35),"[ E ]  交谈 / 收集",body);
  if(mapOpen){float s=Mathf.Min(h-190,620);var r=new Rect(w-s-25,25,s,s*.692f);GUI.DrawTexture(r,minimap);foreach(var spot in data.spots)Dot(r,spot.x,spot.z,new Color(1,.76f,.3f));Dot(r,20,109,Color.cyan);Dot(r,player.position.x*mapXSign,player.position.z,Color.white);GUI.Label(new Rect(r.x,r.yMax+8,s,35),"北 ↑   白：你   蓝：小岚   金：记忆",small);}
  if(paused&&(!qa||pixelStudy)){GUI.Box(new Rect(w/2-230,h/2-135,460,265),"");GUI.Label(new Rect(w/2-185,h/2-113,400,40),"校园漫游已准备就绪",title);GUI.Label(new Rect(w/2-185,h/2-60,370,65),"点击开始后，鼠标控制视角。\n按 ESC 可暂停并释放鼠标。",body);if(GUI.Button(new Rect(w/2-175,h/2+18,350,44),"开始 / 继续探索")){paused=false;ApplyCursor();}if(GUI.Button(new Rect(w/2-175,h/2+72,350,30),"重置探索任务")){started=false;complete=false;collected=0;foreach(var m in memories)m.SetActive(true);Save();}}
 }
 void Dot(Rect r,float x,float z,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(r.x+(x+290)/600*r.width-3,r.y+(z+180)/415*r.height-3,6,6),Texture2D.whiteTexture);GUI.color=old;}
 void BuildMap(){
  minimap=new Texture2D(600,415);var pixels=Enumerable.Repeat(new Color(.21f,.3f,.24f),600*415).ToArray();
  foreach(var f in data.features){Color c=f.kind=="water"?new Color(.18f,.44f,.49f):f.kind=="building"?new Color(.69f,.7f,.65f):new Color(.36f,.47f,.35f);
   int x0=Mathf.Clamp(Mathf.FloorToInt(f.points.Min(p=>p.x)+290),0,599),x1=Mathf.Clamp(Mathf.CeilToInt(f.points.Max(p=>p.x)+290),0,599),z0=Mathf.Clamp(Mathf.FloorToInt(f.points.Min(p=>p.z)+180),0,414),z1=Mathf.Clamp(Mathf.CeilToInt(f.points.Max(p=>p.z)+180),0,414);
   for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++)if(CampusCollision.Inside(x-290,z-180,f.points))pixels[(414-z)*600+x]=c;
  }
  foreach(var road in data.roads)for(int i=1;i<road.points.Length;i++){var a=road.points[i-1];var b=road.points[i];int steps=Mathf.CeilToInt(Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))*2);for(int j=0;j<=steps;j++){int x=Mathf.RoundToInt(Mathf.Lerp(a.x,b.x,j/(float)steps)+290),z=Mathf.RoundToInt(Mathf.Lerp(a.z,b.z,j/(float)steps)+180);for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)if(x+dx>=0&&x+dx<600&&z+dz>=0&&z+dz<415)pixels[(414-z-dz)*600+x+dx]=new Color(.68f,.64f,.5f);}}
  minimap.SetPixels(pixels);minimap.Apply();
 }
 IEnumerator QATimeout(){yield return new WaitForSecondsRealtime(120);Debug.LogError("Campus QA timed out; inspect runtime log.");Application.Quit(1);}
 IEnumerator QA(){
  paused=false;Animate("Idle",0);while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;yield return new WaitForSeconds(3);
  string dir=Path.Combine(Application.dataPath,"../QA");Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"runtime-result.txt"),"RUNNING: validation has not completed yet.\n");
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
  if(animationPlayer==null||animationPlayer["Run"]==null||animationPlayer["Walk"]==null)throw new Exception("Character clips missing");
  float characterHeight=face.bounds.size.y;if(characterHeight<1||characterHeight>2.5f)throw new Exception("Incorrect character height: "+characterHeight);
  string[] names={"entrance","teaching-court","lakeside","overview","living-plan","living-courtyard","living-dining"};Vector3[] positions={new Vector3(3,2.5f,200),new Vector3(65,4,-70),new Vector3(-25,3,76),new Vector3(-150,210,255),new Vector3(229,190,-49),new Vector3(246,6,-83),new Vector3(165,7,26)};Vector3[] look={new Vector3(3,1.4f,180),new Vector3(60,3,-97),new Vector3(-39,1,28),new Vector3(20,0,0),new Vector3(229,0,-48.999f),new Vector3(240,5,-120),new Vector3(195,3,11)};
  for(int i=0;i<names.Length;i++){if(i==0)Teleport(new Vector3(3,.23f,194));followCamera.orthographic=names[i]=="living-plan";followCamera.orthographicSize=86;followCamera.transform.position=MapToWorld(positions[i]);followCamera.transform.LookAt(MapToWorld(look[i]));if(names[i]=="living-plan"&&mapXSign<0)followCamera.transform.rotation=Quaternion.Euler(90,180,0);yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(dir,names[i]+".png"));yield return new WaitForSeconds(1);}followCamera.orthographic=false;
  Teleport(new Vector3(3,.23f,194));yaw=180;player.rotation=Quaternion.Euler(0,180,0);UpdateCamera();Animate("Run",10);yield return new WaitForSeconds(.4f);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"running-character.png"));yield return new WaitForSeconds(1);
  var baked=new Mesh();face.BakeMesh(baked);var verts=baked.vertices.Select(v=>face.transform.TransformPoint(v)).ToArray();
  string visual="Player="+player.position+" bounds="+face.bounds+" visible="+face.isVisible+" world mesh Y="+verts.Min(v=>v.y)+","+verts.Max(v=>v.y)+" center="+face.transform.TransformPoint(baked.bounds.center)+" material="+face.sharedMaterial.shader.name+"\n";Destroy(baked);
  if(pixelStudy){
   Teleport(new Vector3(65,.23f,-72));yaw=180;pitch=18;player.rotation=Quaternion.Euler(0,180,0);Animate("Idle",0);UpdateCamera();yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"pixel-player-court.png"));yield return new WaitForSeconds(1);
   paused=true;yield return new WaitForSeconds(.25f);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"pixel-pause-menu.png"));yield return new WaitForSeconds(1);paused=false;
  }
  File.WriteAllText(Path.Combine(dir,"runtime-result.txt"),"PASS: all mapped quest routes; building/lake collisions; camera rear alignment over 8 full turns; start/collect/finish; character Idle/Walk/Run; screenshots captured.\n"+(pixelStudy?"PASS: pixel render target sizes 1/2/3/4, point sampling, supported materials; teaching courtyard player view.\n":"")+"Character height: "+characterHeight+" m\n"+visual);Application.Quit(0);
 }
}
