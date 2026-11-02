Shader "UI/PageCurl"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _Angle ("Angle (Degrees)", Range(-180, 180)) = 45
        _Distance ("Fold Distance", Range(-1.5, 1.5)) = 0
        _Radius ("Cylinder Radius", Range(0.01, 1.0)) = 0.1
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.5
        
        _BackColor ("Back Color", Color) = (0.9, 0.9, 0.9, 1)
        _BackTex ("Back Texture", 2D) = "white" {}
        _EnableKey ("Enable Back Texture", Float) = 0
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        
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
            Name "Default"
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
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            sampler2D _BackTex;
            float4 _BackTex_ST;
            
            float _Angle;
            float _Distance;
            float _Radius;
            float _ShadowStrength;
            float _EnableKey;
            fixed4 _BackColor;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                
                // Convert angle to direction
                float rad = _Angle * 0.0174532925; // PI / 180
                float2 dir = float2(cos(rad), sin(rad));
                
                // Calculate signed distance from the fold line
                // dot 获取方向上的投影距离
                float d = dot(uv, dir) - _Distance;
                
                // Base color (Flat page)
                half4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;
                
                // If d > 0, we are on the peeled side (void)
                if (d > 0)
                {
                    return fixed4(0,0,0,0);
                }
                
                // Check curl overlap
                // The curl covers region [-Radius, 0] relative to fold line
                if (d > -_Radius)
                {
                    // Map current pixel to the back-face source UV.
                    // t: [0,1], 0 on crease, 1 at far edge of curl band.
                    float safeRadius = max(_Radius, 1e-5);
                    float t = saturate(-d / safeRadius);

                    // Angle and arc length on cylinder from crease.
                    float theta = asin(t);          // [0, PI/2]
                    float arcLen = safeRadius * theta;

                    // Fold line point (d = 0), then move along peeled direction by arc length.
                    float2 foldPos = uv - d * dir;
                    float2 p_src = foldPos + arcLen * dir;

                    // Check validity of source UV
                    if (p_src.x >= 0 && p_src.x <= 1 && p_src.y >= 0 && p_src.y <= 1)
                    {
                        // Sample backface texture:
                        // _EnableKey <= 0.5 : use _MainTex
                        // _EnableKey >  0.5 : use _BackTex
                        fixed4 mainBackTex = tex2D(_MainTex, p_src);
                        fixed4 customBackTex = tex2D(_BackTex, p_src);
                        fixed useBackTex = step(0.5, _EnableKey);
                        fixed4 backTex = lerp(mainBackTex, customBackTex, useBackTex) * _BackColor;
                        
                        // Stronger shadow near fold line, weaker at far edge of curl band.
                        float shadow = pow(1.0 - t, 2) * _ShadowStrength; 
                        backTex.rgb -= shadow;
                        
                        return backTex;
                    }
                }
                
                // Apply UI Clipping
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
