using UnityEngine;

/// <summary>
/// Evori-style AROUND-CHARACTER death helix — pure math, no path mesh (replaces OrbPathFollower for the
/// kill sequence). Two ribbon trails 180° apart orbit the body axis rising from the spawn point, tapering
/// inward and accelerating upward; the orbs' trail sub-emitters draw the ribbons, shedding star motes
/// (art lives on the orb prefab). Self-driving: plays 0→1 over <see cref="duration"/> on enable.
///
/// AUTO-FIT: radius and height are multiplied by the root's lossyScale (max axis) — the kill cue passes
/// enemy bounds through <c>CueContext.scale</c>, FxManager's Place() applies it to the pooled root's
/// localScale, and this driver picks it up. A Witness-sized kill spirals wide and tall; a ghost stays tight.
///
/// The SECOND ribbon is a one-time runtime clone of <see cref="orb"/> (mirror at +180°) so the prefab
/// needs only one authored orb; the clone is created inside this set-piece driver, not at a call site
/// (the FxManager-only rule bans call-site Instantiates — a driver fabricating its own sub-visual is the
/// HelixFollower/ChainBeamDriver set-piece lane). Unscaled time (a death beat must not slow under Setsuna
/// — R10). Pool-safe: every enable restarts at t=0 and re-captures the spawn origin AFTER FxManager's
/// Place() has positioned the instance (first Update, same trick as OrbPathFollower).
/// </summary>
public class CharacterHelixDriver : MonoBehaviour
{
    [Header("Orb (its trail draws the ribbon; defaults to this transform)")]
    [Tooltip("The orb to spiral. May be this root itself (the World-attached cue leaves the root free " +
             "after Place) or a child. Its trail/sub-emitters draw the ribbon.")]
    [SerializeField] private Transform orb;
    [Tooltip("Clone the orb once at +180° for the non-touching twin-ribbon look.")]
    [SerializeField] private bool twinRibbons = true;

    [Header("Helix shape (multiplied by the root's lossyScale — CueContext.scale auto-fit)")]
    [Tooltip("Waist radius around the body axis at t=0, metres (for a 1× human-sized enemy).")]
    [Min(0f)] [SerializeField] private float baseRadius = 0.55f;
    [Tooltip("Total rise over the full play, metres (for a 1× enemy).")]
    [Min(0f)] [SerializeField] private float height = 2.4f;
    [Tooltip("Full turns each ribbon makes over the whole play.")]
    [SerializeField] private float revolutions = 2.5f;
    [Tooltip("Start angle of ribbon A in degrees (B is always +180°).")]
    [SerializeField] private float startAngle = 0f;
    [Tooltip("Radius over normalized life — default tapers 1 → ~0.1 so the strands close toward the axis at the top.")]
    [SerializeField] private AnimationCurve radiusOverLife = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -0.4f), new Keyframe(1f, 0.1f, -1.6f, 0f));
    [Tooltip("Height over normalized life — default eases IN (slow lift-off, accelerating ascent).")]
    [SerializeField] private AnimationCurve heightOverLife = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.25f), new Keyframe(1f, 1f, 2.2f, 0f));

    [Header("Timing (unscaled — R10)")]
    [Min(0.01f)] [SerializeField] private float duration = 0.9f;

    private Transform _mirror;
    private float _t;
    private bool _originCaptured;
    private Vector3 _origin;

    private void OnEnable()
    {
        _t = 0f;
        _originCaptured = false;   // re-capture after FxManager.Place() moves the pooled root
    }

    private void Update()
    {
        if (orb == null) orb = transform;

        if (!_originCaptured)
        {
            _origin = transform.position;   // enemy death point — Place() has run by the first Update
            _originCaptured = true;
            EnsureMirror();
        }

        // Life advances on the shared TravelEase velocity profile (steepening ramp-in, full speed,
        // steepening fall-off — same language as the gate soul flight). Dividing by AverageMultiplier
        // keeps the authored duration as the true total time. Composes with heightOverLife.
        _t += (Time.unscaledDeltaTime / Mathf.Max(0.01f, duration))
              * (TravelEase.SpeedMultiplier(Mathf.Clamp01(_t)) / TravelEase.AverageMultiplier);
        float t = Mathf.Clamp01(_t);

        // Auto-fit: ctx.scale lands on the root's localScale via FxManager.Place().
        Vector3 ls = transform.lossyScale;
        float fit = Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
        if (fit <= 0f) fit = 1f;

        PlaceOrb(orb, t, startAngle, fit);
        if (_mirror != null) PlaceOrb(_mirror, t, startAngle + 180f, fit);
    }

    private void PlaceOrb(Transform o, float t, float angleOffsetDeg, float fit)
    {
        float ang = (angleOffsetDeg + t * revolutions * 360f) * Mathf.Deg2Rad;
        float r = baseRadius * (radiusOverLife != null ? radiusOverLife.Evaluate(t) : 1f) * fit;
        float y = height * (heightOverLife != null ? heightOverLife.Evaluate(t) : t) * fit;
        o.position = _origin + new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r);
    }

    private void EnsureMirror()
    {
        if (!twinRibbons || _mirror != null) return;
        // Clone once, PARENTED UNDER THIS ROOT so the pool's despawn/Clear(true) nets cover it,
        // with the clone's own driver stripped (only this component drives both ribbons).
        var clone = Instantiate(orb.gameObject, transform);
        clone.name = orb.name + "_Mirror";
        foreach (var d in clone.GetComponentsInChildren<CharacterHelixDriver>(true))
        {
            d.enabled = false;   // Destroy is deferred — stop it self-driving (and re-cloning) this frame
            Destroy(d);
        }
        _mirror = clone.transform;
    }
}
