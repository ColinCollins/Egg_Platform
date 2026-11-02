Shader "Game/Level140/CustomerClip"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Main Texture", 2D) = "white" {}
        [Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Texture", Int) = 0
        _Level140ClipY ("World Surface Y", Float) = -10000
        [HideInInspector] _StencilRef ("Stencil Reference", Float) = 1
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass Keep }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _STRAIGHT_ALPHA_INPUT
            #include "UnityCG.cginc"
            #include "Assets/Spine/Runtime/spine-unity/Shaders/CGIncludes/Spine-Common.cginc"
            sampler2D _MainTex;
            float _Level140ClipY;
            struct Input
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Output
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float worldY : TEXCOORD1;
                float4 color : COLOR;
            };
            Output vert(Input input)
            {
                Output output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldY = mul(unity_ObjectToWorld, input.vertex).y;
                output.uv = input.uv;
                output.color = PMAGammaToTargetSpace(input.color);
                return output;
            }
            float4 frag(Output input) : SV_Target
            {
                clip(input.worldY - _Level140ClipY);
                float4 color = tex2D(_MainTex, input.uv);
                #if defined(_STRAIGHT_ALPHA_INPUT)
                color.rgb *= color.a;
                #endif
                return color * input.color;
            }
            ENDCG
        }
    }
}
