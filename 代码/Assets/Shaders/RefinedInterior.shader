Shader "Campus/RefinedInterior"
{
 Properties {
  _BaseColor("Color",Color)=(1,1,1,1)
  _Kind("0 plaster, 1 stone tile, 2 oak, 3 fabric, 4 metal, 5 terrazzo",Float)=0
  _Smoothness("Smoothness",Range(0,1))=.28 _Metallic("Metallic",Range(0,1))=0 _Emission("Lamp emission",Float)=0
  [HideInInspector] _BaseMap("Texture",2D)="white"{} [HideInInspector] _Cull("Cull",Float)=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="UniversalForward"} Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor; float _Kind,_Smoothness,_Metallic,_Emission;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
   half4 frag(V i):SV_Target {
    float3 n=normalize(i.n),view=normalize(GetWorldSpaceViewDir(i.w));if(dot(n,view)<0)n=-n;
    float2 uv=abs(n.y)>.65?i.w.xz:float2(abs(n.x)>.65?i.w.z:i.w.x,i.w.y);
    float3 albedo=_BaseColor.rgb;float smooth=_Smoothness;
    if(n.y<-.7&&_Kind>.5&&_Kind<1.5){albedo=float3(.82,.81,.77);smooth=.10;}
    else if(_Kind>.5&&_Kind<1.5){
     float2 q=uv/float2(1.2,.6),aa=max(fwidth(q),.0005);
     float2 d=min(frac(q),1-frac(q));float seam=1-min(smoothstep(.001,.001+aa.x,d.x),smoothstep(.001,.001+aa.y,d.y));
     albedo*=lerp(1,.73,seam)*(.98+hash(floor(q))*.035);albedo*=.98+noise(uv*4)*.035;
    } else if(_Kind>1.5&&_Kind<2.5){
     float grain=noise(float2(uv.x*70,uv.y*1.6));albedo*=.9+grain*.16;smooth*=.94+grain*.06;
    } else if(_Kind>2.5&&_Kind<3.5){
     float fade=1-saturate(length(fwidth(uv*160)));float weave=sin(uv.x*1000)*sin(uv.y*1000);
     albedo*=1+weave*.035*fade;smooth=.10;
    } else if(_Kind>4.5){
     float2 q=uv*90;float speck=hash(floor(q));float fade=1-saturate(length(fwidth(q)));
     albedo*=lerp(1,speck>.96?.72:speck<.05?1.14:1,fade);
    } else {albedo*=.985+noise(uv*9)*.025;}
    InputData data=(InputData)0;data.positionWS=i.w;data.normalWS=n;data.viewDirectionWS=view;
    data.shadowCoord=TransformWorldToShadowCoord(i.w);data.bakedGI=SampleSH(n);
    data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);data.shadowMask=half4(1,1,1,1);
    SurfaceData surface=(SurfaceData)0;surface.albedo=albedo;surface.metallic=_Metallic;surface.specular=.04;
    surface.smoothness=smooth;surface.occlusion=1;surface.alpha=1;surface.emission=albedo*_Emission;
    return UniversalFragmentPBR(data,surface);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
