using UnityEngine;

/// <summary>
/// ChainBeamDriver — mesh-free visual for the Tether-Breaker chain (Mizuki-style).
///
/// NO MESH anywhere: the chain body is a procedural multi-point <see cref="LineRenderer"/>
/// (a camera-facing strip), the energy crackle is a billboard-quad ParticleSystem. The glow is
/// the LineRenderer's HDR/additive emissive material feeding the URP Bloom pass — set the
/// material + width curve on the LineRenderer in the prefab; this script only drives geometry
/// and spark emission.
///
/// Driven by <see cref="ChainProjectile"/>. Three modes:
///   • Taut       — beam caster→target with catenary sag + perpendicular noise wobble; wobble
///                  and spark rate scale with <c>tension</c> (high while a twin is dragged).
///   • Grounded   — after a miss: ~1m straight out of the hand (sells "he's holding it") then a
///                  curve lying on the ground to the landing point ("the chain fell").
///   • Retracting — reels the grounded chain back toward the hand over a duration (the pull-back).
///
/// SETUP (when wiring the chain prefab):
///   • Add to the chain GO (or a child) that holds the LineRenderer; assign that LineRenderer.
///   • LineRenderer: assign an HDR/additive emissive material, a width curve (thick core → taper),
///     Use World Space = leave it (forced on in Awake).
///   • Optional sparks: a ParticleSystem with Simulation Space = World, Emission Rate over Time = 0
///     (we emit manually), Shape disabled/Point, billboard render (NOT mesh). Assign it.
///   • Wire ChainProjectile._beamDriver to this. Null driver = ChainProjectile falls back to its
///     legacy 2-point line, so nothing breaks before wiring.
///
/// Pool-safe: no persistent physics state (Perlin-noise wobble), retract uses a reset timer, and
/// OnDisable clears all geometry so a reused instance never shows a stale chain.
/// Timing note (R10): uses scaled time on purpose — the chain is gameplay, so Setsuna slows it
/// with everything else (intended, per design).
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class ChainBeamDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LineRenderer _line;
    [Tooltip("Optional billboard spark system riding the chain (World sim, manual emit). No mesh particles.")]
    [SerializeField] private ParticleSystem _sparks;

    [Header("Shape")]
    [SerializeField, Min(2)] private int _segments = 20;
    [Tooltip("Length of the straight taut bit out of the caster's hand in the fallen state (m).")]
    [SerializeField] private float _handForwardLength = 1f;
    [Tooltip("Slack droop when taut, scaled by (1 - tension). World metres.")]
    [SerializeField] private float _tautSag = 0.35f;
    [Tooltip("Droop of the fallen chain curve lying on the ground. World metres.")]
    [SerializeField] private float _groundCurveSag = 0.25f;

    [Header("Wobble")]
    [SerializeField] private float _idleWobble = 0.05f;
    [SerializeField] private float _tensionWobble = 0.35f;
    [SerializeField] private float _wobbleFrequency = 6f;
    [SerializeField] private float _wobbleScrollSpeed = 2.5f;

    [Header("Sparks (per metre / second)")]
    [SerializeField] private float _sparkRateIdle = 2f;
    [SerializeField] private float _sparkRateTension = 12f;

    private enum Mode { Hidden, Taut, Grounded, Retracting, Snapping }
    private Mode _mode = Mode.Hidden;

    private Vector3 _start, _end, _forward, _landing;
    private float _tension;
    private float _retractDuration = 1f, _retractElapsed;
    private float _sparkAccum;
    private float _noiseSeed;

    // Snap (break) state
    private Vector3[] _snapPositions;   // line snapshot captured at the moment of the break
    private Vector3 _snapHand, _snapFar;
    private const int SnapBurstCount = 40;

    private void Reset() => _line = GetComponent<LineRenderer>();

    private void Awake()
    {
        if (_line == null) _line = GetComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.positionCount = 0;
        _noiseSeed = Random.value * 1000f;
    }

    // Pool-safe: a reused instance must not show a stale chain.
    private void OnDisable() => HideImmediate();

    // ── Public API (called by ChainProjectile) ────────────────
    /// <summary>Taut beam from hand to endpoint. tension01: 0 = slack travel, ~0.9 = dragged twin.</summary>
    public void SetBeam(Vector3 start, Vector3 end, float tension01)
    {
        _start = start; _end = end;
        _tension = Mathf.Clamp01(tension01);
        _mode = Mode.Taut;
    }

    /// <summary>Lay the chain on the ground after a miss: 1m straight from hand, then a curve to landing.</summary>
    public void SetGroundedMiss(Vector3 handPos, Vector3 forwardDir, Vector3 landingPos)
    {
        _start = handPos;
        _forward = forwardDir.sqrMagnitude > 0.0001f ? forwardDir.normalized : Vector3.forward;
        _landing = landingPos;
        _mode = Mode.Grounded;
    }

    /// <summary>Reel the grounded chain back to the hand over <paramref name="duration"/> seconds.</summary>
    public void Retract(float duration)
    {
        _retractDuration = Mathf.Max(0.01f, duration);
        _retractElapsed = 0f;
        _mode = Mode.Retracting;
    }

    /// <summary>Snap the chain: burst sparks across its whole length, then whip both halves back to their
    /// anchors and settle over <paramref name="duration"/> s, self-hiding at the end. Call at the moment the
    /// chain breaks (mash escape) — while the taut beam is still shown, so it reads the current line as the
    /// snap start — then destroy the chain GO after the same duration.</summary>
    public void Break(float duration = 0.22f)
    {
        int n = _line != null ? _line.positionCount : 0;
        if (n < 2) { HideImmediate(); return; }
        if (_snapPositions == null || _snapPositions.Length != n) _snapPositions = new Vector3[n];
        for (int i = 0; i < n; i++) _snapPositions[i] = _line.GetPosition(i);
        _snapHand = _snapPositions[0];
        _snapFar = _snapPositions[n - 1];
        BurstSparks(SnapBurstCount);            // the snap flash across the whole chain
        _retractDuration = Mathf.Max(0.01f, duration);
        _retractElapsed = 0f;
        _mode = Mode.Snapping;
    }

    public void HideImmediate()
    {
        _mode = Mode.Hidden;
        if (_line != null) _line.positionCount = 0;
        if (_sparks != null) _sparks.Clear();
    }

    // LateUpdate: endpoints (hand/target) are finalised for the frame by ChainProjectile's coroutine.
    private void LateUpdate()
    {
        switch (_mode)
        {
            case Mode.Hidden:
                return;
            case Mode.Taut:
                BuildTaut();
                break;
            case Mode.Grounded:
                BuildGrounded(1f);
                break;
            case Mode.Retracting:
                _retractElapsed += Time.deltaTime; // scaled — gameplay (Setsuna slows reel too; intended)
                float reach = 1f - Mathf.Clamp01(_retractElapsed / _retractDuration);
                BuildGrounded(reach);
                break;
            case Mode.Snapping:
                _retractElapsed += Time.deltaTime; // scaled — gameplay
                float st = Mathf.Clamp01(_retractElapsed / _retractDuration);
                BuildSnap(st);
                if (st >= 1f) HideImmediate();
                break;
        }
    }

    // ── Geometry builders ─────────────────────────────────────
    private void BuildTaut()
    {
        EnsureCount(_segments);
        Vector3 dir = _end - _start;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        Vector3 fwd = dir.normalized;
        Vector3 perp = Vector3.Cross(fwd, Vector3.up);
        if (perp.sqrMagnitude < 0.001f) perp = Vector3.right;
        perp.Normalize();
        Vector3 perp2 = Vector3.Cross(fwd, perp);

        float sag = _tautSag * (1f - _tension);
        float wob = Mathf.Lerp(_idleWobble, _tensionWobble, _tension);
        float scroll = Time.time * _wobbleScrollSpeed;

        for (int i = 0; i < _segments; i++)
        {
            float t = i / (float)(_segments - 1);
            Vector3 p = Vector3.Lerp(_start, _end, t);
            float edge = Mathf.Sin(t * Mathf.PI);                 // 0 at anchors → no wobble/sag at the ends
            p += -Vector3.up * (edge * sag);
            float nx = (Mathf.PerlinNoise(_noiseSeed + t * _wobbleFrequency, scroll) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(scroll, _noiseSeed + t * _wobbleFrequency) - 0.5f) * 2f;
            p += perp * (nx * wob * edge) + perp2 * (ny * wob * edge);
            _line.SetPosition(i, p);
        }
        EmitSparks(1f);
    }

    // reach01: 1 = fully extended to landing, 0 = reeled fully into the hand
    private void BuildGrounded(float reach01)
    {
        EnsureCount(_segments);
        Vector3 handTip = _start + _forward * _handForwardLength;     // the straight "still holding it" bit
        Vector3 landing = Vector3.Lerp(_start, _landing, reach01);    // far end reels toward the hand
        int straight = Mathf.Max(1, _segments / 6);
        float scroll = Time.time * _wobbleScrollSpeed;
        Vector3 groundPerp = Vector3.Cross((landing - handTip).sqrMagnitude > 0.0001f
            ? (landing - handTip).normalized : Vector3.forward, Vector3.up);

        for (int i = 0; i < _segments; i++)
        {
            Vector3 p;
            if (i <= straight)
            {
                float t = i / (float)straight;
                p = Vector3.Lerp(_start, handTip, t);
            }
            else
            {
                float t = (i - straight) / (float)(_segments - 1 - straight);
                p = Vector3.Lerp(handTip, landing, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * _groundCurveSag;     // curve lying slack on the ground
                float nx = (Mathf.PerlinNoise(_noiseSeed + t * _wobbleFrequency, scroll) - 0.5f);
                p += groundPerp * (nx * _idleWobble);
            }
            _line.SetPosition(i, p);
        }
        if (reach01 > 0.05f) EmitSparks(0.3f);
    }

    private void EmitSparks(float scale)
    {
        if (_sparks == null || _line.positionCount < 2) return;
        float len = 0f;
        for (int i = 1; i < _line.positionCount; i++)
            len += Vector3.Distance(_line.GetPosition(i - 1), _line.GetPosition(i));

        float rate = Mathf.Lerp(_sparkRateIdle, _sparkRateTension, _tension) * scale;
        _sparkAccum += len * rate * Time.deltaTime; // scaled — sparks slow under Setsuna with the chain
        int count = Mathf.FloorToInt(_sparkAccum);
        _sparkAccum -= count;

        for (int s = 0; s < count; s++)
        {
            int seg = Random.Range(1, _line.positionCount);
            Vector3 pos = Vector3.Lerp(_line.GetPosition(seg - 1), _line.GetPosition(seg), Random.value);
            _sparks.Emit(new ParticleSystem.EmitParams { position = pos }, 1);
        }
    }

    // Whip both halves back to their anchors (the chain recoils and splits at the break) over st: 0→1.
    private void BuildSnap(float st)
    {
        int n = _snapPositions.Length;
        EnsureCount(n);
        float ease = 1f - (1f - st) * (1f - st);              // ease-out recoil
        Vector3 dir = _snapFar - _snapHand;
        Vector3 perp = Vector3.Cross(dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward, Vector3.up);
        perp = perp.sqrMagnitude < 0.001f ? Vector3.right : perp.normalized;
        float whip = (1f - st) * _tensionWobble * 2f;         // recoil wobble, decays as it settles
        float scroll = _noiseSeed + st * 20f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            Vector3 anchor = t < 0.5f ? _snapHand : _snapFar; // each half zips back to its own end
            Vector3 p = Vector3.Lerp(_snapPositions[i], anchor, ease);
            float mid = 1f - Mathf.Abs(t - 0.5f) * 2f;        // 1 at the break, 0 at the anchors
            float nx = Mathf.PerlinNoise(scroll + t * _wobbleFrequency, scroll) - 0.5f;
            p += perp * (nx * whip * mid);
            _line.SetPosition(i, p);
        }
        EmitSparks(0.5f);
    }

    // One-shot spark burst spread across the current line (the snap flash).
    private void BurstSparks(int count)
    {
        if (_sparks == null || _line.positionCount < 2) return;
        for (int s = 0; s < count; s++)
        {
            int seg = Random.Range(1, _line.positionCount);
            Vector3 pos = Vector3.Lerp(_line.GetPosition(seg - 1), _line.GetPosition(seg), Random.value);
            _sparks.Emit(new ParticleSystem.EmitParams { position = pos }, 1);
        }
    }

    private void EnsureCount(int n)
    {
        if (_line.positionCount != n) _line.positionCount = n;
    }
}
