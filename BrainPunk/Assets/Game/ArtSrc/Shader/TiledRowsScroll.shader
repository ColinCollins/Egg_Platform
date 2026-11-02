Shader "Custom/TiledRowsScroll"
{
    Properties
    {
        _Tex0 ("Row 0 图片", 2D) = "white" {}
        _Tex1 ("Row 1 图片", 2D) = "white" {}
        _Tex2 ("Row 2 图片", 2D) = "white" {}
        _Tex3 ("Row 3 图片", 2D) = "white" {}
        _Tex4 ("Row 4 图片", 2D) = "white" {}
        _Tex5 ("Row 5 图片", 2D) = "white" {}
        _Tex6 ("Row 6 图片", 2D) = "white" {}
        _Tex7 ("Row 7 图片", 2D) = "white" {}
        _RowCount ("使用的行数 (1~8)", Range(1, 8)) = 4
        _SpacingX ("横排内 图与图间距", Range(0, 2)) = 0.1
        _SpacingY ("横排与横排间距", Range(0, 2)) = 0.1
        _RowStagger ("横排错开 (0=不错开 0.5=错半格)", Range(0, 1)) = 0.5
        _TilingScaleX ("横向平铺密度", Float) = 8
        _TilingScaleY ("纵向平铺密度", Float) = 8
        _ScrollSpeed ("滚动偏移速度", Float) = 0
        _ScrollDir ("UV移动方向 (X右Y上)", Vector) = (0, 1, 0, 0)
        _Color ("整体染色", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        LOD 100

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color     : COLOR;
                float2 uv        : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            TEXTURE2D(_Tex0); SAMPLER(sampler_Tex0);
            TEXTURE2D(_Tex1); SAMPLER(sampler_Tex1);
            TEXTURE2D(_Tex2); SAMPLER(sampler_Tex2);
            TEXTURE2D(_Tex3); SAMPLER(sampler_Tex3);
            TEXTURE2D(_Tex4); SAMPLER(sampler_Tex4);
            TEXTURE2D(_Tex5); SAMPLER(sampler_Tex5);
            TEXTURE2D(_Tex6); SAMPLER(sampler_Tex6);
            TEXTURE2D(_Tex7); SAMPLER(sampler_Tex7);
            float _RowCount;
            float _SpacingX;
            float _SpacingY;
            float _RowStagger;
            float _TilingScaleX;
            float _TilingScaleY;
            float _ScrollSpeed;
            float2 _ScrollDir;
            float4 _Color;
            float4 _ClipRect;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.worldPosition = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half UnityGet2DClipping(float2 position, float4 clipRect)
            {
                float2 size = clipRect.zw - clipRect.xy;
                float2 center = (clipRect.xy + clipRect.zw) * 0.5;
                float2 m = saturate((size - abs(position - center)) * 2 / (size + 1e-5));
                return min(m.x, m.y);
            }

            half4 SampleRow(int index, float2 uv)
            {
                if (index == 0) return SAMPLE_TEXTURE2D(_Tex0, sampler_Tex0, uv);
                if (index == 1) return SAMPLE_TEXTURE2D(_Tex1, sampler_Tex1, uv);
                if (index == 2) return SAMPLE_TEXTURE2D(_Tex2, sampler_Tex2, uv);
                if (index == 3) return SAMPLE_TEXTURE2D(_Tex3, sampler_Tex3, uv);
                if (index == 4) return SAMPLE_TEXTURE2D(_Tex4, sampler_Tex4, uv);
                if (index == 5) return SAMPLE_TEXTURE2D(_Tex5, sampler_Tex5, uv);
                if (index == 6) return SAMPLE_TEXTURE2D(_Tex6, sampler_Tex6, uv);
                return SAMPLE_TEXTURE2D(_Tex7, sampler_Tex7, uv);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float periodX = 1.0 + _SpacingX;
                float periodY = 1.0 + _SpacingY;
                float posX = input.uv.x * _TilingScaleX + _Time.y * _ScrollSpeed * _ScrollDir.x;
                float posY = input.uv.y * _TilingScaleY + _Time.y * _ScrollSpeed * _ScrollDir.y;

                float row = floor(posY / periodY);
                int rowParity = (int)row % 2;
                if (rowParity < 0) rowParity += 2;
                posX += _RowStagger * periodX * (float)rowParity;
                float localX = frac(posX / periodX);
                float localY = frac(posY / periodY);

                float tileMaxX = 1.0 / periodX;
                float tileMaxY = 1.0 / periodY;
                if (localX > tileMaxX || localY > tileMaxY)
                {
                    half4 emptyColor = half4(0, 0, 0, 0);
                    #ifdef UNITY_UI_CLIP_RECT
                        emptyColor.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                    #endif
                    return emptyColor;
                }

                float tileU = localX * periodX;
                float tileV = localY * periodY;
                int imageIndex = (int)row % (int)_RowCount;
                if (imageIndex < 0) imageIndex += (int)_RowCount;

                half4 col_sample = SampleRow(imageIndex, float2(tileU, 1.0 - tileV)) * input.color;

                #ifdef UNITY_UI_CLIP_RECT
                    col_sample.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(col_sample.a - 0.001);
                #endif

                return col_sample;
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "TiledRowsScroll"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _Tex0, _Tex1, _Tex2, _Tex3, _Tex4, _Tex5, _Tex6, _Tex7;
            float _RowCount;
            float _SpacingX;
            float _SpacingY;
            float _RowStagger;
            float _TilingScaleX;
            float _TilingScaleY;
            float _ScrollSpeed;
            float4 _ScrollDir;
            float4 _Color;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 sampleRow(int idx, float2 uv)
            {
                if (idx == 0) return tex2D(_Tex0, uv);
                if (idx == 1) return tex2D(_Tex1, uv);
                if (idx == 2) return tex2D(_Tex2, uv);
                if (idx == 3) return tex2D(_Tex3, uv);
                if (idx == 4) return tex2D(_Tex4, uv);
                if (idx == 5) return tex2D(_Tex5, uv);
                if (idx == 6) return tex2D(_Tex6, uv);
                return tex2D(_Tex7, uv);
            }
            fixed4 frag(v2f IN) : SV_Target
            {
                float periodX = 1.0 + _SpacingX;
                float periodY = 1.0 + _SpacingY;
                float scrollOffset = _ScrollSpeed * _Time.y;
                float posX = IN.texcoord.x * _TilingScaleX + scrollOffset * _ScrollDir.x;
                float posY = IN.texcoord.y * _TilingScaleY + scrollOffset * _ScrollDir.y;
                float row = floor(posY / periodY);
                int rowParity = (int)row % 2;
                if (rowParity < 0) rowParity += 2;
                posX += _RowStagger * periodX * (float)rowParity;
                float localX = frac(posX / periodX);
                float localY = frac(posY / periodY);
                float tileMaxX = 1.0 / periodX;
                float tileMaxY = 1.0 / periodY;
                if (localX > tileMaxX || localY > tileMaxY)
                {
                    half4 color = half4(0, 0, 0, 0);
                    #ifdef UNITY_UI_CLIP_RECT
                        color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                    #endif
                    return color;
                }
                float tileU = localX * periodX;
                float tileV = localY * periodY;
                int imageIndex = (int)row % (int)_RowCount;
                if (imageIndex < 0) imageIndex += (int)_RowCount;
                half4 color = sampleRow(imageIndex, float2(tileU, 1.0 - tileV)) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
