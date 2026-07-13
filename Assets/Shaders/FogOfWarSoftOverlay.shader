Shader "CierzoArena/Fog Of War Soft Overlay"
{
    Properties
    {
        _FromMask ("Previous vision mask", 2D) = "black" {}
        _ToMask ("Target vision mask", 2D) = "black" {}
        _FogColor ("Fog color", Color) = (0.018, 0.04, 0.075, 1)
        _FogEdgeSoftness ("Fog edge softness (world)", Float) = 3
        _WorldSize ("World size", Float) = 172
        _FogExploredAlpha ("Explored alpha", Float) = .64
        _FogUnexploredAlpha ("Unexplored alpha", Float) = .82
        _Transition ("Transition", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FromMask;
            sampler2D _ToMask;
            float4 _FogColor;
            float _FogEdgeSoftness;
            float _WorldSize;
            float _FogExploredAlpha;
            float _FogUnexploredAlpha;
            float _Transition;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float2 SampleMask(float2 uv)
            {
                float t = smoothstep(0.0, 1.0, _Transition);
                float2 from = tex2D(_FromMask, uv).rg;
                float2 to = tex2D(_ToMask, uv).rg;
                return lerp(from, to, t);
            }

            float SampleSoftVisibility(float2 uv)
            {
                float radius = max(_FogEdgeSoftness / max(_WorldSize, .001), .0001);
                float center = SampleMask(uv).r * 4.0;
                float cardinal = SampleMask(uv + float2(radius, 0)).r
                    + SampleMask(uv + float2(-radius, 0)).r
                    + SampleMask(uv + float2(0, radius)).r
                    + SampleMask(uv + float2(0, -radius)).r;
                float diagonal = SampleMask(uv + float2(radius, radius)).r
                    + SampleMask(uv + float2(radius, -radius)).r
                    + SampleMask(uv + float2(-radius, radius)).r
                    + SampleMask(uv + float2(-radius, -radius)).r;
                return smoothstep(.16, .84, (center + cardinal + diagonal * .6) / 10.4);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float visibility = SampleSoftVisibility(input.uv);
                float explored = SampleMask(input.uv).g;
                fixed4 output = _FogColor;
                output.a = lerp(_FogUnexploredAlpha, _FogExploredAlpha, explored) * (1.0 - visibility);
                return output;
            }
            ENDCG
        }
    }
}
