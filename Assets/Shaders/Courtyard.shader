Shader "Campus/Courtyard" {
 Properties {_BaseColor("Color",Color)=(1,1,1,1) _Surface("Surface",Float)=0 _BaseMap("Map",2D)="white"{} _Cull("Cull",Float)=0 _Cutoff("Cutoff",Float)=0}
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="UniversalForward"} Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "CampusNightLighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;float _Surface;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   half4 frag(V i):SV_Target {
    float3 n=normalize(i.n),view=normalize(GetWorldSpaceViewDir(i.w));if(dot(n,view)<0)n=-n;
    float2 uv=abs(n.y)>.6?i.w.xz:float2(abs(n.x)>.6?i.w.z:i.w.x,i.w.y);
    float3 c=_BaseColor.rgb;float grain=noise(uv*85)-.5;float atten=1-saturate(length(fwidth(uv))*60);
    if(_Surface<.5){c*=.94+noise(uv*.7)*.1+grain*.15*atten;n=normalize(n+float3(grain*.045,0,grain*.035)*atten);}
    else if(_Surface<1.5){float2 q=uv/float2(.6,.4);q.x+=fmod(floor(q.y),2)*.5;float2 edge=abs(frac(q)-.5);float2 aa=max(fwidth(q),.002);float2 grout=smoothstep(.48-aa,.48+aa,edge);float fade=1-saturate(max(aa.x,aa.y));c*=lerp(1,.67,max(grout.x,grout.y)*fade)*lerp(1,.92+hash(floor(q))*.14,fade);c*=1+grain*.06*atten;}
    else if(_Surface<2.5){c*=.88+noise(uv*2.2)*.18+noise(uv*.16)*.1+grain*.18*atten;}
    else if(_Surface<3.5){c*=.95+noise(uv*25)*.1;}
    else if(_Surface<4.5){c*=.85+noise(uv*float2(2,60))*.22;}
    else {c*=.9+noise(i.w.xz*8+i.w.y)*.15;}
    Light light=GetMainLight(TransformWorldToShadowCoord(i.w));float3 ambient=max(SampleSH(n),lerp(float3(.24,.26,.25),float3(.45,.5,.52),n.y*.5+.5));
    float3 lit=c*(ambient+light.color*saturate(dot(n,light.direction))*(.15+.85*light.shadowAttenuation));
    lit=CampusOutdoorLight(lit,c,n,i.w,1);
    float fog=distance(GetCameraPositionWS(),i.w)*.0012;return half4(lerp(lit,unity_FogColor.rgb,1-exp(-fog*fog)),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
