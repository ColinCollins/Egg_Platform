Shader "Game/Level140/SeatLights"
{
    Properties
    {
        _Opacity ("Effect Opacity", Range(0,1)) = 0
        _Darkness ("Outside Darkness", Range(0,1)) = 0.4
        _Glow ("Inside Glow", Range(0,1)) = 0.08
        _BeamStrength ("Beam Strength", Range(0,1)) = 1
        _SeatCenters ("Four World Seat X Coordinates", Vector) = (-7,-4,-1,2)
        _BeamTopY ("Beam Top Y", Float) = 6
        _BeamBottomY ("Beam Bottom Y", Float) = -2.3
        _TopHalfWidth ("Top Half Width", Float) = 0.28
        _BottomHalfWidth ("Bottom Half Width", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Opacity, _Darkness, _Glow, _BeamStrength, _BeamTopY, _BeamBottomY, _TopHalfWidth, _BottomHalfWidth;
            float4 _SeatCenters;
            struct Output { float4 position : SV_POSITION; float2 world : TEXCOORD0; };
            Output vert(float4 vertex : POSITION)
            {
                Output output;
                output.position = UnityObjectToClipPos(vertex);
                output.world = mul(unity_ObjectToWorld, vertex).xy;
                return output;
            }
            float4 frag(Output input) : SV_Target
            {
                float depth = saturate((_BeamTopY - input.world.y) / max(0.001, _BeamTopY - _BeamBottomY));
                float width = lerp(_TopHalfWidth, _BottomHalfWidth, depth);
                float4 distances = abs(input.world.x - _SeatCenters);
                float nearest = min(min(distances.x, distances.y), min(distances.z, distances.w));
                float light = 1 - smoothstep(width - 0.035, width + 0.035, nearest);
                light *= step(_BeamBottomY, input.world.y) * step(input.world.y, _BeamTopY);
                // 只闪烁四束灯光，外侧遮罩不跟着整屏明暗跳动。
                light *= _BeamStrength;
                float opacity = lerp(_Darkness, _Glow, light) * _Opacity;
                return float4(light, light, light, opacity);
            }
            ENDCG
        }
    }
}
