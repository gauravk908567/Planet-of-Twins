using System.Collections;
using UnityEngine;

/// <summary>
/// The Manpu glyph renderer (MANPU_SYSTEM.md). A single world-space sprite driven in three modes by
/// the owning <c>ManpuSlot</c>: <b>Pulse</b> (transient ~1s, mood/perception), <b>Held</b> (persistent,
/// ability), <b>Closing</b> (quick one-shot on recovery). Runs on <b>unscaled</b> time so a 1s pulse
/// stays 1s under Setsuna (0.15×) instead of stretching to ~6.6s; the <b>E1 slow-mode</b> hold is baked
/// into the pulse loop (a pulse sustains while slow-mode is on, then clears when it ends). Optional
/// burst particles + one-shot sound play through <c>FxManager</c> (sound → AudioManager; no engine
/// change). Starts hidden; the slot owns lifetime.
/// </summary>
[DisallowMultipleComponent]
public class ManpuGlyph : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _renderer;

    [Header("Size / timing (unscaled seconds)")]
    [SerializeField] private float _displayScale = 0.3f;
    [SerializeField] private float _scaleIn = 0.12f;
 [SerializeField] private float _pulseHold = 1.0f; // ≈ 1s
    [SerializeField] private float _fadeOut = 0.2f;
    [SerializeField] private float _overshoot = 1.4f;
    [SerializeField] private float _colorPulseSpeed = 6f;

    private Coroutine _routine;
    private bool _slowMode;

 // leak fix — the burst accent's handle, tracked so a looping burstPrefab is stopped when the glyph
    // hides instead of leaking forever (the old code discarded this handle). A one-shot burst is unaffected
    // (already finished by Hide → Stop is a no-op).
    private CueHandle _accentHandle;
    private bool _hasAccent;

    public bool IsShowing { get; private set; }

    private void Awake()
    {
        if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>(true);
        Hide();
    }

    // ── public API (called by ManpuSlot) ──────────────────────────────────────
    public void ShowPulse(ManpuVocabulary.GlyphStyle style)
    {
        if (style == null || _renderer == null) return;
        Begin(style);
        _routine = StartCoroutine(PulseRoutine(style));
    }

    public void ShowHeld(ManpuVocabulary.GlyphStyle style)
    {
        if (style == null || _renderer == null) return;
        Begin(style);
        _routine = StartCoroutine(HeldRoutine(style));
    }

    public void PlayClosing(ManpuVocabulary.GlyphStyle style)
    {
        if (style == null || _renderer == null) { Clear(); return; }
        Begin(style);
        _routine = StartCoroutine(ClosingRoutine(style));
    }

    /// <summary>E1: while true, a Pulse sustains instead of clearing (read the board during Setsuna).</summary>
    public void SetSlowMode(bool slow) => _slowMode = slow;

    public void Clear()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        Hide();
    }

    // ── routines (unscaled) ────────────────────────────────────────────────────
    private IEnumerator PulseRoutine(ManpuVocabulary.GlyphStyle style)
    {
        yield return ScaleIn();
        // Hold ≥ _pulseHold, and keep holding while slow-mode is on (E1).
        float t = 0f;
        while (t < _pulseHold || _slowMode)
        {
            t += Time.unscaledDeltaTime;
            PulseColor(style, t);
            yield return null;
        }
        yield return FadeOut();
        Hide();
    }

    private IEnumerator HeldRoutine(ManpuVocabulary.GlyphStyle style)
    {
        yield return ScaleIn();
        float t = 0f;
        while (true) // persists until ManpuSlot calls PlayClosing/Clear
        {
            t += Time.unscaledDeltaTime;
            PulseColor(style, t);
            yield return null;
        }
    }

    private IEnumerator ClosingRoutine(ManpuVocabulary.GlyphStyle style)
    {
        yield return ScaleIn(0.6f);          // snappier than a full pulse
        float t = 0f;
        while (t < _pulseHold * 0.4f) { t += Time.unscaledDeltaTime; PulseColor(style, t); yield return null; }
        yield return FadeOut();
        Hide();
    }

    private IEnumerator ScaleIn(float speedMul = 1f)
    {
        float dur = Mathf.Max(0.01f, _scaleIn / speedMul);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.Lerp(0f, _overshoot * _displayScale, t / dur);
            transform.localScale = Vector3.one * s;
            yield return null;
        }
        // settle overshoot → display
        t = 0f; float settle = dur * 0.4f;
        while (t < settle)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.one * Mathf.Lerp(_overshoot * _displayScale, _displayScale, t / settle);
            yield return null;
        }
        transform.localScale = Vector3.one * _displayScale;
    }

    private IEnumerator FadeOut()
    {
        Color start = _renderer.color;
        Color end = start; end.a = 0f;
        float t = 0f;
        while (t < _fadeOut)
        {
            t += Time.unscaledDeltaTime;
            _renderer.color = Color.Lerp(start, end, t / _fadeOut);
            yield return null;
        }
    }

    private void PulseColor(ManpuVocabulary.GlyphStyle style, float t)
    {
        float p = (Mathf.Sin(t * _colorPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        _renderer.color = Color.Lerp(style.colorA, style.colorB, p);
    }

    private void Begin(ManpuVocabulary.GlyphStyle style)
    {
        if (_routine != null) StopCoroutine(_routine);
        gameObject.SetActive(true);
        IsShowing = true;
        transform.localScale = Vector3.zero;
        if (style.PlaySprite) { _renderer.enabled = true; _renderer.sprite = style.sprite; }
        else _renderer.enabled = false;   // ParticleOnly / no sprite → hide the renderer; the particle still plays
        _renderer.color = style.colorA;
        PlayAccents(style);
    }

    private void Hide()
    {
        StopAccents();   // never leave a looping burst accent playing after the glyph hides
        IsShowing = false;
        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

 // Burst particles + one-shot sound, both through the single FxManager entry point. The burst handle
 // is tracked so a looping burstPrefab is reclaimed on Hide; a new glyph's accent replaces the old.
    private void PlayAccents(ManpuVocabulary.GlyphStyle style)
    {
        StopAccents();
        var fx = FxManager.Instance;
        if (fx == null) return;
        var ctx = new CueContext(transform.position);
        if (style.PlayParticle)
        {
            _accentHandle = fx.PlayParticle(style.burstPrefab, ctx);
            _hasAccent = !_accentHandle.IsNone;
        }
        if (style.sound != null) fx.Play(style.sound, ctx);
    }

    private void StopAccents()
    {
        if (!_hasAccent) return;
        FxManager.Instance?.Stop(_accentHandle);
        _hasAccent = false;
    }
}
