Shader "BrainPunk/Level9/LavaBubble"
{
    Properties
    {
        _MainTex ("单张泡泡轮廓（仅使用透明度）", 2D) = "white" {}
        _BodyColor ("泡体底部橙色", Color) = (1, 0.36, 0.035, 1)
        _TopColor ("泡体顶部暖橙色", Color) = (1, 0.58, 0.10, 1)
        _HighlightColor ("柔和高光", Color) = (1, 0.77, 0.30, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
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
            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor, _TopColor, _HighlightColor;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
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
                // 负 UV 的破口/小滴保持原顶点色；泡体只借用单图轮廓，去掉密集油亮花纹。
                if (input.uv.x < 0) return input.color;
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                half3 color = lerp(_BodyColor.rgb, _TopColor.rgb, smoothstep(0.18, 0.8, input.uv.y));
                float2 highlightOffset = (input.uv - float2(0.35, 0.65)) / float2(0.18, 0.09);
                float highlight = 1.0 - smoothstep(0.25, 1.0, dot(highlightOffset, highlightOffset));
                color = lerp(color, _HighlightColor.rgb, highlight * 0.75);
                return half4(color, alpha) * input.color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
