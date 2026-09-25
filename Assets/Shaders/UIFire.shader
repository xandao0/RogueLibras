// Procedural, cel-banded flame for UI Images (Built-in Render Pipeline).
// Draws the whole flame in the fragment shader - no texture needed - so it can
// be dropped on any UI Image (leave its Sprite empty) and animated purely by
// tint alpha / RectTransform scale from script.
Shader "RogueLibras/UI Fire"
{
    Properties
    {
        // UI assigns the sprite texture to _MainTex; the flame is procedural so
        // it goes unused, but the property must exist for UGUI compatibility.
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color        ("Tint", Color) = (1,1,1,1)

        _CoreColor    ("Core Color",   Color) = (1.0, 0.98, 0.80, 1)
        _MidColor     ("Mid Color",    Color) = (1.0, 0.72, 0.18, 1)
        _EdgeColor    ("Edge Color",   Color) = (0.92, 0.26, 0.06, 1)

        _Speed        ("Scroll Speed", Range(0, 6)) = 1.9
        _NoiseScale   ("Noise Scale",  Range(1, 16)) = 4.0
        _Distort      ("Distortion",   Range(0, 1)) = 0.34
        _Width        ("Flame Width",  Range(0.05, 0.6)) = 0.36
        _Taper        ("Flame Taper",  Range(0.2, 3)) = 1.15
        _Softness     ("Edge Softness",Range(0.005, 0.3)) = 0.06
        _Bands        ("Color Banding",Range(0, 1)) = 0.75

        // Standard UI plumbing so this behaves inside Masks / RectMask2D.
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
        // Additive-ish so the flame glows over whatever is behind it.
        Blend SrcAlpha One
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
            fixed4 _Color;
            fixed4 _CoreColor;
            fixed4 _MidColor;
            fixed4 _EdgeColor;
            float _Speed, _NoiseScale, _Distort, _Width, _Taper, _Softness, _Bands;

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
                for (int i = 0; i < 3; i++)
                {
                    sum += valueNoise(p) * amp;
                    p *= 2.03;
                    amp *= 0.5;
                }
                return sum;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Fire rises: scroll the noise field downward in UV so the
                // pattern appears to travel up the flame.
                float t = _Time.y * _Speed;
                float n = fbm(float2(uv.x * _NoiseScale, uv.y * _NoiseScale - t));

                // Wobble the silhouette horizontally with the same noise field.
                float wobble = (n - 0.5) * _Distort * (0.25 + uv.y);
                float cx = (uv.x - 0.5) + wobble;

                // Teardrop silhouette: widest near the base, tapering to a tip.
                float halfWidth = _Width * pow(saturate(1.0 - uv.y), _Taper);

                // Silhouette mask, then a *gradual* falloff inside it so the
                // flame has a hot core and cooler edges instead of one flat plateau.
                float inside = 1.0 - smoothstep(halfWidth - _Softness, halfWidth + _Softness, abs(cx));
                float across = 1.0 - saturate(abs(cx) / max(halfWidth, 1e-4));

                // Hottest near the base, cooling toward the tip.
                float up = 1.0 - saturate(uv.y / 0.85);

                float heat = inside * across * lerp(0.35, 1.0, up);

                // Fade the very bottom and the tip so it reads as a flame, not a blob.
                heat *= smoothstep(0.0, 0.10, uv.y);
                heat *= 1.0 - smoothstep(0.70, 1.0, uv.y);

                // Flicker the interior so it never looks static.
                heat *= lerp(0.70, 1.25, n);
                heat = saturate(heat);

                // Cel-style banding for the anime look.
                float banded = lerp(heat, floor(heat * 4.0) / 4.0 + 0.125, _Bands);
                banded = saturate(banded);

                // Ramp: edge -> mid -> core as heat climbs.
                fixed3 col = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.05, 0.45, banded));
                col = lerp(col, _CoreColor.rgb, smoothstep(0.55, 0.85, banded));

                // Alpha follows the silhouette so the flame stays solid while the
                // colour ramp handles the hot-core-to-cool-edge look.
                float alpha = saturate(inside * lerp(0.55, 1.0, banded)) * i.color.a;
                clip(alpha - 0.003);

                return fixed4(col * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
