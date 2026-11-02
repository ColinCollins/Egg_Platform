// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

Shader "UI/UITilling"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Rotate ("Rotate", Float) = 0
        _Tilling ("TillingOffset", Vector) = (1,1,0,0)
        _SpacingX ("横排内 图与图间距", Range(0, 2)) = 0
        _SpacingY ("横排与横排间距", Range(0, 2)) = 0
        _RowStagger ("横排错开 (0=不错开 0.5=错半格)", Range(0, 1)) = 0

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
            Name "UITilling"
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
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
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
            float _SpacingX;
            float _SpacingY;
            float _RowStagger;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = _Tilling.xy * v.texcoord.xy + _Tilling.zw; //TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                if (_Rotate != 0) 
                {
                    float angle = 3.1415926 / 180 * _Rotate;
                    float2 temp_uv = (IN.texcoord - _Tilling.zw) / _Tilling.xy - float2(0.5,0.5);
                    IN.texcoord.x = temp_uv.x * cos(angle) + temp_uv.y * sin(angle);
                    IN.texcoord.y = -temp_uv.x * sin(angle) + temp_uv.y  * cos(angle);
                    IN.texcoord = (IN.texcoord + float2(0.5,0.5)) * _Tilling.xy + _Tilling.zw;
                }

                float2 uv = IN.texcoord;
                if (_SpacingX != 0 || _SpacingY != 0 || _RowStagger != 0)
                {
                    float periodX = 1.0 + _SpacingX;
                    float periodY = 1.0 + _SpacingY;
                    float posX = uv.x;
                    float posY = uv.y;
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
                    uv = float2(localX * periodX, localY * periodY);
                }

                half4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}