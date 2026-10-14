Shader "Campus/Pixel Surface" {
 Properties {
  _BaseMap("Pixel texture",2D)="white"{} _BaseColor("Palette color",Color)=(1,1,1,1)
  _Cutoff("Cutout",Float)=0 _Smoothness("Smoothness",Range(0,1))=0
  _Water("Water",Float)=0 _Wind("Wind",Float)=0
  _Style("Surface: plain, brick, paving, lawn, glass",Float)=0
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
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST,_BaseColor; float _Cutoff,_Smoothness,_Water,_Wind,_Style;
   CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct V {float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;float4 color:COLOR;};
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   V vert(A a){V o;float3 p=TransformObjectToWorld(a.p.xyz);p.x+=_Wind*sin(_Time.y+p.z*.15)*.045*saturate(p.y/3);o.world=p;o.p=TransformWorldToHClip(p);o.n=TransformObjectToWorldNormal(a.n);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.color=a.color;return o;}
   half4 frag(V i):SV_Target {
    half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor*i.color;clip(c.a-_Cutoff);
    float3 n=normalize(i.n);float3 view=normalize(GetWorldSpaceViewDir(i.world));if(dot(n,view)<0)n=-n;
    // Patterns are anchored to the surface, so camera motion doesn't slide them.
    float3 cell=floor(i.world*8)/8;
    if(_Style>.5&&_Style<1.5){
     float2 p=float2(abs(n.x)>abs(n.z)?cell.z:cell.x,cell.y);
     float row=floor(p.y/.375);float2 tile=float2(p.x/.875+fmod(row,2)*.5,p.y/.375);
     float2 f=frac(tile);float mortar=step(f.x,.065)+step(f.y,.15);
     c.rgb*=lerp(.94+floor(hash(floor(tile))*3)*.045,.77,saturate(mortar));
    }
    if(_Style>1.5&&_Style<2.5){float2 p=cell.xz/.875;float2 f=frac(p);c.rgb*=lerp(.94+floor(hash(floor(p))*3)*.045,.74,saturate(step(f.x,.055)+step(f.y,.055)));}
    if(_Style>2.5&&_Style<3.5)c.rgb*=.91+floor(hash(floor(cell.xz*3))*3)*.065;
    Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
    float diffuse=floor(saturate(dot(n,light.direction))*3+.5)/3;
    float shadow=lerp(.64,1,step(.48,light.shadowAttenuation));
    half3 lit=c.rgb*(half3(.66,.72,.78)+diffuse*half3(.42,.36,.25))*shadow;
    if(_Style>3.5){float stripe=step(.89,frac((cell.x+cell.z+cell.y*1.7)*.22));lit=lerp(lit,half3(.67,.87,.91),stripe*.32);}
    if(_Water>.5){float2 p=floor(i.world.xz*5)/5;float ripple=sin(p.x*2+p.y*1.3-floor(_Time.y*8)/8*1.5);lit=lerp(half3(.075,.39,.43),half3(.38,.76,.75),step(.8,ripple)*.65+.12);}
    // Quantize in display space instead of crushing dark linear colors.
    half3 srgb=LinearToSRGB(max(lit,0));srgb=floor(saturate(srgb)*31+.5)/31;lit=SRGBToLinear(srgb);
    float fog=1-exp(-pow(distance(GetCameraPositionWS(),i.world)*.0013,2));
    return half4(lerp(lit,unity_FogColor.rgb,fog),c.a);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
