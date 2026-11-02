// Dig专用沙土显示：可调内外笔触描边，外扩仅改变显示覆盖，不修改挖洞遮罩。
Shader "Dig/Sand"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MaskTex ("Mask (R=Erase)", 2D) = "black" {}
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        _SpriteRectUV ("Sprite Rect UV (x,y,w,h)", Vector) = (0,0,1,1)
        _UseSpriteRectUV ("Use Sprite Rect UV", Float) = 0
        [HideInInspector] _SpriteLocalRect ("Sprite Local Rect", Vector) = (-0.5,-0.5,1,1)
        [HideInInspector] _UseSpriteSliced ("Use Sprite Sliced", Float) = 0
        [HideInInspector] _SpriteBorderUV ("Sprite Border UV", Vector) = (0,0,0,0)
        [HideInInspector] _SpriteBorderLocal ("Sprite Border Local", Vector) = (0,0,0,0)
        [Header(Sand Outline)]
        _OutlineColor ("Outline Color", Color) = (0.5137255,0.3215686,0.2,1)
        // 内外宽度独立调整；各自设0关闭，不再分配同一个总宽度。
        _InnerOutlineWidth ("Inner Outline Width (Screen Pixels)", Range(0,24)) = 3
        _OuterOutlineWidth ("Outer Outline Width (Screen Pixels)", Range(0,24)) = 5
        _OutlineSoftness ("Outline Softness", Range(0.01,1)) = 0.5
        [Header(Brush Noise)]
        _NoiseTex ("Noise Texture (R)", 2D) = "gray" {}
        _NoiseScale ("Noise Scale (World Space)", Range(0.1,30)) = 4
        _NoiseStrength ("Noise Width Variation", Range(0,1)) = 0.45
        _NoiseOpacity ("Noise Opacity Variation", Range(0,1)) = 0.2
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex, _MaskTex, _NoiseTex;
            fixed4 _Color, _OutlineColor;
            float4 _SpriteRectUV, _SpriteLocalRect, _NoiseTex_ST, _SpriteBorderUV, _SpriteBorderLocal;
            float _UseSpriteSliced;
            float _UseSpriteRectUV, _InnerOutlineWidth, _OuterOutlineWidth, _OutlineSoftness;
            float _NoiseScale, _NoiseStrength, _NoiseOpacity;

            v2f vert(appdata_t input)
            {
                v2f output;
                float4 rectUV = _UseSpriteRectUV > 0.5 ? _SpriteRectUV : float4(0,0,1,1);
                float2 localUV = (input.texcoord - rectUV.xy) / max(rectUV.zw, 1e-6);
                if (_UseSpriteSliced > 0.5)
                    localUV = (input.vertex.xy - _SpriteLocalRect.xy) / max(_SpriteLocalRect.zw, 1e-6);
                // 显示网格扩展到原Sprite之外；按投影像素换算局部位移并同步外推UV。
                float4 clip = UnityObjectToClipPos(input.vertex);
                float4 clipX = UnityObjectToClipPos(input.vertex + float4(1,0,0,0));
                float4 clipY = UnityObjectToClipPos(input.vertex + float4(0,1,0,0));
                float2 pixelsPerUnit = float2(
                    length((clipX.xy / clipX.w - clip.xy / clip.w) * _ScreenParams.xy * 0.5),
                    length((clipY.xy / clipY.w - clip.xy / clip.w) * _ScreenParams.xy * 0.5));
                float2 expansion = sign(localUV - 0.5) * (_OuterOutlineWidth * 1.6 + step(0.001, _OuterOutlineWidth)) / max(pixelsPerUnit, 1e-6);
                if (_UseSpriteSliced > 0.5)
                {
                    // 九宫格内部顶点连续扩展；片元按原矩形重采样，不沿切片UV移动洞口。
                    float2 padding = (_OuterOutlineWidth * 1.6 + step(0.001, _OuterOutlineWidth)) / max(pixelsPerUnit, 1e-6);
                    float2 center = _SpriteLocalRect.xy + _SpriteLocalRect.zw * 0.5;
                    input.vertex.xy = center + (input.vertex.xy - center) * (1 + 2 * padding / max(_SpriteLocalRect.zw, 1e-6));
                    localUV = (input.vertex.xy - _SpriteLocalRect.xy) / max(_SpriteLocalRect.zw, 1e-6);
                }
                else
                {
                    input.vertex.xy += expansion;
                    localUV += expansion / max(_SpriteLocalRect.zw, 1e-6);
                }
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.texcoord = rectUV.xy + localUV * rectUV.zw;
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xy;
                #ifdef PIXELSNAP_ON
                output.vertex = UnityPixelSnap(output.vertex);
                #endif
                return output;
            }

            // 边界外视作透明，并约束采样在本Sprite矩形内，避免邻接图集图片串入轮廓。
            float SliceAxis(float uv, float size, float left, float right, float sourceLeft, float sourceRight)
            {
                float factor = min(1, size / max(left + right, 1e-6));
                left *= factor;
                right *= factor;
                float position = uv * size;
                if (position < left)
                    return position / max(left, 1e-6) * sourceLeft;
                if (position > size - right)
                    return 1 - sourceRight + (position - size + right) / max(right, 1e-6) * sourceRight;
                return sourceLeft + (position - left) / max(size - left - right, 1e-6) * (1 - sourceLeft - sourceRight);
            }

            // 图片九宫格UV与线性遮罩UV分开，边框宽度按Border/PPU保留。
            float2 SpriteImageUV(float2 localUV, float4 rectUV)
            {
                float2 imageUV = saturate(localUV);
                if (_UseSpriteSliced > 0.5)
                {
                    imageUV.x = SliceAxis(imageUV.x, _SpriteLocalRect.z, _SpriteBorderLocal.x, _SpriteBorderLocal.z, _SpriteBorderUV.x, _SpriteBorderUV.z);
                    imageUV.y = SliceAxis(imageUV.y, _SpriteLocalRect.w, _SpriteBorderLocal.y, _SpriteBorderLocal.w, _SpriteBorderUV.y, _SpriteBorderUV.w);
                }
                return rectUV.xy + imageUV * rectUV.zw;
            }

            float SampleCoverage(float2 localUV, float4 rectUV)
            {
                float inside = step(0, localUV.x) * step(localUV.x, 1) * step(0, localUV.y) * step(localUV.y, 1);
                float2 safeUV = saturate(localUV);
                float spriteAlpha = tex2D(_MainTex, SpriteImageUV(safeUV, rectUV)).a;
                return inside * spriteAlpha * (1 - tex2D(_MaskTex, safeUV).r);
            }

            // 固定八方向采样；内侧取最小覆盖，外侧取最大覆盖，避免新增RT或CPU扫描。
            float SampleOutlineRing(float2 uv, float4 rectUV, float2 x, float2 y, float outer)
            {
                float result = SampleCoverage(uv + x, rectUV);
                float sample = SampleCoverage(uv - x, rectUV);
                result = lerp(min(result, sample), max(result, sample), outer);
                sample = SampleCoverage(uv + y, rectUV);
                result = lerp(min(result, sample), max(result, sample), outer);
                sample = SampleCoverage(uv - y, rectUV);
                result = lerp(min(result, sample), max(result, sample), outer);
                sample = SampleCoverage(uv + (x+y)*0.70710678, rectUV);
                result = lerp(min(result, sample), max(result, sample), outer);
                sample = SampleCoverage(uv - (x+y)*0.70710678, rectUV);
                result = lerp(min(result, sample), max(result, sample), outer);
                sample = SampleCoverage(uv + (x-y)*0.70710678, rectUV);
                result = lerp(min(result, sample), max(result, sample), outer);
                sample = SampleCoverage(uv - (x-y)*0.70710678, rectUV);
                return lerp(min(result, sample), max(result, sample), outer);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float4 rectUV = _UseSpriteRectUV > 0.5 ? _SpriteRectUV : float4(0,0,1,1);
                float2 localUV = (input.texcoord - rectUV.xy) / max(rectUV.zw, 1e-6);
                fixed4 color = tex2D(_MainTex, SpriteImageUV(localUV, rectUV)) * input.color;
                float center = SampleCoverage(localUV, rectUV);
                float coverage = center * input.color.a;
                // 采用屏幕导数，非等比Sand和不同遮罩分辨率下保持横纵描边宽度一致。
                float2 pixelX = ddx(localUV);
                float2 pixelY = ddy(localUV);
                float outerAlpha = 0;
                if (_InnerOutlineWidth > 0 || _OuterOutlineWidth > 0)
                {
                    float2 noiseUV = input.worldPosition * _NoiseScale * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
                    float noise = tex2D(_NoiseTex, noiseUV).r;
                    float widthVariation = lerp(1, lerp(0.4,1.6,noise), _NoiseStrength);
                    float opacity = _OutlineColor.a * lerp(1, noise, _NoiseOpacity);
                    if (_InnerOutlineWidth > 0 && center > 0)
                    {
                        float innerWidth = _InnerOutlineWidth * widthVariation;
                        float neighbor = SampleOutlineRing(localUV, rectUV, pixelX*innerWidth, pixelY*innerWidth, 0);
                        float edge = saturate((center - neighbor) / max(center, 1e-6));
                        edge = smoothstep(0, max(_OutlineSoftness, 0.01), edge);
                        color.rgb = lerp(color.rgb, _OutlineColor.rgb, saturate(edge * opacity));
                    }
                    if (_OuterOutlineWidth > 0)
                    {
                        float outerWidth = _OuterOutlineWidth * widthVariation;
                        float neighbor = SampleOutlineRing(localUV, rectUV, pixelX*outerWidth, pixelY*outerWidth, 1);
                        float edge = smoothstep(0, max(_OutlineSoftness, 0.01), saturate(neighbor - center));
                        outerAlpha = saturate(edge * opacity * input.color.a);
                    }
                }
                color.rgb = color.rgb * coverage + _OutlineColor.rgb * outerAlpha * (1 - coverage);
                color.a = coverage + outerAlpha * (1 - coverage);
                return color;
            }
            ENDCG
        }
    }
}
