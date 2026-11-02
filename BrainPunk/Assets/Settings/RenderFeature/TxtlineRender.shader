Shader "Game/TxtlineRender"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Blend Settings)]
        [Enum(Normal,0,Multiply,1,Screen,2,Overlay,3,Add,4,Subtract,5,Darken,6,Lighten,7,SoftLight,8,HardLight,9)]
        _BlendMode("Blend Mode", Int) = 0

        
        [Header(UV Settings)]
        _UVParams("UV Scale (XY) Offset (ZW)", Vector) = (1, 1, 0, 0)
        _MaskIntensity("Mask Intensity", Range(0, 1)) = 1.0
        
        [Header(Line Settings)]
        [Enum(Normal,0,Multiply,1,Screen,2,Overlay,3,Add,4,Subtract,5,Darken,6,Lighten,7,SoftLight,8,HardLight,9)]
        _LineBlendMode("Line Blend Mode", Int) = 0
        _BlendIntensity("Blend Intensity", Range(0, 1)) = 1.0
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
        _LineWidth ("Line Width", Float) = 2.0
        _LineSpacing ("Line Period", Float) = 6.0
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
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend DstColor Zero
        
        Pass
        {
            Name "TxtlineRender"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _Color;
            float4 _UVParams; // xy: Scale, zw: Offset
            float _MaskIntensity;
            int _LineBlendMode;
            float _BlendIntensity;
            float4 _LineColor;
            float _LineWidth;
            float _LineSpacing;

            // 混合模式函数
            half3 BlendNormal(half3 base, half3 blend, half opacity)
            {
                return lerp(base, blend, opacity);
            }

            half3 BlendMultiply(half3 base, half3 blend, half opacity)
            {
                half3 result = base * blend;
                return lerp(base, result, opacity);
            }

            half3 BlendScreen(half3 base, half3 blend, half opacity)
            {
                half3 result = 1.0 - (1.0 - base) * (1.0 - blend);
                return lerp(base, result, opacity);
            }

            half3 BlendOverlay(half3 base, half3 blend, half opacity)
            {
                half3 result = base < 0.5 ? 2.0 * base * blend : 1.0 - 2.0 * (1.0 - base) * (1.0 - blend);
                return lerp(base, result, opacity);
            }

            half3 BlendAdd(half3 base, half3 blend, half opacity)
            {
                half3 result = base + blend;
                return lerp(base, result, opacity);
            }

            half3 BlendSubtract(half3 base, half3 blend, half opacity)
            {
                half3 result = base - blend;
                return lerp(base, result, opacity);
            }

            half3 BlendDarken(half3 base, half3 blend, half opacity)
            {
                half3 result = min(base, blend);
                return lerp(base, result, opacity);
            }

            half3 BlendLighten(half3 base, half3 blend, half opacity)
            {
                half3 result = max(base, blend);
                return lerp(base, result, opacity);
            }

            half3 BlendSoftLight(half3 base, half3 blend, half opacity)
            {
                half3 result = base < 0.5 ? 
                    2.0 * base * blend + base * base * (1.0 - 2.0 * blend) :
                    2.0 * base * (1.0 - blend) + sqrt(base) * (2.0 * blend - 1.0);
                return lerp(base, result, opacity);
            }

            half3 BlendHardLight(half3 base, half3 blend, half opacity)
            {
                half3 result = blend < 0.5 ? 
                    2.0 * base * blend :
                    1.0 - 2.0 * (1.0 - base) * (1.0 - blend);
                return lerp(base, result, opacity);
            }

            half3 ApplyBlendMode(half3 base, half3 blend, half opacity, int mode)
            {
                switch(mode)
                {
                    case 1: return BlendMultiply(base, blend, opacity);
                    case 2: return BlendScreen(base, blend, opacity);
                    case 3: return BlendOverlay(base, blend, opacity);
                    case 4: return BlendAdd(base, blend, opacity);
                    case 5: return BlendSubtract(base, blend, opacity);
                    case 6: return BlendDarken(base, blend, opacity);
                    case 7: return BlendLighten(base, blend, opacity);
                    case 8: return BlendSoftLight(base, blend, opacity);
                    case 9: return BlendHardLight(base, blend, opacity);
                    default: return BlendNormal(base, blend, opacity);
                }
            }

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
                
                // 对象空间到世界空间
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                // 世界空间到裁剪空间
                output.positionCS = TransformWorldToHClip(positionWS);
                
                // UV 坐标
                output.uv = input.uv;
                
                // 颜色
                output.color = input.color * _Color;
                
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 使用 Vector 控制 sourceColor 的 UV (xy: Scale, zw: Offset)
                float2 sourceUV = input.uv;
                float2 lineUV = input.uv * _UVParams.xy + _UVParams.zw;
                
                // 采样主纹理
                half4 sourceColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sourceUV);
                sourceColor *= input.color;
                
                // 基于 UV V 坐标计算横线位置（从 0 到 1 不断绘制）
                float lineWidth = max(_LineWidth / 1000.0, 0.0);
                float period = max(_LineSpacing / 1000.0, 0.0001);

                // 使用 UV V 坐标（0 到 1）计算横线
                // 将 UV V 坐标映射到周期范围内
                float uvV = lineUV.y;
                float phase = frac(uvV / period) * period;
                
                // 判断是否在横线区域内
                float feather = max(fwidth(uvV) * 0.5, 0.00001);
                feather = min(feather, period * 0.5);
                float safeLineWidth = min(lineWidth, max(period - feather * 2.0, 0.0));
                float edge0 = max(min(feather, safeLineWidth * 0.5), 0.00001);
                float edge1 = max(safeLineWidth - feather, edge0 + 0.00001);
                float edge2 = max(safeLineWidth, edge1 + 0.00001);
                float startFade = smoothstep(0.0, edge0, phase);
                float endFade = 1.0 - smoothstep(edge1, edge2, phase);
                float lineMask = startFade * endFade * step(feather, safeLineWidth);
                
                // 叠加线段颜色（不修改 alpha）
                half4 lineColor = _LineColor;
                half lineBlendFactor = lineMask * _BlendIntensity;
                half3 lineBlendedColor = ApplyBlendMode(sourceColor.rgb, lineColor.rgb, lineBlendFactor, _LineBlendMode);

                lineBlendedColor = pow(lineBlendedColor, _MaskIntensity);
                
                // 最终结果：保持源颜色的 alpha
                return half4(lineBlendedColor, sourceColor.a);
            }
            ENDHLSL
        }
    }
    
    Fallback Off
}
