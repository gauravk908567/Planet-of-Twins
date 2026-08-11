using System.Collections;
using UnityEngine;

/// <summary>
/// Plug-and-play reveal driver: animates ONE material float (a "reveal amount") from a start value to an
/// end value over a per-instance, Inspector-tweakable duration. Drop it on any object that carries a reveal
/// material — the chain-throw marker, the Witness reveal, a siphon-ghost bind telegraph — and set that
/// object's OWN property / duration / curve. It is deliberately NOT one shared timing: each consumer holds
/// its own driver so their reveal times differ freely (the stated requirement).
///
/// Generic mechanism, per-instance data. Uses a <see cref="MaterialPropertyBlock"/> (no per-renderer
/// material-instance leak) and is pool-safe: on enable it snaps back to <see cref="_from"/> so a reused
/// pooled instance never shows a stale reveal, and auto-plays (so a pooled cue spawn "just works" with no
/// caller wiring). It can also be driven explicitly via <see cref="Play"/> / <see cref="Reveal(float,float)"/>.
/// R10: pick scaled vs unscaled per object (a gameplay telegraph slows under Setsuna; a UI reveal shouldn't).
/// </summary>
public class MaterialRevealDriver : MonoBehaviour
{
    [Tooltip("Renderers whose material reveal float is driven. Empty = every child Renderer (incl. inactive).")]
    [SerializeField] private Renderer[] _targets;
    [Tooltip("Shader float property to drive (the reveal amount). The SDF Reveal graph uses '_val'.")]
    [SerializeField] private string _property = "_val";
    [Tooltip("Seconds for the reveal — TWEAK PER OBJECT (chain-marker windup, Witness reveal, ghost bind all differ).")]
    [Min(0f)][SerializeField] private float _duration = 0.5f;
    [Tooltip("Reveal float value at the start of the animation.")]
    [SerializeField] private float _from = 0f;
    [Tooltip("Reveal float value at the end of the animation.")]
    [SerializeField] private float _to = 1f;
    [Tooltip("Reveal shape over normalised time (X 0..1 time, Y 0..1 amount). Linear by default.")]
    [SerializeField] private AnimationCurve _curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [Tooltip("ON = unscaled real time (pause / Setsuna-proof, UI-like). OFF = scaled (slows with gameplay under Setsuna).")]
    [SerializeField] private bool _unscaled = false;
    [Tooltip("Auto-run the reveal each time this object is enabled — so a pooled cue spawn needs no extra call.")]
    [SerializeField] private bool _playOnEnable = true;

    /// <summary>The authored reveal length (seconds) — read it to sync a gameplay beat to the reveal.</summary>
    public float Duration => _duration;

    private int _propId;
    private MaterialPropertyBlock _mpb;
    private Coroutine _routine;

    private void Awake()
    {
        _propId = Shader.PropertyToID(_property);
        _mpb = new MaterialPropertyBlock();
        if (_targets == null || _targets.Length == 0)
            _targets = GetComponentsInChildren<Renderer>(true);
    }

    // Pool-safe: snap to the start value so a reused instance never shows a stale reveal, then optionally play.
    private void OnEnable()
    {
        Apply(_from);
        if (_playOnEnable) Play();
    }

    private void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    /// <summary>Run the reveal from→to over the authored <see cref="Duration"/>.</summary>
    public void Play() => Reveal(_from, _to, _duration);

    /// <summary>Run the reveal between explicit values over the authored <see cref="Duration"/>.</summary>
    public void Reveal(float from, float to) => Reveal(from, to, _duration);

    /// <summary>Run the reveal between explicit values over an explicit duration (override the authored one).</summary>
    public void Reveal(float from, float to, float duration)
    {
        if (!isActiveAndEnabled) { Apply(to); return; }   // can't run a coroutine while disabled — snap
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(RevealRoutine(from, to, Mathf.Max(0f, duration)));
    }

    /// <summary>Snap the reveal to a value immediately (no animation).</summary>
    public void SetImmediate(float value)
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        Apply(value);
    }

    private IEnumerator RevealRoutine(float from, float to, float duration)
    {
        if (duration <= 0f) { Apply(to); _routine = null; yield break; }
        float t = 0f;
        while (t < duration)
        {
            Apply(Mathf.LerpUnclamped(from, to, _curve.Evaluate(t / duration)));
            t += _unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }
        Apply(to);
        _routine = null;
    }

    private void Apply(float value)
    {
        if (_targets == null) return;
        for (int i = 0; i < _targets.Length; i++)
        {
            var r = _targets[i];
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_propId, value);
            r.SetPropertyBlock(_mpb);
        }
    }
}
