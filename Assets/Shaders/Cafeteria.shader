Shader "Campus/Cafeteria" {
 Properties {
  _BaseColor("Color",Color)=(1,1,1,1) _BaseMap("Texture",2D)="white"{}
  _Wood("Wood grain",Float)=0 _Stone("Stone panels",Float)=0 _Glass("Glass",Float)=0 _Emission("Emission",Float)=0
  _Smoothness("Smoothness",Range(0,1))=.3 _Cutoff("Cutoff",Float)=0 _Cull("Cull",Float)=0
  _SrcBlend("Source",Float)=1 _DstBlend("Destination",Float)=0 _ZWrite("Depth",Float)=1
  [HideInInspector] _ViewCutCenter("View cut center",Vector)=(0,0,0,0)
  [HideInInspector] _ViewCutSize("View cut half size",Vector)=(0,0,0,0)
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
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor,_ViewCutCenter,_ViewCutSize;float _Wood,_Stone,_Glass,_Emission,_Smoothness;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   half4 frag(V i):SV_Target {
    if(_ViewCutCenter.w>.5&&all(abs(i.w-_ViewCutCenter.xyz)<_ViewCutSize.xyz))clip(-1);
    float3 view=normalize(GetWorldSpaceViewDir(i.w)),n=normalize(i.n);if(dot(n,view)<0)n=-n;
    float2 uv=abs(n.y)>.7?i.w.xz:float2(abs(n.x)>.7?i.w.z:i.w.x,i.w.y);
    float3 c=_BaseColor.rgb;
    if(_Stone>.5){
     float2 q=uv/float2(.9,.55),aa=max(fwidth(q),.001),edge=abs(frac(q)-.5);
     float2 joint=smoothstep(.492-aa,.492+aa,edge);float fade=1-saturate(max(aa.x,aa.y));
     c*=lerp(1,.72,max(joint.x,joint.y)*fade)*lerp(1,.96+hash(floor(q))*.07,fade);
     c*=1+sin(uv.x*9+sin(uv.y*3))*.009;
    }
    if(_Wood>.5)c*=.94+.075*sin(uv.x*65+sin(uv.y*3)*1.2);
    Light light=GetMainLight(TransformWorldToShadowCoord(i.w));
    float3 ambient=max(SampleSH(n),lerp(float3(.32,.33,.32),float3(.55,.59,.62),n.y*.5+.5));
    float3 lit=c*(ambient+light.color*saturate(dot(n,light.direction))*(.12+.88*light.shadowAttenuation));
    lit+=light.color*pow(saturate(dot(n,normalize(view+light.direction))),lerp(16,170,_Smoothness))*_Smoothness*.22*light.shadowAttenuation;
    lit+=c*_Emission;
    float alpha=_BaseColor.a;
    if(_Glass>.5){float3 r=reflect(-view,n);float f=pow(1-saturate(dot(n,view)),4);
     float3 sky=lerp(float3(.17,.23,.2),float3(.57,.71,.82),smoothstep(-.1,.4,r.y));
     float cloud=pow(saturate(sin(r.x*16+r.z*8)*sin(r.z*18-r.x*9)),4);
     lit=lerp(lit,lerp(sky,float3(.85,.86,.83),cloud*.4),.7);alpha=lerp(alpha,.72,f);
    }
    float fog=distance(GetCameraPositionWS(),i.w)*.0012;return half4(lerp(lit,unity_FogColor.rgb,1-exp(-fog*fog)),alpha);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  Pass {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
   ZWrite On ColorMask 0 Cull Off
   HLSLPROGRAM
   #pragma vertex depthVert
   #pragma fragment depthFrag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor,_ViewCutCenter,_ViewCutSize;float _Wood,_Stone,_Glass,_Emission,_Smoothness;
   CBUFFER_END
   struct DA{float4 p:POSITION;};struct DV{float4 p:SV_POSITION;float3 w:TEXCOORD0;};
   DV depthVert(DA a){DV o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);return o;}
   half4 depthFrag(DV i):SV_Target{if(_ViewCutCenter.w>.5&&all(abs(i.w-_ViewCutCenter.xyz)<_ViewCutSize.xyz))clip(-1);return 0;}
   ENDHLSL
  }
 }
}
