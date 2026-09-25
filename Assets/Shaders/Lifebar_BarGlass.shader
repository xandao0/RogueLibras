// Anime glass tube. Below the fill line it is a clean fresnel rim + specular
// streaks. Above it (the emptied stretch) the life tube frosts over and cracks,
// while the energy tube instead goes clear and stops glowing.
Shader "Lifebar/BarGlass"
{
    Properties
    {
        _Fill ("Fill (0-1)", Range(0,1)) = 1

        [Header(Glass)]
        _Tint      ("Glass tint", Color) = (0.80,0.90,1.00,1)
        // Soft-edged rather than a hard step, so the sheen fades into the tube
        // instead of ending on a visible line. 0 softness = the old step.
        _RimStart  ("Rim start",    Range(0,1)) = 0.62
        _RimSoft   ("Rim softness", Range(0,1)) = 0.18
        _StreakA   ("Streak 1 (centre, width)", Vector) = (0.130,0.050,0,0)
        _StreakB   ("Streak 2 (centre, width, strength)", Vector) = (0.824,0.048,0.38,0)
        _Alpha     ("Base alpha", Range(0,1)) = 0.05

        [Header(Cracks for the life tube)]
        _CrackTex    ("Crack map (R)", 2D) = "black" {}
        _CrackTiling ("Crack tiling", Vector) = (3.2,1.1,0,0)
        _CrackColor  ("Crack colour", Color) = (0.30,0.72,1.00,1)
        _CrackBoost  ("Crack boost",  Range(0,4)) = 1.8
        _FrostColor  ("Frost colour", Color) = (0.88,0.93,0.99,1)
        _FrostAmount ("Frost amount", Range(0,1)) = 0.44

        [Header(Energy tube)]
        _EmptyClear ("Go clear past the fill", Range(0,1)) = 0
        _GlowByFill ("Glow follows fill",      Range(0,1)) = 0

        [Header(Outline)]
        _OutlineColor ("Outline colour", Color) = (0.055,0.020,0.020,1)
        _OutlineWidth ("Outline width",  Range(0,0.06)) = 0.010
        // see LifebarOutline.cginc - LifebarController feeds _PartCenter per
        // renderer so the hull is not extruded through a part's bore
        _PartCenter    ("Part centre (object space)", Vector) = (0,0,0,0)
        _OutlineInward ("Extrude inward-facing faces", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        // ---- depth prime: the tube is transparent so it writes no depth of its
        //      own, and without this the outline hull's far wall paints black
        //      straight over the liquid behind the glass.
        Pass
        {
            Name "DEPTHPRIME"
            Cull Back
            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; };
            struct v2f     { float4 pos : SV_POSITION; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o; }
            fixed4 frag (v2f i) : SV_Target { return 0; }
            ENDCG
        }

        // ---- outline: inverted hull, clipped to outside the silhouette ----
        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On
            ZTest LEqual

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

        // ---- glass ------------------------------------------------------
        Pass
        {
            Name "GLASS"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            fixed4 _Tint, _CrackColor, _FrostColor;
            float4 _StreakA, _StreakB, _CrackTiling;
            float  _Fill, _RimStart, _RimSoft, _Alpha, _CrackBoost, _FrostAmount;
            float  _EmptyClear, _GlowByFill;
            sampler2D _CrackTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos  : SV_POSITION;
                float2 uv   : TEXCOORD0;
                float3 nrm  : TEXCOORD1;
                float3 vdir : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                float3 objScale = float3(
                    length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21),
                    length(unity_ObjectToWorld._m02_m12_m22));
                objScale = max(objScale, 1e-4);
                // divide by the object scale so the elliptical rings shade like they
                // do in Blender (which uses world-space normals) while the lighting
                // still stays locked to the model, not to the scene.
                o.nrm = normalize(v.normal / objScale);
                float3 camOS = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                o.vdir = (camOS - v.vertex.xyz) * objScale;
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                float u = i.uv.x, v = i.uv.y;
                float empty  = smoothstep(_Fill, _Fill + 0.02, u);   // 1 past the fill line
                float filled = 1.0 - empty;
                float dmg    = saturate(0.30 + 0.70 * (1.0 - _Fill)); // emptier bar, worse damage

                float3 nrm = normalize(i.nrm) * (facing >= 0 ? 1.0 : -1.0);
                float fres = 1.0 - saturate(dot(normalize(i.vdir), nrm));
                float rim  = smoothstep(_RimStart, _RimStart + _RimSoft + 1e-4, fres);
                // the far wall contributes, but softly - it is seen through the near one
                float sideK = facing >= 0 ? 1.0 : 0.45;

                float s1 = 1.0 - smoothstep(0.0, _StreakA.y, abs(v - _StreakA.x));
                float s2 = (1.0 - smoothstep(0.0, _StreakB.y, abs(v - _StreakB.x))) * _StreakB.z;
                float hl = saturate(s1 + s2);

                float ck    = saturate(tex2D(_CrackTex, i.uv * _CrackTiling.xy).r * _CrackBoost);
                ck         *= empty * dmg;
                float frost = _FrostAmount * empty * dmg;

                fixed3 c = _Tint.rgb * rim + hl.xxx;
                c += _FrostColor.rgb * frost;
                c += _CrackColor.rgb * ck;
                float a = (rim * 0.80 + hl * 0.95 + frost + ck + _Alpha) * sideK;

                // energy tube: nothing behind the glass past the fill -> go clear
                float clearK = lerp(1.0, saturate(0.18 + 0.82 * filled), _EmptyClear);
                c *= lerp(1.0, saturate(0.28 + 0.72 * filled), _EmptyClear);
                a *= clearK;

                // ...and stop emitting as the charge drops
                float glowK = lerp(1.0, saturate(0.18 + 0.82 * _Fill), _GlowByFill);
                c *= glowK;
                a *= glowK;

                return fixed4(saturate(c), saturate(a));
            }
            ENDCG
        }
    }
}
