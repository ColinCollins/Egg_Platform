Shader "BrainPunk/Level132/RopeCurve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Rope texture", 2D) = "white" {}
        [PerRendererData] _AlphaTex ("External alpha", 2D) = "white" {}
        _UseExternalAlpha ("Use external alpha", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "RopeCurve"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_AlphaTex);
            SAMPLER(sampler_AlphaTex);
            CBUFFER_START(UnityPerMaterial)
                float _UseExternalAlpha;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half alpha = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, input.uv).r;
                color.a = lerp(color.a, alpha, saturate(_UseExternalAlpha));
                return color * input.color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
