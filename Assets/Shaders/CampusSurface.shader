Shader "Campus/Surface" {
 Properties {
  _BaseMap("Texture",2D)="white"{} _BaseColor("Color",Color)=(1,1,1,1)
  _Cutoff("Cutout",Float)=0 _Smoothness("Smoothness",Range(0,1))=.3
  [HideInInspector] _CampusLampOn("Night lamp emission",Float)=0
  _Water("Water motion",Float)=0 _Wind("Foliage motion",Float)=0
  [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source",Float)=1
  [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination",Float)=0
  _ZWrite("Depth write",Float)=1 [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="UniversalForward"}
   Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] Cull [_Cull]
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "CampusNightLighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST,_BaseColor; float _Cutoff,_Smoothness,_Water,_Wind,_CampusLampOn;
   CBUFFER_END
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float4 color:COLOR;float fog:TEXCOORD3;};
   V vert(A a){V o;float3 p=TransformObjectToWorld(a.positionOS.xyz);p.x+=_Wind*sin(_Time.y*1.1+p.x*.17+p.z*.11)*.055*saturate(p.y/4);o.world=p;o.positionCS=TransformWorldToHClip(p);o.normal=TransformObjectToWorldNormal(a.normalOS);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.color=a.color;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
   half4 frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target {
    half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor*i.color;clip(c.a-_Cutoff);
    float3 view=normalize(GetWorldSpaceViewDir(i.world));
    float3 n=normalize(i.normal);if(dot(n,view)<0)n=-n;
    if(_Water>.5)n=normalize(n+float3(sin(i.world.x*1.4+_Time.y)*.13,0,cos(i.world.z*1.1+_Time.y*.8)*.13));
    Light l=GetMainLight(TransformWorldToShadowCoord(i.world));
    half3 ambient=lerp(half3(.15,.17,.15),half3(.37,.42,.46),n.y*.5+.5);
    half3 lit=c.rgb*(max(SampleSH(n),ambient)+l.color*saturate(dot(n,l.direction))*(.22+.78*l.shadowAttenuation));
    lit+=l.color*pow(saturate(dot(n,normalize(view+l.direction))),lerp(12,180,_Smoothness))*_Smoothness*.3*l.shadowAttenuation;
    if(_Water>.5){float fresnel=pow(1-saturate(dot(n,view)),3);lit=lerp(lit,half3(.47,.66,.76),fresnel*.7);}
    lit=CampusOutdoorLight(lit,c.rgb,n,i.world,1)+c.rgb*_CampusLampOn*1.5;
    // The terrain is one large plane: evaluate fog per pixel, not at its distant corners.
    float fogDistance=distance(GetCameraPositionWS(),i.world)*.0012;
    return half4(lerp(lit,unity_FogColor.rgb,1-exp(-fogDistance*fogDistance)),c.a);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
