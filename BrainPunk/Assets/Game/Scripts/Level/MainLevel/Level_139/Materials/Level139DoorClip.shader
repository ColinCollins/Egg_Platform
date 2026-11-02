Shader "BrainPunk/Level139/DoorClip"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _DoorClipRect ("World XY Door Aperture", Vector) = (-100000,-100000,100000,100000)
        [HideInInspector] _DoorSeamRect ("World XY Closed Seam", Vector) = (0,0,0,0)
        [HideInInspector] _DoorClosed ("Seal Only When Closed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _DoorClipRect;
            float4 _DoorSeamRect;
            float _DoorClosed;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 positionWS : TEXCOORD1;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings DoorVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            SetUpSpriteInstanceProperties();
            float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
            float3 positionWS = TransformObjectToWorld(positionOS);
            output.positionCS = TransformWorldToHClip(positionWS);
            output.positionWS = positionWS.xy;
            output.uv = input.uv;
            output.color = input.color * _Color * unity_SpriteColor;
            return output;
        }

        half4 DoorFragment(Varyings input) : SV_Target
        {
            // 只丢弃门洞外的像素。门洞内部使用原 Sprite UV、九宫格网格和颜色，
            // 不读取 Stencil，不依赖 SpriteMask 或其排序范围。
            clip(input.positionWS - _DoorClipRect.xy);
            clip(_DoorClipRect.zw - input.positionWS);
            half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            // 两扇门的矩形已经接齐，但原 PNG 边缘仍有透明像素。
            // 仅在完全关闭、且位于轿厢内部中缝时补齐 alpha；保留原边线 RGB，
            // 不扩大门宽、不改变 UV，也不影响开门过程及原开门终态。
            if (_DoorClosed > 0.5
                && all(input.positionWS >= _DoorSeamRect.xy)
                && all(input.positionWS <= _DoorSeamRect.zw))
                texel.a = 1.0h;
            return texel * input.color;
        }
        ENDHLSL

        Pass
        {
            Name "Door2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex DoorVertex
            #pragma fragment DoorFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DoorForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex DoorVertex
            #pragma fragment DoorFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
