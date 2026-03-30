using System;
using System.Collections;
using UnityEngine;

public class TeleportAbility : AbilityBase, IAbilityHUDSource
{
    private readonly Player _caster;
    private readonly Player _target;
    private readonly SoulPlayer _soul;
    private readonly ITimeFactorController _timeFactorController;
    private readonly ICoroutineRunner _coroutineRunner;
    private readonly ISelectionLock _selectionLock;
    private IAbilityLock _casterAbilityLock;
    private IAbilityLock _targetAbilityLock;
    private IRescueActive _rescueActive;  // gate — teleport only usable during rescue

    private CharacterController _soulCC;
    private SoulPulseSystem _soulPulse;     // cached on construction
    private AbilityUpgradeData _gateData;   // injected for pulse upgrade values
    private bool _soulHasArrived = false;
    private bool _soulTimerPaused = false;
    private float _activationTime = 0f;
    private Coroutine _activeTeleportCoroutine = null;
    private Vector3 _markerPosition;
    private bool _markerSet = false;

    // ── Cancel window ─────────────────────────────────────────
    // After the soul arrives, the player has CancelHoldDuration seconds of
    // continuous X-hold to cancel the ability early. The cooldown starts
    // immediately either way (on natural end or on cancel), so there is no
    // penalty beyond the cooldown itself.
    private const float CancelHoldDuration = 0.75f; // TODO: expose on AbilityData
    private bool _cancelWindowOpen = false;
    private float _cancelHoldProgress = 0f;

    public bool IsCancelWindowOpen => _cancelWindowOpen;

    // Fired to HUD when cancel window opens/closes
    public event Action OnCancelWindowOpened;
    public event Action OnCancelWindowClosed;

    // 0-1 progress of the X-hold bar (for HUD ring or fill)
    public event Action<float> OnCancelProgressUpdated;

    // ── Timer events ──────────────────────────────────────────
    public event Action<float> OnSoulTimerUpdated;
    public event Action OnSoulArrived;

    private float _distanceTravelled = 0f;
    private float minTravelDistance = 3f;

    // ── IAbilityHUDSource ─────────────────────────────────────
    public string AbilityName => data?.name ?? "Gate";
    public bool IsActive => isActive;
    public int CurrentCharges => 1;
    public int MaxCharges => 1;
    // CooldownProgress: 0 = on cooldown, 1 = ready.
    // AbilityBase must expose CurrentCooldownNormalized for this to work.
    // If AbilityBase does not have it, this defaults to 1 (always ready visual).
    public float CooldownProgress => GetCooldownProgress();

    public TeleportAbility(
        AbilityData data,
        Player caster,
        Player target,
        Player soulPlayer,
        ITimeFactorController timeFactorController,
        ICoroutineRunner coroutineRunner,
        ISelectionLock selectionLock,
        IRescueActive rescueActive = null)
        : base(data)
    {
        _caster = caster;
        _target = target;
        _soul = soulPlayer as SoulPlayer;
        _timeFactorController = timeFactorController;
        _coroutineRunner = coroutineRunner;
        _selectionLock = selectionLock;
        _rescueActive = rescueActive;

        if (_soul == null)
            UnityEngine.Debug.LogError("[TeleportAbility] soulPlayer is not SoulPlayer.");
        else
        {
            _soul.SetLinkedPlayers(caster, target);
            _soulCC = _soul.GetComponent<CharacterController>();
            _soul.OnSoulDied += HandleSoulDied;
            _soulPulse = _soul.GetComponent<SoulPulseSystem>();
        }

        _casterAbilityLock = caster.GetComponent<AbilityController>();
        _targetAbilityLock = target.GetComponent<AbilityController>();
    }

    // ── Gate data injection ──────────────────────────────────
    /// <summary>Inject Gate upgrade data so pulse values scale with upgrades.</summary>
    public void SetGateData(AbilityUpgradeData gateData) => _gateData = gateData;

    // ── Public API ────────────────────────────────────────────
    public void SetMarkerPosition(Vector3 worldPosition)
    {
        _markerPosition = worldPosition;
        _markerSet = true;
    }

    public Transform GetTeleportPos() => _target.transform;

    public void PauseSoulTimer() => _soulTimerPaused = true;
    public void ResumeSoulTimer() => _soulTimerPaused = false;

    public void ForceEnd() { if (isActive) End(); }

    /// <summary>
    /// Called by TwinAbilityDispatcher every frame while GetCancelHeld() is true
    /// and IsCancelWindowOpen is true. Accumulates hold time; triggers ForceEnd
    /// when CancelHoldDuration is reached.
    /// </summary>
    public void NotifyCancelInput(float deltaTime)
    {
        if (!_cancelWindowOpen) return;

        _cancelHoldProgress += deltaTime;
        float progress = Mathf.Clamp01(_cancelHoldProgress / CancelHoldDuration);
        OnCancelProgressUpdated?.Invoke(progress);

        if (_cancelHoldProgress >= CancelHoldDuration)
            ForceEnd();
    }

    /// <summary>Called by dispatcher when X is released — resets hold progress.</summary>
    public void NotifyCancelReleased()
    {
        if (!_cancelWindowOpen) return;
        _cancelHoldProgress = 0f;
        OnCancelProgressUpdated?.Invoke(0f);
    }

    // ── AbilityBase overrides ─────────────────────────────────
    protected override bool Activate()
    {
        if (_soul == null || _caster == null || _coroutineRunner == null)
        {
            UnityEngine.Debug.LogError("[TeleportAbility] Null ref — check Inspector slots.");
            return false;
        }

        // GATE: only usable when a twin is in danger (_activeTarget != null).
        // HasActiveRescueTarget is true the moment HandlePlayerGrabbed runs,
        // before state transitions out of Idle. IsRescueActive was wrong here
        // because state stays Idle until the soul physically arrives — causing
        // a deadlock where teleport was blocked before rescue could even start.
        if (_rescueActive != null && !_rescueActive.HasActiveRescueTarget)
        {
            UnityEngine.Debug.Log("[TeleportAbility] Blocked — no twin in danger.");
            return false;
        }

        // Double teleport guard — soul is already active, don't fire again
        if (_soul != null && _soul.gameObject.activeSelf && isActive)
        {
            UnityEngine.Debug.Log("[TeleportAbility] Blocked — soul already active.");
            return false;
        }

        if (_activeTeleportCoroutine != null)
        {
            _coroutineRunner.StopCoroutine(_activeTeleportCoroutine);
            _activeTeleportCoroutine = null;
        }

        _timeFactorController?.TriggerEffect();
        _soulHasArrived = false;
        _soulTimerPaused = false;
        _cancelWindowOpen = false;
        _cancelHoldProgress = 0f;

        _selectionLock?.LockSelection();
        _casterAbilityLock?.LockAbilities();
        _targetAbilityLock?.LockAbilities();

        _soul.ShouldSoulSleep(false);

        // FIX: soul is invincible during all travel (forward and return).
        // Without this, enemies whose attack overlap hits the soul layer during
        // the cancel window kill the soul instantly on ResolveEffect() resuming time,
        // firing OnSoulDied → RescueEventController transitions to SoulDied.
        _soul.Health?.SetInvincible(true);

        if (_soulCC != null) _soulCC.enabled = false;
        _soul.transform.position = _caster.transform.position;
        if (_soulCC != null) _soulCC.enabled = true;

        _soul.Movement?.SetMovementLocked(true);

        Vector3 destination = _markerSet ? _markerPosition : _target.transform.position;

        _activeTeleportCoroutine = _coroutineRunner.StartCoroutine(
            TravelToTarget(_soul.transform, destination, speed: 40f,
                requireMinDistance: true,
                onArrival: () =>
                {
                    _soulHasArrived = true;
                    _activationTime = UnityEngine.Time.time;
                    _activeTeleportCoroutine = null;
                    _soul.Movement?.SetMovementLocked(false);
                    OnSoulArrived?.Invoke();

                    // Start soul pulse — applies fear/slow (and burn in Accord)
                    if (_soulPulse != null)
                    {
                        if (_gateData != null)
                            _soulPulse.ApplyUpgradeValues(
                                _gateData.CurrentPulseInterval,
                                _gateData.CurrentPulseRadius,
                                _gateData.basePulseFearDuration,
                                _gateData.basePulseSlowMultiplier,
                                _gateData.basePulseSlowDuration,
                                _gateData.baseBurnDps,
                                _gateData.baseBurnDuration);
                        _soulPulse.StartPulsing();
                    }

                    // Open cancel window after arrival
                    _cancelWindowOpen = true;
                    _cancelHoldProgress = 0f;
                    OnCancelWindowOpened?.Invoke();
                }));

        return true;
    }

    public override void Tick()
    {
        if (!_soulHasArrived) return;
        if (!_soulTimerPaused)
        {
            base.Tick();
            float elapsed = UnityEngine.Time.time - _activationTime;
            float remaining = Mathf.Max(0f, data.duration - elapsed);
            OnSoulTimerUpdated?.Invoke(remaining / data.duration);
        }
    }

    protected override void End()
    {
        _soulHasArrived = false;
        _soulTimerPaused = false;
        _markerSet = false;

        // Close cancel window
        if (_cancelWindowOpen)
        {
            _cancelWindowOpen = false;
            _cancelHoldProgress = 0f;
            OnCancelWindowClosed?.Invoke();
            OnCancelProgressUpdated?.Invoke(0f);
        }

        // Stop soul pulse
        _soulPulse?.StopPulsing();

        _soul?.Movement?.SetMovementLocked(true);

        if (_activeTeleportCoroutine != null)
        {
            _coroutineRunner.StopCoroutine(_activeTeleportCoroutine);
            _activeTeleportCoroutine = null;
        }

        _timeFactorController?.ResolveEffect();
        _selectionLock?.UnlockSelection();
        _casterAbilityLock?.UnlockAbilities();
        _targetAbilityLock?.UnlockAbilities();

        // Soul returns to caster — same travel path as forward journey
        _activeTeleportCoroutine = _coroutineRunner.StartCoroutine(
            TravelToTarget(_soul.transform, _caster.transform.position, speed: 40f,
                onArrival: () =>
                {
                    _soul?.Health?.SetInvincible(false);
                    _soul?.ShouldSoulSleep(true);
                    _soul?.Movement?.SetMovementLocked(false);
                    _activeTeleportCoroutine = null;
                }));

        // base.End() starts the cooldown — happens immediately whether natural or cancelled
        base.End();
    }

    private void HandleSoulDied() { if (isActive) ForceEnd(); }

    private IEnumerator TravelToTarget(
        Transform origin, Vector3 destination, float speed,
        Action onArrival, bool requireMinDistance = false)
    {
        _distanceTravelled = 0f;

        while (Vector3.Distance(origin.position, destination) > 0.05f)
        {
            if (_soulCC != null) _soulCC.enabled = false;

            Vector3 newPos = Vector3.MoveTowards(
                origin.position, destination, speed * UnityEngine.Time.deltaTime);

            _distanceTravelled += Vector3.Distance(origin.position, newPos);
            origin.position = newPos;

            if (_soulCC != null) _soulCC.enabled = true;
            yield return null;
        }

        if (requireMinDistance && _distanceTravelled < minTravelDistance)
            yield return new WaitForSeconds(0.3f);

        onArrival?.Invoke();
        UnityEngine.Debug.Log($"[TeleportAbility] Soul arrived — firing OnSoulArrived, subscribers={OnSoulArrived?.GetInvocationList()?.Length ?? 0}");
    }
}