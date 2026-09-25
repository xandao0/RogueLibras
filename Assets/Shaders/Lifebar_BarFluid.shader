// The liquid inside a tube. UV.x runs 0 at the heart end to 1 at the tip, so
// _Fill clips the liquid to the current bar value. Used for both the red life
// fluid and the amber energy fluid (the energy one turns brown via _DrainLerp).
Shader "Lifebar/BarFluid"
{
    Properties
    {
        _Fill ("Fill (0-1)", Range(0,1)) = 1

        [Header(Colour)]
        _ColorLow ("Bottom",    Color) = (0.220,0.006,0.022,1)
        _ColorMid ("Body",      Color) = (0.760,0.035,0.090,1)
        _ColorTop ("Top light", Color) = (0.980,0.180,0.230,1)

        [Header(Drain to brown for the energy tube)]
        _DrainColor ("Drained colour", Color) = (0.145,0.072,0.028,1)
        _DrainLerp  ("Drain amount (0 = life, 1 = energy)", Range(0,1)) = 0

        [Header(Emission)]
        _EmissionBase   ("Emission base",      Range(0,4)) = 1.3
        _EmissionByFill ("Emission from fill", Range(0,4)) = 0

        [Header(Detail)]
        _MenColor  ("Meniscus colour", Color) = (0.45,0.16,0.18,1)
        _MenWidth  ("Meniscus width",  Range(0,0.2)) = 0.018
        _RimDark   ("Rim darkening",   Color) = (0.30,0.02,0.07,1)
        _RimStart  ("Rim start",       Range(0,1)) = 0.42
        // Blender ramps this in over a wide range rather than switching at a
        // hard edge; 0 softness reproduces the old step.
        _RimSoft   ("Rim softness",    Range(0,1)) = 0.45
        _AlphaFull  ("Alpha when full",    Range(0,1)) = 0.94
        _AlphaDrain ("Alpha when drained", Range(0,1)) = 0.94
        _SparkleTex    ("Sparkle (optional)", 2D) = "black" {}
        _SparkleTiling ("Sparkle tiling", Vector) = (6,2,0,0)
        _SparkleAmount ("Sparkle amount", Range(0,2)) = 0

        // Procedural specks suspended in the liquid, one per grid cell at a
        // hashed offset so they do not read as a grid. Amount defaults to 0,
        // so the energy tube keeps its plain fluid unless it is asked for.
        // Gentle undulation of the liquid's leading edge. Reads as a surface
        // rather than a hard clip line. 0 amplitude = the old straight cut.
        [Header(Surface motion)]
        _WaveAmp   ("Fill edge wave",       Range(0,0.05)) = 0
        _WaveFreq  ("Fill edge wave freq",  Range(0,8)) = 2
        _WaveSpeed ("Fill edge wave speed", Range(0,8)) = 1.5

        [Header(Suspended motes)]
        _MoteColor  ("Mote colour", Color) = (1.00,0.86,0.94,1)
        _MoteGrid   ("Mote grid (cells x, cells y, cell aspect)", Vector) = (26,4,3,0)
        _MoteSize   ("Mote size",   Range(0,0.5)) = 0.17
        _MoteAmount ("Mote amount", Range(0,3)) = 0
        _MoteSpeed  ("Mote twinkle speed", Range(0,4)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Name "FLUID"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ColorLow, _ColorMid, _ColorTop, _DrainColor, _MenColor, _RimDark, _MoteColor;
            float  _Fill, _DrainLerp, _EmissionBase, _EmissionByFill;
            float  _MenWidth, _RimStart, _RimSoft, _AlphaFull, _AlphaDrain, _SparkleAmount;
            float  _MoteSize, _MoteAmount, _MoteSpeed;
            float  _WaveAmp, _WaveFreq, _WaveSpeed;
            sampler2D _SparkleTex;
            float4 _SparkleTiling, _MoteGrid;

            float2 hash22 (float2 p)
            {
                float3 q = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.xx + q.yz) * q.zy);
            }

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

            fixed4 frag (v2f i) : SV_Target
            {
                // everything past the fill line is simply not there
                float edge = _Fill + _WaveAmp *
                    sin(i.uv.y * 6.2831853 * _WaveFreq + _Time.y * _WaveSpeed);
                clip(edge - i.uv.x - 0.00001);

                // shading gradient around the tube (UV.y 0.25 is the top)
                float g = 0.5 + 0.5 * sin(i.uv.y * 6.2831853);
                fixed3 c = lerp(_ColorLow.rgb, _ColorMid.rgb, smoothstep(0.04, 0.60, g));
                c = lerp(c, _ColorTop.rgb, smoothstep(0.60, 0.93, g));

                // charge level pulls the whole liquid toward the drained colour
                float t = pow(saturate(_Fill), 0.7);
                c = lerp(c, lerp(_DrainColor.rgb, c, t), _DrainLerp);

                // darken toward the silhouette so it reads as a cylinder
                float fres = 1.0 - saturate(dot(normalize(i.vdir), normalize(i.nrm)));
                c = lerp(c, c * _RimDark.rgb,
                         smoothstep(_RimStart, _RimStart + _RimSoft + 1e-4, fres) * 0.6);

                c += _MenColor.rgb * smoothstep(edge - _MenWidth, edge, i.uv.x);
                c += tex2D(_SparkleTex, i.uv * _SparkleTiling.xy).rgb * _SparkleAmount;

                if (_MoteAmount > 0.0)
                {
                    float2 muv  = i.uv * _MoteGrid.xy;
                    float2 cell = floor(muv);
                    float2 rnd  = hash22(cell);
                    // dot centre inside the cell, kept off the cell edges so
                    // neighbouring cells never clip each other's specks
                    float2 d    = frac(muv) - (0.25 + 0.5 * rnd);
                    d.y *= max(_MoteGrid.z, 1e-4);   // cell aspect: keeps the specks round on screen
                    float tw    = 0.5 + 0.5 * sin(_Time.y * _MoteSpeed * (0.6 + 1.4 * rnd.x)
                                                  + rnd.y * 6.2831853);
                    float r     = _MoteSize * (0.35 + 0.65 * rnd.y) * (0.4 + 0.6 * tw);
                    float mote  = 1.0 - smoothstep(r * 0.45, r, length(d));
                    // fade them out toward the silhouette so they sit in the
                    // liquid instead of crawling on the surface
                    c += _MoteColor.rgb * mote * _MoteAmount * saturate(1.0 - fres * 1.3);
                }

                c *= (_EmissionBase + _EmissionByFill * pow(saturate(_Fill), 1.5));

                float a = lerp(_AlphaDrain, _AlphaFull, saturate(_Fill));
                return fixed4(saturate(c), a);
            }
            ENDCG
        }
    }
}
