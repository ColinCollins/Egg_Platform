// Mask RenderTexture stamping shader (GPU).
// Reads previous mask and "max" blends a new stamp (circle or brush texture) into R channel.
Shader "Hidden/MaskRT/Stamp"
{
    Properties
    {
        _MainTex ("Prev Mask", 2D) = "black" {}
        _BrushTex ("Brush (A)", 2D) = "white" {}
        _Center ("Center UV", Vector) = (0.5,0.5,0,0)
        _Size ("Size UV", Float) = 0.05
        _Soft ("Soft UV", Float) = 0.005
        _Aspect ("Aspect", Float) = 1
        _UseBrush ("Use Brush", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BrushTex;
            float4 _Center;
            float _Size;
            float _Soft;
            float _Aspect;
            float _UseBrush;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float SmoothCircle(float2 uv, float2 center, float size, float soft, float aspect)
            {
                float2 p = uv;
                float2 c = center;
                p.x *= aspect;
                c.x *= aspect;
                float dist = length(p - c);
                float edge0 = max(size - soft, 0.0);
                float edge1 = size + soft;
                float t = saturate((dist - edge0) / max(edge1 - edge0, 1e-6));
                // smoothstep-like
                t = t * t * (3.0 - 2.0 * t);
                return 1.0 - t;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float prev = tex2D(_MainTex, i.uv).r;

                float stamp = 0.0;
                if (_UseBrush > 0.5)
                {
                    // Map uv to brush local (0..1) inside stamp square
                    float2 d = i.uv - _Center.xy;
                    d.x *= _Aspect;
                    float2 local = d / max(_Size, 1e-6) + 0.5;
                    if (local.x >= 0.0 && local.x <= 1.0 && local.y >= 0.0 && local.y <= 1.0)
                    {
                        float a = tex2D(_BrushTex, local).a;
                        // Soft edge around the stamp area
                        float circle = SmoothCircle(i.uv, _Center.xy, _Size * 0.5, _Soft, _Aspect);
                        stamp = a * circle;
                    }
                }
                else
                {
                    stamp = SmoothCircle(i.uv, _Center.xy, _Size * 0.5, _Soft, _Aspect);
                }

                float outv = max(prev, stamp);
                return fixed4(outv, outv, outv, 1.0);
            }
            ENDCG
        }
    }
}

