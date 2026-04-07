using System.Collections;
using UnityEngine;

/// <summary>
/// Ikari anger mark — pops in, pulses between two colours, fades out.
/// One GO per enemy. Play() accepts sprite + two colours per state.
///
/// PREFAB SETUP:
///   Root GO: IkariMarkVFX (this script + UIBillboard)
///   └── Mark (SpriteRenderer — your anger mark sprite)
///   └── BurstParticles (ParticleSystem — optional anger burst effect)
///       Duration 0.3s, Loop OFF, Play On Awake OFF
///       Burst 15 particles at t=0, Shape Sphere 0.1, Speed 2-4
///
/// USAGE: Call Play(sprite, colourA, colourB) — all 3 params optional.
/// Starts disabled. EnemyStateUIController calls Play() per state.
/// </summary>
public class IkariMarkVFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer _markRenderer;
    [SerializeField] private ParticleSystem _burstParticles; // optional anger burst

    [Header("Size")]
    [Tooltip("Final display scale of the mark. Reduce if too big.")]
    [SerializeField] private float _displayScale = 0.3f;

    [Header("Timing")]
    [SerializeField] private float _scaleInDuration = 0.12f;
    [SerializeField] private float _holdDuration = 0.6f;
    [SerializeField] private float _fadeOutDuration = 0.25f;
    [SerializeField] private float _overshootScale = 1.4f; // multiplier on top of _displayScale

    [Header("Pulse")]
    [SerializeField] private float _pulseSpeed = 6f; // oscillations per second between colourA/B

    private Coroutine _playCoroutine;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Play the ikari mark.
    /// sprite: which anger mark image to show (rage/fear/panic sprite).
    /// colourA/colourB: the two colours to pulse between while visible.
    /// </summary>
    public void Play(Sprite sprite = null, Color? colourA = null, Color? colourB = null)
    {
        if (_playCoroutine != null) StopCoroutine(_playCoroutine);
        gameObject.SetActive(true);

        if (_markRenderer != null && sprite != null)
            _markRenderer.sprite = sprite;

        Color a = colourA ?? Color.red;
        Color b = colourB ?? Color.black;

        if (_burstParticles != null)
            _burstParticles.Play();

        _playCoroutine = StartCoroutine(PlayRoutine(a, b));
    }

    private IEnumerator PlayRoutine(Color colourA, Color colourB)
    {
        if (_markRenderer == null) { gameObject.SetActive(false); yield break; }

        // Scale in: 0 → overshoot
        transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < _scaleInDuration)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(0f, _overshootScale * _displayScale, t / _scaleInDuration);
            transform.localScale = Vector3.one * s;
            yield return null;
        }

        // Settle: overshoot → 1
        t = 0f;
        float settleDur = _scaleInDuration * 0.4f;
        while (t < settleDur)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(_overshootScale, 1f, t / settleDur);
            transform.localScale = Vector3.one * s;
            yield return null;
        }
        transform.localScale = Vector3.one;

        // Hold + pulse between colourA and colourB
        t = 0f;
        while (t < _holdDuration)
        {
            t += Time.deltaTime;
            float pulse = (Mathf.Sin(t * _pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            _markRenderer.color = Color.Lerp(colourA, colourB, pulse);
            yield return null;
        }

        // Fade out (from current pulse colour to transparent)
        t = 0f;
        Color startColour = _markRenderer.color;
        Color fadeTo = startColour; fadeTo.a = 0f;
        while (t < _fadeOutDuration)
        {
            t += Time.deltaTime;
            _markRenderer.color = Color.Lerp(startColour, fadeTo, t / _fadeOutDuration);
            yield return null;
        }

        gameObject.SetActive(false);
        _playCoroutine = null;
    }
}