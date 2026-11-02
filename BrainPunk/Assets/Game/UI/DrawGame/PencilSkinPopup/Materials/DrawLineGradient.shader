Shader "BrainPunk/DrawGame/LineGradient"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Base Texture", 2D) = "white" {}
        _BaseRepeat ("Base Repeat", Float) = 1
        _BaseSegmentSpacing ("Base Segment Spacing (Negative = Overlap)", Float) = 0
        _BaseRotation ("Base Rotation (Degrees)", Range(-180, 180)) = 0
        _BaseCutoff ("Base Alpha Cutoff", Range(0, 1)) = 0.5
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct VertexToFragment
            {
                float4 vertex : SV_POSITION;
                float2 lineUv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float _BaseRepeat;
            float _BaseSegmentSpacing;
            float _BaseRotation;
            fixed _BaseCutoff;
            fixed4 _Color;

            float SampleBaseStampAlpha(
                float stampDistance,
                float textureLength,
                float lineV,
                float rotationSin,
                float rotationCos)
            {
                float2 centeredUv = float2(
                    stampDistance / textureLength,
                    lineV) - 0.5;
                float2 rotatedUv = float2(
                    centeredUv.x * rotationCos - centeredUv.y * rotationSin,
                    centeredUv.x * rotationSin + centeredUv.y * rotationCos)
                    + 0.5;
                float insideTexture =
                    step(0, rotatedUv.x) * step(rotatedUv.x, 1)
                    * step(0, rotatedUv.y) * step(rotatedUv.y, 1);
                float stampAlpha = tex2D(
                    _MainTex,
                    saturate(rotatedUv)).a;
                return stampAlpha
                    * insideTexture
                    * step(stampDistance, textureLength);
            }

            VertexToFragment Vert(AppData input)
            {
                VertexToFragment output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.lineUv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Frag(VertexToFragment input) : SV_Target
            {
                float baseRepeat = max(_BaseRepeat, 0.0001);
                float baseTextureLength = rcp(baseRepeat);
                float minimumBaseSpacing = -baseTextureLength * 0.75;
                float baseSpacing = max(
                    _BaseSegmentSpacing,
                    minimumBaseSpacing);
                float baseCycleLength = baseTextureLength + baseSpacing;
                float baseCycleDistance = frac(
                    input.lineUv.x / baseCycleLength)
                    * baseCycleLength;
                float rotationSin;
                float rotationCos;
                sincos(
                    radians(_BaseRotation),
                    rotationSin,
                    rotationCos);

                float baseMask = SampleBaseStampAlpha(
                    baseCycleDistance,
                    baseTextureLength,
                    input.lineUv.y,
                    rotationSin,
                    rotationCos);
                if (baseSpacing < 0)
                {
                    baseMask = max(
                        baseMask,
                        SampleBaseStampAlpha(
                            baseCycleDistance + baseCycleLength * 3,
                            baseTextureLength,
                            input.lineUv.y,
                            rotationSin,
                            rotationCos));
                    baseMask = max(
                        baseMask,
                        SampleBaseStampAlpha(
                            baseCycleDistance + baseCycleLength * 2,
                            baseTextureLength,
                            input.lineUv.y,
                            rotationSin,
                            rotationCos));
                    baseMask = max(
                        baseMask,
                        SampleBaseStampAlpha(
                            baseCycleDistance + baseCycleLength,
                            baseTextureLength,
                            input.lineUv.y,
                            rotationSin,
                            rotationCos));
                }

                float baseCutoff = saturate(_BaseCutoff);
                float baseEdgeWidth = max(
                    fwidth(baseMask) * 0.5,
                    0.0001);
                float baseCoverage = smoothstep(
                    baseCutoff - baseEdgeWidth,
                    baseCutoff + baseEdgeWidth,
                    baseMask);
                float lineEdgeDistance = min(
                    input.lineUv.y,
                    1 - input.lineUv.y);
                float lineEdgeWidth = max(
                    fwidth(input.lineUv.y),
                    0.0001);
                float lineEdgeCoverage = smoothstep(
                    0,
                    lineEdgeWidth,
                    lineEdgeDistance);
                fixed4 baseColor = input.color;
                baseColor.a *=
                    baseMask * baseCoverage * lineEdgeCoverage;
                clip(baseColor.a - 0.001);
                return baseColor;
            }
            ENDCG
        }
    }
}
