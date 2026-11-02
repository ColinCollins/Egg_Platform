// IWanner 主题背景共用：旧材质可继续显示顶点色，新材质通过暗／亮两色控制语义层配色。
Shader "BrainPunk/IWanner/ThemeMaterialGradient"
{
    Properties
    {
        [Toggle] _UseMaterialColor ("使用材质配色", Range(0, 1)) = 0
        _DarkColor ("暗部颜色", Color) = (1, 1, 1, 1)
        [MainColor] _LightColor ("亮部颜色", Color) = (1, 1, 1, 1)
        [HideInInspector] _SourceLuminanceMin ("源颜色最低亮度", Float) = 0
        [HideInInspector] _SourceLuminanceRange ("源颜色亮度范围", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
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

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor;
                half4 _LightColor;
                half _UseMaterialColor;
                half _SourceLuminanceMin;
                half _SourceLuminanceRange;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half sourceLuminance = dot(input.color.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half weight = saturate((sourceLuminance - _SourceLuminanceMin) / max(_SourceLuminanceRange, 0.0001h));
                half4 materialColor = lerp(_DarkColor, _LightColor, weight);
                materialColor.a *= input.color.a;
                return lerp(input.color, materialColor, saturate(_UseMaterialColor));
            }
            ENDHLSL
        }
    }
}
