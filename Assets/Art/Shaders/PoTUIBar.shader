Shader "PoT/UIBar"
{
    // PoT UI Bar — ONE generic Canvas (UI) fill-bar shader for every bar in the game:
    // twin health, enemy health, the shared-health emblem, the accord bar. Never fork a
    // per-consumer variant of this shader — dial it from UIBarView / the material instead.
    //
    // Plumbing is Unity's built-in "UI/Default" (Stencil/ColorMask/ZTest/clip-rect/alpha-clip)
    // so this drops onto any UnityEngine.UI.Image — world-space or screen-space Canvas,
    // works under RectMask2D / Mask, participates in the normal UI sorting/raycast pipeline.
    //
    // Assigned to the FILL sprite only (a solid-white-interior mask authored per frame art);
    // the frame/outline sprite is a separate Image (plain tint, drawn ABOVE this one) and is
    // NOT this shader. _MainTex.a is the only channel read from the sprite — it gates every-
    // thing: outside the mask's interior this shader is fully transparent so the frame above
    // supplies the visible outline.
    //
    // Two ORTHOGONAL channels, never conflate them:
    //   FILL  (_Fill / _FillB)   — the real value (health, accord progress...). Moves the
    //                              visible edge of the coloured region.
    //   DRAIN (_Drain / _DrainB) — a separate weakness/bond signal. Never changes how much is
    //                              filled — it only re-tints the ALREADY-filled region toward
    //                              _DrainColor. A bar can be 100% filled and 100% drained
    //                              (fully present, fully grey) at the same time.
    // On top of both: a script-driven low-value FLASH pulse and a script-driven SWEEP band
    // (accord-bar on-complete flourish). Neither channel is computed from _Fill in-shader —
    // the driving script (UIBarView) owns those thresholds/curves and just writes the amount.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (Fill Mask)", 2D) = "white" {}
        _Color ("Tint (Image.color multiplies this)", Color) = (1,1,1,1)

        // Canvas mask stencil plumbing — do not hand-edit
        [Header(UI Internal)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        [Header(Fill)]
        [HDR] _FillColor ("Fill Color", Color) = (1,1,1,1)
        _Fill ("Fill Amount", Range(0,1)) = 1
        [Enum(LeftToRight,0,RightToLeft,1,BottomToTop,2,TopToBottom,3,DualFromEdges,4,DualFromCentre,5)]
        _FillDirection ("Fill Direction", Float) = 0
        _Softness ("Fill Edge Softness (UV)", Range(0,0.2)) = 0.02
        // The art is a full-canvas PNG whose trough occupies only part of it, so 0..1 fill
        // must map to the TROUGH, not the whole sprite — otherwise "25% health" fills the
        // badge instead of the bar. Measured per art; tune by eye if the ends look off.
        _FillUVMin ("Trough Start (along fill axis)", Range(0,1)) = 0
        _FillUVMax ("Trough End (along fill axis)", Range(0,1)) = 1
        // Backing behind the UNFILLED part. The world swings from golden hour to blue dusk
        // to fog, so this is a neutral cool near-black at partial alpha: dark enough to hold
        // the bar against bright sky/sunlit stone, neutral enough not to fight either pole.
        _TroughColor ("Trough / Backing Colour", Color) = (0.045,0.045,0.065,0.62)

        // shared emblem: two clan colours in one material
        [Header(Split Halves)]
        [Toggle] _SplitHalves ("Split Halves", Float) = 0
        [HDR] _FillColorB ("Fill Color B (right half)", Color) = (1,1,1,1)
        _SplitPoint ("Split Point (UV.x)", Range(0,1)) = 0.5
        _FillB ("Fill Amount B (right half, used only when Split Halves is on)", Range(0,1)) = 1

        // orthogonal to Fill — never moves the fill edge
        [Header(Drain or Weakness)]
        _Drain ("Drain Amount", Range(0,1)) = 0
        _DrainB ("Drain Amount B (right half, used only when Split Halves is on)", Range(0,1)) = 0
        _DrainColor ("Drain Color", Color) = (0.35,0.35,0.38,1)
        [Enum(TopDown,0,BottomUp,1,Uniform,2)] _DrainDirection ("Drain Direction", Float) = 0
        _DrainSoftness ("Drain Edge Softness (UV)", Range(0,0.3)) = 0.05

        // script-driven amount — NOT computed from Fill in the shader
        [Header(Low State Flash)]
        _FlashAmount ("Flash Amount (0 = off)", Range(0,1)) = 0
        [HDR] _FlashColor ("Flash Color", Color) = (1,0.92,0.75,1)
        _FlashSpeed ("Flash Pulse Speed", Range(0,20)) = 6
        _FlashEmptyOpacity ("Flash Glow In Empty Region", Range(0,0.6)) = 0.15
        _FlashFillBoost ("Flash Brightness Inside Fill", Range(0,1)) = 0.45

        // LINE MODE — the SAME shader driving the frame/outline+symbol layer instead of the
        // fill layer. Fill and drain are ignored entirely; the sprite's line art is tinted
        // _LineColor and pulses toward _LineFlashColor (make that HDR so it blooms). This is
        // what carries the low-health flash: the symbol and outline light up, which reads far
        // better than washing the bar interior.
        [Header(Line Mode for the frame and symbol layer)]
        [Toggle] _LineMode ("Line Mode", Float) = 0
        [HDR] _LineColor ("Line Colour (normal)", Color) = (0.95,0.93,0.88,1)
        [HDR] _LineFlashColor ("Line Colour (flash)", Color) = (3,2.4,1.2,1)

        // script-driven — the accord-bar on-complete flourish
        [Header(Outline Sweep)]
        [Toggle] _SweepActive ("Sweep Active (gates the sweep entirely - see header comment)", Float) = 0
        _SweepAmount ("Sweep Position (0..1)", Range(0,1)) = 0
        [HDR] _SweepColor ("Sweep Color", Color) = (1,1,1,1)
        _SweepWidth ("Sweep Band Width (UV)", Range(0.01,0.5)) = 0.12
        [Toggle] _SweepVertical ("Sweep Vertical (on = top to bottom)", Float) = 1

        [Header(Debug)]
        _Grayscale ("Grayscale Test (0/1)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            #pragma target 3.0

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
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;

            fixed4 _FillColor;
            float _Fill;
            float _FillDirection;
            float _Softness;
            float _FillUVMin;
            float _FillUVMax;
            fixed4 _TroughColor;

            float _SplitHalves;
            fixed4 _FillColorB;
            float _SplitPoint;
            float _FillB;

            float _Drain;
            float _DrainB;
            fixed4 _DrainColor;
            float _DrainDirection;
            float _DrainSoftness;

            float _FlashAmount;
            fixed4 _FlashColor;
            float _FlashSpeed;
            float _FlashEmptyOpacity;
            float _FlashFillBoost;
            float _LineMode;
            float4 _LineColor;
            float4 _LineFlashColor;

            float _SweepActive;
            float _SweepAmount;
            fixed4 _SweepColor;
            float _SweepWidth;
            float _SweepVertical;

            float _Grayscale;

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

            // 0 at the "empty" edge, 1 at the "full" edge for the requested direction.
            // Dual modes always operate along UV.x by design (edges = left/right) — when
            // called with a half-local UV (Split Halves) they read as "that half's own
            // left/right edges", which stays meaningful.
            half FillCoordMask(float2 rawUV, float dir, float fillAmt, float softness)
            {
                // Remap into trough space so fill 0..1 spans the drawn bar, not the whole PNG.
                // _FillUVMin/Max are measured ALONG THE FILL AXIS — remap only the axis the
                // direction actually reads (audit fix 2026-07-21: Y was previously remapped
                // with the X-axis trough bounds, silently wrong for vertical fills).
                float lo = _FillUVMin, hi = _FillUVMax;
                float span = max(hi - lo, 1e-4);
                bool vertical = (dir >= 1.5 && dir < 3.5);   // BottomToTop / TopToBottom
                float2 uv = rawUV;
                if (vertical) uv.y = saturate((rawUV.y - lo) / span);
                else          uv.x = saturate((rawUV.x - lo) / span);

                if (dir < 4.5)
                {
                    float coord;
                    if (dir < 0.5)      coord = uv.x;          // LeftToRight
                    else if (dir < 1.5) coord = 1.0 - uv.x;    // RightToLeft
                    else if (dir < 2.5) coord = uv.y;          // BottomToTop
                    else                coord = 1.0 - uv.y;    // TopToBottom
                    return 1.0 - smoothstep(fillAmt - softness, fillAmt + softness, coord);
                }
                else if (dir < 5.5)
                {
                    // DualFromEdges — each side reaches inward by fillAmt*0.5 of the width.
                    float distFromEdge = min(uv.x, 1.0 - uv.x);
                    return 1.0 - smoothstep(fillAmt * 0.5 - softness, fillAmt * 0.5 + softness, distFromEdge);
                }
                else
                {
                    // DualFromCentre — grows outward from UV.x = 0.5 by fillAmt*0.5 each way.
                    float distFromCentre = abs(uv.x - 0.5);
                    return 1.0 - smoothstep(fillAmt * 0.5 - softness, fillAmt * 0.5 + softness, distFromCentre);
                }
            }

            // Drain re-tints the already-filled colour toward _DrainColor by position;
            // it never reads _Fill/FillCoordMask — kept strictly orthogonal to fill.
            half DrainCoordMask(float2 uv, float dir, float drainAmt, float softness)
            {
                if (dir > 1.5) return drainAmt; // Uniform — flat, position-independent
                float topCoord = (dir < 0.5) ? (1.0 - uv.y) : uv.y; // TopDown vs BottomUp
                return 1.0 - smoothstep(drainAmt - softness, drainAmt + softness, topCoord);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                half spriteAlpha = tex2D(_MainTex, uv).a; // mask alpha gates everything

                // Shared pulse — both layers flash in sync because they read the same
                // _FlashAmount and the same clock.
                half flashPulse = 0.5 + 0.5 * sin(_Time.y * _FlashSpeed);

                // ── LINE MODE: frame/outline + symbol layer ────────────────────────────
                if (_LineMode > 0.5)
                {
                    half3 lineCol = lerp(_LineColor.rgb, _LineFlashColor.rgb,
                                         saturate(_FlashAmount * flashPulse));
                    fixed4 lc = fixed4(lineCol * IN.color.rgb, spriteAlpha * _LineColor.a * IN.color.a);
                    if (_Grayscale > 0.5) lc.rgb = dot(lc.rgb, half3(0.2126, 0.7152, 0.0722)).xxx;
                    #ifdef UNITY_UI_CLIP_RECT
                    lc.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                    #endif
                    #ifdef UNITY_UI_ALPHACLIP
                    clip(lc.a - 0.001);
                    #endif
                    return lc;
                }

                float softness = max(_Softness, 0.0005);      // guard: smoothstep needs edge0 < edge1
                float drainSoftness = max(_DrainSoftness, 0.0005);

                half fillMask;
                half3 fillColor;
                half drainAmt;

                if (_SplitHalves > 0.5)
                {
                    float splitP = saturate(_SplitPoint);
                    float2 uvL = float2(saturate(uv.x / max(splitP, 1e-4)), uv.y);
                    float2 uvR = float2(saturate((uv.x - splitP) / max(1.0 - splitP, 1e-4)), uv.y);

                    half maskL = FillCoordMask(uvL, _FillDirection, _Fill,  softness);
                    half maskR = FillCoordMask(uvR, _FillDirection, _FillB, softness);
                    half isRight = step(splitP, uv.x);

                    fillMask  = lerp(maskL, maskR, isRight);
                    fillColor = lerp(_FillColor.rgb, _FillColorB.rgb, isRight);

                    half drainL = DrainCoordMask(uv, _DrainDirection, _Drain,  drainSoftness);
                    half drainR = DrainCoordMask(uv, _DrainDirection, _DrainB, drainSoftness);
                    drainAmt = lerp(drainL, drainR, isRight);
                }
                else
                {
                    fillMask  = FillCoordMask(uv, _FillDirection, _Fill, softness);
                    fillColor = _FillColor.rgb;
                    drainAmt  = DrainCoordMask(uv, _DrainDirection, _Drain, drainSoftness);
                }

                half3 baseColor = lerp(fillColor, _DrainColor.rgb, saturate(drainAmt));

                // --- LOW-STATE FLASH (interior) --- deliberately the QUIET half of the flash:
                // the loud half is the line/symbol layer above. Scaled _Time.y: bars are
                // gameplay values already frozen via TimeScaleService (R10) under Setsuna/
                // pause, so the pulse should freeze with them rather than tick on.
                half pulse = flashPulse;
                half3 flashAdd = _FlashColor.rgb * _FlashAmount * pulse * _FlashFillBoost;

                // --- OUTLINE SWEEP --- a soft band across the whole sprite silhouette,
                // independent of the current fill mask. _SweepActive gates it fully: at
                // _SweepAmount == 0 with _SweepActive == 0 the band is off (not sitting at
                // the top/left edge) — the driving script must clear _SweepActive when idle,
                // it is not inferred from _SweepAmount alone.
                float sweepPos = _SweepVertical > 0.5 ? (1.0 - uv.y) : uv.x;
                float sweepHalfWidth = max(_SweepWidth * 0.5, 1e-4);
                half sweepMask = _SweepActive * smoothstep(sweepHalfWidth, 0.0, abs(sweepPos - _SweepAmount));
                half3 sweepAdd = _SweepColor.rgb * sweepMask;

                // The FILL EDGE MUST STAY HONEST. The unfilled part shows the dark TROUGH
                // backing (so the bar reads against any sky/terrain), lifted slightly by the
                // flash — never the fill colour at full alpha, or a critically-low bar would
                // read as a FULL bar, the exact ambiguity this system exists to remove.
                half flashPhase = saturate(_FlashAmount * pulse);
                half3 emptyCol = lerp(_TroughColor.rgb, _FlashColor.rgb, flashPhase * 0.85h);
                half3 rgb = lerp(emptyCol, baseColor + flashAdd, fillMask) + sweepAdd;
                half emptyAlpha = saturate(_TroughColor.a + flashPhase * _FlashEmptyOpacity);
                half a = spriteAlpha * saturate(lerp(emptyAlpha, 1.0h, fillMask) + sweepMask);

                fixed4 color = fixed4(rgb * IN.color.rgb, a * IN.color.a);

                if (_Grayscale > 0.5)
                {
                    half lum = dot(color.rgb, half3(0.2126, 0.7152, 0.0722));
                    color.rgb = lum.xxx;
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
