using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ButtonPromptView — the "press THIS button" feel for a mash prompt (game.md §17.5 button-ring language).
/// Sits on the mash-ring GameObject and adds tactility WITHOUT owning the ring's fill: the ring keeps
/// showing mash PROGRESS (driven by WorldSpaceRescueUI / QTEManager), while this component makes it feel
/// alive — it BREATHES when idle, and on each press it PUNCHES the ring, fires an outward RIPPLE, and
/// brightens a surround GLOW ("the glow fills with each press"). The glyph (F / E) rides in the centre and
/// punches with it; its readability glow is a TMP glow material assigned to the label in the scene (mirrors
/// the ability key-cap).
///
/// The consumer calls <see cref="Pulse"/> once per detected press. Billboarding is a sibling concern
/// (UIBillboard on the canvas). Timers run on UNSCALED time so the prompt keeps pulsing through
/// Setsuna/pause (R10).
/// </summary>
[DisallowMultipleComponent]
public class ButtonPromptView : MonoBehaviour
{
    [Header("Parts (optional — wire what the prompt has)")]
    [Tooltip("The device-aware glyph label. Its readability glow is a TMP glow material assigned in the scene.")]
    [SerializeField] private TMP_Text glyphLabel;
    [Tooltip("Soft radial glow behind the glyph; alpha rises from idle to peak on each press, then relaxes.")]
    [SerializeField] private Graphic surroundGlow;
    [Tooltip("Ripple ring that expands outward and fades on each press. Scaled/faded here; a soft ring sprite.")]
    [SerializeField] private RectTransform pingRing;

    [Header("Idle breathing (unscaled)")]
    [SerializeField] private float _idleScaleAmp = 0.05f;   // ±5% breathe so an untouched prompt still attracts
    [SerializeField] private float _idleSpeed = 2.2f;

    [Header("Press feedback (unscaled)")]
    [SerializeField] private float _pressPunch = 0.20f;     // extra scale kicked in on each press
    [SerializeField] private float _pressDecay = 6f;        // how fast the punch relaxes back
    [SerializeField] private float _pingMaxScale = 2.4f;    // the ripple grows to this × its rest size
    [SerializeField] private float _pingDuration = 0.4f;    // ripple expand+fade seconds
    [SerializeField, Range(0f, 1f)] private float _glowIdle = 0.3f;
    [SerializeField, Range(0f, 1f)] private float _glowPeak = 1f;
    [SerializeField] private float _glowDecay = 2.5f;       // how fast the glow relaxes to idle after a press

    /// <summary>The glyph label the prompt code paints via InputGlyphText. May be null.</summary>
    public TMP_Text GlyphLabel => glyphLabel;

    private RectTransform _rt;
    private CanvasGroup _pingGroup;
    private float _breathe;     // accumulates unscaled time for the idle sine
    private float _punch;       // current press punch, decays to 0
    private float _glowT;       // 1 on press, decays — drives glow brightness above idle
    private float _pingT = -1f; // 0..1 running ripple; <0 = idle
    // Rings sit at authored non-unit / non-uniform scales (the struggle MashRing is 0.4, and the FX
    // children bake their fit-to-ring size into localScale because RectTransform anchors aren't
    // editable here). So the punch/ripple MULTIPLY these captured base scales — never overwrite them,
    // which would blow the ring up to unit size and wipe the child's sizing.
    private Vector3 _baseScale = Vector3.one;
    private Vector3 _pingBaseScale = Vector3.one;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        _baseScale = _rt.localScale;
        if (pingRing != null)
        {
            _pingBaseScale = pingRing.localScale;
            _pingGroup = pingRing.GetComponent<CanvasGroup>();
            if (_pingGroup == null) _pingGroup = pingRing.gameObject.AddComponent<CanvasGroup>();
            _pingGroup.alpha = 0f;
        }
        ApplyGlow(_glowIdle);
    }

    /// <summary>Call once per button press — punches the ring/glyph, brightens the glow, fires one ripple.</summary>
    public void Pulse()
    {
        _punch = _pressPunch;
        _glowT = 1f;
        _pingT = 0f;
        if (_pingGroup != null) _pingGroup.alpha = 1f;
    }

    private void OnDisable()
    {
        _punch = 0f; _glowT = 0f; _pingT = -1f;
        if (_rt != null) _rt.localScale = _baseScale;
        if (pingRing != null) pingRing.localScale = _pingBaseScale;
        if (_pingGroup != null) _pingGroup.alpha = 0f;
        ApplyGlow(_glowIdle);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // idle breathe + press punch on the ring root (glyph + glow ride along as children)
        _breathe += dt * _idleSpeed;
        float idle = 1f + Mathf.Sin(_breathe) * _idleScaleAmp;
        _punch = Mathf.MoveTowards(_punch, 0f, _pressDecay * dt);
        float s = idle + _punch;
        if (_rt != null) _rt.localScale = new Vector3(_baseScale.x * s, _baseScale.y * s, _baseScale.z);

        // surround glow "fills" on press, relaxes to idle
        _glowT = Mathf.MoveTowards(_glowT, 0f, _glowDecay * dt);
        ApplyGlow(Mathf.Lerp(_glowIdle, _glowPeak, _glowT));

        // outward ripple on press
        if (_pingT >= 0f && pingRing != null)
        {
            _pingT += dt / Mathf.Max(_pingDuration, 0.01f);
            if (_pingT >= 1f)
            {
                _pingT = -1f;
                if (_pingGroup != null) _pingGroup.alpha = 0f;
            }
            else
            {
                float scale = Mathf.Lerp(1f, _pingMaxScale, _pingT);
                pingRing.localScale = new Vector3(_pingBaseScale.x * scale, _pingBaseScale.y * scale, _pingBaseScale.z);
                if (_pingGroup != null) _pingGroup.alpha = 1f - _pingT;   // fade as it grows
            }
        }
    }

    private void ApplyGlow(float a)
    {
        if (surroundGlow == null) return;
        var c = surroundGlow.color;
        c.a = a;
        surroundGlow.color = c;
    }
}
