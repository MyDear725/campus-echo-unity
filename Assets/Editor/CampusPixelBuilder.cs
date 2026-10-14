using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class CampusPixelBuilder {
 const string Root="Assets/PixelStudy",Scene="Assets/Scenes/CampusPixel.unity";
 static readonly Dictionary<Material,Material> materials=new();
 static readonly Dictionary<Texture,Texture2D> textures=new();
 static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var c);return c.linear;}
 [MenuItem("Campus/Pixel Study/Create and Build")]
 public static void CreateAndBuild(){
  EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
  Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Textures");AssetDatabase.Refresh();materials.Clear();textures.Clear();
  var shader=Shader.Find("Campus/Pixel Surface");if(shader==null)throw new Exception("Pixel shader not imported");
  var renderers=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
  foreach(var renderer in renderers)renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Convert(m,shader)).ToArray();
  var sky=new Material(Shader.Find("Campus/Pixel Sky"));Save(sky,Root+"/Sky.mat");RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Sky.mat");
  RenderSettings.fogColor=Hex("B0D6D1");RenderSettings.ambientMode=AmbientMode.Trilight;
  RenderSettings.ambientSkyColor=Hex("BBD6DD");RenderSettings.ambientEquatorColor=Hex("ADBC9E");RenderSettings.ambientGroundColor=Hex("767E76");
  if(RenderSettings.sun!=null){RenderSettings.sun.color=Hex("FFF1D0");RenderSettings.sun.intensity=1;RenderSettings.sun.transform.rotation=Quaternion.Euler(48,-35,0);}
  var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();game.pixelStudy=true;
  game.player.position=game.MapToWorld(new Vector3(65,.23f,-72));
  var camera=game.followCamera;camera.transform.position=game.MapToWorld(new Vector3(65,3.5f,-66));camera.transform.LookAt(game.player.position+Vector3.up*1.5f);
  var pixel=camera.gameObject.AddComponent<CampusPixelView>();pixel.worldCamera=camera;pixel.pixelSize=3;
  // Keep the unlabelled western dorm neutral in this revision as requested.
  string map=game.mapData.text.Replace("第三学生公寓","西侧长条楼（名称待确认）");
  File.WriteAllText(Root+"/CampusMap.json",map);AssetDatabase.ImportAsset(Root+"/CampusMap.json");game.mapData=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/CampusMap.json");
  foreach(var obj in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))if(obj.name.Contains("第三学生公寓"))obj.name=obj.name.Replace("第三学生公寓","西侧长条楼（名称待确认）");
  AddFlowerBorders(game,shader);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);AssetDatabase.SaveAssets();
  var errors=UnityEditor.ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
  if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.message)));
  Build();
 }
 static void Save(UnityEngine.Object value,string path){
  var old=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
  if(old==null)AssetDatabase.CreateAsset(value,path);else{EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);}
 }
 static Material Convert(Material source,Shader shader){
  if(source==null)return null;if(materials.TryGetValue(source,out var cached))return cached;
  var m=new Material(shader){name="Pixel · "+source.name,enableInstancing=true};
  Color c=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):Color.white;
  // Lift existing dark procedural colors into a gentler campus palette.
  var display=c.gamma;Color.RGBToHSV(display,out float h,out float s,out float v);
  var palette=Color.HSVToRGB(h,Mathf.Clamp(s*.85f,0,.72f),Mathf.Clamp(v*1.1f+.035f,0,.94f)).linear;palette.a=c.a;
  m.SetColor("_BaseColor",palette);
  foreach(var p in new[]{"_Cutoff","_Water","_Wind","_SrcBlend","_DstBlend","_ZWrite","_Cull"})if(source.HasProperty(p))m.SetFloat(p,source.GetFloat(p));
  if(source.HasProperty("_BaseMap")&&source.GetTexture("_BaseMap")!=null){m.SetTexture("_BaseMap",PixelTexture(source.GetTexture("_BaseMap")));m.SetTextureScale("_BaseMap",source.GetTextureScale("_BaseMap"));}
  m.renderQueue=source.renderQueue;
  if(m.GetFloat("_Cutoff")>0){m.EnableKeyword("_ALPHATEST_ON");m.SetOverrideTag("RenderType","TransparentCutout");}
  if(m.GetFloat("_ZWrite")==0)m.SetOverrideTag("RenderType","Transparent");
  var file=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(source));
  if(int.TryParse(file,out int id)){
   // Three independently authored palettes: sandstone A, sage B, warm clay C.
   if(id==30||id==51||id==72){m.SetColor("_BaseColor",Hex(id==30?"CDBD9E":id==51?"A9BEAE":"C8A58D"));m.SetTexture("_BaseMap",null);m.SetFloat("_Style",1);}
   if(id==13||id==34||id==55)m.SetColor("_BaseColor",Hex("F1E7CF"));
   if(id==12||id==33||id==54)m.SetColor("_BaseColor",Hex("687D76"));
   if(id==31||id==52||id==73)m.SetColor("_BaseColor",Hex("DBDFD0"));
  }
  string name=source.name.ToLowerInvariant();
  if(name.Contains("paving")){m.SetTexture("_BaseMap",null);m.SetColor("_BaseColor",Hex("CFC3A7"));m.SetFloat("_Style",2);}
  if(name.Contains("grass")){m.SetTexture("_BaseMap",null);m.SetColor("_BaseColor",Hex("86AA68"));m.SetFloat("_Style",3);}
  if(name.Contains("glass")){var glass=Hex("71B9C6");glass.a=.45f;m.SetColor("_BaseColor",glass);m.SetFloat("_Style",4);}
  string path=Root+"/Materials/"+file+".mat";Save(m,path);m=AssetDatabase.LoadAssetAtPath<Material>(path);materials[source]=m;return m;
 }
 static Texture2D PixelTexture(Texture source){
  if(textures.TryGetValue(source,out var existing))return existing;
  var input=source as Texture2D;if(input==null||!input.isReadable)return input;
  int w=Mathf.Min(64,input.width),h=Mathf.Min(64,input.height);var texture=new Texture2D(w,h,TextureFormat.RGBA32,true,false){filterMode=FilterMode.Point,wrapMode=input.wrapMode,anisoLevel=0,name="Pixel "+source.name};
  var pixels=new Color[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++){var c=input.GetPixel(Mathf.Min(input.width-1,x*input.width/w),Mathf.Min(input.height-1,y*input.height/h));pixels[y*w+x]=new Color(Mathf.Round(c.r*15)/15,Mathf.Round(c.g*15)/15,Mathf.Round(c.b*15)/15,c.a>.45f?1:0);}
  texture.SetPixels(pixels);texture.Apply(true,false);string path=Root+"/Textures/"+source.name+".asset";Save(texture,path);texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);textures[source]=texture;return texture;
 }
 static void AddFlowerBorders(CampusGame game,Shader shader){
  // Small, passable flower clusters on the court's planted margins.
  var root=new GameObject("Pixel study · courtyard flower borders");
  var colors=new[]{"5B865A","F2C766","DF8D99","EDE3BE"};
  var lists=colors.Select(_=>new List<CombineInstance>()).ToArray();
  var probe=GameObject.CreatePrimitive(PrimitiveType.Cube);var cube=probe.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(probe);
  var rng=new System.Random(417);
  for(int side=0;side<2;side++)for(int i=0;i<40;i++){
   float x=side==0?44.3f:70.8f,z=side==0?-100+i*.23f:-82+i*.23f;
   x+=(float)rng.NextDouble()*.6f;float height=.25f+(float)rng.NextDouble()*.24f;
   Vector3 p=game.MapToWorld(new Vector3(x,height/2,z));
   lists[0].Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(p,Quaternion.identity,new Vector3(.07f,height,.07f))});
   for(int j=0;j<3;j++){var petal=game.MapToWorld(new Vector3(x+(j-1)*.10f,height,z));lists[1+i%3].Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(petal,Quaternion.identity,new Vector3(.13f,j==1?.12f:.07f,.16f))});}
  }
  for(int i=0;i<colors.Length;i++){
   var mesh=new Mesh{name="Pixel flower blocks "+i};mesh.CombineMeshes(lists[i].ToArray());Save(mesh,Root+"/Flowers-"+i+".asset");
   var mat=new Material(shader);mat.SetColor("_BaseColor",Hex(colors[i]));Save(mat,Root+"/Materials/Flowers-"+i+".mat");
   var obj=new GameObject("Pixel flowers "+i);obj.transform.parent=root.transform;obj.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Flowers-"+i+".asset");obj.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Flowers-"+i+".mat");
  }
 }
 [MenuItem("Campus/Pixel Study/Open")]
 public static void Open(){EditorSceneManager.OpenScene(Scene);var game=UnityEngine.Object.FindFirstObjectByType<CampusGame>();SceneView.lastActiveSceneView?.LookAt(game.MapToWorld(new Vector3(60,2,-89)),Quaternion.Euler(25,180,0),34);}
 [MenuItem("Campus/Pixel Study/Build Windows")]
 public static void Build(){
  Directory.CreateDirectory("Builds/PixelStudy");
  var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName="Builds/PixelStudy/CampusPixel.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
  if(result.summary.result!=BuildResult.Succeeded)throw new Exception("Pixel build failed: "+result.summary.result);
  foreach(var name in new[]{"Campus/Pixel Surface","Campus/Pixel Sky"})foreach(var error in ShaderUtil.GetShaderMessages(Shader.Find(name)))if(error.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new Exception(name+": "+error.message);
  Debug.Log("PIXEL_STUDY_BUILD_COMPLETE");
 }
}
