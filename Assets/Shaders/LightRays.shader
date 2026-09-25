// Procedural god rays for a UI Image. Rays fan out from a point given in the
// quad's own UV space, so the origin can sit off the quad (above the top edge,
// say) for light spilling in from outside the frame.
//
// Three sine bands at unrelated angular frequencies keep the rays from reading
// as an evenly spaced pinwheel, and all three drift at different rates so the
// pattern never repeats on any comfortable timescale.
Shader "RogueLibras/Light Rays"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0.94, 0.76, 1)

        _Origin ("Ray Origin (UV)", Vector) = (0.5, 1.15, 0, 0)
        _Aspect ("Aspect (width/height)", Float) = 1
        _RayCount ("Ray Count", Float) = 9
        _Sharpness ("Sharpness", Range(0.5, 10)) = 2.6
        _Speed ("Drift Speed", Float) = 0.08
        _Reach ("Reach", Range(0.1, 3)) = 1.25
        _Falloff ("Falloff", Range(0.5, 8)) = 2.2
        _CoreFade ("Core Fade", Range(0, 1)) = 0.1
        _EdgeFade ("Edge Fade", Range(0, 0.5)) = 0.16
        _Intensity ("Intensity", Range(0, 4)) = 1

        // UGUI drives these; without them an Image using this material cannot be
        // masked and ignores the canvas' own render state.
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
        Blend One One   // premultiplied additive: light adds, it never darkens

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
                float4 pos    : SV_POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _Origin;
            float _Aspect;
            float _RayCount;
            float _Sharpness;
            float _Speed;
            float _Reach;
            float _Falloff;
            float _CoreFade;
            float _EdgeFade;
            float _Intensity;

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
                float2 d = i.uv - _Origin.xy;
                d.x *= max(_Aspect, 0.0001);   // keep the fan circular on a wide quad

                float dist = length(d);
                float ang = atan2(d.y, d.x);
                float t = _Time.y * _Speed;

                float b1 = sin(ang * _RayCount + t * 1.00);
                float b2 = sin(ang * _RayCount * 0.6180339 - t * 0.73 + 1.7);
                float b3 = sin(ang * _RayCount * 1.7320508 + t * 0.41 + 4.1);

                float rays = (b1 * 0.5 + 0.5) * 0.60
                           + (b2 * 0.5 + 0.5) * 0.28
                           + (b3 * 0.5 + 0.5) * 0.12;
                rays = pow(saturate(rays), _Sharpness);

                // Fade with distance from the source, and blank out the singularity
                // right at it so the origin is not a hard white dot.
                float radial = pow(saturate(1.0 - dist / max(_Reach, 0.0001)), _Falloff);
                float core = smoothstep(0.0, max(_CoreFade, 0.0001), dist);

                // Feather the quad's own edges so the rectangle never shows.
                float2 e = min(i.uv, 1.0 - i.uv);
                float edge = smoothstep(0.0, max(_EdgeFade, 0.0001), min(e.x, e.y));

                float a = rays * radial * core * edge * _Intensity
                        * i.color.a * _Color.a * tex2D(_MainTex, i.uv).a;

                return fixed4(_Color.rgb * i.color.rgb * a, a);
            }
            ENDCG
        }
    }

    Fallback Off
}
