Shader "Campus/Dormitory" {
 Properties {
  [HideInInspector] _CampusOutdoorSurface("Exterior lighting",Float)=0
  _BaseColor("Color",Color)=(1,1,1,1) _BaseMap("Texture",2D)="white"{}
  _Smoothness("Smoothness",Range(0,1))=.3 _Tile("Ceramic tile",Float)=0 _Glass("Glazing",Float)=0
  _Cutoff("Cutoff",Float)=0 _Cull("Cull",Float)=0
  _SrcBlend("Source",Float)=1 _DstBlend("Destination",Float)=0 _ZWrite("Depth",Float)=1
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "CampusNightLighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor; float _Smoothness,_Tile,_Glass,_CampusOutdoorSurface;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   half4 frag(V i):SV_Target {
    float3 view=normalize(GetWorldSpaceViewDir(i.w)),n=normalize(i.n);if(dot(n,view)<0)n=-n;
    float3 c=_BaseColor.rgb;
    if(_Tile>.5) {
     float2 uv=abs(n.y)>.7?i.w.xz:float2(abs(n.x)>.7?i.w.z:i.w.x,i.w.y);
     float2 grid=uv/float2(.095,.095),cell=abs(frac(grid)-.5);
     float2 aa=max(fwidth(grid),.005);float2 grout=smoothstep(.46-aa,.46+aa,cell);
     float mortar=max(grout.x,grout.y);float fade=1-saturate(max(aa.x,aa.y)*.65);
     c*=lerp(1,.82,mortar*fade)*lerp(1,lerp(.95,1.025,hash(floor(grid))),fade);
     float weather=sin(uv.x*18.2+sin(uv.x*3))*sin(uv.x*4.1)*.018;
     c*=1+weather;
    }
    Light l=GetMainLight(TransformWorldToShadowCoord(i.w));
    float3 ambient=max(SampleSH(n),lerp(float3(.27,.29,.28),float3(.55,.59,.63),n.y*.5+.5));
    float3 lit=c*(ambient+l.color*saturate(dot(n,l.direction))*(.13+.87*l.shadowAttenuation));
    lit+=l.color*pow(saturate(dot(n,normalize(view+l.direction))),lerp(16,170,_Smoothness))*_Smoothness*.22*l.shadowAttenuation;
    float alpha=_BaseColor.a;
    if(_Glass>.5) {
     float3 reflection=reflect(-view,n);float fresnel=pow(1-saturate(dot(n,view)),4);
     float3 sky=lerp(float3(.16,.24,.23),float3(.56,.7,.81),smoothstep(-.15,.35,reflection.y));
     float cloud=pow(saturate(sin(reflection.x*19+reflection.z*7)*sin(reflection.z*23-reflection.x*11)),5);
     sky=lerp(sky,float3(.85,.86,.82),cloud*.4);
     lit=lerp(lit,sky,.55+.35*fresnel);alpha=lerp(alpha,.8,fresnel);
    }
    lit=CampusOutdoorLight(lit,c,n,i.w,_CampusOutdoorSurface);
    float fog=distance(GetCameraPositionWS(),i.w)*.0012;
    return half4(lerp(lit,unity_FogColor.rgb,1-exp(-fog*fog)),alpha);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
