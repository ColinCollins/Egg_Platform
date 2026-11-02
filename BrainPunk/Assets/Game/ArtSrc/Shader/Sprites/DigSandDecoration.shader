// 装饰使用原始 Sprite/Atlas UV，挖洞遮罩使用世界位置映射到 Sand UV；两种坐标互不拉伸。
Shader "Dig/SandDecoration"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MaskTex ("Sand Mask (R=Erase)", 2D) = "black" {}
        _SandRect ("Sand Local Rect", Vector) = (-0.5,-0.5,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex DecorationVert
            #pragma fragment DecorationFrag
            #pragma target 2.0
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"

            sampler2D _MaskTex;
            float4 _SandRect;
            float4x4 _SandWorldToLocal;
            struct DecorationVaryings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 spriteUV : TEXCOORD0;
                float2 sandUV : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            DecorationVaryings DecorationVert(appdata_t input)
            {
                DecorationVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float4 position = UnityFlipSprite(input.vertex, _Flip);
                output.vertex = UnityObjectToClipPos(position);
                output.color = input.color * _Color * _RendererColor;
                output.spriteUV = input.texcoord;
                float2 sandLocal = mul(_SandWorldToLocal, mul(unity_ObjectToWorld, position)).xy;
                output.sandUV = (sandLocal - _SandRect.xy) / _SandRect.zw;
                return output;
            }
            fixed4 DecorationFrag(DecorationVaryings input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.spriteUV) * input.color;
                float inside = step(0, input.sandUV.x) * step(input.sandUV.x, 1) *
                    step(0, input.sandUV.y) * step(input.sandUV.y, 1);
                color.a *= inside * (1 - tex2D(_MaskTex, input.sandUV).r);
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
