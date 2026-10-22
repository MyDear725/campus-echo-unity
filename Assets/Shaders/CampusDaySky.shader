Shader "Campus/DaySky" {
 Properties {_TopColor("Zenith",Color)=(.23,.43,.68,1) _HorizonColor("Horizon",Color)=(.77,.84,.88,1)}
 SubShader {
 Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"}
 Cull Off ZWrite Off
 Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 float4 _TopColor,_HorizonColor;
 float _CampusNightAmount;
 struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 d:TEXCOORD0;};
 V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.d=a.p.xyz;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 float fbm(float2 p){return noise(p)*.52+noise(p*2.07)*.26+noise(p*4.15)*.14+noise(p*8.3)*.08;}
 half4 frag(V i):SV_Target {
  float3 d=normalize(i.d);float h=saturate(d.y);float3 sky=lerp(_HorizonColor.rgb,_TopColor.rgb,pow(h,.45));
  float2 p=d.xz/(max(.04,d.y)+.23)*2.2+float2(5.1,1.2)+_Time.y*.0005;
  float n=fbm(p+noise(p*.7)*.45);float clouds=smoothstep(.53,.70,n)*smoothstep(.02,.20,h);
  float edge=fbm(p+float2(.06,.12));float3 white=lerp(float3(.66,.75,.82),float3(1,.98,.92),saturate((n-edge)*5+.58));
  sky=lerp(sky,white,clouds*.86);
  float sun=saturate(dot(d,GetMainLight().direction));
  sky+=float3(1,.79,.48)*pow(sun,48)*.12;
  sky+=float3(1,.92,.72)*smoothstep(.99990,.99997,sun)*.8;
  float3 night=lerp(float3(.025,.04,.075),float3(.004,.009,.025),pow(h,.45));sky=lerp(sky,night,saturate(_CampusNightAmount));return half4(sky,1);
 }
 ENDHLSL
 }
 }
}
