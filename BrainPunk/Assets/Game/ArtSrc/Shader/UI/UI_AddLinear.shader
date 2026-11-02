// UI Shader: MainTex + Rotatable Line Drawing (ref: UITilling + TxtlineRender)
Shader "UI/UI_AddLinear"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Rotate ("Rotate", Float) = 0
        _Tilling ("TillingOffset (XY:Scale ZW:Offset)", Vector) = (1,1,0,0)

        [Header(Line Settings)]
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
        _LineWidth ("Line Width", Float) = 2.0
        _LineSpacing ("Line Spacing", Float) = 6.0
        _BlendIntensity ("Line Blend Intensity", Range(0, 1)) = 1.0
        _MoveSpeed ("Line Move Speed", Float) = 0.0

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
            Name "UI_AddLinear"
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

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float4 _Tilling;
            float _Rotate;
            float4 _LineColor;
            float _LineWidth;
            float _LineSpacing;
            float _BlendIntensity;
            float _MoveSpeed;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = _Tilling.xy * v.texcoord.xy + _Tilling.zw;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // Rotate UV (lines rotate with it)
                if (_Rotate != 0)
                {
                    float angle = 3.1415926 / 180.0 * _Rotate;
                    float2 temp_uv = (uv - _Tilling.zw) / _Tilling.xy - float2(0.5, 0.5);
                    uv.x = temp_uv.x * cos(angle) + temp_uv.y * sin(angle);
                    uv.y = -temp_uv.x * sin(angle) + temp_uv.y * cos(angle);
                    uv = (uv + float2(0.5, 0.5)) * _Tilling.xy + _Tilling.zw;
                }

                // Sample MainTex with transformed UV
                half4 sourceColor = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;

                // Line drawing (ref: TxtlineRender, horizontal lines in UV space)
                float lineWidth = max(_LineWidth / 1000.0, 0.0);
                float spacing = max(_LineSpacing / 1000.0, 0.0);
                float period = max(lineWidth + spacing, 0.0001);

                // 根据时间和 _MoveSpeed 让线段在水平方向移动
                float uvV = uv.x + _Time.y * _MoveSpeed;
                float normalizedV = uvV / period;
                float t = frac(normalizedV) * period;
                float lineMask = step(t, lineWidth);

                half lineBlendFactor = lineMask * _BlendIntensity;
                half3 lineBlendedColor = lerp(sourceColor.rgb, _LineColor.rgb, lineBlendFactor * _LineColor.a);

                half4 color = half4(lineBlendedColor, sourceColor.a);

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
}
