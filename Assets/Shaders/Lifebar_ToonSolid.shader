// Cel-shaded solid parts of the lifebar: gold rings/frame/cap, the ruby, the
// dark recess. Lit by ONE fixed object-space direction (not scene lights) so it
// looks identical in Unity and in the Blender source file.
Shader "Lifebar/ToonSolid"
{
    Properties
    {
        [Header(Toon bands)]
        _Band0 ("Band 0 - shadow",   Color) = (0.290,0.140,0.035,1)
        _Band1 ("Band 1 - dark",     Color) = (0.560,0.300,0.065,1)
        _Band2 ("Band 2 - mid",      Color) = (0.820,0.560,0.150,1)
        _Band3 ("Band 3 - lit",      Color) = (0.955,0.775,0.330,1)
        _Band4 ("Band 4 - specular", Color) = (1.000,0.945,0.700,1)
        // 6th stop, for parts whose Blender ramp has six (the gem). _Cut5 above
        // 1 leaves it unreachable, so five-band materials are unaffected.
        _Band5 ("Band 5 - highlight", Color) = (1,1,1,1)
        _Cuts  ("Band cuts (x,y,z,w)", Vector) = (0.20,0.40,0.62,0.88)
        _Cut5  ("Band cut 5", Range(0,1.2)) = 1.2
        _LightDir ("Light dir (object space)", Vector) = (0.62,0.66,0.42)

        [Header(Rim)]
        _RimColor  ("Rim colour", Color) = (0.85,0.66,0.30,1)
        _RimStart  ("Rim start",  Range(0,1)) = 0.55
        _RimAmount ("Rim amount", Range(0,2)) = 0.75

        // Hard-edged specular. The gem has flat faceted normals, so a step()
        // instead of a pow() lobe makes whole facets flip to white at once -
        // that is what reads as "cut stone" rather than "shiny plastic".
        // Amounts default to 0 so the gold/frame materials are unaffected.
        [Header(Gem shine)]
        _ShineColor ("Shine colour", Color) = (1,1,1,1)
        _ShineCut   ("Shine cut - broad",  Range(0,1)) = 0.70
        _Shine      ("Shine - broad",      Range(0,3)) = 0
        _ShineCoreCut ("Shine cut - core", Range(0,1)) = 0.90
        _ShineCore    ("Shine - core",     Range(0,3)) = 0
        _ShineDir2  ("Fill-shine dir (object space)", Vector) = (-0.35,0.72,-0.60)
        _ShineCut2  ("Fill-shine cut",    Range(0,1)) = 0.80
        _Shine2     ("Fill-shine amount", Range(0,3)) = 0

        // Dark contour drawn on the part's own surface, near its silhouette.
        // The hull outline cannot show where a part is socketed inside another
        // (the gem inside its bezel) - this can. 0 amount = off.
        [Header(Inner stroke)]
        _StrokeColor  ("Inner stroke colour", Color) = (0.060,0.003,0.010,1)
        _StrokeStart  ("Inner stroke start",  Range(0,1)) = 0.72
        _StrokeSoft   ("Inner stroke softness", Range(0.001,0.5)) = 0.10
        _StrokeAmount ("Inner stroke amount", Range(0,1)) = 0

        [Header(Outline)]
        _OutlineColor ("Outline colour", Color) = (0.055,0.020,0.020,1)
        _OutlineWidth ("Outline width",  Range(0,0.06)) = 0.012
        // For a part socketed in front of another (the gem in its bezel) the
        // inverted hull draws nothing: its back faces are deeper than the part
        // behind, so they fail the depth test. This expands the FRONT faces
        // instead and lets the toon fill paint over the middle, leaving a ring.
        // 0 disables the pass outright.
        _SocketWidth ("Socket outline width", Range(0,0.06)) = 0
        // Surfaces whose normal points back toward the part's centre (the bore
        // of a ring) must not be extruded - that pushes the black hull into
        // whatever the part is wrapped around. LifebarController feeds the
        // centre; 1 restores the old extrude-everything behaviour.
        _PartCenter    ("Part centre (object space)", Vector) = (0,0,0,0)
        _OutlineInward ("Extrude inward-facing faces", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        // ---- inverted hull outline -------------------------------------
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

        // ---- socket outline: front faces pushed out, no depth write, so the
        //      toon fill below covers the middle and only the ring survives ---
        Pass
        {
            Name "SOCKET_OUTLINE"
            Cull Back
            ZWrite Off
            // Always, because the whole point is to draw over the part this one
            // is sunk into. Give the material a render queue above that part so
            // it lands on top of it; the toon fill then reclaims the middle.
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "LifebarOutline.cginc"

            fixed4 _OutlineColor;
            float  _SocketWidth;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f     { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(LifebarExtrude(v.vertex, v.normal, _SocketWidth));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                clip(_SocketWidth - 1e-6);   // width 0 -> the pass costs nothing
                return _OutlineColor;
            }
            ENDCG
        }

        // ---- banded toon fill ------------------------------------------
        Pass
        {
            Name "TOON"
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Band0, _Band1, _Band2, _Band3, _Band4, _Band5, _RimColor, _ShineColor, _StrokeColor;
            float4 _Cuts, _LightDir, _ShineDir2;
            float  _RimStart, _RimAmount, _Cut5;
            float  _ShineCut, _Shine, _ShineCoreCut, _ShineCore, _ShineCut2, _Shine2;
            float  _StrokeStart, _StrokeSoft, _StrokeAmount;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 pos  : SV_POSITION;
                float3 nrm  : TEXCOORD0;
                float3 vdir : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
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

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.nrm);
                float ndl = saturate(dot(n, normalize(_LightDir.xyz)));

                fixed3 c = _Band0.rgb;
                c = ndl >= _Cuts.x ? _Band1.rgb : c;
                c = ndl >= _Cuts.y ? _Band2.rgb : c;
                c = ndl >= _Cuts.z ? _Band3.rgb : c;
                c = ndl >= _Cuts.w ? _Band4.rgb : c;
                c = ndl >= _Cut5   ? _Band5.rgb : c;

                float3 vdir = normalize(i.vdir);
                float fres = 1.0 - saturate(dot(vdir, n));
                c += _RimColor.rgb * step(_RimStart, fres) * _RimAmount;

                // two gleams: the key light gives the broad facet flash plus a
                // small hot core inside it, the second direction catches the
                // facets turned away from the key so the stone reads all over.
                float ndh  = saturate(dot(n, normalize(normalize(_LightDir.xyz) + vdir)));
                float ndh2 = saturate(dot(n, normalize(normalize(_ShineDir2.xyz) + vdir)));
                float shine = step(_ShineCut,     ndh)  * _Shine
                            + step(_ShineCoreCut, ndh)  * _ShineCore
                            + step(_ShineCut2,    ndh2) * _Shine2;
                c += _ShineColor.rgb * shine;

                // inner stroke last, so it stays a clean dark contour even
                // where a bright facet runs right up to the edge
                c = lerp(c, _StrokeColor.rgb, _StrokeAmount *
                         smoothstep(_StrokeStart - _StrokeSoft, _StrokeStart + _StrokeSoft, fres));

                return fixed4(saturate(c), 1);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
