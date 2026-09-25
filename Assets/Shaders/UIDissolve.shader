// Burn-away dissolve for UI Images (Built-in Render Pipeline).
//
// Eats the sprite along a noisy front and leaves a glowing char line just ahead
// of it, so a card looks like paper catching rather than a sprite fading out.
// Drive _Dissolve from 0 (intact) to 1 (gone).
//
// The front is biased by UV so the burn travels in one direction instead of
// pocking the whole card at once - _BurnDir picks which way.
Shader "RogueLibras/UI Dissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color      ("Tint", Color) = (1,1,1,1)

        _Dissolve   ("Dissolve", Range(0,1)) = 0
        _NoiseScale ("Noise Scale", Range(1,24)) = 7
        _Bias       ("Directional bias", Range(0,1)) = 0.45
        _BurnDir    ("Burn direction (x,y)", Vector) = (0,1,0,0)

        [Header(Char edge)]
        _EdgeWidth  ("Edge width", Range(0.001,0.5)) = 0.14
        _EdgeColor  ("Edge colour", Color) = (1.0, 0.62, 0.12, 1)
        _CoreColor  ("Edge core colour", Color) = (1.0, 0.98, 0.80, 1)
        _CharColor  ("Char colour", Color) = (0.10, 0.05, 0.03, 1)
        _CharWidth  ("Char width", Range(0,0.5)) = 0.09
        _Emissive   ("Edge brightness", Range(1,6)) = 2.2

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color, _EdgeColor, _CoreColor, _CharColor;
            float _Dissolve, _NoiseScale, _Bias, _EdgeWidth, _CharWidth, _Emissive;
            float4 _BurnDir;

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    sum += valueNoise(p) * amp;
                    p *= 2.07;
                    amp *= 0.5;
                }
                return sum;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.uv) * i.color;

                // How "burnable" this pixel is: mostly noise, plus a slope along
                // _BurnDir so the front sweeps across instead of appearing everywhere.
                float n = fbm(i.uv * _NoiseScale);
                float2 dir = normalize(_BurnDir.xy + 1e-6);
                float along = saturate(dot(i.uv - 0.5, dir) + 0.5);
                float field = lerp(n, 1.0 - along, _Bias);

                // Starts one full band below zero so _Dissolve = 0 leaves the
                // card completely untouched - no glow on frame one - and ends
                // past 1 so the last ember goes out instead of freezing lit.
                float band = _EdgeWidth + _CharWidth;
                float threshold = _Dissolve * (1.0 + band) - band;
                float d = field - threshold;

                clip(d);                                  // already burnt away

                // d = 0 is the burning front, and the paper cools as d grows:
                // glowing ember line first, then char, then untouched card.
                float glowT = 1.0 - saturate(d / max(_EdgeWidth, 1e-4));
                float charT = (1.0 - glowT) *
                              (1.0 - saturate((d - _EdgeWidth) / max(_CharWidth, 1e-4)));

                fixed3 col = src.rgb;
                col = lerp(col, _CharColor.rgb, charT);

                fixed3 glow = lerp(_EdgeColor.rgb, _CoreColor.rgb, glowT);
                col = lerp(col, glow * _Emissive, glowT);

                return fixed4(col, src.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
