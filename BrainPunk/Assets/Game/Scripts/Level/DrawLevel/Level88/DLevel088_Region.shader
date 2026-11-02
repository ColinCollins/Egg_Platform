Shader "Game/Draw/DLevel088 Region"
{
    Properties
    {
        _RegionColor ("区域颜色", Color) = (0.59607846,0.627451,0.98039216,0.5)
        _MainTex ("内部手绘平铺纹理", 2D) = "white" {}
        _PatternTileSize ("平铺宽高", Vector) = (2.33,2.01,0,0)
        _PatternStrength ("手绘纹理强度", Range(0,8)) = 4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _RegionColor;
            float4 _PatternTileSize;
            half _PatternStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                // 使用本地尺寸平铺，不归一化到整块区域；改变区域大小只增加重复次数。
                o.uv = v.positionOS.xy / max(_PatternTileSize.xy, float2(0.01,0.01)) + 0.5;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 pattern = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                // 原图透明处保留底色；笔触叠加只影响颜色，区域Alpha仍完全由Inspector控制。
                half3 color = lerp(_RegionColor.rgb, pattern.rgb, saturate(pattern.a * _PatternStrength));
                return half4(color, _RegionColor.a);
            }
            ENDHLSL
        }
    }
}
