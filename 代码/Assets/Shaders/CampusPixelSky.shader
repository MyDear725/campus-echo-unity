Shader "Campus/Pixel Sky" {
 SubShader {
  Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   struct A {float4 p:POSITION;};struct V {float4 p:SV_POSITION;float3 dir:TEXCOORD0;};
   V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.dir=a.p.xyz;return o;}
   half4 frag(V i):SV_Target {
    float3 d=normalize(i.dir);float h=saturate(d.y);
    float3 sky=lerp(float3(.69,.84,.82),float3(.27,.57,.72),floor(h*12)/12);
    if(d.y>.055){
     float2 p=floor((d.xz/(d.y+.3)+float2(_Time.y*.0018,0))*55)/55;
     float cloud=sin(p.x*2.1+sin(p.y*1.4))+.58*sin(p.y*3.3)+.28*cos(p.x*6.8+p.y);
     float mask=step(1.02,cloud)*smoothstep(.06,.22,d.y);
     sky=lerp(sky,lerp(float3(.83,.89,.84),float3(1,.97,.86),step(1.3,cloud)),mask);
    }
    return half4(SRGBToLinear(sky),1);
   }
   ENDHLSL
  }
 }
}
