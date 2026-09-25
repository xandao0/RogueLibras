// UI shader that thickens and outlines a sprite from its own alpha.
//
// SignWriting glyphs are fine line art. On a card that ends up ~180px tall they
// collapse into a smudge, so this dilates the alpha to fatten the strokes
// (_Bold) and rings the result in a contrasting colour (_OutlineWidth), both
// measured in source texels so the effect is independent of on-screen size.
//
// Only safe on sprites with room around them in their texture - a tightly
// packed atlas would bleed neighbours into the dilation.
Shader "RogueLibras/UI Sign Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // In fractions of the sprite, not texels: the source art is ~450px wide
        // but draws at ~20px, so a texel-sized dilation would be invisible.
        _Bold         ("Bold (fraction of glyph)", Range(0,0.08)) = 0.012
        _OutlineWidth ("Outline (fraction of glyph)", Range(0,0.08)) = 0.014
        _OutlineColor ("Outline colour", Color) = (1,1,1,1)
        // The sign SVGs are authored in colour, so multiplying them by a tint
        // just darkens them. 0 keeps the artwork's own colour; 1 replaces it
        // with the tint, for rows that need a specific colour (the name signs).
        _Colorize     ("Replace artwork colour with tint", Range(0,1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color, _OutlineColor;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _Bold, _OutlineWidth, _Colorize;

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPos = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // Largest alpha within radius `r`, sampled on a ring of 8 plus the
            // centre. Cheap, and plenty for line art this thin.
            //
            // r is a fraction of the glyph's DRAWN HEIGHT, not of its texture
            // width: signs in a row share a height but not an aspect, so a
            // width-relative radius made wide glyphs bold and narrow ones
            // almost untouched. Dividing x by the aspect keeps the ring circular
            // on screen and the stroke weight equal across a whole word.
            float MaxAlpha (float2 uv, float r)
            {
                if (r <= 0.0) return tex2D(_MainTex, uv).a;

                float aspect = max(_MainTex_TexelSize.z, 1.0) / max(_MainTex_TexelSize.w, 1.0);
                float2 t = float2(r / aspect, r);
                float a = tex2D(_MainTex, uv).a;
                a = max(a, tex2D(_MainTex, uv + float2( t.x, 0)).a);
                a = max(a, tex2D(_MainTex, uv + float2(-t.x, 0)).a);
                a = max(a, tex2D(_MainTex, uv + float2(0,  t.y)).a);
                a = max(a, tex2D(_MainTex, uv + float2(0, -t.y)).a);
                const float k = 0.7071;
                a = max(a, tex2D(_MainTex, uv + float2( t.x,  t.y) * k).a);
                a = max(a, tex2D(_MainTex, uv + float2( t.x, -t.y) * k).a);
                a = max(a, tex2D(_MainTex, uv + float2(-t.x,  t.y) * k).a);
                a = max(a, tex2D(_MainTex, uv + float2(-t.x, -t.y) * k).a);
                return a;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.uv) + _TextureSampleAdd;

                float fill    = MaxAlpha(i.uv, _Bold);
                float outline = MaxAlpha(i.uv, _Bold + _OutlineWidth);

                fixed3 glyph = lerp(src.rgb * i.color.rgb, i.color.rgb, _Colorize);

                // outline first, glyph painted over it
                fixed4 col = _OutlineColor;
                col.a *= outline;
                col.rgb = lerp(col.rgb, glyph, fill);
                col.a   = max(col.a * (1.0 - fill), fill);
                col.a  *= i.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
