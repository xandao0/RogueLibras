// Shared inverted-hull extrusion for the lifebar parts.
//
// The plain version (push every vertex along its normal) breaks on parts that
// wrap around something else: a ring's bore faces inward, so extruding it
// shoves the black hull into the tube the ring is threaded onto, and that shows
// up as dark slabs floating inside the glass.
//
// So the extrusion is gated by whether the surface faces away from the part's
// own centre. _PartCenter is the mesh bounds centre in object space, pushed per
// renderer by LifebarController. _OutlineInward = 1 restores the ungated
// behaviour if a part ever needs it.
#ifndef LIFEBAR_OUTLINE_INCLUDED
#define LIFEBAR_OUTLINE_INCLUDED

float4 _PartCenter;
float  _OutlineInward;

float3 LifebarObjectScale()
{
    float3 s = float3(
        length(unity_ObjectToWorld._m00_m10_m20),
        length(unity_ObjectToWorld._m01_m11_m21),
        length(unity_ObjectToWorld._m02_m12_m22));
    return max(s, 1e-4);
}

// vertex/normal in object space; returns the extruded object-space position.
float4 LifebarExtrude (float4 vertex, float3 normal, float width)
{
    float3 objScale = LifebarObjectScale();

    // the facing test stays in plain object space - the hull displacement is
    // the part that has to be corrected for a non-uniform transform
    float3 outward = vertex.xyz - _PartCenter.xyz;
    float  facing  = dot(normalize(normal), normalize(outward + 1e-7));
    float  gate    = lerp(smoothstep(-0.30, -0.05, facing), 1.0, _OutlineInward);

    float3 n = normalize(normal / objScale);
    float4 p = vertex;
    p.xyz += (n / objScale) * width * gate;
    return p;
}

#endif
