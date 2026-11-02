Shader "UI/PostcardPageGrayFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillAmount ("fillAmount", Range(0, 1)) = 0
        _GrayAmount ("Gray Amount", Range(0, 1)) = 1
        
        _ShinyWidth ("shinyWidth", Float) = 0.5
        _ShinyOffset ("shinyOffset", Float) = 0
        _ShinyInstensity ("shinyInstensity", Float) = 0.5
        _ShinyColor ("shinyColor1", Color) = (1,1,1,1)

        _FillUVRect ("Fill UV Rect", Vector) = (0,0,1,1)

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
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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
            Name "PostcardPageGrayFill"

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
                float2 fillCoord     : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _FillAmount;
            float _GrayAmount;
            float _ShinyWidth;
            float _ShinyOffset;
            float _ShinyInstensity;
            fixed4 _ShinyColor;
            float4 _FillUVRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.fillCoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                half gray = dot(color.rgb, half3(0.299h, 0.587h, 0.114h));

                float fill = saturate(1 - _FillAmount);
                float fillU = saturate((IN.fillCoord.x - _FillUVRect.x) / max(_FillUVRect.z, 0.0001));
                half grayMask = step(fillU, fill) * step(0.0001, fill);
                color.rgb = lerp(color.rgb, half3(gray, gray, gray), grayMask * saturate(_GrayAmount));

                // shiny mask：从填充边界 + offset 开始向原色侧延伸 width，边界处最亮并渐变衰减，灰度区域不混合 shiny
                float shinyStart = fill + _ShinyOffset;
                float shinyDist = saturate(fillU - shinyStart);
                float shinyMask = smoothstep(_ShinyWidth, 0.0, shinyDist) * saturate(_ShinyInstensity);
                shinyMask *= (1.0 - grayMask);
                color.rgb = lerp(color.rgb, _ShinyColor.rgb, shinyMask);

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
