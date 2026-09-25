// Drifting volumetric-looking fog for a 2D scene, plus an optional vignette and
// warm grade so the sprites, the UI and the background settle into one image.
//
// Two layers of value-noise fbm scroll in opposite directions at different
// scales; where they overlap you get thick banks, where they don't you get thin
// haze. A vertical gradient keeps the fog pooling low instead of filling the
// frame evenly.
Shader "RogueLibras/Scene Fog"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Fog Tint", Color) = (0.82, 0.78, 0.74, 1)

        _Density ("Density", Range(0, 2)) = 0.55
        _Scale ("Noise Scale", Float) = 2.4
        _Speed ("Drift Speed", Float) = 0.02
        _Softness ("Edge Softness", Range(0.01, 1)) = 0.45

        _FloorBias ("Pool Toward Floor", Range(0, 1)) = 0.6
        _EdgeFade ("Quad Edge Fade", Range(0, 0.5)) = 0.18

        _Vignette ("Vignette", Range(0, 2)) = 0
        _VignetteColor ("Vignette Colour", Color) = (0.05, 0.02, 0.01, 1)
        _Warmth ("Warm Grade", Range(0, 1)) = 0

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
        ColorMask [_ColorMask]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 uv    : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Density, _Scale, _Speed, _Softness;
            float _FloorBias, _EdgeFade;
            float _Vignette, _Warmth;
            fixed4 _VignetteColor;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);       // smoothstep the cell
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0, amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += valueNoise(p) * amp;
                    p *= 2.02;
                    amp *= 0.5;
                }
                return v;
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = i.uv * _Scale;

                // Opposing drifts, so banks build and thin out instead of sliding
                // past as one sheet.
                float a = fbm(uv + float2(t, t * 0.22));
                float b = fbm(uv * 1.7 - float2(t * 1.35, t * 0.11) + 17.3);
                float n = saturate(a * 0.65 + b * 0.45);

                // Thicker low in the frame.
                float floorGrad = lerp(1.0, 1.0 - i.uv.y, saturate(_FloorBias));
                float fog = smoothstep(0.5 - _Softness, 0.5 + _Softness, n) * floorGrad * _Density;

                // Never let the quad's own rectangle show.
                float2 e = min(i.uv, 1.0 - i.uv);
                float edge = smoothstep(0.0, max(_EdgeFade, 0.0001), min(e.x, e.y));
                fog *= edge;

                float3 rgb = _Color.rgb;
                float alpha = saturate(fog) * _Color.a * i.color.a;

                // Vignette and warm grade ride along on the same quad so the whole
                // frame can be pulled together with one object.
                float2 d = i.uv - 0.5;
                float vig = saturate(dot(d, d) * 4.0);
                float vigA = pow(vig, 1.6) * _Vignette;

                float3 outRgb = rgb;
                float outA = alpha;
                if (vigA > 0.0)
                {
                    outRgb = lerp(rgb, _VignetteColor.rgb, saturate(vigA / max(alpha + vigA, 0.0001)));
                    outA = saturate(alpha + vigA);
                }
                outRgb = lerp(outRgb, outRgb * float3(1.12, 1.0, 0.86), _Warmth);

                return fixed4(outRgb * i.color.rgb, outA * tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }

    Fallback Off
}
