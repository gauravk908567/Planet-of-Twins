#ifndef POT_COEXISTENCE_COMMON_INCLUDED
#define POT_COEXISTENCE_COMMON_INCLUDED
// Shared by ALL passes of PoT/Coexistence (ForwardLit / ShadowCaster / DepthOnly).
// The UnityPerMaterial CBUFFER must be byte-identical across passes for the SRP batcher,
// and the wind displacement must run in every vertex stage — otherwise shadows stay still
// while the mesh sways, and the DepthOnly prepass (depth priming is on for PC_RPAsset)
// stops matching the forward pass. Include AFTER Core.hlsl.

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _VeinMap_ST;
    float4 _CorrMap_ST;
    float4 _BaseColor;
    float4 _CorruptionColor;
    float4 _ClanA;
    float4 _ClanB;
    float4 _SplitCenter;
    float  _CorruptionStrength;
    float  _CorruptionBias;
    float  _CorrDistribution;
    float  _CorrReverse;
    float  _CorrSpreadSharp;
    float  _CorrFeather;
    float  _CorrNoiseScale;
    float  _CorrNoiseAmount;
    float  _CorrMapAmount;
    float  _ClanIntensity;
    float  _GlowMode;
    float  _Distribution;
    float  _Reverse;
    float  _SplitSharp;
    float  _RadialInner;
    float  _RadialOuter;
    float  _RimPower;
    float  _RimStrength;
    float  _CreaseStrength;
    float  _CreaseSharp;
    float  _VeinStrength;
    float  _Grayscale;
    float  _WindEnable;
    float  _WindMode;
    float  _WindPivotY;
    float  _WindAmount;
    float  _WindResponse;
    float4 _EmissionColor;   // HDR additive glow (lanterns/signs) — 0 = off, no change to old materials
    float4 _EmissionMap_ST;
CBUFFER_END

// ── Wind globals — set every frame by WindDriver (Persistent). OUTSIDE the CBUFFER,
//    same contract as _WorldCorruption. Zero when no driver exists → shader is inert.
float4 _PoTWind;       // xz = horizontal direction (normalized), w = strength 0..1
float  _PoTWindGust;   // animated gust factor 0..1 (layered sines, CPU side)
// Up to 4 LocalWindZones (crack energy leaks etc.) — rows: xyz = pos, w = extra strength.
float4x4 _PoTWindPoints;
float4   _PoTWindPointRadii;

// Vertex wind sway. Mask grows with object-space distance from the attachment line:
//   Standing (mode 0): base is fixed, the TOP sways   (banner poles, plants, stall flags)
//   Hanging  (mode 1): top is fixed, the BOTTOM sways (lanterns, tassels, hanging signs)
// Per-object phase comes from the object's world origin so a row of lanterns never
// swings in lockstep. NOTE: _Time is scaled time — wind slows under Setsuna with the
// rest of the world (intended, R10 comment).
float3 PoTApplyWind(float3 positionWS, float yOS)
{
    if (_WindEnable < 0.5) return positionWS;

    float mask = (_WindMode < 0.5)
        ? saturate((yOS - _WindPivotY) * _WindResponse)
        : saturate((_WindPivotY - yOS) * _WindResponse);

    float3 origin = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
    float  phase  = dot(origin.xz, float2(0.31, 0.57));
    float  t      = _Time.y;

    // three octaves of sway + a slower perpendicular figure-eight component
    float s  = sin(t * 1.6 + phase)        * 0.55
             + sin(t * 3.7 + phase * 1.7)  * 0.30
             + sin(t * 7.3 + phase * 2.9)  * 0.15;
    float s2 = sin(t * 2.3 + phase * 1.3)  * 0.5;

    // Local zone boost (crack energy leaks): amplitude gain + a fast nervous flutter inside.
    float boost = 0.0;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float4 P = _PoTWindPoints[i];
        float  r = max(_PoTWindPointRadii[i], 0.01);
        boost += P.w * saturate(1.0 - distance(positionWS, P.xyz) / r);
    }
    s += sin(t * 13.0 + phase * 3.7) * 0.35 * saturate(boost);

    float2 dir  = _PoTWind.xz;
    float2 perp = float2(-dir.y, dir.x);
    float  amp  = _PoTWind.w * (1.0 + _PoTWindGust) * (1.0 + boost) * _WindAmount * mask;

    positionWS.xz += dir * (s * amp) + perp * (s2 * amp * 0.35);
    return positionWS;
}
#endif
