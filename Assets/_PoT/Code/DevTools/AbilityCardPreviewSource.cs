using UnityEngine;

/// <summary>
/// DEV-ONLY preview harness (not shipping HUD). Fakes an <see cref="IAbilityHUDSource"/> that cycles
/// cooldown → ready → hold → active on a loop, and feeds it to the <see cref="BorderFillDriver"/> on the
/// same GameObject. Drop this on a card Image (PoT/UIAbilityCard material + BorderFillDriver) in TestLab and
/// press Play to watch the border runner sweep, the liquid tank rise on cooldown and drain during the active
/// window, and the shine flick on when ready — no full-game boot needed. Track D / Option B look review.
/// </summary>
[RequireComponent(typeof(BorderFillDriver))]
public class AbilityCardPreviewSource : MonoBehaviour, IAbilityHUDSource
{
    [Header("Phase durations (seconds, unscaled)")]
    [SerializeField] private float cooldownDuration = 3f;
    [SerializeField] private float readyHold = 1.5f;
    [SerializeField] private float holdDuration = 0.75f;
    [SerializeField] private float activeDuration = 2.5f;
    [Tooltip("Include a charge-hold phase (glow ramp) between ready and active. Off = ready → active directly.")]
    [SerializeField] private bool includeHold = true;
    [Tooltip("Simulate discrete fill jumps (e.g. Soul Convergence kills) during the cooldown phase — drives the " +
             "BorderFillDriver flash. Off = smooth cooldown fill.")]
    [SerializeField] private bool steppedFill = false;
    [SerializeField, Range(2, 20)] private int steps = 8;

    private enum Phase { Cooldown, Ready, Hold, Active }
    private Phase _phase = Phase.Cooldown;
    private float _t;

    // ── IAbilityHUDSource ─────────────────────────────────────
    public string AbilityName => "Preview";
    public int CurrentCharges => 1;
    public int MaxCharges => 1;
    public float CooldownProgress
    {
        get
        {
            if (_phase != Phase.Cooldown) return 1f;
            float f = Mathf.Clamp01(_t / cooldownDuration);
            return steppedFill ? Mathf.Floor(f * steps) / steps : f;   // discrete jumps → flash on each "soul"
        }
    }
    public bool IsActive => _phase == Phase.Active;
    public float ActiveProgress => _phase == Phase.Active ? Mathf.Clamp01(_t / activeDuration) : 1f;
    public bool IsHolding => _phase == Phase.Hold;
    public float HoldProgress => _phase == Phase.Hold ? Mathf.Clamp01(_t / holdDuration) : 0f;

    private void Start() => GetComponent<BorderFillDriver>()?.SetSource(this);

    private void Update()
    {
        _t += Time.unscaledDeltaTime;
        switch (_phase)
        {
            case Phase.Cooldown: if (_t >= cooldownDuration) Next(Phase.Ready); break;
            case Phase.Ready:    if (_t >= readyHold) Next(includeHold ? Phase.Hold : Phase.Active); break;
            case Phase.Hold:     if (_t >= holdDuration) Next(Phase.Active); break;
            case Phase.Active:   if (_t >= activeDuration) Next(Phase.Cooldown); break;
        }
    }

    private void Next(Phase p) { _phase = p; _t = 0f; }
}
