Shader "Campus/GymInterior" {
 Properties {
  _BaseColor("Color",Color)=(1,1,1,1) _BaseMap("Texture",2D)="white"{}
  _Wood("Wood grain",Float)=0 _Stone("Stone panels",Float)=0 _Emission("Emission",Float)=0
  _Smoothness("Smoothness",Range(0,1))=.3 _Metallic("Metallic",Range(0,1))=0
  _Cutoff("Cutoff",Float)=0 _Cull("Cull",Float)=0
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
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   // This material evaluates the supplied additional-light list per pixel even
   // when the exterior pipeline uses the cheaper vertex-light mode.
   #ifndef _ADDITIONAL_LIGHTS
   #define _ADDITIONAL_LIGHTS
   #endif
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;float _Wood,_Stone,_Emission,_Smoothness,_Metallic;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;};
   struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
   half4 frag(V i):SV_Target {
    float3 view=normalize(GetWorldSpaceViewDir(i.w)),n=normalize(i.n);if(dot(n,view)<0)n=-n;
    float2 uv=abs(n.y)>.7?i.w.xz:float2(abs(n.x)>.7?i.w.z:i.w.x,i.w.y);
    float3 c=_BaseColor.rgb;float smooth=_Smoothness;
    if(_Stone>.5){
     float2 q=uv/float2(1.2,.8),aa=max(fwidth(q),.001),edge=abs(frac(q)-.5);
     float2 joint=smoothstep(.498-aa,.498+aa,edge);float fade=1-saturate(max(aa.x,aa.y));
     c*=lerp(1,.88,max(joint.x,joint.y)*fade)*lerp(1,.985+hash(floor(q))*.03,fade);
     float vein=noise(uv*3+noise(uv*7)*.7);c*=.96+vein*.07;smooth*=.9+vein*.1;
    }
    if(_Wood>.5){
      float2 q=float2(uv.x/.18,uv.y/2.1+floor(uv.x/.18)*.37);
      float2 aa=max(fwidth(q),.001),edge=abs(frac(q)-.5);
      float seam=max(smoothstep(.493-aa.x,.493+aa.x,edge.x),smoothstep(.498-aa.y,.498+aa.y,edge.y));
      float fade=1-saturate(max(aa.x,aa.y));
      float grain=noise(float2(uv.x*140,uv.y*2.4));
      c*=(.88+hash(floor(q))*.19)*(.95+grain*.08)*lerp(1,.68,seam*fade);
      smooth*=.90+grain*.10;
    }
    InputData data=(InputData)0;data.positionWS=i.w;data.normalWS=n;data.viewDirectionWS=view;data.shadowCoord=TransformWorldToShadowCoord(i.w);
    data.bakedGI=max(SampleSH(n),float3(.35,.36,.34));data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);data.shadowMask=half4(1,1,1,1);
    SurfaceData surface=(SurfaceData)0;surface.albedo=c;surface.metallic=_Metallic;surface.specular=.04;surface.smoothness=smooth;surface.occlusion=1;surface.alpha=1;surface.emission=c*_Emission;
    return UniversalFragmentPBR(data,surface);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
