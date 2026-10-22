Shader "Campus/RoadLandscape" {
 Properties {
  _BaseColor("Colour",Color)=(.6,.6,.6,1) _BaseMap("Map",2D)="white"{}
  _Surface("0 aggregate, 1 paving, 2 stone, 3 wood, 4 foliage, 5 metal, 6 bark",Float)=0
  _Smoothness("Smoothness",Range(0,1))=.18 _Metallic("Metallic",Range(0,1))=0
  _TileSize("Paver size in metres",Vector)=(.6,.4,0,0)
  _VertexTint("Use leaf colours",Float)=0 _Cutoff("Cutoff",Float)=0 _Cull("Cull",Float)=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="UniversalForward"} Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "CampusEnvironmentLighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor,_TileSize;float _Surface,_Smoothness,_Metallic,_VertexTint;
   CBUFFER_END
   #include "CampusNightLighting.hlsl"
   struct A {float4 p:POSITION;float3 n:NORMAL;float4 c:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float4 c:COLOR;float fog:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
   V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;o.fog=ComputeFogFactor(o.p.z);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   half4 frag(V i):SV_Target {
    UNITY_SETUP_INSTANCE_ID(i);
    float3 n=normalize(i.n),view=GetWorldSpaceNormalizeViewDir(i.w);if(dot(n,view)<0)n=-n;
    float2 uv=abs(n.y)>.65?i.w.xz:float2(abs(n.x)>.65?i.w.z:i.w.x,i.w.y);
    float grain=noise(uv*90)-.5,fade=1-saturate(length(fwidth(uv))*45);
    float3 c=_BaseColor.rgb*lerp(1,i.c.rgb,_VertexTint);float smoothness=_Smoothness;
    if(_Surface<.5){c*=.92+.12*noise(uv*.6)+grain*.25*fade;n=normalize(n+float3(grain*.065,0,grain*.05)*fade);}
    else if(_Surface<1.5){
     float2 q=uv/max(_TileSize.xy,.05);q.x+=fmod(floor(q.y),2)*.5;float2 aa=max(fwidth(q),.002);
     float2 edge=smoothstep(.497-aa,.497+aa,abs(frac(q)-.5));float detail=1-saturate(max(aa.x,aa.y));
     float joint=max(edge.x,edge.y)*detail;c*=lerp(1,.72,joint)*lerp(1,.965+hash(floor(q))*.07,detail);
     c*=1+grain*.1*fade;smoothness*=1-joint*.7;
    } else if(_Surface<2.5)c*=.94+.08*noise(uv*8)+grain*.09*fade;
    else if(_Surface<3.5)c*=.78+.25*noise(uv*float2(3,85))+.05*sin(uv.y*150+noise(uv*4)*8);
    else if(_Surface<4.5)c*=.86+.14*noise(i.w.xz*10+i.w.y);
    else if(_Surface>5.5)c*=.72+.32*noise(uv*float2(30,2));
    InputData d=(InputData)0;d.positionWS=i.w;d.normalWS=n;d.viewDirectionWS=view;
    d.shadowCoord=TransformWorldToShadowCoord(i.w);d.bakedGI=CampusEnvironmentAmbient(n);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=1;
    SurfaceData s=(SurfaceData)0;s.albedo=c;s.alpha=1;s.metallic=_Metallic;s.smoothness=smoothness;s.occlusion=1;s.normalTS=float3(0,0,1);
    if(_Surface>3.5&&_Surface<4.5){Light l=GetMainLight(d.shadowCoord);s.emission=c*l.color*pow(saturate(dot(-l.direction,view)),3)*.13*l.shadowAttenuation;}
    half4 col=UniversalFragmentPBR(d,s);col.rgb=CampusOutdoorLight(col.rgb,c,n,i.w,1);col.rgb=CampusEnvironmentFog(col.rgb,i.w);return col;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
