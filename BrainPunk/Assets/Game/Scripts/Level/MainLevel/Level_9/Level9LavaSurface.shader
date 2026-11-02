Shader "BrainPunk/Level9/LavaSurface"
{
    Properties
    {
        _BodyColor ("岩浆主体（Alpha 控制深层不透明度）", Color) = (1, 0.29, 0.018, 0.98)
        _DeepColor ("深层红橙色", Color) = (0.92, 0.095, 0.015, 1)
        _HotColor ("缓慢流动的熔融纹路", Color) = (1, 0.67, 0.065, 1)
        _RimColor ("表面炽热亮边", Color) = (1, 0.79, 0.18, 1)
        _FlowSpeed ("内部热流速度", Range(0, 1)) = 0.12
        _PatternScale ("热流纹路密度", Range(0.1, 2)) = 0.45
        _SurfaceBand ("表层厚度占比", Range(0.01, 0.15)) = 0.055
        _WaveSpeed ("表面翻涌速度", Float) = 0.22
        _WaveFlowDirection ("翻涌方向", Float) = 1
        _WaveCount ("波浪密度", Float) = 1.45
        _WaveSize ("波浪高度", Float) = 0.13
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
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BodyColor, _DeepColor, _HotColor, _RimColor;
                float _FlowSpeed, _PatternScale, _SurfaceBand;
                float _WaveSpeed, _WaveFlowDirection, _WaveCount, _WaveSize;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                // 与原水面及 Level9LavaBubbles.SurfacePoint 保持同一位移公式。
                float phase = _Time.y * _WaveSpeed * _WaveFlowDirection;
                input.positionOS.y += sin((input.positionOS.x + phase) * _WaveCount) * _WaveSize * input.uv.y;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.positionWS = world.xy;
                output.uv = input.uv;
                return output;
            }

            // 连续的低频值噪声，只用于本关水体着色，不分配或加载额外纹理。
            float Hash(float2 coordinate)
            {
                float3 value = frac(float3(coordinate.x, coordinate.y, coordinate.x) * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float Noise(float2 coordinate)
            {
                float2 cell = floor(coordinate);
                float2 weight = frac(coordinate);
                weight = weight * weight * (3.0 - 2.0 * weight);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), weight.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), weight.x), weight.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _FlowSpeed;
                // 世界坐标使纹路不随 Water 的非等比缩放拉伸；不同流向缓慢扭动。
                float2 coordinate = input.positionWS * _PatternScale * float2(0.85, 1.8);
                float warp = Noise(coordinate * 0.65 + float2(time * 0.22, -time * 0.18));
                coordinate += float2(warp * 1.6 + time * 0.3, warp * 0.9 - time * 0.2);
                float broad = Noise(coordinate);
                float detail = Noise(coordinate * 1.9 + float2(-time * 0.25, 7.3));
                float flow = broad * 0.78 + detail * 0.22;
                float distanceToVein = abs(flow - 0.5);
                float aa = max(fwidth(flow), 0.003);
                float vein = 1.0 - smoothstep(0.025, 0.095 + aa, distanceToVein);

                float depth = saturate(1.0 - input.uv.y);
                half3 color = lerp(_BodyColor.rgb, _DeepColor.rgb, smoothstep(0.06, 0.8, depth));
                color = lerp(color, _HotColor.rgb, vein * (0.46 + 0.18 * broad));
                // 表层边缘厚薄不一，亮边下方保留暖橙色，避免普通水面的均匀色条。
                float band = _SurfaceBand * (0.7 + 0.6 * detail);
                float surface = 1.0 - smoothstep(band * 0.4, band, depth);
                float edge = 1.0 - smoothstep(band * 0.12, band * 0.38, depth);
                color = lerp(color, _HotColor.rgb, surface * 0.82);
                color = lerp(color, _RimColor.rgb, edge * (0.7 + broad * 0.3));
                // 水面在角色前方：浅层保留原透光度以看清黑化，往下逐渐被熔岩吞没。
                float alpha = lerp(min(0.7882353, _BodyColor.a), _BodyColor.a, smoothstep(0.04, 0.28, depth));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
