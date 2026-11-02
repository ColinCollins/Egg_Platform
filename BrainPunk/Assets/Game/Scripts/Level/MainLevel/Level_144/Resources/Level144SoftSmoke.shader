// 本关烟雾纹理为 RGB 灰度图；用亮度作透明度，黑色边缘必须完全透明。
Shader "Game/Level144/SoftSmoke"
{
    Properties
    {
        _MainTex ("Smoke density (RGB)", 2D) = "black" {}
        _Density ("Density multiplier", Range(0, 4)) = 2.3
        _Color ("Smoke tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Density;
            fixed4 _Color;
            struct VertexInput { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct VertexOutput { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            VertexOutput vert(VertexInput input)
            {
                VertexOutput output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            fixed4 frag(VertexOutput input) : SV_Target
            {
                fixed density = saturate(tex2D(_MainTex, input.uv).r * _Density);
                return fixed4(input.color.rgb, input.color.a * density);
            }
            ENDCG
        }
    }
}
