Shader "BrainPunk/Level139/PortalBlack"
{
    Properties
    {
        [HideInInspector] _EdgeCount ("Contour Edge Count", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float _EdgeCount;
            float4 _Edges[16];
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 positionOS : TEXCOORD0;
        };
        Varyings PortalVertex(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.positionOS = input.positionOS.xy;
            return output;
        }
        half4 PortalFragment(Varyings input) : SV_Target
        {
            float distanceToEdge = 1e10;
            [unroll]
            for (int edge = 0; edge < 16; edge++)
            {
                if (edge < _EdgeCount)
                    distanceToEdge = min(distanceToEdge, dot(_Edges[edge].xy, input.positionOS) + _Edges[edge].z);
            }
            // 只在原轮廓内侧约一屏幕像素过渡；不扩轮廓、不整体透明、不模糊内部。
            float pixelWidth = max(fwidth(distanceToEdge), 1e-6);
            half coverage = saturate(distanceToEdge / pixelWidth);
            return half4(0, 0, 0, coverage);
        }
        ENDHLSL

        Pass
        {
            Name "Portal2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PortalVertex
            #pragma fragment PortalFragment
            ENDHLSL
        }
        Pass
        {
            Name "PortalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PortalVertex
            #pragma fragment PortalFragment
            ENDHLSL
        }
    }
}
