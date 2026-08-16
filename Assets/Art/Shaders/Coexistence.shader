Shader "PoT/Coexistence"
{
    // Coexistence v4 (TEST) — ONE generic shader for the whole world.
    //  BASE      : the object's own albedo + texture (dominant → identity: "this is a house/leaf").
    //  CORRUPTION: a GLOBAL float `_WorldCorruption` (0..1, driven over time by story progress) sweeps
    //              the base toward `_CorruptionColor`. v4: the sweep has its own DISTRIBUTION
    //              (Uniform / LeftRight / TopDown / Radial, reversible) + noise and an optional pattern
    //              texture, so the tint CREEPS across the object (dissolve-style front with a feathered,
    //              noisy edge) instead of fading everywhere at once.
    //  CLAN      : the two clan colours (A=Vethara violet, B=Luminari gold) shown ONLY on the object's
    //              ENERGY EDGES — silhouette (fresnel) + creases (normal-derivative edge detection, the
    //              same signal toon outlines use) + an optional leaf VEIN mask — split HALF/HALF by a
    //              chosen distribution (LeftRight / TopDown / Radial, reversible). GLOW mode = additive
    //              energy (bloom); FLAT mode = the edges are just coloured, no glow.
    //  v4 fixes  : Radial split now remaps by inner/outer RADIUS (v3 saturated to one colour on
    //              building-sized objects); crease edge detection got a sharpness control + a much
    //              higher strength ceiling so hard edges actually light up.
    Properties
    {
        [Header(Base Identity)]
        _BaseMap        ("Base Texture (optional)", 2D) = "white" {}
        _BaseColor      ("Base / Natural Colour", Color) = (0.66,0.60,0.50,1)

        [Header(Corruption)]
        _CorruptionColor ("Corruption Colour", Color) = (0.11,0.52,0.40,1)
        _CorruptionStrength ("Corruption Max Tint", Range(0,1)) = 0.85
        _CorruptionBias  ("Corruption Local Bias", Range(0,1)) = 0.0
        [Enum(Uniform,0,LeftRight,1,TopDown,2,Radial,3)] _CorrDistribution ("Corruption Spread", Float) = 0
        [Toggle] _CorrReverse ("Corruption Reverse Direction", Float) = 0
        _CorrSpreadSharp ("Corruption Spread Sharpness", Range(0.02,4)) = 0.4
        _CorrFeather    ("Corruption Front Feather", Range(0.02,1)) = 0.25
        _CorrNoiseScale ("Corruption Noise Scale", Float) = 0.8
        _CorrNoiseAmount ("Corruption Noise Amount", Range(0,1)) = 0.35
        _CorrMap        ("Corruption Pattern (optional)", 2D) = "gray" {}
        _CorrMapAmount  ("Corruption Pattern Amount", Range(0,1)) = 0.0

        [Header(Clan Energy)]
        _ClanA          ("Clan A (Vethara violet)", Color) = (0.45,0.20,0.95,1)
        _ClanB          ("Clan B (Luminari gold)", Color) = (1.0,0.78,0.28,1)
        _ClanIntensity  ("Clan Intensity", Range(0,4)) = 0.9
        [Toggle] _GlowMode ("Glow (off = Flat colour)", Float) = 1

        [Header(Distribution)]
        [Enum(LeftRight,0,TopDown,1,Radial,2)] _Distribution ("Split Distribution", Float) = 0
        [Toggle] _Reverse ("Reverse Direction", Float) = 0
        _SplitCenter    ("Split Center (object space)", Vector) = (0,0,0,0)
        _SplitSharp     ("Split Sharpness", Range(0.02,4)) = 0.4
        _RadialInner    ("Radial Inner Radius", Float) = 0.0
        _RadialOuter    ("Radial Outer Radius", Float) = 3.0

        [Header(Edges and Veins)]
        _RimPower       ("Silhouette Power", Range(0.5,8)) = 3.0
        _RimStrength    ("Silhouette Strength", Range(0,4)) = 0.6
        _CreaseStrength ("Crease Strength", Range(0,30)) = 6.0
        _CreaseSharp    ("Crease Sharpness", Range(0.1,4)) = 1.0
        _VeinMap        ("Leaf Vein Mask (optional)", 2D) = "black" {}
        _VeinStrength   ("Vein Strength", Range(0,4)) = 1.0

        // Emissive glow (lanterns, lit paper, signs). HDR colour > 1 blooms; the map (default
        // white) restricts glow to a region (e.g. the paper, not the wood frame). Black = off,
        // so every existing material is unchanged. Prefer this over placing/baking real lights.
        [Header(Emission)]
        [HDR] _EmissionColor ("Emission (HDR)", Color) = (0,0,0,1)
        _EmissionMap    ("Emission Mask (optional)", 2D) = "white" {}

        _Grayscale      ("Grayscale Test (0/1)", Range(0,1)) = 0

        // Angular sway (v5): the free side ROTATES about the attachment plane by up to Max Swing
        // Angle (the WindAnchor gizmo draws this cone — tune it to clear neighbours). Additive Sway
        // layers a small metres-based flutter on top for a natural feel. Prefer the WindAnchor
        // component to author these per-object (it writes them via MaterialPropertyBlock).
        [Header(Wind Sway (WindDriver globals))]
        [Toggle] _WindEnable ("Wind Sway", Float) = 0
        [Enum(Standing,0,Hanging,1)] _WindMode ("Wind Mode (what is anchored)", Float) = 0
        _WindPivotY     ("Attachment Y (object space)", Float) = 0
        _WindMaxAngle   ("Max Swing Half-Angle (deg +/-)", Range(0, 180)) = 8
        _WindAmount     ("Additive Sway (metres)", Range(0, 1)) = 0.06
        _WindResponse   ("Bend Stiffness (base to tip)", Range(0.05, 5)) = 1

        // Off = double-sided (greybox/ProBuilder meshes with mixed face windings render solid;
        // lighting stays correct — the fragment flips the normal on back faces).
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling (Back = normal, Off = double-sided)", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_VeinMap); SAMPLER(sampler_VeinMap);
            TEXTURE2D(_CorrMap); SAMPLER(sampler_CorrMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            // GLOBALS — driven by managers; deliberately outside the CBUFFER.
            float _WorldCorruption;   // WorldAmbienceDriver (story progress)
            float _PoTWetness;        // RainController (0 dry .. 1 soaked); exact no-op at 0

            // UnityPerMaterial CBUFFER + wind sway live in the shared include (all passes must match).
            #include "CoexistenceCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float2 uv         : TEXCOORD3;
            };

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float vnoise(float3 x)
            {
                float3 i = floor(x); float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i + float3(0,0,0)); float n100 = hash13(i + float3(1,0,0));
                float n010 = hash13(i + float3(0,1,0)); float n110 = hash13(i + float3(1,1,0));
                float n001 = hash13(i + float3(0,0,1)); float n101 = hash13(i + float3(1,0,1));
                float n011 = hash13(i + float3(0,1,1)); float n111 = hash13(i + float3(1,1,1));
                float nx00 = lerp(n000,n100,f.x); float nx10 = lerp(n010,n110,f.x);
                float nx01 = lerp(n001,n101,f.x); float nx11 = lerp(n011,n111,f.x);
                float nxy0 = lerp(nx00,nx10,f.y); float nxy1 = lerp(nx01,nx11,f.y);
                return lerp(nxy0,nxy1,f.z);
            }

            // 0 = start side, 1 = far side, for a given distribution mode over object-space offset.
            float SpreadCoord(float mode, float3 rel, float sharp, float rin, float rout)
            {
                if (mode < 0.5)      return saturate(rel.x * sharp + 0.5);                       // LeftRight
                else if (mode < 1.5) return saturate(rel.y * sharp + 0.5);                       // TopDown
                else                 return saturate((length(rel) - rin) / max(rout - rin, 1e-3)); // Radial
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                posWS = PoTApplyWind(posWS, IN.positionOS.y);   // no-op when _WindEnable is off
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.positionWS = posWS;
                OUT.normalWS = nrm.normalWS;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                // Double-sided support: back faces light with the flipped normal.
                float3 N = normalize(IN.normalWS) * (frontFace ? 1.0 : -1.0);
                float3 V = normalize(GetCameraPositionWS() - IN.positionWS);
                float3 rel = IN.positionOS - _SplitCenter.xyz;

                // --- BASE IDENTITY ---
                float2 uv = IN.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                float3 baseAlb = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;

                // --- CORRUPTION SWEEP (directional front + noisy/patterned feathered edge) ---
                float w = saturate(_WorldCorruption + _CorruptionBias);
                float sC = 0.0;
                if (_CorrDistribution > 0.5)
                    sC = SpreadCoord(_CorrDistribution - 1.0, rel, _CorrSpreadSharp, _RadialInner, _RadialOuter);
                if (_CorrReverse > 0.5) sC = 1.0 - sC;
                float2 cuv = IN.uv * _CorrMap_ST.xy + _CorrMap_ST.zw;
                float nz = vnoise(IN.positionWS * _CorrNoiseScale) * _CorrNoiseAmount
                         + (SAMPLE_TEXTURE2D(_CorrMap, sampler_CorrMap, cuv).r - 0.5) * _CorrMapAmount;
                // w sweeps the front past sC + noise; over-drive so w=1 fully covers even noisy peaks
                float corr = smoothstep(0.0, _CorrFeather,
                                        w * (1.0 + _CorrFeather + _CorrNoiseAmount + _CorrMapAmount) - sC - nz);
                // Blood-moon FILM model (user canon 2026-07-16): multiplicative tint — the
                // colour cast sits OVER the surface, every albedo detail survives underneath.
                // x2 multiply keeps mid-tones near original brightness under the cast.
                float3 corrFilm = baseAlb * _CorruptionColor.rgb * 2.0;
                float3 baseFinal = lerp(baseAlb, corrFilm, corr * _CorruptionStrength);

                // --- LIT BODY ---
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndl = saturate(dot(N, mainLight.direction));

                // --- RAIN WETNESS (global film, same pattern as corruption) ---
                // Wet surfaces darken and pick up a light glint. This shader is custom-lit
                // (no PBR smoothness), so wetness = albedo darken + cheap Blinn-Phong spec.
                float wet = saturate(_PoTWetness);
                baseFinal *= lerp(1.0, 0.68, wet);

                float3 lit = baseFinal * (mainLight.color * (ndl * mainLight.shadowAttenuation) + SampleSH(N));

                if (wet > 0.001)
                {
                    float3 H = normalize(mainLight.direction + V);
                    float spec = pow(saturate(dot(N, H)), 48.0) * ndl * mainLight.shadowAttenuation;
                    lit += mainLight.color * spec * wet * 0.55;
                }

                // --- CLAN SPLIT (half A / half B) ---
                float g = SpreadCoord(_Distribution, rel, _SplitSharp, _RadialInner, _RadialOuter);
                if (_Reverse > 0.5) g = 1.0 - g;
                float3 clanCol = lerp(_ClanA.rgb, _ClanB.rgb, g);

                // --- ENERGY EDGES: silhouette + crease edge-detection + optional leaf-vein mask ---
                float fres   = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimStrength;
                float crease  = pow(saturate(length(fwidth(N)) * _CreaseStrength), _CreaseSharp);
                float2 vuv    = IN.uv * _VeinMap_ST.xy + _VeinMap_ST.zw;
                float veinMask= SAMPLE_TEXTURE2D(_VeinMap, sampler_VeinMap, vuv).r * _VeinStrength;
                float edge    = saturate(fres + crease + veinMask);

                // --- GLOW vs FLAT ---
                float3 color;
                if (_GlowMode > 0.5) color = lit + clanCol * edge * _ClanIntensity;               // additive glow
                else                 color = lerp(lit, clanCol, saturate(edge * _ClanIntensity)); // flat colour

                // --- EMISSION (HDR additive; bloom turns it into a light source without a real light) ---
                float2 euv = IN.uv * _EmissionMap_ST.xy + _EmissionMap_ST.zw;
                color += _EmissionColor.rgb * SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, euv).rgb;

                if (_Grayscale > 0.5)
                {
                    float lum = dot(color, float3(0.2126, 0.7152, 0.0722));
                    color = lum.xxx;
                }
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "CoexistenceCommon.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; };

            V shadowVert(A IN)
            {
                V OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                posWS = PoTApplyWind(posWS, IN.positionOS.y);   // shadows sway with the mesh
                float3 nWS = TransformObjectToWorldNormal(IN.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 L = normalize(_LightPosition - posWS);
                #else
                    float3 L = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, L));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = cs;
                return OUT;
            }
            half4 shadowFrag(V IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            // culling must match ForwardLit — depth priming is on (PC_RPAsset)
            ZWrite On ColorMask 0 Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CoexistenceCommon.hlsl"
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V depthVert(A IN)
            {
                V OUT;
                // wind must match ForwardLit exactly — depth priming is on (PC_RPAsset)
                float3 posWS = PoTApplyWind(TransformObjectToWorld(IN.positionOS.xyz), IN.positionOS.y);
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }
            half4 depthFrag(V IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            // Writes world normals + depth into URP's DepthNormals prepass. SSAO Source = DepthNormals
            // (and any screen-space effect that samples _CameraNormalsTexture / _CameraDepthTexture) runs
            // that prepass; a shader WITHOUT this pass is skipped in it, so the object is missing from
            // those textures and depth-driven effects render straight through it = "see-through".
            // Wind + culling MUST match ForwardLit/DepthOnly so the prepass aligns with the lit render.
            ZWrite On Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex depthNormalsVert
            #pragma fragment depthNormalsFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CoexistenceCommon.hlsl"

            struct AN { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct VN { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            VN depthNormalsVert(AN IN)
            {
                VN OUT;
                // position + wind identical to ForwardLit/DepthOnly; normal via the same transform the
                // ShadowCaster uses (proven to resolve with Core.hlsl alone).
                float3 posWS = PoTApplyWind(TransformObjectToWorld(IN.positionOS.xyz), IN.positionOS.y);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 depthNormalsFrag(VN IN, bool frontFace : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(IN.normalWS) * (frontFace ? 1.0 : -1.0);   // match ForwardLit double-sided
                return half4(n, 0.0);
            }
            ENDHLSL
        }
    }
}
