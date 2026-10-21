Shader "Campus/VRoid URP"
{
 Properties {
  _BaseMap("Artist texture",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1)
  _Cutoff("Cutout",Range(0,1))=.45 [HideInInspector]_Cull("Cull",Float)=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
  Pass {
   Name "Forward" Tags {"LightMode"="UniversalForward"} Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;half4 _BaseColor;float _Cutoff;
   CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
   struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;};
   V vert(A i){V o;o.w=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(i.n);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);return o;}
   half4 frag(V i):SV_Target {
    half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;clip(albedo.a-_Cutoff);
    float3 n=normalize(i.n);if(dot(n,GetWorldSpaceViewDir(i.w))<0)n=-n;
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
    float diffuse=smoothstep(-.30,.65,dot(n,sun.direction));
    half3 ambient=max(SampleSH(n),half3(.30,.31,.33));
    half3 light=ambient*.64+sun.color*(.30+diffuse*.45)*lerp(.78,1,sun.shadowAttenuation);
    // Keep the hand-painted face readable in interior lighting and campus shadows.
    light=clamp(light,half3(.60,.61,.64),half3(1.16,1.14,1.12));
    return half4(albedo.rgb*light,1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
