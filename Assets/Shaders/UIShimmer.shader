// A slow diagonal "glint" that sweeps across a UI graphic, like light catching
// the edge of a deck of cards. Drop it on the Compra (draw pile) image as a
// material; it animates on its own from _Time, no script needed.
Shader "RogueLibras/UI Shimmer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ShimmerColor ("Shimmer Colour", Color) = (1,1,1,1)
        _ShimmerStrength ("Shimmer Strength", Range(0,3)) = 0.9
        _ShimmerWidth ("Shimmer Width", Range(0.02,0.6)) = 0.14
        _ShimmerSpeed ("Shimmer Speed", Range(0.05,3)) = 0.5
        _ShimmerAngle ("Shimmer Angle", Range(-1,1)) = 0.6
        _ShimmerGap ("Pause between sweeps (0-1 of cycle)", Range(0,0.95)) = 0.55

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
            "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"
            "PreviewType"="Plane" "CanUseSpriteAtlas"="True"
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
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            fixed4 _ShimmerColor;
            float _ShimmerStrength, _ShimmerWidth, _ShimmerSpeed, _ShimmerAngle, _ShimmerGap;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // position along the sweep axis, 0..~1.6 across the quad
                float axis = IN.texcoord.x + IN.texcoord.y * _ShimmerAngle;

                // sweep head marches 0 -> 1 then holds for _ShimmerGap of the cycle
                float cyc = frac(_Time.y * _ShimmerSpeed);
                float run = 1.0 - _ShimmerGap;
                float head = saturate(cyc / max(run, 0.001)) * (1.0 + _ShimmerAngle + _ShimmerWidth);

                float d = abs(axis - head);
                float band = smoothstep(_ShimmerWidth, 0.0, d);
                // only while actually sweeping, and only over opaque pixels
                band *= step(cyc, run) * color.a;

                color.rgb += _ShimmerColor.rgb * band * _ShimmerStrength;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif
                return color;
            }
        ENDCG
        }
    }
}
