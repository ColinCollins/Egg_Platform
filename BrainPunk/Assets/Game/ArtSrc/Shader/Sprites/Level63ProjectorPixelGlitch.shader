Shader "Sprites/Level63ProjectorPixelGlitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlockSize ("Block Size", Float) = 64
        _ShiftStrength ("Shift Strength", Float) = 0.012
        _NoiseSpeed ("Noise Speed", Float) = 9
        _ChannelOffset ("Channel Offset", Float) = 0.006
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
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _BlockSize;
                float _ShiftStrength;
                float _NoiseSpeed;
                float _ChannelOffset;
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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float blockSize = max(1.0, _BlockSize);
                float2 block = floor(input.uv * blockSize);
                float timeTick = fmod(floor(_Time.y * _NoiseSpeed), 256.0);
                float pulse = step(0.74, Hash21(float2(block.y, timeTick)));
                float shift = (Hash21(block) * 2.0 - 1.0) * _ShiftStrength * pulse;
                float2 uv = input.uv + float2(shift, 0.0);

                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half red = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(_ChannelOffset * pulse, 0.0)).r;
                half blue = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(_ChannelOffset * pulse, 0.0)).b;
                baseColor.r = red;
                baseColor.b = blue;
                return baseColor * input.color;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
