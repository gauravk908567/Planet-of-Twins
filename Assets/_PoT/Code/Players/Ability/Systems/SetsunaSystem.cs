using System.Collections;
using PoT.Fx;
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
///   8. Twins travel back to the cast positions over 1.5s (unscaled) along a smoothed path (RewindPath: stills
///      dropped, wiggles simplified, corners rounded, teleport gaps blinked), at an even speed shaped by _rewindEase
///   9. Health restored to snapshot value
///  10. Invulnerability removed, CharacterControllers re-enabled
///
/// SCENE SETUP:
///   Add SetsunaSystem to same GO as SoulConvergenceSystem.
///   Wire: _scSystem, _accordMode, _leftTwin, _rightTwin, _healthPool.
///   Wire: _unlockStateMono (SkillTreeManager), _inputProviderMono, _rescueActiveMono.
///   Optional: _setsunaPanel (UI), _chargeBar, _timerText.
/// </summary>
public class SetsunaSystem : MonoBehaviour, IAbilityHUDSource, IAbilityActiveState
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

    [Header("Rewind feel (Tracer's Recall)")]
    [Tooltip("Speed profile of the return. X = time through the rewind (0→1 of Rewind Duration), Y = share of the path " +
             "covered (0→1). The path is measured by distance, so a straight line = an even speed.")]
    [SerializeField] private AnimationCurve _rewindEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Metres. Samples closer than this to the last kept one count as standing still and are dropped, so a " +
             "pause never replays as a stop.")]
    [SerializeField] private float _rewindMinStep = 0.05f;
    [Tooltip("Metres the smoothed path may stray from the one actually run. Higher = broader, smoother curves that " +
             "cut more corners (and can clip walls); 0 = replay every wiggle.")]
    [SerializeField] private float _rewindSmoothing = 0.3f;
    [Tooltip("Metres. A jump longer than this between two samples was a teleport: the rewind blinks across it " +
             "instead of flying through whatever lay between. Normal Setsuna running is ~1 m per sample.")]
    [SerializeField] private float _rewindBlinkDistance = 8f;
    [Tooltip("Hide the twins' bodies while they travel back (Tracer's phase), so only the streak cue " +
             "(setsuna_rewindKai/Lyra) shows the motion. Leave off until the streak has its own art, or the twins " +
             "just vanish for the rewind.")]
    [SerializeField] private bool _phaseDuringRewind = false;

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
    private WorldLocationSO _leftCastLocation;    // streaming area at cast; null outside the streaming graph (TestLab)
    private WorldLocationSO _rightCastLocation;
    private float _castHealth;

    // Path recording — positions sampled every _recordInterval
    private readonly System.Collections.Generic.List<Vector3> _leftPath = new();
    private readonly System.Collections.Generic.List<Vector3> _rightPath = new();
    private float _recordTimer = 0f;

    // Per-twin held cues: charge (during Charging) + trail (during Active slow-mo) + streak (during Rewinding).
    // Kai=right/Vethara, Lyra=left/Luminari. Book resolved lazily from PlayerVfxLibrary (R4). NOTE: author the trail
    // element unscaled so it animates at full speed while the world is at timeScale 0.15.
    private CueBookData _cueBook;
    private CueHandle _chargeKaiHandle, _chargeLyraHandle, _trailKaiHandle, _trailLyraHandle;
    private CueHandle _rewindKaiHandle, _rewindLyraHandle;

    // Body renderers this system hid for the rewind phase; only these are shown again.
    private readonly System.Collections.Generic.List<Renderer> _phasedRenderers = new();

    // ── IAbilityHUDSource (Track D border-as-timer) + IAbilityActiveState ──────
    // Setsuna is the SC slot's ACCORD form (hold-charge family). While Accord is up, the SAME owner-frame card
    // that showed Soul Convergence in normal mode is re-pointed at THIS source by AccordIconSlot (no second
    // driver), so it now reads Setsuna: the SC souls meter RISES the card to ready (CooldownProgress), the joint
    // F-hold that triggers Setsuna is the hold ramp (IsHolding/HoldProgress), and the 7s slow-window (+ rewind)
    // is the active drain (IsActive/ActiveProgress). Name stays "" — the designer sets the TMP text in-scene.
    public string AbilityName => "";  // name set directly on TMP text by designer
    public bool IsActive => _state == State.Active || _state == State.Rewinding;
    public bool IsCharging => _state == State.Charging;
    public float ChargeProgress => _chargeProgress;

    public int CurrentCharges => 1;
    public int MaxCharges => 1;
    public bool IsHolding => _state == State.Charging;   // joint F-hold to trigger Setsuna
    public float HoldProgress => _chargeProgress;        // 0→1 of the activation hold
    // Souls charge the card to ready (mirrors the normal SC slot); full while the window runs.
    public float CooldownProgress =>
        IsActive ? 1f
                 : (_scSystem != null && _scSystem.SoulCap > 0
                        ? Mathf.Clamp01((float)_scSystem.SoulCount / _scSystem.SoulCap)
                        : 1f);
    // Active window drains full→empty over the 7s; the 1.5s rewind reads as fully elapsed.
    public float ActiveProgress =>
        _state == State.Active && _activeDuration > 0f ? Mathf.Clamp01(_activeTimer / _activeDuration) : 1f;

    // IAbilityActiveState — AccordIconSlot defers returning the slot to the normal SC view while Setsuna's
    // slow-window (and rewind) is still running, so the card keeps draining Setsuna instead of snapping back.
    public bool IsAbilityActive => IsActive;

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
        PoTLog.Crumb(PoTCrumb.Power, "Setsuna on");
        _state = State.Active;
        _activeTimer = 0f;

        // Snapshot positions and health
        _leftCastPos = _leftTwin.transform.position;
        _rightCastPos = _rightTwin.transform.position;
        _leftCastLocation = SceneFlowManager.Instance?.LocationOf(_leftTwin);
        _rightCastLocation = SceneFlowManager.Instance?.LocationOf(_rightTwin);
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
        PoTLog.Combat?.Info("Setsuna ACTIVATED — world slowed");
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

        NotifyStreamingOfReturn();

        // One smooth return path per twin, travelled by distance (not one fixed slice of time per sample, which
        // turned every pause into a stop and every fast run into a zip). Both twins share the clock, so they
        // arrive together.
        var leftReturn = RewindPath.FromRecording(_leftPath, _leftTwin.transform.position,
                                                  _rewindMinStep, _rewindSmoothing, _rewindBlinkDistance);
        var rightReturn = RewindPath.FromRecording(_rightPath, _rightTwin.transform.position,
                                                   _rewindMinStep, _rewindSmoothing, _rewindBlinkDistance);

        SetPhased(true);        // before the streak starts, so its renderers are never collected
        StartRewindVFX();

        float elapsed = 0f;     // unscaled: the rewind runs in real time
        while (elapsed < _rewindDuration)
        {
            float share = RewindShare(elapsed / _rewindDuration);
            _leftTwin.transform.position = leftReturn.Evaluate(share);
            _rightTwin.transform.position = rightReturn.Evaluate(share);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Final snap to exact cast positions
        _leftTwin.transform.position = _leftCastPos;
        _rightTwin.transform.position = _rightCastPos;

        StopRewindVFX();
        SetPhased(false);

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

        PoTLog.Combat?.Info("Setsuna REWIND complete");
    }

    // ── Helpers ───────────────────────────────────────────────
    // BUG-149: the rewind is a scripted teleport (CharacterControllers off, no trigger sees it), so tell streaming
    // where each twin lands. Done as the rewind STARTS, not when it ends: the cast area begins loading during the
    // flight, while the twins have no collision to fall through unloaded ground.
    private void NotifyStreamingOfReturn()
    {
        var flow = SceneFlowManager.Instance;
        if (flow == null) return;
        if (flow.LocationOf(_leftTwin) != _leftCastLocation) flow.NotifyTeleported(_leftTwin, _leftCastLocation);
        if (flow.LocationOf(_rightTwin) != _rightCastLocation) flow.NotifyTeleported(_rightTwin, _rightCastLocation);
    }

    // _rewindEase mapped to a path share, clamped so an overshooting curve can't carry the twins past either end.
    private float RewindShare(float timeShare)
    {
        timeShare = Mathf.Clamp01(timeShare);
        return Mathf.Clamp01(_rewindEase != null && _rewindEase.length > 0 ? _rewindEase.Evaluate(timeShare) : timeShare);
    }

    // Tracer's phase: hide the twins' bodies (mesh + skinned mesh renderers only; particles, lines and trails keep
    // drawing, so the streak and any aura stay). forceRenderingOff leaves each renderer's own enabled state alone,
    // and un-phasing shows only the renderers this call hid.
    private void SetPhased(bool phased)
    {
        if (!phased)
        {
            foreach (var r in _phasedRenderers) if (r != null) r.forceRenderingOff = false;
            _phasedRenderers.Clear();
            return;
        }
        if (!_phaseDuringRewind) return;
        HideBody(_leftTwin);
        HideBody(_rightTwin);
    }

    private void HideBody(Player twin)
    {
        if (twin == null) return;
        foreach (var r in twin.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || r.forceRenderingOff) continue;
            r.forceRenderingOff = true;
            _phasedRenderers.Add(r);
        }
    }

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

    // The return streak (clan colour per twin). An empty book slot plays nothing, silently.
    private void StartRewindVFX()
    {
        _cueBook ??= VfxLibraryProvider.Instance?.Player?.Setsuna;   // R4
        var fx = FxManager.Instance;
        if (_cueBook == null || fx == null) return;
        if (_rightTwin != null) _rewindKaiHandle  = fx.PlayBook(_cueBook, FxIds.Player.Setsuna.setsuna_rewindKai,  CueContext.Follow(_rightTwin.transform));
        if (_leftTwin  != null) _rewindLyraHandle = fx.PlayBook(_cueBook, FxIds.Player.Setsuna.setsuna_rewindLyra, CueContext.Follow(_leftTwin.transform));
    }

    private void StopRewindVFX()
    {
        FxManager.Instance?.Stop(_rewindKaiHandle);
        FxManager.Instance?.Stop(_rewindLyraHandle);
        _rewindKaiHandle = _rewindLyraHandle = CueHandle.None;
    }

    public void ForceEnd()
    {
        StopAllCoroutines();
        TimeScaleService.Instance?.Release(this);
        OnActiveChanged?.Invoke(false);   // E1 — back to pulse-mode
        StopChargeVFX();                  // safety — force-end may interrupt any phase
        StopTrailVFX();
        StopRewindVFX();
        SetPhased(false);                 // a rewind cut short must never leave a twin invisible

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