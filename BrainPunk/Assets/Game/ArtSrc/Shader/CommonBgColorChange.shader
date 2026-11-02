Shader "Game/2D/CommonBgColorChange"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Height Gradient)]
        _StartHeight ("Start Height (World Y)", Float) = 0
        _EndHeight ("End Height (World Y)", Float) = 10
        _StartColor ("Start Color", Color) = (1,1,1,1)
        _EndColor ("End Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Sprite"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "CommonBgColorChange"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ PIXELSNAP_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _Color;
            float _StartHeight;
            float _EndHeight;
            float4 _StartColor;
            float4 _EndColor;

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
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = input.uv;
                output.color = input.color * _Color;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // 根据世界坐标 Y（V 方向）计算渐变 t
                float heightRange = _EndHeight - _StartHeight;
                float t = (abs(heightRange) < 0.0001)
                    ? 0
                    : saturate((input.positionWS.y - _StartHeight) / heightRange);

                half4 gradientColor = half4(lerp(_StartColor.rgb, _EndColor.rgb, t), lerp(_StartColor.a, _EndColor.a, t));

                half4 result = texColor * gradientColor * input.color;
                return result;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit"
}
