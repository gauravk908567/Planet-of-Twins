Shader "PoT/UIGlassPanel"
{
    // PoT UI Glass Panel — ONE generic Canvas (UI) backing-panel shader. It is NOT the ability
    // panel; the ability panel is its first consumer. Any HUD slab that wants the frosted-glass
    // look with a travelling edge current uses this same shader, dialled from the material or
    // from UIGlassPanelView. Never fork a per-consumer variant.
    //
    // Plumbing is Unity's built-in "UI/Default" (Stencil/ColorMask/ZTest/clip-rect/alpha-clip)
    // so this drops onto any UnityEngine.UI.Image and behaves in the normal UI sorting pipeline.
    //
    // THE SHAPE IS PROCEDURAL — there is no silhouette sprite and none is wanted.
    // The outline is a signed distance field: a tapered blade (thin tips, thick straight middle)
    // smooth-unioned with a raised notch at the centre where the shared-health emblem sits.
    // This exists because the panel GROWS as abilities unlock. A sliced sprite cannot do that
    // here: the notch sits at the centre, which is precisely the region a 3-slice stretches, so
    // the notch would smear wider every time the panel grew. In SDF form the caps and the notch
    // are authored in units of PANEL HEIGHT and therefore never change when the width changes —
    // only the straight middle run extends, which is exactly the authored intent.
    //
    // The travelling "current" is the second reason this is procedural. A glow that runs along
    // the outline needs distance-to-edge; a sprite would need a baked edge mask re-baked per
    // width, whereas the SDF already knows the distance, so the current stays correct at any
    // width for free. Two currents run inward from the outer tips toward the centre in the two
    // clan colours, meeting where the emblem sits.
    //
    // _MainTex is an OPTIONAL ornament overlay multiplied over the result (etched detail, clan
    // filigree). It defaults to white so that by default it changes nothing — the shape never
    // depends on it.
    Properties
    {
        [PerRendererData] _MainTex ("Ornament Overlay (optional)", 2D) = "white" {}
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

        // Written by UIGlassPanelView every time the RectTransform resizes. Without it the
        // shader cannot know its own aspect and the caps would stretch with the panel.
        [Header(Panel Metrics)]
        _PanelSize ("Panel Pixel Size", Vector) = (600,120,0,0)

        // All sizes below are in units of PANEL HEIGHT so they stay fixed as the width grows.
        [Header(Blade Silhouette)]
        _MidThickness ("Middle Thickness", Range(0.05,1)) = 0.52
        _TipThickness ("Tip Thickness", Range(0,1)) = 0.13
        _CapLength ("Cap Length", Range(0.05,4)) = 1.15
        _EdgeRound ("Corner Rounding", Range(0,0.3)) = 0.06
        _CentreOffset ("Vertical Centre Offset", Range(-0.5,0.5)) = -0.08

        [Header(Centre Notch)]
        _NotchWidth ("Notch Width", Range(0,3)) = 0.62
        _NotchHeight ("Notch Height", Range(0,3)) = 0.78
        _NotchCentreY ("Notch Centre Y", Range(-1,1)) = 0.12
        _NotchRound ("Notch Rounding", Range(0,0.5)) = 0.16
        _NotchBlend ("Notch Blend Radius", Range(0.001,0.5)) = 0.12

        // Frosted glass body. Kept dark and low alpha: the world swings from golden hour to blue
        // dusk to fog, so a near black neutral holds the panel against every background without
        // fighting either pole. Same reasoning as the bar trough colour.
        [Header(Glass Body)]
        _GlassColor ("Glass Colour", Color) = (0.035,0.04,0.06,0.44)
        _GlassTop ("Glass Top Lift", Range(0,1)) = 0.28
        _SheenStrength ("Sheen Strength", Range(0,2)) = 0.32
        _SheenAngle ("Sheen Angle", Range(-2,2)) = 0.6
        _SheenWidth ("Sheen Width", Range(0.02,2)) = 0.55
        _RimGlow ("Inner Rim Glow", Range(0,3)) = 0.55
        _RimFalloff ("Inner Rim Falloff", Range(0.005,0.5)) = 0.09

        [Header(Outline)]
        [HDR] _EdgeColor ("Edge Colour", Color) = (0.75,0.78,0.92,0.85)
        _EdgeWidth ("Edge Width", Range(0.001,0.2)) = 0.022
        _EdgeSoft ("Edge Antialias", Range(0.0005,0.05)) = 0.006

        // Two currents run inward from the tips toward the centre. Left carries clan A, right
        // carries clan B, and they meet under the emblem. Count and speed are shared so the two
        // sides stay in lockstep.
        [Header(Edge Current)]
        [HDR] _CurrentColorA ("Current Colour Left", Color) = (1.0,0.78,0.29,1)
        [HDR] _CurrentColorB ("Current Colour Right", Color) = (0.49,0.30,1.0,1)
        _CurrentStrength ("Current Strength", Range(0,4)) = 1.0
        _CurrentSpeed ("Current Speed", Range(0,4)) = 0.35
        _CurrentCount ("Current Repeat Count", Range(1,12)) = 3
        _CurrentSharpness ("Current Head Sharpness", Range(1,64)) = 14
        _CurrentFadeStart ("Current Fade Start", Range(0,1)) = 0.62
        _CurrentWidth ("Current Band Width", Range(0.005,0.3)) = 0.05

        [Header(Debug)]
        _Grayscale ("Grayscale Test", Range(0,1)) = 0
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

            float4 _PanelSize;

            float _MidThickness;
            float _TipThickness;
            float _CapLength;
            float _EdgeRound;
            float _CentreOffset;

            float _NotchWidth;
            float _NotchHeight;
            float _NotchCentreY;
            float _NotchRound;
            float _NotchBlend;

            fixed4 _GlassColor;
            float _GlassTop;
            float _SheenStrength;
            float _SheenAngle;
            float _SheenWidth;
            float _RimGlow;
            float _RimFalloff;

            fixed4 _EdgeColor;
            float _EdgeWidth;
            float _EdgeSoft;

            fixed4 _CurrentColorA;
            fixed4 _CurrentColorB;
            float _CurrentStrength;
            float _CurrentSpeed;
            float _CurrentCount;
            float _CurrentSharpness;
            float _CurrentFadeStart;
            float _CurrentWidth;

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

            // Rounded box distance. Standard iq formulation: negative inside, positive outside,
            // and it stays a true distance outside the shape which is what the edge band needs.
            float RoundBox(float2 p, float2 halfExtent, float radius)
            {
                float2 q = abs(p) - halfExtent + radius;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - radius;
            }

            // Polynomial smooth minimum. Used for the blade/notch union so the notch grows out of
            // the bar with a fillet rather than a hard seam.
            float SmoothUnion(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / max(k, 1e-4));
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            // The blade: a straight middle run of constant thickness with a fixed length cap at
            // each end tapering along a circular arc to a thin tip. Because _CapLength is in
            // height units and only halfW grows, widening the panel extends ONLY the middle.
            float BladeSDF(float2 p, float halfW)
            {
                float xa = abs(p.x);
                float capLen = min(_CapLength, halfW);
                float capStart = halfW - capLen;

                float t = saturate((xa - capStart) / max(capLen, 1e-4));
                float profile = sqrt(saturate(1.0 - t * t));
                float halfT = lerp(_TipThickness, _MidThickness, profile) * 0.5;

                float dV = abs(p.y - _CentreOffset) - halfT;
                float dH = xa - halfW;

                float2 d2 = float2(dH, dV);
                return min(max(d2.x, d2.y), 0.0) + length(max(d2, 0.0)) - _EdgeRound;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Panel space: y spans -0.5..0.5, x is scaled by aspect. One unit = panel height,
                // so every authored size below is width independent.
                float aspect = _PanelSize.x / max(_PanelSize.y, 1e-4);
                float2 p = float2((IN.texcoord.x - 0.5) * aspect, IN.texcoord.y - 0.5);
                float halfW = aspect * 0.5;

                float dBlade = BladeSDF(p, halfW);
                float dNotch = RoundBox(p - float2(0.0, _NotchCentreY),
                                        float2(_NotchWidth * 0.5, _NotchHeight * 0.5),
                                        _NotchRound);
                float d = SmoothUnion(dBlade, dNotch, _NotchBlend);

                // Coverage. Antialiased against the SDF rather than the texture so the silhouette
                // stays crisp at any panel size.
                half inside = 1.0h - smoothstep(-_EdgeSoft, _EdgeSoft, d);
                half edge = 1.0h - smoothstep(0.0, _EdgeWidth, abs(d));

                // Frosted glass body: dark neutral base, lifted toward the top, plus a broad
                // diagonal sheen and an inner rim glow that fakes the thickness of the pane.
                half topLift = saturate(IN.texcoord.y) * _GlassTop;
                half sheenCoord = (p.x * _SheenAngle + p.y) / max(_SheenWidth, 1e-4);
                half sheen = exp(-sheenCoord * sheenCoord) * _SheenStrength;
                half rim = exp(-max(-d, 0.0) / max(_RimFalloff, 1e-4)) * _RimGlow;

                half3 glassRGB = _GlassColor.rgb + topLift + sheen + _EdgeColor.rgb * rim * 0.35h;

                // Two currents travelling inward from the tips. travel runs 0 at the tip to 1 at
                // the centre on each side, so subtracting time marches the train toward centre.
                half side = p.x < 0.0 ? 0.0h : 1.0h;
                half travel = saturate(1.0h - abs(p.x) / max(halfW, 1e-4));
                half train = frac(travel * _CurrentCount - _Time.y * _CurrentSpeed);
                half head = pow(saturate(1.0h - train), _CurrentSharpness);

                // Fade out as it nears the emblem so the two sides do not pile up under the notch.
                half envelope = 1.0h - smoothstep(_CurrentFadeStart, 1.0h, travel);
                half band = 1.0h - smoothstep(0.0, _CurrentWidth, abs(d));
                half current = head * envelope * band * _CurrentStrength;

                half3 currentRGB = lerp(_CurrentColorA.rgb, _CurrentColorB.rgb, side) * current;

                half3 rgb = glassRGB * inside + _EdgeColor.rgb * edge + currentRGB;
                half a = saturate(inside * _GlassColor.a + edge * _EdgeColor.a + current);

                fixed4 col = fixed4(rgb, a) * IN.color;

                // Optional ornament overlay. Defaults to white so it is a no-op until authored.
                col *= tex2D(_MainTex, IN.texcoord);

                if (_Grayscale > 0.5)
                {
                    half l = dot(col.rgb, half3(0.299h, 0.587h, 0.114h));
                    col.rgb = half3(l, l, l);
                }

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
    Fallback "UI/Default"
}
