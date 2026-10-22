Shader "Campus/CourtPaint"
{
    Properties { _BaseColor("Paint colour",Color)=(.93,.92,.87,1) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            Cull Off ZWrite Off Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct A {float4 p:POSITION;};
            struct V {float4 p:SV_POSITION;};
            V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);return o;}
            half4 frag(V i):SV_Target{return _BaseColor;}
            ENDHLSL
        }
    }
}
