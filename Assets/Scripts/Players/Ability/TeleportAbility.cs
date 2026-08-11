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
    // Exposed so TwinAbilitySetup can configure world slow during soul travel
    // Default 0.85 = 85% world speed. Set to 1.0 to disable.
    private float _soulTravelTimeFactor = 0.85f;
    public void SetSoulTravelTimeFactor(float factor) =>
        _soulTravelTimeFactor = Mathf.Clamp(factor, 0.1f, 1f);
    private SoulPulseSystem _soulPulse;     // cached on construction
    private AbilityUpgradeData _gateData;   // injected for pulse upgrade values
    private bool _soulHasArrived = false;
    private bool _soulTimerPaused = false;
    private float _activationTime = 0f;
    private Coroutine _activeTeleportCoroutine = null;
    private Vector3 _markerPosition;
    private bool _markerSet = false;

    // Weaver's Gate cues (PlayerVfxLibrary.Teleport, R4 lazy): tele_castmark = landing telegraph @ destination,
    // tele_castout = departure burst @ caster, tele_casttravel = held effect Following the soul (the gate helix
    // rides this element), tele_castin = arrival burst @ destination. The helix set-piece driver is authored on
    // the tele_casttravel prefab. See [[project_gate_helix]].
    private CueBookData _cueBook;
    private CueHandle _travelHandle;
    // Landing telegraph held at the destination from cast until the soul arrives (looping castmark
    // prefab must be code-stopped — a fire-and-forget looping cue is held forever by FxManager).
    private CueHandle _markHandle;
    // Sequencing beats (both unscaled — Setsuna/pause must not stretch the choreography):
    // teleport-OUT plays alone, THEN the helix departs; castin plays, THEN the soul is revealed.
    private const float CastOutLeadIn = 0.25f;
    private const float CastInReveal = 0.2f;
    // Twins are movement-locked during the RETURN journey — "player can move only after teleport-in".
    private bool _twinsLocked = false;
    // HelixFollower drivers on the live tele_casttravel instances (orb A + orb B = the twin ribbons).
    // Wired per cast: endpoints = this trip's start→end, progress = the soul's actual travel fraction,
    // so the ribbon pair arrives exactly when the soul does (the "helix not playing" defect was these
    // never being driven). The travel elements are World-attached — the followers own the orb positions.
    private readonly System.Collections.Generic.List<HelixFollower> _travelHelix
        = new System.Collections.Generic.List<HelixFollower>();
    // During travel the SOUL IS HIDDEN -- the tele_casttravel helix represents it (user call
    // 2026-07-09). Renderers cached lazily; shown again on every arrival (both directions).
    private Renderer[] _soulRenderers;
    private CueBookData GateBook() => _cueBook ??= VfxLibraryProvider.Instance?.Player?.Teleport;

    // ── Cancel window ─────────────────────────────────────────
    // After the soul arrives, the player has CancelHoldDuration seconds of
    // continuous X-hold to cancel the ability early. The cooldown starts
    // immediately either way (on natural end or on cancel), so there is no
    // penalty beyond the cooldown itself.
    private const float CancelHoldDuration = 0.75f; // TODO: expose on AbilityData
    private bool _cancelWindowOpen = false;
    private float _cancelHoldProgress = 0f;

    public bool IsCancelWindowOpen => _cancelWindowOpen;

    // BUG-082: true while this twin's rescue soul is AWAY FROM HOME — from the frame the Gate
    // teleport commits (Activate) until the soul finishes travelling back to the caster (end of
    // ReturnSequence). The enemy soft-freeze (PoT.Game.IsRescueActive) must stay on through the
    // RETURN trip, which happens AFTER the rescue state has already gone Idle at Success — so the
    // rescue state alone unfreezes enemies too early. Polled by RescueEventController /
    // PoTWorldStateWriter (never cached), so a cancelled, re-cast, or destroyed ability can't
    // leave the shared freeze flag stuck true.
    public bool IsSoulDeployed { get; private set; }

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
        // A stopped return coroutine never reached its unlock — release before re-casting.
        SetTwinsMovementLocked(false);

        // BUG-082: soul is now committed to deploy — enemies stay frozen until it is home again
        // (cleared at the end of ReturnSequence). A re-cast re-enters here and re-sets true.
        IsSoulDeployed = true;

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

        // Snap the landing point to the ground so the soul stands ON it — the marker rides the
        // caster's ground plane, which left the soul's pivot at floor level (half in the ground).
        Vector3 destination = SnapToGround(_markerSet ? _markerPosition : _target.transform.position);

        _activeTeleportCoroutine = _coroutineRunner.StartCoroutine(CastSequence(destination));
        return true;
    }

    // Forward choreography (user contract, playtest round 2): teleport-OUT plays →
    // helix starts → travels → arrival stops the helix + plays teleport-IN →
    // ONLY THEN the soul appears and gameplay (pulse/timer/cancel window) begins.
    private IEnumerator CastSequence(Vector3 destination)
    {
        var gate = GateBook();
        if (gate != null)
        {
            // Tier-resolved: each id independently tries its _t[n] variant (Gate tree), else its base.
            _markHandle = FxManager.Instance?.PlayBook(gate,
                UpgradeCueResolver.Resolve(gate, _gateData, FxIds.Player.Teleport.tele_castmark),
                new CueContext(destination)) ?? CueHandle.None;
            FxManager.Instance?.PlayBook(gate,
                UpgradeCueResolver.Resolve(gate, _gateData, FxIds.Player.Teleport.tele_castout),
                new CueContext(_caster.transform.position));
        }

        // The helix IS the soul while it travels — hide the soul body until arrival.
        SetSoulVisible(false);

        // Let the departure burst read on its own before the helix leaves. Unscaled (soul travel already is).
        yield return new WaitForSecondsRealtime(CastOutLeadIn);

        Vector3 travelStart = _soul.transform.position;
        if (gate != null)
        {
            _travelHandle = FxManager.Instance?.PlayBook(gate,
                UpgradeCueResolver.Resolve(gate, _gateData, FxIds.Player.Teleport.tele_casttravel),
                new CueContext(travelStart)) ?? CueHandle.None;
            WireTravelHelix(travelStart, destination);
        }

        yield return TravelToTarget(_soul.transform, destination, speed: 40f, requireMinDistance: true,
            onArrival: null, onProgress: DriveTravelHelix);

        // Soul reached the destination — stop the travel helix + landing telegraph, play the arrival burst.
        FxManager.Instance?.Stop(_travelHandle);
        _travelHandle = CueHandle.None;
        FxManager.Instance?.Stop(_markHandle);
        _markHandle = CueHandle.None;
        if (gate != null)
            FxManager.Instance?.PlayBook(gate,
                UpgradeCueResolver.Resolve(gate, _gateData, FxIds.Player.Teleport.tele_castin),
                new CueContext(destination));

        // The soul appears ONLY after the teleport-in burst has started reading.
        yield return new WaitForSecondsRealtime(CastInReveal);
        SetSoulVisible(true);   // reveal under the tele_castin burst (masks the pop-in)

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

        // Slow world to 85% so enemies move during soul travel
        // Soul movement uses unscaled deltaTime — unaffected by timeScale
        TimeScaleService.Instance?.Request(this, _soulTravelTimeFactor);

        // Open cancel window after arrival
        _cancelWindowOpen = true;
        _cancelHoldProgress = 0f;
        OnCancelWindowOpened?.Invoke();
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

        // Stop any in-flight forward travel cue + held landing telegraph
        // (End may fire mid-travel on cancel / soul death).
        FxManager.Instance?.Stop(_travelHandle);
        _travelHandle = CueHandle.None;
        FxManager.Instance?.Stop(_markHandle);
        _markHandle = CueHandle.None;

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

        // Restore world speed
        TimeScaleService.Instance?.Release(this);

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

        // Soul returns to caster — mirrored choreography: teleport-OUT at the soul's CURRENT
        // position → helix travels back → teleport-IN at the twin → only then the player can move.
        if (_soul != null)
            _activeTeleportCoroutine = _coroutineRunner.StartCoroutine(ReturnSequence());

        // base.End() starts the cooldown — happens immediately whether natural or cancelled
        base.End();
    }

    private IEnumerator ReturnSequence()
    {
        var gate = GateBook();
        if (gate != null)
            FxManager.Instance?.PlayBook(gate,
                UpgradeCueResolver.Resolve(gate, _gateData, FxIds.Player.Teleport.tele_castout),
                new CueContext(_soul.transform.position));

        SetSoulVisible(false);   // helix represents the returning soul too
        SetTwinsMovementLocked(true);   // twins hold still until the return teleport-in lands

        yield return new WaitForSecondsRealtime(CastOutLeadIn);   // unscaled — cast beat

        Vector3 returnStart = _soul.transform.position;
        Vector3 returnEnd = _caster.transform.position;
        if (gate != null)
        {
            _travelHandle = FxManager.Instance?.PlayBook(gate,
                UpgradeCueResolver.Resolve(gate, _gateData, FxIds.Player.Teleport.tele_casttravel),
                new CueContext(returnStart)) ?? CueHandle.None;
            WireTravelHelix(returnStart, returnEnd);
        }

        // Twins are locked, so the caster position is stable for the whole flight.
        yield return TravelToTarget(_soul.transform, returnEnd, speed: 40f,
            onArrival: null, onProgress: DriveTravelHelix);

        FxManager.Instance?.Stop(_travelHandle);
        _travelHandle = CueHandle.None;
        if (gate != null)
            FxManager.Instance?.PlayBook(gate,
                UpgradeCueResolver.Resolve(gate, _gateData, FxIds.Player.Teleport.tele_castin),
                new CueContext(_caster.transform.position));

        yield return new WaitForSecondsRealtime(CastInReveal);   // unscaled — arrival beat

        SetSoulVisible(true);   // clean state for the next (non-gate) soul use
        _soul?.Health?.SetInvincible(false);
        _soul?.ShouldSoulSleep(true);
        _soul?.Movement?.SetMovementLocked(false);
        SetTwinsMovementLocked(false);
        IsSoulDeployed = false;   // BUG-082: soul is home — release the enemy freeze extension
        _activeTeleportCoroutine = null;
    }

    // Twin movement lock for the return beat. Re-entrancy safe: Activate() force-releases a
    // stale lock if a previous return coroutine was cut short (StopCoroutine skips the unlock).
    private void SetTwinsMovementLocked(bool locked)
    {
        if (_twinsLocked == locked) return;
        _twinsLocked = locked;
        _caster?.Movement?.SetMovementLocked(locked);
        _target?.Movement?.SetMovementLocked(locked);
    }
    // Raycast the ground under a landing point and stand the soul's CharacterController ON it
    // (marker positions ride the caster's ground plane — wrong on slopes/steps, and the soul's
    // pivot sits mid-body, so raw marker Y buried it to the waist).
    private Vector3 SnapToGround(Vector3 pos)
    {
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            float pivotAboveFeet = 1f; // fallback if no CC
            if (_soulCC != null)
                pivotAboveFeet = _soulCC.height * 0.5f - _soulCC.center.y + _soulCC.skinWidth;
            pos.y = hit.point.y + pivotAboveFeet;
        }
        return pos;
    }

    private void SetSoulVisible(bool visible)
    {
        if (_soul == null) return;
        _soulRenderers ??= _soul.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < _soulRenderers.Length; i++)
            if (_soulRenderers[i] != null) _soulRenderers[i].enabled = visible;
    }


    private void HandleSoulDied() { if (isActive) ForceEnd(); }

    private void WireTravelHelix(Vector3 start, Vector3 end)
    {
        _travelHelix.Clear();
        FxManager.Instance?.FindAllOnInstance(_travelHandle, _travelHelix);
        for (int i = 0; i < _travelHelix.Count; i++)
        {
            _travelHelix[i].SetEndpoints(start, end);
            _travelHelix[i].SetProgress(0f);
        }
    }

    private void DriveTravelHelix(float t)
    {
        for (int i = 0; i < _travelHelix.Count; i++)
            if (_travelHelix[i] != null) _travelHelix[i].SetProgress(t);
    }

    private IEnumerator TravelToTarget(
        Transform origin, Vector3 destination, float speed,
        Action onArrival, bool requireMinDistance = false, Action<float> onProgress = null)
    {
        _distanceTravelled = 0f;
        float totalDistance = Mathf.Max(0.05f, Vector3.Distance(origin.position, destination));

        while (Vector3.Distance(origin.position, destination) > 0.05f)
        {
            if (_soulCC != null) _soulCC.enabled = false;

            // Kiriko-style velocity profile (TravelEase): steepening ramp over the first 15% of the
            // route, full speed through the middle, steepening fall-off over the last 10%.
            float progress = Mathf.Clamp01(1f - Vector3.Distance(origin.position, destination) / totalDistance);
            float easedSpeed = speed * TravelEase.SpeedMultiplier(progress);

            Vector3 newPos = Vector3.MoveTowards(
                origin.position, destination, easedSpeed * UnityEngine.Time.unscaledDeltaTime);

            _distanceTravelled += Vector3.Distance(origin.position, newPos);
            origin.position = newPos;

            if (_soulCC != null) _soulCC.enabled = true;
            onProgress?.Invoke(Mathf.Clamp01(1f - Vector3.Distance(origin.position, destination) / totalDistance));
            yield return null;
        }
        onProgress?.Invoke(1f);

        if (requireMinDistance && _distanceTravelled < minTravelDistance)
            yield return new WaitForSecondsRealtime(0.3f);

        onArrival?.Invoke();
        UnityEngine.Debug.Log($"[TeleportAbility] Soul arrived — firing OnSoulArrived, subscribers={OnSoulArrived?.GetInvocationList()?.Length ?? 0}");
    }
}