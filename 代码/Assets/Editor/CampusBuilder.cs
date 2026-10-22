using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor.Build.Reporting;

[Serializable] class ExportTexture {public int id,width,height;public float[] repeat;public bool clamp;}
[Serializable] class ExportMaterial {public int id,map;public string name;public float[] color;public float roughness,metallic,cutoff;public bool transparent,doubleSided,water;}
[Serializable] class ExportMesh {public int id,material,vertices;public string name;public bool shadow;}
[Serializable] class ExportData {public ExportTexture[] textures;public ExportMaterial[] materials;public ExportMesh[] meshes;}

public static class CampusBuilder {
 static string Generated="Assets/CampusGenerated";
 static string ScenePath="Assets/Scenes/Campus.unity";
 static void ApplyMapHandedness(CampusGame game){
  if(game.mapXSign<0)return;
  // Three.js uses a right-handed map (east +X, south +Z). Reflect X once for
  // Unity so east remains on the right when looking north, including input.
  GameObject.Find("校园环境").transform.localScale=new Vector3(-1,1,1);
  foreach(Transform wall in GameObject.Find("Camera collision surfaces").transform){var p=wall.position;p.x=-p.x;wall.position=p;wall.rotation=Quaternion.Euler(0,-wall.eulerAngles.y,0);}
  foreach(var actor in new[]{game.player,game.guide,game.followCamera.transform}.Concat(game.memories.Select(m=>m.transform))){var p=actor.position;p.x=-p.x;actor.position=p;}
  game.mapXSign=-1;
 }
 public static void CorrectLivingHandednessAndBuild(){
  ScenePath="Assets/Scenes/CampusLiving.unity";EditorSceneManager.OpenScene(ScenePath);var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();ApplyMapHandedness(game);EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);Build();
 }
 [MenuItem("Campus/Rebuild Living Scene and Windows Demo")]
 public static void GenerateLivingStandalone(){
  Generated="Assets/LivingRevision";ScenePath="Assets/Scenes/CampusLiving.unity";GenerateInternal();Build();Debug.Log("LIVING_REVISION_COMPLETE");
 }
 public static void FixVisualAndBuild(){
  foreach(var path in Directory.GetFiles(Generated+"/Materials","Character-*.mat")){var material=AssetDatabase.LoadAssetAtPath<Material>(path);material.SetFloat("_Cull",0);EditorUtility.SetDirty(material);}AssetDatabase.SaveAssets();Build();
 }
 public static void DiagnoseVisual(){
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/CampusTraveler.fbx");var model=UnityEngine.Object.Instantiate(prefab);
  var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();var report="";
  foreach(var clip in AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/CampusTraveler.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__"))){clip.SampleAnimation(model,.4f);var baked=new Mesh();skin.BakeMesh(baked);report+=clip.name+" bounds="+skin.bounds+" mesh="+baked.bounds+" position="+skin.transform.position+" scale="+skin.transform.lossyScale+"\n";UnityEngine.Object.DestroyImmediate(baked);}
  UnityEngine.Object.DestroyImmediate(model);EditorSceneManager.OpenScene("Assets/Scenes/Campus.unity");var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();model=game.player.GetComponentInChildren<Animation>().gameObject;skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
  report+="SCENE player="+game.player.position+" model="+model.transform.position+" scale="+model.transform.lossyScale+" skin="+skin.bounds+" enabled="+skin.enabled+" active="+skin.gameObject.activeInHierarchy+"\n";
  foreach(var clip in AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/CampusTraveler.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__"))){clip.SampleAnimation(model,.4f);var baked=new Mesh();skin.BakeMesh(baked);var verts=baked.vertices.Select(v=>skin.transform.TransformPoint(v)).ToArray();report+=clip.name+" worldX="+verts.Min(v=>v.x)+","+verts.Max(v=>v.x)+" worldY="+verts.Min(v=>v.y)+","+verts.Max(v=>v.y)+" worldZ="+verts.Min(v=>v.z)+","+verts.Max(v=>v.z)+"\n";UnityEngine.Object.DestroyImmediate(baked);}
  report+=string.Join("\n",skin.sharedMaterials.Select(m=>m.name+" shader="+m.shader.name+" color="+m.color));File.WriteAllText("QA/character-visual.txt",report);
 }
 public static void OpenProject(){
  EditorSceneManager.OpenScene(File.Exists("Assets/Scenes/CampusLiving.unity")?"Assets/Scenes/CampusLiving.unity":"Assets/Scenes/Campus.unity");
  var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();
  if(game!=null){Selection.activeGameObject=game.player.gameObject;var view=SceneView.lastActiveSceneView;if(view!=null){bool living=File.Exists("Assets/Scenes/CampusLiving.unity");view.LookAt(game.MapToWorld(living?new Vector3(230,0,-55):new Vector3(60,0,-89)),Quaternion.Euler(living?55:35,180,0),living?120:70);}}
 }
 public static void InspectCharacter(){
  EditorSceneManager.OpenScene("Assets/Scenes/Campus.unity");var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();
  var animation=game.player.GetComponentInChildren<Animation>();var face=game.player.GetComponentInChildren<SkinnedMeshRenderer>();
  var report="";
  foreach(var name in new[]{"Idle","Walk","Run"}){animation[name].clip.SampleAnimation(animation.gameObject,.4f);var baked=new Mesh();face.BakeMesh(baked);report+=name+": renderer="+face.bounds.size+" baked="+baked.bounds.size+" rendererScale="+face.transform.lossyScale+" root="+animation.transform.localScale+"\n";UnityEngine.Object.DestroyImmediate(baked);}
  File.WriteAllText("QA/character-inspection.txt",report);Debug.Log(report);
 }
 public static void RepairCharacterAndBuild(){
  var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/Characters/CampusTraveler.fbx");importer.globalScale=1f;importer.SaveAndReimport();
  var probe=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/CampusTraveler.fbx"));
  var skin=probe.GetComponentInChildren<SkinnedMeshRenderer>();Debug.Log("CHARACTER_CORRECTED_HEIGHT="+skin.bounds.size.y);if(skin.bounds.size.y<1||skin.bounds.size.y>2.5f)throw new Exception("Character scale repair failed");UnityEngine.Object.DestroyImmediate(probe);Build();
 }
 [MenuItem("Campus/Rebuild campus scene")]
 public static void Generate(){
  try{GenerateInternal();}catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);throw;}
 }
 static void GenerateInternal(){
  string root=Directory.GetParent(Application.dataPath).FullName,source=Path.Combine(root,"TempMigration");
  var json=File.ReadAllText(Path.Combine(source,"campus.json"));var export=JsonUtility.FromJson<ExportData>(json);var data=JsonUtility.FromJson<CampusData>(json);
  foreach(var folder in new[]{Generated,Generated+"/Meshes",Generated+"/Materials",Generated+"/Textures","Assets/Scenes","Assets/Settings"})Directory.CreateDirectory(folder);
  AssetDatabase.Refresh();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  // Unity's own URP renderer and pipeline assets, using the installed editor packages.
  var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();renderer.name="Campus Renderer";renderer.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
  AssetDatabase.CreateAsset(renderer,"Assets/Settings/CampusRenderer.asset");var pipeline=UniversalRenderPipelineAsset.Create(renderer);pipeline.name="Campus URP";pipeline.shadowDistance=95;pipeline.msaaSampleCount=2;pipeline.supportsHDR=true;pipeline.renderScale=1;pipeline.mainLightShadowmapResolution=2048;
  AssetDatabase.CreateAsset(pipeline,"Assets/Settings/CampusURP.asset");GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=1;QualitySettings.shadows=UnityEngine.ShadowQuality.All;
  PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.productName="Campus Echo";PlayerSettings.companyName="Campus Studio";PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
  PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
  var textures=new Texture2D[export.textures.Length];
  foreach(var t in export.textures){var texture=new Texture2D(t.width,t.height,TextureFormat.RGBA32,true,false);texture.name="CampusTexture"+t.id;texture.SetPixelData<byte>(File.ReadAllBytes(Path.Combine(source,$"texture-{t.id}.rgba")),0);texture.Apply(true,false);texture.wrapMode=t.clamp?TextureWrapMode.Clamp:TextureWrapMode.Repeat;texture.anisoLevel=4;AssetDatabase.CreateAsset(texture,$"{Generated}/Textures/{t.id}.asset");textures[t.id]=texture;}
  var materials=new Material[export.materials.Length];
  foreach(var m in export.materials){var mat=new Material(Shader.Find("Campus/Surface"));mat.name=m.name;mat.SetColor("_BaseColor",new Color(m.color[0],m.color[1],m.color[2],m.color[3]));mat.SetFloat("_Smoothness",1-m.roughness);mat.SetFloat("_Cutoff",m.cutoff);mat.SetFloat("_Water",m.water?1:0);mat.SetFloat("_Cull",0);mat.enableInstancing=true;
   if(m.map>=0){mat.SetTexture("_BaseMap",textures[m.map]);var t=export.textures[m.map];mat.SetTextureScale("_BaseMap",new Vector2(t.repeat[0],t.repeat[1]));}
   if(m.cutoff>0){mat.EnableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=2450;}
   if(m.transparent){mat.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);mat.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);mat.SetInt("_ZWrite",0);mat.renderQueue=3000;mat.SetOverrideTag("RenderType","Transparent");}
   AssetDatabase.CreateAsset(mat,$"{Generated}/Materials/{m.id}.mat");materials[m.id]=mat;
  }
  var campus=new GameObject("校园环境");
  var previousSerialization=EditorSettings.serializationMode;EditorSettings.serializationMode=SerializationMode.ForceBinary;
  AssetDatabase.StartAssetEditing();
  try{foreach(var item in export.meshes){
   using var stream=new BinaryReader(File.OpenRead(Path.Combine(source,$"mesh-{item.id}.bin")));int n=stream.ReadInt32();var vertices=new Vector3[n];var normals=new Vector3[n];var uv=new Vector2[n];var colors=new Color[n];var indices=new int[n];
   for(int i=0;i<n;i++){vertices[i]=new Vector3(stream.ReadSingle(),stream.ReadSingle(),stream.ReadSingle());normals[i]=new Vector3(stream.ReadSingle(),stream.ReadSingle(),stream.ReadSingle());uv[i]=new Vector2(stream.ReadSingle(),stream.ReadSingle());colors[i]=new Color(stream.ReadSingle(),stream.ReadSingle(),stream.ReadSingle(),stream.ReadSingle());indices[i]=i;}
   var mesh=new Mesh{name=item.name,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.colors=colors;mesh.SetIndices(indices,MeshTopology.Triangles,0);mesh.RecalculateBounds();
   AssetDatabase.CreateAsset(mesh,$"{Generated}/Meshes/{item.id}.asset");var obj=new GameObject(item.name);obj.transform.parent=campus.transform;obj.AddComponent<MeshFilter>().sharedMesh=mesh;var r=obj.AddComponent<MeshRenderer>();r.sharedMaterial=materials[item.material];r.shadowCastingMode=item.shadow?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=true;
   if(item.name.Contains("garden-plants"))materials[item.material].SetFloat("_Wind",1);
  }}finally{AssetDatabase.StopAssetEditing();EditorSettings.serializationMode=previousSerialization;}
  var collisions=new GameObject("Camera collision surfaces");
  foreach(var f in data.features.Where(f=>f.kind=="building"))for(int i=0;i<f.points.Length;i++){var a=f.points[i];var b=f.points[(i+1)%f.points.Length];float length=Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));var c=new GameObject(f.name+" wall");c.transform.parent=collisions.transform;c.layer=8;c.transform.position=new Vector3((a.x+b.x)*.5f,7,(a.z+b.z)*.5f);c.transform.rotation=Quaternion.Euler(0,-Mathf.Atan2(b.z-a.z,b.x-a.x)*Mathf.Rad2Deg,0);c.AddComponent<BoxCollider>().size=new Vector3(length,14,.25f);}
  var ground=new GameObject("Camera ground collision");ground.layer=8;ground.transform.position=new Vector3(0,-.25f,0);ground.AddComponent<BoxCollider>().size=new Vector3(2400,.3f,2200);
  var sun=new GameObject("Sun · late morning").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.95f,.84f);sun.intensity=1.4f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);RenderSettings.sun=sun;
  var sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetFloat("_AtmosphereThickness",.9f);sky.SetColor("_SkyTint",new Color(.49f,.55f,.61f));sky.SetFloat("_Exposure",1.1f);AssetDatabase.CreateAsset(sky,"Assets/Settings/CampusSky.mat");RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.52f,.65f,.77f);RenderSettings.ambientEquatorColor=new Color(.36f,.39f,.34f);RenderSettings.ambientGroundColor=new Color(.18f,.2f,.17f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.68f,.77f,.8f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0012f;
  var camera=new GameObject("Player Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.08f;camera.farClipPlane=1800;camera.fieldOfView=58;camera.gameObject.AddComponent<AudioListener>();camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/CampusTraveler.fbx");if(prefab==null)throw new Exception("Character FBX failed to import");
  // Remap imported Blender material colors to native URP/Lit materials.
  var imported=AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/CampusTraveler.fbx").OfType<Material>().ToArray();
  foreach(var mat in imported){Color color=mat.HasProperty("_Color")?mat.color:Color.white;var replacement=new Material(Shader.Find("Universal Render Pipeline/Lit"));replacement.name=mat.name;replacement.SetColor("_BaseColor",color);replacement.SetFloat("_Smoothness",.25f);AssetDatabase.CreateAsset(replacement,Generated+"/Materials/Character-"+mat.name+".mat");}
  Transform SpawnCharacter(string name,Vector3 at){var rootObj=new GameObject(name);rootObj.transform.position=at;var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);model.name="Campus Traveler";model.transform.SetParent(rootObj.transform,false);
   foreach(var r in model.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>(Generated+"/Materials/Character-"+m.name+".mat")??m).ToArray();
   var animation=model.GetComponent<Animation>()??model.AddComponent<Animation>();foreach(var clip in AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/CampusTraveler.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__"))){foreach(var n in new[]{"Idle","Walk","Run","Jump","Fall","Land","Tired","Talk"})if(clip.name.Contains(n)){animation.AddClip(clip,n);animation[n].wrapMode=n=="Jump"||n=="Land"?WrapMode.ClampForever:WrapMode.Loop;break;}}if(animation["Idle"]!=null){animation.clip=animation["Idle"].clip;animation.playAutomatically=true;}return rootObj.transform;
  }
  var player=SpawnCharacter("Player · 校园旅人",new Vector3(3,.23f,194));player.rotation=Quaternion.Euler(0,180,0);var guide=SpawnCharacter("小岚 · 书韵广场向导",new Vector3(20,.23f,109));
  var items=new GameObject[3];var gold=new Material(Shader.Find("Universal Render Pipeline/Lit"));gold.SetColor("_BaseColor",new Color(1,.64f,.18f));gold.EnableKeyword("_EMISSION");gold.SetColor("_EmissionColor",new Color(.6f,.27f,.04f));AssetDatabase.CreateAsset(gold,Generated+"/Materials/Memory.mat");
  for(int i=0;i<3;i++){var s=data.spots[i];var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.name=s.area+" · 记忆";item.transform.position=new Vector3(s.x,1.35f,s.z);item.transform.localScale=Vector3.one*.5f;item.transform.rotation=Quaternion.Euler(35,45,20);item.GetComponent<Renderer>().sharedMaterial=gold;UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());items[i]=item;}
  var runtime=new GameObject("Campus Game").AddComponent<CampusGame>();File.WriteAllText(Generated+"/CampusMap.json",json);AssetDatabase.ImportAsset(Generated+"/CampusMap.json");runtime.mapData=AssetDatabase.LoadAssetAtPath<TextAsset>(Generated+"/CampusMap.json");runtime.player=player;runtime.guide=guide;runtime.followCamera=camera;runtime.memories=items;PlayerCharacterSelectionInstaller.ConfigureScene(runtime);
  camera.transform.position=new Vector3(3,3.5f,200);camera.transform.LookAt(player.position+Vector3.up*1.4f);
  var clipsPresent=AssetDatabase.LoadAllAssetsAtPath("Assets/Characters/CampusTraveler.fbx").OfType<AnimationClip>().Select(c=>c.name).ToArray();
  if(!clipsPresent.Any(n=>n.Contains("Run"))||!clipsPresent.Any(n=>n.Contains("Walk")))throw new Exception("Missing character animation clips: "+string.Join(",",clipsPresent));
  ApplyMapHandedness(runtime);EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};AssetDatabase.SaveAssets();
  Directory.CreateDirectory(Path.Combine(root,"QA"));File.WriteAllText(Path.Combine(root,"QA/import-result.json"),JsonUtility.ToJson(new ImportReport{meshes=export.meshes.Length,features=data.features.Length,obstacles=data.obstacles.Length,animations=clipsPresent},true));
  Debug.Log("CAMPUS_IMPORT_COMPLETE");
 }
 [Serializable]class ImportReport{public int meshes,features,obstacles;public string[] animations;}
 [MenuItem("Campus/Build Windows Demo")]
 public static void Build(){
  string root=Directory.GetParent(Application.dataPath).FullName;Directory.CreateDirectory(Path.Combine(root,"Builds/Windows"));
  var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
  var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=Path.Combine(root,"Builds/Windows/CampusEcho.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
  if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+result.summary.result);Debug.Log("CAMPUS_BUILD_COMPLETE");
 }
}


