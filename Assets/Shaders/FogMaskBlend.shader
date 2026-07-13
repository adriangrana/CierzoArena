Shader "Hidden/CierzoArena/Fog Mask Blend"
{
    Properties
    {
        _FromMask ("From", 2D) = "black" {}
        _ToMask ("To", 2D) = "black" {}
        _Blend ("Blend", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _FromMask;
            sampler2D _ToMask;
            float _Blend;
            fixed4 frag(v2f_img input) : SV_Target
            {
                return lerp(tex2D(_FromMask, input.uv), tex2D(_ToMask, input.uv), _Blend);
            }
            ENDCG
        }
    }
}
