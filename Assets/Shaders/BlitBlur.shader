// Separable Gaussian for Graphics.Blit. Nine taps collapsed to five by letting
// the bilinear sampler average each neighbouring pair, which is the usual trick
// for this kernel. Blit once with _BlurDir = (1,0) and again with (0,1) for a
// full 2D blur.
Shader "RogueLibras/Blit Blur"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float2  _BlurDir;      // texel-space direction, scaled by the spread

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 o = _MainTex_TexelSize.xy * _BlurDir;

                fixed4 c  = tex2D(_MainTex, i.uv) * 0.2270270270;
                c += tex2D(_MainTex, i.uv + o * 1.3846153846) * 0.3162162162;
                c += tex2D(_MainTex, i.uv - o * 1.3846153846) * 0.3162162162;
                c += tex2D(_MainTex, i.uv + o * 3.2307692308) * 0.0702702703;
                c += tex2D(_MainTex, i.uv - o * 3.2307692308) * 0.0702702703;
                return c;
            }
            ENDCG
        }
    }
}
