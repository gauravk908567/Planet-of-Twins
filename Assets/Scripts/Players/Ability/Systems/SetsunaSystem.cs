using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SetsunaSystem — enhanced Soul Convergence activated inside Accord State.
///
/// UNLOCK: Automatic when both SC and Accord State are purchased. No new node.
///
/// TRIGGER: Accord active + SC charged → hold F 0.75s → Setsuna activates.
///
/// FLOW:
///   1. Snapshot: both twin positions + shared health
///   2. Time.timeScale = 0.15 — world slows to 15%
///   3. Twins use unscaled time — move at full speed in slow world
///   4. SC damage buffs remain active (+35% out, -35% in)
///   5. After 7s (unscaled): timeScale restored, SC deactivated
///   6. Both twins set invulnerable
///   7. CharacterControllers disabled
///   8. Twins lerp back to cast positions over 1.5s (unscaled)
///   9. Health restored to snapshot value
///  10. Invulnerability removed, CharacterControllers re-enabled
///
/// SCENE SETUP:
///   Add SetsunaSystem to same GO as SoulConvergenceSystem.
///   Wire: _scSystem, _accordMode, _leftTwin, _rightTwin, _healthPool.
///   Wire: _unlockStateMono (SkillTreeManager), _inputProviderMono, _rescueActiveMono.
///   Optional: _setsunaPanel (UI), _chargeBar, _timerText.
/// </summary>
public class SetsunaSystem : MonoBehaviour
{
    [Header("Inject")]
    [SerializeField] private SoulConvergenceSystem _scSystem;
    [SerializeField] private MonoBehaviour _accordModeMono;
    [SerializeField] private MonoBehaviour _inputProviderMono;
    [SerializeField] private MonoBehaviour _unlockStateMono;
    [SerializeField] private MonoBehaviour _rescueActiveMono;

    [Header("Twins")]
    [SerializeField] private Player _leftTwin;
    [SerializeField] private Player _rightTwin;

    [Header("Health pool")]
    [SerializeField] private SharedHealthPool _healthPool;

    [Header("Co-op — joint activation (D2)")]
    [Tooltip("Synchronized-start leniency (seconds, UNSCALED): the max gap between the two players' " +
             "F-holds that still counts as pressing 'together'. Higher = more forgiving; 0 = frame-perfect.")]
    [SerializeField, Range(0f, 1.5f)] private float _jointLeniency = 0.5f;
    private readonly JointHoldSync _jointSync = new JointHoldSync();

    [Header("Timing")]
    [SerializeField] private float _chargeHoldTime = 2f;
    [SerializeField] private float _activeDuration = 7f;    // unscaled seconds
    [SerializeField] private float _rewindDuration = 1.5f;  // unscaled seconds
    [SerializeField] private float _timeScaleFactor = 0.15f;
    [Tooltip("Speed multiplier applied to twins during active window. 1 = normal speed relative to slowed world. Reduce to slow twins down.")]
    [SerializeField] private float _twinSpeedMultiplier = 1f;

    [Header("Path recording")]
    [Tooltip("Record twin positions every N seconds during active window.")]
    [SerializeField] private float _recordInterval = 0.05f;

    [Header("HUD UI — optional")]
    [SerializeField] private GameObject _setsunaPanel;
    [SerializeField] private Slider _chargeBar;
    [SerializeField] private TMP_Text _timerText;

    [Header("HUD UI — Accord slot (SetsunaAccordIconUI) — owned by this system")]
    [Tooltip("PowerStatePanel inside SetsunaAccordIconUI")]
    [SerializeField] private GameObject _accordPowerStatePanel;
    [Tooltip("PowerTimerTxt inside SetsunaAccordIconUI PowerStatePanel")]
    [SerializeField] private TMP_Text _accordPowerTimerText;

    // ── Resolved ──────────────────────────────────────────────
    private IAccordModeProvider _accordMode;
    private IInputProvider _input;
    private ISkillUnlockState _unlockState;
    private IRescueActive _rescueActive;

    // ── State ─────────────────────────────────────────────────
    private enum State { Idle, Charging, Active, Rewinding }
    private State _state = State.Idle;

    private float _chargeProgress = 0f;
    private float _activeTimer = 0f;

    // ── Snapshot ──────────────────────────────────────────────
    private Vector3 _leftCastPos;
    private Vector3 _rightCastPos;
    private float _castHealth;

    // Path recording — positions sampled every _recordInterval
    private readonly System.Collections.Generic.List<Vector3> _leftPath = new();
    private readonly System.Collections.Generic.List<Vector3> _rightPath = new();
    private float _recordTimer = 0f;

    // Per-twin held cues: charge (during Charging) + trail (during Active slow-mo). Kai=right/Vethara, Lyra=left/Luminari.
    // Book resolved lazily from PlayerVfxLibrary (R4). NOTE: author the trail element unscaled so it animates at
    // full speed while the world is at timeScale 0.15.
    private CueBookData _cueBook;
    private CueHandle _chargeKaiHandle, _chargeLyraHandle, _trailKaiHandle, _trailLyraHandle;

    // ── IAbilityHUDSource (for accord panel binding) ──────────
    // Name set by designer in Inspector — never hardcoded
    public string AbilityName => "";  // name set directly on TMP text by designer
    public bool IsActive => _state == State.Active || _state == State.Rewinding;
    public bool IsCharging => _state == State.Charging;
    public float ChargeProgress => _chargeProgress;

    public static SetsunaSystem Instance { get; private set; }

    /// <summary>Fires when Setsuna's slow window begins (true) / ends (false). Manpu switches mood
    /// glyphs to hold-mode while true (E1 — read the board during slow-mo).</summary>
    public static event System.Action<bool> OnActiveChanged;

    // ── Lifecycle ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _accordMode = _accordModeMono as IAccordModeProvider;
        _input = _inputProviderMono as IInputProvider;
        _unlockState = _unlockStateMono as ISkillUnlockState;
        _rescueActive = _rescueActiveMono as IRescueActive;

        if (_scSystem == null) Debug.LogError("[SetsunaSystem] SoulConvergenceSystem not assigned.", this);
        if (_accordMode == null) Debug.LogError("[SetsunaSystem] Missing IAccordModeProvider.", this);
        if (_input == null) Debug.LogError("[SetsunaSystem] Missing IInputProvider.", this);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        _setsunaPanel?.SetActive(false);
        if (_chargeBar != null) _chargeBar.gameObject.SetActive(false);
        // _accordPowerStatePanel initial state set in scene by designer — mask controls visibility
    }

    private void Update()
    {
        if (!IsUnlocked()) return;

        switch (_state)
        {
            case State.Idle: HandleIdle(); break;
            case State.Charging: HandleCharging(); break;
            case State.Active: HandleActive(); break;
                // Rewinding: coroutine owns state — Update does nothing
        }

        RefreshAccordPanel();
    }

    // Accord PowerStatePanel owned entirely by SetsunaSystem.
    // Shows when SC power state is running OR Setsuna itself is active.
    private void RefreshAccordPanel()
    {
        if (_accordPowerStatePanel == null) return;

        bool scRunning = _scSystem != null && _scSystem.IsAbilityRunning;
        bool setsunaRunning = _state == State.Active || _state == State.Rewinding;
        bool show = scRunning || setsunaRunning;

        _accordPowerStatePanel.SetActive(show);

        if (_accordPowerTimerText != null && show)
        {
            if (setsunaRunning)
                _accordPowerTimerText.text = $"{Mathf.Max(0f, _activeDuration - _activeTimer):F1}s";
            else if (scRunning && _scSystem != null)
                _accordPowerTimerText.text = $"{Mathf.Max(0f, _scSystem.PowerTimeRemaining):F1}s";
        }
    }

    private void OnDisable()
    {
        if (_state == State.Active || _state == State.Rewinding)
            ForceEnd();
    }

    // ── State handlers ────────────────────────────────────────
    /// <summary>Both players holding the Convergence key (F) together, synced within <c>_jointLeniency</c>
    /// (D2 — Setsuna is a JOINT power). Shares the key with Soul Convergence (each has its own sync);
    /// only ticked while listening (HandleIdle resets on context-exit). Single-device (P2→P1) → solo.
    /// No router/roster → solo read.</summary>
    private bool JointConvergenceHeld()
    {
        var pa = PlayerInputRouter.For(_leftTwin);
        var pb = PlayerInputRouter.For(_rightTwin);
        if (pa == null || pb == null) return _input.GetConvergenceHeld();
        return _jointSync.Tick(pa.GetConvergenceHeld(), pb.GetConvergenceHeld(),
                               _jointLeniency, Time.unscaledDeltaTime);
    }

    private void HandleIdle()
    {
        // Must be: Accord active + SC charged + no rescue
        if (!_accordMode.IsAccordActive) { _jointSync.Reset(); return; }
        if (_scSystem == null || !_scSystem.IsCharged && !_scSystem.IsAbilityRunning) { _jointSync.Reset(); return; }
        if (_rescueActive != null && _rescueActive.IsRescueActive) { _jointSync.Reset(); return; }
        if (!JointConvergenceHeld()) return;

        _chargeProgress = 0f;
        _state = State.Charging;
        StartChargeVFX();
        if (_chargeBar != null) _chargeBar.gameObject.SetActive(true);
    }

    private void HandleCharging()
    {
        if (!JointConvergenceHeld())
        {
            CancelCharge();
            return;
        }

        if (_rescueActive != null && _rescueActive.IsRescueActive)
        {
            CancelCharge();
            return;
        }

        _chargeProgress = Mathf.Clamp01(
            _chargeProgress + Time.unscaledDeltaTime / _chargeHoldTime);

        if (_chargeBar != null) _chargeBar.value = _chargeProgress;

        if (_chargeProgress >= 1f)
        {
            CancelCharge();
            Activate();
        }
    }

    private void HandleActive()
    {
        _activeTimer += Time.unscaledDeltaTime;

        if (_timerText != null)
            _timerText.text = $"{Mathf.Max(0f, _activeDuration - _activeTimer):F1}s";

        // Record path every _recordInterval seconds
        _recordTimer += Time.unscaledDeltaTime;
        if (_recordTimer >= _recordInterval)
        {
            _recordTimer = 0f;
            _leftPath.Add(_leftTwin.transform.position);
            _rightPath.Add(_rightTwin.transform.position);
        }

        if (_activeTimer >= _activeDuration)
            StartCoroutine(BeginRewind());
    }

    // ── Activation ────────────────────────────────────────────
    private void Activate()
    {
        _state = State.Active;
        _activeTimer = 0f;

        // Snapshot positions and health
        _leftCastPos = _leftTwin.transform.position;
        _rightCastPos = _rightTwin.transform.position;
        _castHealth = _healthPool != null ? _healthPool.CurrentHealth : 0f;

        // Clear path lists from any previous activation
        _leftPath.Clear();
        _rightPath.Clear();
        _recordTimer = 0f;

        // Record starting position as first path point
        _leftPath.Add(_leftCastPos);
        _rightPath.Add(_rightCastPos);

        // Slow the world
        TimeScaleService.Instance?.Request(this, _timeScaleFactor);

        // Twins move using unscaled time — speed is designer-controlled
        float speedBoost = (1f / _timeScaleFactor) * _twinSpeedMultiplier;
        _leftTwin.Movement.SetUseUnscaledTime(true);
        _rightTwin.Movement.SetUseUnscaledTime(true);
        _leftTwin.Movement.SetSpeedMultiplier(speedBoost);
        _rightTwin.Movement.SetSpeedMultiplier(speedBoost);

        _setsunaPanel?.SetActive(true);

        StartTrailVFX();                 // per-twin slow-mo trail (charge cues already stopped in CancelCharge)
        OnActiveChanged?.Invoke(true);   // E1 — Manpu mood glyphs switch to hold-mode
        Debug.Log("[SetsunaSystem] Setsuna ACTIVATED — world slowed");
    }

    // ── Rewind ────────────────────────────────────────────────
    private IEnumerator BeginRewind()
    {
        _state = State.Rewinding;

        // Restore timeScale immediately
        TimeScaleService.Instance?.Release(this);
        OnActiveChanged?.Invoke(false);   // E1 — back to pulse-mode
        StopTrailVFX();                   // slow-mo window over — trail off

        // Restore twin movement settings
        _leftTwin.Movement.SetUseUnscaledTime(false);
        _rightTwin.Movement.SetUseUnscaledTime(false);
        _leftTwin.Movement.SetSpeedMultiplier(1f);
        _rightTwin.Movement.SetSpeedMultiplier(1f);

        // Set both twins invulnerable during rewind
        SetInvulnerable(true);

        // Disable CharacterControllers so we can set position directly
        var leftCC = _leftTwin.GetComponent<CharacterController>();
        var rightCC = _rightTwin.GetComponent<CharacterController>();
        if (leftCC != null) leftCC.enabled = false;
        if (rightCC != null) rightCC.enabled = false;

        // Replay path in reverse — step through recorded positions backward
        // Time per step = total rewind duration / number of recorded points
        int pointCount = Mathf.Max(_leftPath.Count, 1);
        float stepTime = _rewindDuration / pointCount;

        for (int i = _leftPath.Count - 1; i >= 0; i--)
        {
            Vector3 leftTarget = _leftPath[i];
            Vector3 rightTarget = i < _rightPath.Count ? _rightPath[i] : _rightCastPos;

            Vector3 leftStart = _leftTwin.transform.position;
            Vector3 rightStart = _rightTwin.transform.position;

            float elapsed = 0f;
            while (elapsed < stepTime)
            {
                float t = elapsed / stepTime;
                _leftTwin.transform.position = Vector3.Lerp(leftStart, leftTarget, t);
                _rightTwin.transform.position = Vector3.Lerp(rightStart, rightTarget, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _leftTwin.transform.position = leftTarget;
            _rightTwin.transform.position = rightTarget;
        }

        // Final snap to exact cast positions
        _leftTwin.transform.position = _leftCastPos;
        _rightTwin.transform.position = _rightCastPos;

        // Restore CharacterControllers
        if (leftCC != null) leftCC.enabled = true;
        if (rightCC != null) rightCC.enabled = true;

        // Restore health to cast snapshot
        if (_healthPool != null && _castHealth > 0f)
            _healthPool.ForceSetHealth(_castHealth);

        // Remove invulnerability
        SetInvulnerable(false);

        // End SC cleanly
        _scSystem?.ForceDeactivate();

        _setsunaPanel?.SetActive(false);
        _accordPowerStatePanel?.SetActive(false);
        _state = State.Idle;

        Debug.Log("[SetsunaSystem] Setsuna REWIND complete");
    }

    // ── Helpers ───────────────────────────────────────────────
    private void CancelCharge()
    {
        StopChargeVFX();
        _chargeProgress = 0f;
        _state = State.Idle;
        if (_chargeBar != null)
        {
            _chargeBar.value = 0f;
            _chargeBar.gameObject.SetActive(false);
        }
    }

 // ── Setsuna cues (per-twin held; Tier 1) ────────────
    private void StartChargeVFX()
    {
        _cueBook ??= VfxLibraryProvider.Instance?.Player?.Setsuna;   // R4
        var fx = FxManager.Instance;
        if (_cueBook == null || fx == null) return;
        if (_rightTwin != null) _chargeKaiHandle  = fx.PlayBook(_cueBook, FxIds.Player.Setsuna.setsuna_chargeKai,  CueContext.Follow(_rightTwin.transform));
        if (_leftTwin  != null) _chargeLyraHandle = fx.PlayBook(_cueBook, FxIds.Player.Setsuna.setsuna_chargeLyra, CueContext.Follow(_leftTwin.transform));
    }

    private void StopChargeVFX()
    {
        FxManager.Instance?.Stop(_chargeKaiHandle);
        FxManager.Instance?.Stop(_chargeLyraHandle);
        _chargeKaiHandle = _chargeLyraHandle = CueHandle.None;
    }

    private void StartTrailVFX()
    {
        _cueBook ??= VfxLibraryProvider.Instance?.Player?.Setsuna;   // R4
        var fx = FxManager.Instance;
        if (_cueBook == null || fx == null) return;
        if (_rightTwin != null) _trailKaiHandle  = fx.PlayBook(_cueBook, FxIds.Player.Setsuna.setsuna_trailKai,  CueContext.Follow(_rightTwin.transform));
        if (_leftTwin  != null) _trailLyraHandle = fx.PlayBook(_cueBook, FxIds.Player.Setsuna.setsuna_trailLyra, CueContext.Follow(_leftTwin.transform));
    }

    private void StopTrailVFX()
    {
        FxManager.Instance?.Stop(_trailKaiHandle);
        FxManager.Instance?.Stop(_trailLyraHandle);
        _trailKaiHandle = _trailLyraHandle = CueHandle.None;
    }

    public void ForceEnd()
    {
        StopAllCoroutines();
        TimeScaleService.Instance?.Release(this);
        OnActiveChanged?.Invoke(false);   // E1 — back to pulse-mode
        StopChargeVFX();                  // safety — force-end may interrupt either phase
        StopTrailVFX();

        _leftTwin.Movement.SetUseUnscaledTime(false);
        _rightTwin.Movement.SetUseUnscaledTime(false);
        _leftTwin.Movement.SetSpeedMultiplier(1f);
        _rightTwin.Movement.SetSpeedMultiplier(1f);

        var leftCC = _leftTwin.GetComponent<CharacterController>();
        var rightCC = _rightTwin.GetComponent<CharacterController>();
        if (leftCC != null) leftCC.enabled = true;
        if (rightCC != null) rightCC.enabled = true;

        SetInvulnerable(false);
        _scSystem?.ForceDeactivate();
        _setsunaPanel?.SetActive(false);
        _accordPowerStatePanel?.SetActive(false);
        _state = State.Idle;
    }

    private void SetInvulnerable(bool value)
    {
        // Lock movement AND set true invincibility during rewind — enemy hits would otherwise
        // empty the shared pool mid-coroutine before the snapshot health is restored.
        _leftTwin.Movement.SetMovementLocked(value);
        _rightTwin.Movement.SetMovementLocked(value);
        _leftTwin.Health?.SetInvincible(value);
        _rightTwin.Health?.SetInvincible(value);
    }

    private bool IsUnlocked() =>
        _unlockState != null
        && _unlockState.IsAccordStateUnlocked
        && _unlockState.IsSoulConvergenceUnlocked;
}