Shader "Sprites/FlowMap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _FlowMap ("Flow Map", 2D) = "white" {}
        _FlowSpeed ("Vector Field Strength", Float) = 0.1
        _TimeSpeed ("Global Flow Speed", Float) = 1
        [Toggle(_REVERSE_FLOW_ON)] _ReverseFlow ("Reverse Flow", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FlowerMap"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _REVERSE_FLOW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_FlowMap);
            SAMPLER(sampler_FlowMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _FlowMap_ST;
                float4 _MainColor;
                float _FlowSpeed;
                float _TimeSpeed;
                float _ReverseFlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _MainColor;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 flowMapUV = input.uv * _FlowMap_ST.xy + _FlowMap_ST.zw;
                float2 flowDir = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, flowMapUV).rg * 2.0 - 1.0;
                flowDir *= _FlowSpeed;

                #if defined(_REVERSE_FLOW_ON)
                    flowDir *= -1.0;
                #endif

                float phase0 = frac(_Time.y * _TimeSpeed);
                float phase1 = frac(_Time.y * _TimeSpeed + 0.5);
                float flowLerp = abs((0.5 - phase0) / 0.5);

                float2 mainUV = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                half4 tex0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV - flowDir * phase0);
                half4 tex1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV - flowDir * phase1);

                half4 color = lerp(tex0, tex1, flowLerp) * input.color;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit"
}
