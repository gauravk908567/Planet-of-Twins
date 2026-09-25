// PoT/CoexistenceSkybox v2 — a HAPPY, realistic sky whose identity survives; corruption is a TINT.
//
// Model (user spec 2026-07-15, mirrors the PoT/Coexistence surface shader):
//   • Sky = believable gradient + planar-projected cloud layer + thin haze trails near the horizon.
//   • Clouds stay white/grey (identity) — their EDGES carry faint clan rims: the sun-facing rim
//     tints Luminari gold, the shadow rim Vethara violet ("clouds outlined by both clans").
//   • Corruption NEVER blindsides the sky: as the global grows, clouds/haze/gradient/sun are
//     progressively TINTED toward the corruption colour — same "stain, don't replace" rule as the
//     house shader. No front, no wall.
//   • Sun disc: warm core with a clan-split halo (gold toward the sun's azimuth side A, violet B).
// Driven by the SAME global as the surfaces: Shader.SetGlobalFloat("_WorldCorruption", 0..1)
// (WorldAmbienceDriver owns it at runtime).
Shader "PoT/CoexistenceSkybox"
{
    Properties
    {
        [Header(Sky Gradient)]
        _SkyTop        ("Sky Top",      Color) = (0.24, 0.42, 0.80, 1)
        _SkyHorizon    ("Sky Horizon",  Color) = (0.78, 0.86, 0.95, 1)
        _SkyBottom     ("Sky Bottom (below horizon)", Color) = (0.60, 0.62, 0.66, 1)
        _HorizonSoft   ("Horizon Softness", Range(0.05, 1)) = 0.4

        [Header(Clouds (planar layer))]
        _CloudColor    ("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudShadow   ("Cloud Shadow Color", Color) = (0.72, 0.74, 0.80, 1)
        _CloudScale    ("Cloud Scale", Range(0.2, 8)) = 1.6
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudSoft     ("Cloud Softness", Range(0.02, 0.8)) = 0.25
        _CloudSpeed    ("Cloud Drift Speed", Range(0, 0.2)) = 0.01
        _CloudHeight   ("Cloud Layer Height", Range(0.5, 10)) = 2.0
        [NoScaleOffset] _CloudTex ("Cloud Texture (optional)", 2D) = "gray" {}
        _CloudTexInfluence ("Cloud Texture Influence (0 = pure procedural)", Range(0, 1)) = 0
        _CloudTexScale ("Cloud Texture Scale", Range(0.05, 4)) = 1

        [Header(Clan Rims on Clouds)]
        _ClanA         ("Clan A (Vethara violet)", Color) = (0.45, 0.20, 0.95, 1)
        _ClanB         ("Clan B (Luminari gold)",  Color) = (1.0, 0.78, 0.28, 1)
        _RimStrength   ("Clan Rim Strength", Range(0, 2)) = 0.55

        [Header(Haze Trails (horizon))]
        _HazeStrength  ("Haze Strength", Range(0, 1)) = 0.35
        _HazeScale     ("Haze Scale", Range(0.5, 12)) = 4
        _HazeSpeed     ("Haze Drift Speed", Range(0, 0.5)) = 0.04

        [Header(Sun)]
        _SunColor      ("Sun Core Color", Color) = (1, 0.97, 0.90, 1)
        _SunSize       ("Sun Size", Range(0.001, 0.5)) = 0.03
        _SunIntensity  ("Sun Intensity", Range(0, 10)) = 3
        _SunHalo       ("Clan Halo Strength", Range(0, 2)) = 0.8

        [Header(Moon)]   // oversized stylized disc; intensity 0 = off
        _MoonDir       ("Moon Direction (xyz)", Vector) = (0.35, 0.16, -1, 0)
        _MoonColor     ("Moon Color", Color) = (0.93, 0.91, 0.85, 1)
        _MoonSize      ("Moon Size", Range(0.001, 0.6)) = 0.12
        _MoonIntensity ("Moon Intensity", Range(0, 10)) = 0
        _MoonHalo      ("Moon Halo Strength", Range(0, 3)) = 1.2
        _MoonHaloColor ("Moon Halo Color", Color) = (0.72, 0.78, 1.0, 1)
        _MoonDetail    ("Moon Surface Detail", Range(0, 1)) = 0.35
        _MoonHorizonVeil("Moon Horizon Veil (bottom melts into haze)", Range(0, 1)) = 0.6

        [Header(Corruption Tint (global driven))]
        _CorruptionColor("Corruption Tint", Color) = (0.42, 0.12, 0.34, 1)
        _CorrSkyAmount  ("Max Sky Tint",    Range(0, 1)) = 0.55
        _CorrCloudAmount("Max Cloud Tint",  Range(0, 1)) = 0.8
        _CorrSunDim     ("Max Sun Dim",     Range(0, 1)) = 0.6
        _CorrNoiseScale ("Tint Unevenness Scale", Range(0.2, 8)) = 1.5
        _CorrNoiseAmt   ("Tint Unevenness", Range(0, 1)) = 0.35
        [Enum(Uniform,0,FromCentreDir,1,LeftRight,2,TopDown,3)]
        _CorrDistribution("Tint Spread (where the stain starts)", Float) = 1
        [Toggle] _CorrReverse("Reverse Spread", Float) = 0
        _CorrCenterDir  ("Stain Centre Direction (xyz, FromCentreDir mode)", Vector) = (0, 0.2, 1, 0)
        _CorrSpreadFeather("Spread Feather (how gradual the stain front is)", Range(0.1, 2)) = 0.9
        _CorruptionBias ("Corruption Bias (preview/test)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyTop, _SkyHorizon, _SkyBottom;
                float _HorizonSoft;
                half4 _CloudColor, _CloudShadow;
                float _CloudScale, _CloudCoverage, _CloudSoft, _CloudSpeed, _CloudHeight;
                float _CloudTexInfluence, _CloudTexScale;
                half4 _ClanA, _ClanB;
                float _RimStrength;
                float _HazeStrength, _HazeScale, _HazeSpeed;
                half4 _SunColor;
                float _SunSize, _SunIntensity, _SunHalo;
                float4 _MoonDir;
                half4 _MoonColor, _MoonHaloColor;
                float _MoonSize, _MoonIntensity, _MoonHalo, _MoonDetail, _MoonHorizonVeil;
                half4 _CorruptionColor;
                float _CorrSkyAmount, _CorrCloudAmount, _CorrSunDim;
                float _CorrNoiseScale, _CorrNoiseAmt, _CorruptionBias;
                float _CorrDistribution, _CorrReverse, _CorrSpreadFeather;
                float4 _CorrCenterDir;
            CBUFFER_END

            // Story-progress global — OUTSIDE the CBUFFER (WorldAmbienceDriver sets it; shared with
            // every PoT/Coexistence surface).
            float _WorldCorruption;

            TEXTURE2D(_CloudTex); SAMPLER(sampler_CloudTex);   // optional authored cloud shapes

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirWS      : TEXCOORD0;
            };

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            float vnoise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash12(i);
                float b = hash12(i + float2(1, 0));
                float c = hash12(i + float2(0, 1));
                float d = hash12(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            // Domain-warped fbm — billowy, believable cloud bodies (plain fbm reads as smoke).
            float cloudFbm(float2 p)
            {
                float2 warp = float2(vnoise2(p * 0.7 + 3.1), vnoise2(p * 0.7 - 7.7));
                p += (warp - 0.5) * 1.6;
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += a * vnoise2(p); p = p * 2.13 + 17.0; a *= 0.5; }
                return v;
            }
            // Cloud density source: procedural fbm, optionally blended with an authored cloud
            // texture (its luminance = density). Influence 0 keeps the sky byte-identical.
            float cloudDensity(float2 p)
            {
                float d = cloudFbm(p);
                if (_CloudTexInfluence > 0.001)
                {
                    half3 tex = SAMPLE_TEXTURE2D_LOD(_CloudTex, sampler_CloudTex, p * _CloudTexScale * 0.12, 0).rgb;
                    d = lerp(d, dot(tex, half3(0.299, 0.587, 0.114)), _CloudTexInfluence);
                }
                return d;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dirWS = TransformObjectToWorldDir(IN.positionOS.xyz, false);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dirWS);
                float up = dir.y;
                float w = saturate(_WorldCorruption + _CorruptionBias);
                float t = _Time.y;

                // ── gradient ─────────────────────────────────────────────────
                float tSky = saturate(up / max(_HorizonSoft, 1e-3));
                half3 sky = lerp(_SkyHorizon.rgb, _SkyTop.rgb, tSky);
                sky = lerp(_SkyBottom.rgb, sky, smoothstep(-_HorizonSoft * 0.5, 0.02, up));

                // ── clouds: project the view ray onto a flat layer above the camera ──
                // uv = where the ray pierces a plane at _CloudHeight → real sky-layer perspective
                // (clouds compress toward the horizon). Only above the horizon.
                float3 sunDir = normalize(_MainLightPosition.xyz);
                half3 col = sky;
                float cloudDens = 0.0;   // remembered for the corruption stain (clouds stain harder)
                if (up > 0.005)
                {
                    float2 uv = dir.xz / (up * _CloudHeight) * _CloudScale;
                    uv += float2(t * _CloudSpeed, t * _CloudSpeed * 0.6);
                    float d = cloudDensity(uv);
                    // coverage remap → density
                    float dens = smoothstep(1.0 - _CloudCoverage - _CloudSoft, 1.0 - _CloudCoverage + _CloudSoft, d);
                    float horizonFade = smoothstep(0.0, 0.12, up);      // clouds thin out at the horizon line
                    dens *= horizonFade;

                    // self-shading: sample density toward the sun → lit vs shadow side of each billow
                    float2 toSun = normalize(sunDir.xz + 1e-4) * 0.18;
                    float dSun = cloudDensity(uv + toSun);
                    float lit = saturate((d - dSun) * 4.0 + 0.5);       // 1 = sun-facing side
                    half3 cloudCol = lerp(_CloudShadow.rgb, _CloudColor.rgb, lit);

                    // clan rims: the soft EDGE of the cloud (density falloff band) carries the tint —
                    // gold on the sun-facing rim, violet on the shadow rim. Faint by design.
                    float edgeBand = dens * (1.0 - dens) * 4.0;          // 1 at the silhouette band
                    half3 rim = lerp(_ClanA.rgb, _ClanB.rgb, lit);
                    cloudCol += rim * edgeBand * _RimStrength;

                    col = lerp(col, cloudCol, dens);
                    cloudDens = dens;
                }

                // ── haze trails near the horizon (thin, subtle, both clans) ──
                float az = atan2(dir.z, dir.x);
                float hazeBand = saturate(1.0 - abs(up - 0.06) / 0.14);
                if (hazeBand > 0.001)
                {
                    float hA = vnoise2(float2(az * _HazeScale,  up * _HazeScale * 9.0) + t * _HazeSpeed);
                    float hB = vnoise2(float2(az * _HazeScale + 11.7, up * _HazeScale * 9.0) - t * _HazeSpeed);
                    float sA = smoothstep(0.62, 0.85, hA) * hazeBand;
                    float sB = smoothstep(0.62, 0.85, hB) * hazeBand;
                    col += _ClanA.rgb * sA * _HazeStrength * 0.5;
                    col += _ClanB.rgb * sB * _HazeStrength * 0.5;
                    col += lerp(_SkyHorizon.rgb, half3(1,1,1), 0.5) * (sA + sB) * _HazeStrength * 0.25; // hazy body
                }

                // ── moon: oversized stylized disc (WWM register) — inert at intensity 0 ──
                // Scale is an ART statement (15–25° of sky, not 0.5°). Four tricks stacked:
                // hard-ish disc + fbm crater mottling, wide cool halo (bloom does the glow),
                // horizon veil melting the low edge into the haze (seats it IN the world),
                // and the cloud layer passing IN FRONT (density attenuates the disc).
                if (_MoonIntensity > 0.001)
                {
                    float3 moonDir = normalize(_MoonDir.xyz);
                    float cosMoon = dot(dir, moonDir);
                    float mCore = smoothstep(1.0 - _MoonSize, 1.0 - _MoonSize * 0.8, cosMoon);
                    float mHalo = smoothstep(1.0 - _MoonSize * 5.0, 1.0 - _MoonSize, cosMoon) * (1.0 - mCore);
                    // stable disc-plane UVs → crater mottling from the same fbm as the clouds
                    float3 mR = normalize(cross(float3(0, 1, 0), moonDir));
                    float3 mU = cross(moonDir, mR);
                    float2 mUV = float2(dot(dir, mR), dot(dir, mU)) / max(_MoonSize, 1e-3);
                    float mottle = cloudFbm(mUV * 3.1 + 27.0);
                    float surface = 1.0 - _MoonDetail * smoothstep(0.45, 0.75, mottle) * 0.5;
                    float veil = lerp(1.0, smoothstep(-0.02, 0.10, up), _MoonHorizonVeil);
                    float moonLive = 1.0 - w * _CorrSunDim;          // corruption dims the moon like the sun
                    float discMask = mCore * veil * moonLive * (1.0 - cloudDens * 0.85);
                    col = lerp(col, _MoonColor.rgb * surface * _MoonIntensity, discMask);
                    col += _MoonHaloColor.rgb * mHalo * _MoonHalo * veil * moonLive * (1.0 - cloudDens * 0.5);
                }

                // ── sun: warm core + clan-split halo ─────────────────────────
                float cosSun = dot(dir, sunDir);
                float core = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.35, cosSun);
                float halo = smoothstep(1.0 - _SunSize * 6.0, 1.0 - _SunSize, cosSun) * (1.0 - core);
                // split the halo by which side of the sun's vertical plane the ray falls on
                float side = saturate(dot(normalize(cross(float3(0,1,0), sunDir)), dir) * 3.0 + 0.5);
                half3 haloCol = lerp(_ClanA.rgb, _ClanB.rgb, side);
                float sunLive = 1.0 - w * _CorrSunDim;                   // corruption dims the sun
                col += _SunColor.rgb * core * _SunIntensity * sunLive;
                col += haloCol * halo * _SunHalo * sunLive;

                // ── corruption TINT (never a wall): stain everything progressively ──
                // Distribution mirrors the house shader: the stain STARTS somewhere (the centre dir /
                // one side / the zenith) and expands across the dome with a wide feathered front —
                // but it is still a TINT: colours multiply toward the corruption colour, clouds stain
                // harder than the clear gradient, slow noise keeps the stain uneven/alive.
                float sC = 0.0;   // spread coordinate: 0 = stains first, 1 = stains last
                if (_CorrDistribution > 0.5)
                {
                    if (_CorrDistribution < 1.5)
                        sC = acos(clamp(dot(dir, normalize(_CorrCenterDir.xyz)), -1.0, 1.0)) / 3.14159265; // FromCentreDir
                    else if (_CorrDistribution < 2.5)
                        sC = saturate(dir.x * 0.5 + 0.5);                                                  // LeftRight
                    else
                        sC = 1.0 - saturate(up);                                                            // TopDown (zenith first)
                    if (_CorrReverse > 0.5) sC = 1.0 - sC;
                }
                // Uniform (sC stays 0) → local == w exactly; spread modes push a wide gradual front.
                float local = _CorrDistribution > 0.5
                    ? saturate((w * (1.0 + _CorrSpreadFeather) - sC) / _CorrSpreadFeather) * saturate(w * 6.0)
                    : w;
                float uneven = 1.0 + (vnoise2(dir.xz * _CorrNoiseScale + up * 2.0) - 0.5) * _CorrNoiseAmt * 2.0;
                float tint = saturate(local * lerp(_CorrSkyAmount, _CorrCloudAmount, cloudDens) * uneven);
                col = lerp(col, col * _CorruptionColor.rgb * 1.8 + _CorruptionColor.rgb * 0.10, tint);

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
