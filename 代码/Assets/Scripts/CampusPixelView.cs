using UnityEngine;
using UnityEngine.Rendering.Universal;

// The world is rendered at an integer fraction of the window resolution.
// IMGUI remains full resolution; the player camera still moves continuously.
public sealed class CampusPixelView : MonoBehaviour {
 public Camera worldCamera;
 public int pixelSize=3;
 RenderTexture image;
 public RenderTexture Image=>image;
 public int EffectivePixelSize=>Mathf.Clamp(pixelSize,1,6);
 void OnEnable(){if(worldCamera==null)worldCamera=GetComponent<Camera>();Resize();}
 void Update(){if(Input.GetKeyDown(KeyCode.F6))pixelSize=pixelSize==3?4:pixelSize==4?2:pixelSize==2?1:3;Resize();}
 void Resize(){
  if(worldCamera==null)return;
  int w=Mathf.Max(1,Screen.width/EffectivePixelSize),h=Mathf.Max(1,Screen.height/EffectivePixelSize);
  if(image!=null&&image.width==w&&image.height==h)return;
  Release();image=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32){name="Campus pixel frame",filterMode=FilterMode.Point,antiAliasing=1,useMipMap=false};
  image.Create();worldCamera.targetTexture=image;worldCamera.aspect=(float)Screen.width/Screen.height;worldCamera.allowMSAA=false;
  var cameraData=worldCamera.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=false;cameraData.antialiasing=AntialiasingMode.None;
 }
 public void DrawBackground(){
  if(image==null)return;
  var matrix=GUI.matrix;var color=GUI.color;
  GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;
  // Center an integer-sized image. At most five edge pixels are letterboxed.
  GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.blackTexture);
  float w=image.width*EffectivePixelSize,h=image.height*EffectivePixelSize;
  GUI.DrawTexture(new Rect((Screen.width-w)/2,(Screen.height-h)/2,w,h),image,ScaleMode.StretchToFill,false);
  GUI.matrix=matrix;GUI.color=color;
 }
 void Release(){if(image==null)return;if(worldCamera!=null&&worldCamera.targetTexture==image)worldCamera.targetTexture=null;image.Release();Destroy(image);image=null;}
 void OnDisable(){Release();}
}
