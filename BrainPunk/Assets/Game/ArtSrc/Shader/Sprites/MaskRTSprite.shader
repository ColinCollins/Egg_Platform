// 采样 _MaskTex 做镂空（SpriteRenderer 专用）。
// _MaskTex 使用 Sprite UV（texcoord）采样，避免 UI/MaskRT 的 screenUV 在场景中错位。
Shader "Sprites/MaskRTSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MaskTex ("Mask (R=Erase)", 2D) = "black" {}
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        // When Sprite uses atlas/textureRect, Sprite mesh UV is in atlas space.
        // Provide rect in UV space (x,y,w,h) to remap atlas UV -> local 0..1 for mask sampling.
        _SpriteRectUV ("Sprite Rect UV (x,y,w,h)", Vector) = (0,0,1,1)
        _UseSpriteRectUV ("Use Sprite Rect UV", Float) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile _ PIXELSNAP_ON

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            fixed4 _Color;
            float4 _SpriteRectUV;
            float _UseSpriteRectUV;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap(o.vertex);
                #endif
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.texcoord) * i.color;
                float2 maskUV = i.texcoord;
                if (_UseSpriteRectUV > 0.5)
                {
                    // atlas uv -> local uv
                    maskUV = (maskUV - _SpriteRectUV.xy) / max(_SpriteRectUV.zw, 1e-6);
                }
                fixed mask = tex2D(_MaskTex, maskUV).r;
                c.a *= (1.0 - mask);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}

