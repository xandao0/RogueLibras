// The zig-zag "light string" running through the energy tube. The wire is always
// there full length: the stretch below the charge line glows gold, the stretch
// above it is dead brown. As the charge drops even the lit part dims out.
Shader "Lifebar/Filament"
{
    Properties
    {
        _Fill ("Charge (0-1)", Range(0,1)) = 1
        _LitColor  ("Lit colour",  Color) = (1.000,0.920,0.550,1)
        _DeadColor ("Dead colour", Color) = (0.125,0.065,0.026,1)
        _Glow  ("Glow",  Range(0,12)) = 6.0
        _Floor ("Unlit floor", Range(0,2)) = 0.20
        _Soft  ("Edge softness", Range(0.001,0.2)) = 0.015

        [Header(Outline)]
        _OutlineColor ("Outline colour", Color) = (0.055,0.020,0.020,1)
        _OutlineWidth ("Outline width",  Range(0,0.06)) = 0.006
        // see LifebarOutline.cginc - LifebarController feeds _PartCenter per
        // renderer so the hull is not extruded through a part's bore
        _PartCenter    ("Part centre (object space)", Vector) = (0,0,0,0)
        _OutlineInward ("Extrude inward-facing faces", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        LOD 100

        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "LifebarOutline.cginc"

            fixed4 _OutlineColor;
            float  _OutlineWidth;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f     { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(LifebarExtrude(v.vertex, v.normal, _OutlineWidth));
                return o;
            }
            fixed4 frag (v2f i) : SV_Target { return _OutlineColor; }
            ENDCG
        }

        Pass
        {
            Name "FILAMENT"
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _LitColor, _DeadColor;
            float  _Fill, _Glow, _Floor, _Soft;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float lit = 1.0 - smoothstep(_Fill, _Fill + _Soft, i.uv.x);
                float k   = lit * saturate(0.28 + 0.72 * _Fill);
                fixed3 c  = lerp(_DeadColor.rgb, _LitColor.rgb, k);
                c *= (_Floor + _Glow * k);
                return fixed4(saturate(c), 1);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
