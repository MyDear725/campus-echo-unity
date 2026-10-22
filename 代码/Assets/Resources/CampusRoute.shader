Shader "Campus/Route" {
 Properties { _BaseColor("Route color",Color)=(.7,1,.65,.95) }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass {
   Tags {"LightMode"="SRPDefaultUnlit"}
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off ZTest LEqual
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor;
   CBUFFER_END
   struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;};
   V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);return o;}
   half4 frag(V i):SV_Target{return _BaseColor;}
   ENDHLSL
  }
 }
}
