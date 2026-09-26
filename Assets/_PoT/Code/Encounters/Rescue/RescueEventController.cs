using System;
using System.Collections.Generic;
using UnityEngine;

public class RescueEventController : MonoBehaviour, IRescueActive, ITutorialRescueProvider, IRescueTrapRegistry
{
    public static RescueEventController Instance { get; private set; }
    [Header("Twin References")]
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    // LEGACY (pre-couch single-soul). Proximity/mash-range now track the DEPLOYED soul via
    // ActiveSoulTransform (couch two-soul model, ba9c88e). No longer read — kept so the Persistent
    // scene reference isn't orphaned; delete in a dedicated scene-touching cleanup commit.
    [SerializeField] private Transform soulTransform;

    [Header("Coordinators")]
    [SerializeField] private MonoBehaviour inputProviderObject;

    [Header("Proximity")]
    [SerializeField] private float rescueProximityRadius = 2.5f;

    [Header("Emergency Teleport")]
    [SerializeField] private EmergencyTeleportMonitor emergencyTeleportMonitor;
    [SerializeField] private MonoBehaviour timeFactorControllerObject; // → ITimeFactorController

    [Tooltip("Soul must be within this radius of grabbed player to register F press")]
    [SerializeField] private float mashProximityRadius = 2.5f;

    [Header("Debug")]
    [Tooltip("Verbose [Rescue] state/grab/soul/mash logging for the regen investigation. " +
             "Untick to silence the per-mash-press spam.")]
    [SerializeField] private bool _debugRescue = true;

    // ââ Public events for UI 
    public event Action<RescueState> OnRescueStateChanged;
    public event Action<float> OnMashProgressUpdated;
    public event Action<float> OnMashTimeUpdated;
    public event Action<float> OnCooldownTimeUpdated;
    public event Action<Player> OnPlayerInDanger;
    public event Action<IRescueTarget> OnActiveTargetChanged;
    public event Action OnSoulArrived;      // fires when soul reaches destination
    public event Action OnRescueResolved;   // fires on Success or Failed
    // Fires ONLY on a failed rescue, from inside EnterState(Failed) BEFORE CleanupRescueEvent resets
    // _state to Idle. Needed because OnRescueStateChanged deliberately never delivers the terminal
    // Failed value (the `if (_state == next)` guard in TransitionTo suppresses it so PoTWorldStateWriter's
    // IsRescueActive lands on Idle — else enemies freeze forever). Game-over listens here.
    public event Action OnRescueFailed;

    public RescueState CurrentRescueState { get; private set; }

    /// <summary>
    /// Fires when the grabbed player successfully presses E to struggle.
    /// Only fires for tier-1 traps (CanGrabbedPlayerStruggle = true).
    /// UI listens to this to animate the struggle ring.
    /// </summary>
    public event Action OnStruggleActivated;
    /// <summary>Fires when 30% TTK hack cap is reached — UI hides E ring.</summary>
    public event Action OnStruggleCapReached;

    // ── Siphon Ghost support ───────────────────────────────────
    private int _activeGhostCount = 0;
    private const int MaxActiveGhosts = 1;

    /// <summary>Returns true if ghost was registered. False if cap reached.</summary>
    public bool TryRegisterGhost()
    {
        if (_activeGhostCount >= MaxActiveGhosts) return false;
        _activeGhostCount++;
        return true;
    }

    public void UnregisterGhost() =>
        _activeGhostCount = Mathf.Max(0, _activeGhostCount - 1);

    // ── TTK control — called by SiphonGhost when it chains the soul ──
    // Delegates to the active rescue target so SiphonGhost doesn't need
    // a direct reference to GroupGrabEnemy / PlayerDeathRescueProxy.
    public void PauseTTK() => _activeTarget?.PauseTTK();
    public void ResumeTTK() => _activeTarget?.ResumeTTK();

    // ── LastBlowEnemy — for Ashen Tide fear exclusion ──────────
    public GameObject LastBlowEnemy { get; private set; }


    public Player ActiveGrabbedPlayer => _activeTarget?.GrabbedPlayer;
    public float ActiveMashWindowDuration => _mashWindowDuration;
    public IRescueTarget ActiveTarget => _activeTarget;

    // IRescueActive â read by SoulConvergenceSystem to block F-hold during rescue
    public bool IsRescueActive => _state != RescueState.Idle;
    public bool HasActiveRescueTarget => _activeTarget != null;

    /// <summary>
    /// BUG-082: true while EITHER twin's rescue soul is deployed — from the Gate teleport's
    /// commit until the soul finishes travelling home (TeleportAbility.ReturnSequence). The
    /// enemy soft-freeze (shared PoT.Game.IsRescueActive) must persist through the soul's RETURN
    /// trip, which runs AFTER the rescue state has already gone Idle at Success. Polled live by
    /// PoTWorldStateWriter (never cached) so a cancelled / re-cast / destroyed ability cannot
    /// leave the freeze flag stuck true.
    /// </summary>
    public bool IsAnySoulDeployed
    {
        get
        {
            for (int i = 0; i < _teleportAbilities.Count; i++)
                if (_teleportAbilities[i] != null && _teleportAbilities[i].IsSoulDeployed) return true;
            return false;
        }
    }

    /// <summary>
    /// Latches true when rescue succeeds. Stays true until ResetSuccessFlag() is called.
    /// Used by TutorialRescueWatchStepSO to detect success without missing
    /// the one-frame window before state resets to Idle.
    /// </summary>
    public bool WasSuccessful { get; private set; } = false;

    public void ResetSuccessFlag() => WasSuccessful = false;

    /// <summary>
    /// Latches true the moment a rescue FAILS and stays true until ResetFailFlag(). Mirrors WasSuccessful,
    /// and for the same reason: EnterState(Failed) → CleanupRescueEvent() resets CurrentRescueState to Idle
    /// synchronously in the SAME call, so a frame-poll of "CurrentRescueState == Failed" never observes the
    /// terminal value — the tutorial fade → reset → retry never fired. The tutorial rescue-watch step polls
    /// this instead.
    /// </summary>
    public bool WasFailed { get; private set; } = false;

    /// <summary>Reset the failure latch before watching a new rescue / after handling one failure.</summary>
    public void ResetFailFlag() => WasFailed = false;

    /// <summary>
    /// Tutorial retry: fully restore BOTH twins — clears death flags, HP to max, resets distance drain,
    /// unfreezes movement and clears the grabbed flag — so a failed tutorial rescue can be re-attempted.
    /// The tutorial trap deals REAL lethal damage on TTK-timeout (SkeletonTrap.KillPlayer → HP 0), so
    /// without this the killed twin stays dead (regen halted) and the retry loop can never re-grab.
    /// Non-tutorial failures are game-over, so they never call this.
    /// </summary>
    public void ReviveTwinsForRetry()
    {
        ReviveOneForRetry(leftTwin);
        ReviveOneForRetry(rightTwin);
    }

    private static void ReviveOneForRetry(Player twin)
    {
        if (twin == null) return;
        twin.Health?.RestoreToFull();
        (twin.Movement as IMovementFreezable)?.SetFrozen(false);
        twin.SetGrabbed(false);
    }

    /// <summary>
    /// While true, a Failed rescue does NOT fire OnRescueFailed (the game-over signal). The Failed
    /// state transition and CurrentRescueState are unaffected — only the game-over trigger is gated.
    /// Set by TutorialRescueWatchStepSO while it owns the rescue so a failed tutorial rescue drives its
    /// own fade → reset → retry instead of "battle lost" (BUG-103). Non-tutorial rescues leave it false.
    /// </summary>
    public bool SuppressFailGameOver { get; set; } = false;

    /// <summary>
    /// Hard-resets rescue state to Idle. Called by SoftResetController on respawn.
    /// Unfreezes any grabbed twin, clears all internal state, fires state-change events.
    /// </summary>
    public void ForceReset()
    {
        if (_state == RescueState.Idle && _activeTarget == null) return;

        // Unfreeze grabbed player if mid-rescue
        if (_activeTarget?.GrabbedPlayer != null)
        {
            var isLeft = _activeTarget.GrabbedPlayer == leftTwin;
            emergencyTeleportMonitor?.SetEmergencyOverride(isLeft, false);
            var moveable = _activeTarget.GrabbedPlayer.Movement as IMovementFreezable;
            moveable?.SetFrozen(false);
        }

        CleanupRescueEvent();
        _state = RescueState.Idle;
        CurrentRescueState = RescueState.Idle;
        OnRescueStateChanged?.Invoke(RescueState.Idle);
        OnActiveTargetChanged?.Invoke(null);
    }

    /// <summary>Exposed so SiphonGhost can read the soul owner's bind-break mash (Struggle) without raw Input calls.</summary>
    public IInputProvider InputProvider =>
        ActiveSoul?.Caster != null ? PlayerInputRouter.For(ActiveSoul.Caster) : _input;

    // ââ Internal âââââââââââââââââââââââââââââââââââââââââââââââ
    private RescueState _state = RescueState.Idle;
    private IRescueTarget _activeTarget;
    private float _totalStruggleTimePaused;
    private bool _struggleCapReached;
    private TeleportAbility _activeSoulAbility;
    /// <summary>The soul currently deployed — resolved from whichever registered gate has its soul OUT
    /// (<see cref="TeleportAbility.IsSoulDeployed"/>). Note: <c>_activeSoulAbility</c> is never assigned
    /// (SetActiveSoulAbility has no callers), so we poll the gate list instead. Couch: the dispatchers drive
    /// this soul with its CASTER's input; null when no soul is out.</summary>
    public SoulPlayer ActiveSoul
    {
        get
        {
            for (int i = 0; i < _teleportAbilities.Count; i++)
                if (_teleportAbilities[i] != null && _teleportAbilities[i].IsSoulDeployed)
                    return _teleportAbilities[i].Soul;
            return null;
        }
    }

    /// <summary>The transform the proximity / mash-range checks measure against — the DEPLOYED soul in the
    /// couch two-soul model (each twin's Weaver's Gate owns its own soul, ba9c88e), NOT the legacy single
    /// serialized <see cref="soulTransform"/>. That fixed field references only ONE of the two per-twin
    /// souls, so a rescue driven by the OTHER twin's soul measured distance to the wrong (resting) soul
    /// (~10 m) and never reached Triggered — the "Press F never appears" bug. Null when no soul is deployed,
    /// so the proximity checks correctly do NOT trigger (you cannot rescue without a soul out; a fallback to
    /// soulTransform here would false-trigger whenever a resting soul sits on the grabbed twin).</summary>
    private Transform ActiveSoulTransform => ActiveSoul != null ? ActiveSoul.transform : null;

    // BUG-082: both twins' Gate abilities register here (left+right caster). Polled via
    // IsAnySoulDeployed to keep enemies frozen until the rescue soul is home. Distinct from
    // _activeSoulAbility, which CleanupRescueEvent nulls at Success — too early for the return trip.
    private readonly List<TeleportAbility> _teleportAbilities = new List<TeleportAbility>();

    private IInputProvider _input;
    private ITimeFactorController _timeFactorController;

    private float _mashProgress = 0f;
    private float _mashTimeRemaining = 0f;
    private float _cooldownRemaining = 0f;
    private float _mashWindowDuration = 3f;
    private float _requiredPressesTotal;

    private struct TrapDelegates
    {
        public Action<Player> OnGrabbed;
        public Action OnReleased;
        public Action OnKilled;
    }

    private readonly Dictionary<IRescueTarget, TrapDelegates> _dyingDelegates
        = new Dictionary<IRescueTarget, TrapDelegates>();

    private readonly Dictionary<IRescueTarget, TrapDelegates> _trapDelegates
        = new Dictionary<IRescueTarget, TrapDelegates>();

    // ââ Unity lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _input = inputProviderObject as IInputProvider;

        if (_input == null) Debug.LogError("[RescueEventController] inputProviderObject missing IInputProvider.", this);
        _timeFactorController = timeFactorControllerObject as ITimeFactorController;
    }

    private void Start()
    {
        // Traps now self-register via OnEnable/OnDisable — scan removed.
        RegisterDeathProxies();

        if (leftTwin?.Health != null)  leftTwin.Health.OnDeath  += HandleLeftTwinDeath;
        if (rightTwin?.Health != null) rightTwin.Health.OnDeath += HandleRightTwinDeath;
    }

    private void RegisterDeathProxies()
    {
        var leftProxy = leftTwin.GetComponent<PlayerDeathRescueProxy>();
        var rightProxy = rightTwin.GetComponent<PlayerDeathRescueProxy>();
        if (leftProxy != null) RegisterDyingProxy(leftProxy, leftTwin);
        if (rightProxy != null) RegisterDyingProxy(rightProxy, rightTwin);
    }

    // ââ Proxy registration 
    public void RegisterDyingProxy(PlayerDeathRescueProxy proxy, Player owner)
    {
        if (_dyingDelegates.ContainsKey(proxy)) return;

        var captured = (IRescueTarget)proxy;
        var delegates = new TrapDelegates
        {
            OnGrabbed = (player) => HandlePlayerGrabbed(player, captured),
            OnReleased = () => HandleDyingReleased(captured),
            OnKilled = () => HandleDyingKilled(captured)
        };

        _dyingDelegates[proxy] = delegates;
        proxy.OnPlayerGrabbed += delegates.OnGrabbed;
        proxy.OnPlayerReleased += delegates.OnReleased;
        proxy.OnPlayerKilled += delegates.OnKilled;
    }

    // DELETED: HandleDyingStarted â was dead code, never subscribed.
    // All its logic already lives in HandlePlayerGrabbed below.

    // ââ Dying proxy handlers âââââââââââââââââââââââââââââââââââ
    private void HandleDyingReleased(IRescueTarget sender)
    {
        if (sender != _activeTarget) return;
        if (_state == RescueState.Success) return;

        if (_state == RescueState.Idle || _state == RescueState.SoulDied)
        {
            // Killer died before soul mash flow started â player already freed
            // by ReleasePlayer(). Clean up _activeTarget so CheckProximityForTrigger
            // doesn't immediately start a new rescue cycle this frame.
            if (_activeTarget?.GrabbedPlayer != null)
            {
                bool isLeft = (_activeTarget.GrabbedPlayer == leftTwin);
                emergencyTeleportMonitor?.SetEmergencyOverride(isLeft, false);
            }
            // rescue selection-lock removed (S5); unlock onceâ matched by single LockSelection in HandlePlayerGrabbed
            CleanupRescueEvent();
            return;
        }

        // Normal soul mash path â Success EnterState handles unlock
        TransitionTo(RescueState.Success);
    }

    private void HandleDyingKilled(IRescueTarget sender)
    {
        if (sender != _activeTarget) return;
        if (_state == RescueState.Success) return;
        // Do NOT skip Idle/SoulDied â TTK can expire after soul has left

        if (_activeTarget?.GrabbedPlayer != null)
        {
            bool isLeft = (_activeTarget.GrabbedPlayer == leftTwin);
            emergencyTeleportMonitor?.SetEmergencyOverride(isLeft, false);
        }

        TransitionTo(RescueState.Failed);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (leftTwin?.Health  != null) leftTwin.Health.OnDeath  -= HandleLeftTwinDeath;
        if (rightTwin?.Health != null) rightTwin.Health.OnDeath -= HandleRightTwinDeath;
        foreach (var kvp in _trapDelegates)
        {
            kvp.Key.OnPlayerGrabbed -= kvp.Value.OnGrabbed;
            kvp.Key.OnPlayerReleased -= kvp.Value.OnReleased;
            kvp.Key.OnPlayerKilled -= kvp.Value.OnKilled;
        }
        _trapDelegates.Clear();
    }

    // ââ Trap registration 
    public void RegisterTrap(IRescueTarget target)
    {
        if (_trapDelegates.ContainsKey(target)) return;

        var capturedTrap = target;
        var delegates = new TrapDelegates
        {
            OnGrabbed = (player) => HandlePlayerGrabbed(player, capturedTrap),
            OnReleased = () => HandlePlayerReleased(capturedTrap),
            OnKilled = () => HandlePlayerKilled(capturedTrap)
        };

        _trapDelegates[target] = delegates;
        target.OnPlayerGrabbed += delegates.OnGrabbed;
        target.OnPlayerReleased += delegates.OnReleased;
        target.OnPlayerKilled += delegates.OnKilled;
    }

    public void UnregisterTrap(IRescueTarget target)
    {
        if (!_trapDelegates.TryGetValue(target, out var delegates)) return;

        target.OnPlayerGrabbed -= delegates.OnGrabbed;
        target.OnPlayerReleased -= delegates.OnReleased;
        target.OnPlayerKilled -= delegates.OnKilled;

        _trapDelegates.Remove(target);
    }

    // ââ TeleportAbility registration 
    public void RegisterTeleportAbility(TeleportAbility ability)
    {
        if (ability == null) return;
        // BUG-082: keep every registered ability so IsAnySoulDeployed can poll it, even after
        // CleanupRescueEvent has nulled _activeSoulAbility at Success (the soul is still flying home).
        if (!_teleportAbilities.Contains(ability)) _teleportAbilities.Add(ability);
        ability.OnSoulArrived += HandleSoulArrived;
    }

    public void SetActiveSoulAbility(TeleportAbility ability)
    {
        _activeSoulAbility = ability;
    }

    // ââ Grab handlers 
    private void HandlePlayerGrabbed(Player grabbedPlayer, IRescueTarget target)
    {
        if (_activeTarget != null)
        {
            // Check if the first player is STILL actively dying.
            // If proxy.IsActive is false, the rescue just completed and _activeTarget
            // is stale — clear it and allow the new rescue to proceed normally.
            // If proxy.IsActive is true, both players are genuinely trapped simultaneously
            // which is instant fail per design (Option A).
            var firstProxy = _activeTarget as PlayerDeathRescueProxy;
            if (firstProxy != null && firstProxy.IsActive)
            {
                Debug.Log("[RescueEventController] Both players actively trapped — instant fail.");
                TransitionTo(RescueState.Failed);
                return;
            }
            // Stale reference — first rescue completed, clear and continue
            Debug.Log("[RescueEventController] Stale _activeTarget cleared, starting new rescue.");
            _activeTarget = null;
        }
        _activeTarget = target;
        OnActiveTargetChanged?.Invoke(_activeTarget);

        // Store the enemy GO for Ashen Tide fear exclusion
        LastBlowEnemy = (target as MonoBehaviour)?.gameObject;

        // Notify SoulPulseSystem of last blow enemy
        var pulse = ActiveSoul?.GetComponent<SoulPulseSystem>();
        pulse?.SetLastBlowEnemy(LastBlowEnemy);

        bool isLeft = (grabbedPlayer == leftTwin);
        emergencyTeleportMonitor?.SetEmergencyOverride(isLeft, true);

        var moveable = grabbedPlayer.Movement as IMovementFreezable;
        moveable?.SetFrozen(true);

        // Couch M3.1: nothing to hijack — the grabbed twin's player is frozen and the partner already
        // controls the free twin. (Was: ForceSelect(otherTwin) when the grabbed twin was the selected
        // one.) Twin-switching no longer exists after the S5 teardown.
        OnPlayerInDanger?.Invoke(grabbedPlayer);
        PoTLog.Crumb(PoTCrumb.Rescue, $"{grabbedPlayer?.name} grabbed by '{(target as MonoBehaviour)?.name}'");

        if (_debugRescue)
            Debug.Log($"[Rescue] BEGAN — '{grabbedPlayer?.name}' grabbed by " +
                      $"'{(target as MonoBehaviour)?.name}' " +
                      $"(mashWindow={_activeTarget?.MashWindowDuration:F1}s, " +
                      $"healOnRescue={_activeTarget?.PartialHealAmount:F0})");
    }

    private void HandlePlayerReleased(IRescueTarget sender)
    {
        if (sender != _activeTarget) return;

        if (_activeTarget?.GrabbedPlayer != null)
        {
            bool isLeft = (_activeTarget.GrabbedPlayer == leftTwin);
            emergencyTeleportMonitor?.SetEmergencyOverride(isLeft, false);

            var moveable = _activeTarget.GrabbedPlayer.Movement as IMovementFreezable;
            moveable?.SetFrozen(false);
        }

        if (_state != RescueState.Success)
            CleanupRescueEvent();
    }

    private void HandlePlayerKilled(IRescueTarget sender)
    {
        if (sender != _activeTarget) return;

        if (_activeTarget?.GrabbedPlayer != null)
        {
            bool isLeft = (_activeTarget.GrabbedPlayer == leftTwin);
            emergencyTeleportMonitor?.SetEmergencyOverride(isLeft, false);
        }

        TransitionTo(RescueState.Failed);
    }

    private void HandleLeftTwinDeath()  => HandleTwinDeath(leftTwin);
    private void HandleRightTwinDeath() => HandleTwinDeath(rightTwin);

    private void HandleTwinDeath(Player deadTwin)
    {
        if (_activeTarget?.GrabbedPlayer == deadTwin) return;
        if (_state != RescueState.Idle) return;

        var moveable = deadTwin.Movement as IMovementFreezable;
        moveable?.SetFrozen(true);

        Player survivor = (deadTwin == leftTwin) ? rightTwin : leftTwin;
        if (survivor.Health.IsDead) return;

        // Couch M3.1: no ForceSelect(survivor) — the survivor's own player already controls it.
        // NOTE: do NOT LockSelection here â HandlePlayerGrabbed fires immediately
        // after via OnPlayerGrabbed and locks it. Double-locking prevents unlock
        // after rescue since only one UnlockSelection is called in HandleDyingReleased.
    }

    private void HandleSoulArrived()
    {
        OnSoulArrived?.Invoke(); // fire regardless of state — Siphon listens here
        if (_state != RescueState.Idle && _state != RescueState.SoulDied) return;
        if (_activeTarget?.GrabbedPlayerTransform == null) return;
        var soul = ActiveSoulTransform;
        if (soul == null) return;   // no soul deployed → nothing to have arrived

        float dist = Vector3.Distance(
            soul.position,
            _activeTarget.GrabbedPlayerTransform.position);

        if (_debugRescue)
            Debug.Log($"[Rescue] Soul arrived — dist={dist:F1} (trigger radius={rescueProximityRadius}) → " +
                      $"{(dist <= rescueProximityRadius ? "TRIGGERED" : "still out of range")}");

        if (dist <= rescueProximityRadius)
            TransitionTo(RescueState.Triggered);
    }

    // âUpdate
    // Couch M3.1 — rescue is a PARTNER-mash. The grabbed twin is frozen; its OWNER presses E to
    // struggle (buy time), while the PARTNER (owner of the free twin) mashes F to fill the rescue bar.
    // Reads route by twin ownership via PlayerInputRouter; single-device (P2->P1) collapses both to one
    // provider, so solo play is unchanged (one player does both E and F).
    private Player PartnerOfGrabbed()
    {
        var grabbed = _activeTarget?.GrabbedPlayer;
        if (grabbed == null) return null;
        return grabbed == leftTwin ? rightTwin : leftTwin;
    }

    /// <summary>The provider the F (rescue-mash) prompt should reflect — the PARTNER (non-grabbed twin), i.e. the
    /// device that actually mashes F. The button-glyph HUD reads this so the shown glyph matches the read input.</summary>
    public IInputProvider RescueMashInput
    {
        get { var partner = PartnerOfGrabbed(); return partner != null ? PlayerInputRouter.For(partner) : _input; }
    }

    /// <summary>The provider the E (struggle) prompt should reflect — the GRABBED twin's own device.</summary>
    public IInputProvider StruggleInput
    {
        get { var grabbed = _activeTarget?.GrabbedPlayer; return grabbed != null ? PlayerInputRouter.For(grabbed) : _input; }
    }

    /// <summary>F (rescue mash) read from the PARTNER — the owner of the non-grabbed twin.</summary>
    private bool PartnerRescueMash() => (RescueMashInput ?? _input)?.GetRescueMash() ?? false;

    /// <summary>E (struggle) read from the GRABBED twin's own owner (buys time; tier-1 traps).</summary>
    private bool GrabbedStruggleMash() => (StruggleInput ?? _input)?.GetStruggleMash() ?? false;

    private void Update()
    {
        // ── Struggle (E mash by grabbed player, tier-1 traps only) ──
        // Capped at 30% of full TTK duration — after that E does nothing.
        // Also disabled once rescue teleport fires (state != Idle).
        if (_activeTarget != null &&
            _activeTarget.CanGrabbedPlayerStruggle &&
            !_struggleCapReached &&
            _state == RescueState.Idle && // disabled once rescue triggered
            _input != null &&
            GrabbedStruggleMash())
        {
            _activeTarget.OnStruggle();
            OnStruggleActivated?.Invoke();

            // Track total struggle time used
            _totalStruggleTimePaused += _activeTarget.StrugglePauseDuration;

            float maxHack = _activeTarget.MashWindowDuration * 0.3f; // 30% of TTK
            if (_totalStruggleTimePaused >= maxHack)
            {
                _struggleCapReached = true;
                OnStruggleCapReached?.Invoke(); // UI hides E ring
            }
        }

        switch (_state)
        {
            case RescueState.Idle:
                CheckProximityForTrigger();
                break;

            case RescueState.Triggered:
                if (IsSoulInMashRange() && PartnerRescueMash())
                {
                    TransitionTo(RescueState.Mashing);
                }
                else if (!IsSoulInMashRange() &&
                         _activeTarget?.GrabbedPlayerTransform != null)
                {
                    // ActiveSoulTransform null == deployed soul is gone (returned / died) → treat as "far".
                    var soul = ActiveSoulTransform;
                    float dist = soul != null
                        ? Vector3.Distance(soul.position, _activeTarget.GrabbedPlayerTransform.position)
                        : float.PositiveInfinity;
                    // SoulDied preserves _activeTarget â Idle causes immediate re-trigger loop
                    if (dist > rescueProximityRadius * 2f)
                        TransitionTo(RescueState.SoulDied);
                }
                break;

            case RescueState.Mashing:
                TickMashing();
                break;

            case RescueState.Cooldown:
                TickCooldown();
                break;

            case RescueState.SoulDied:
                // Recovery (Bug: "mash UI never comes back after the soul leaves range"): SoulDied is entered
                // BOTH when the soul truly dies AND when a still-deployed soul merely wanders >2×radius away
                // (see the Triggered case). The wander case MUST be recoverable — if the live soul returns
                // within range, re-arm the mash. A genuinely dead / returned-home soul has no
                // ActiveSoulTransform, so a real death correctly stays terminal (no false recovery).
                if (_activeTarget?.GrabbedPlayerTransform != null)
                {
                    var backSoul = ActiveSoulTransform;
                    if (backSoul != null &&
                        Vector3.Distance(backSoul.position, _activeTarget.GrabbedPlayerTransform.position) <= rescueProximityRadius)
                        TransitionTo(RescueState.Triggered);
                }
                break;
        }
    }

    private void CheckProximityForTrigger()
    {
        if (_activeTarget?.GrabbedPlayerTransform == null) return;
        var soul = ActiveSoulTransform;
        if (soul == null) return;   // no soul deployed yet → nothing to trigger against

        float dist = Vector3.Distance(
            soul.position,
            _activeTarget.GrabbedPlayerTransform.position);

        if (dist <= rescueProximityRadius)
            TransitionTo(RescueState.Triggered);
    }

    private void TickMashing()
    {
        if (!IsSoulInMashRange())
        {
            _activeTarget?.ResumeTTK();
            _activeSoulAbility?.ResumeSoulTimer();
            TransitionTo(RescueState.Triggered);
            return;
        }

        _mashTimeRemaining -= Time.deltaTime;
        OnMashTimeUpdated?.Invoke(_mashTimeRemaining);

        if (PartnerRescueMash())
        {
            float fillPerPress = 1f / _requiredPressesTotal;
            _mashProgress = Mathf.Clamp01(_mashProgress + fillPerPress);
            OnMashProgressUpdated?.Invoke(_mashProgress);
            if (_debugRescue)
                Debug.Log($"[Rescue] mash press → progress={_mashProgress:P0} (time left={_mashTimeRemaining:F1}s)");
        }

        if (_mashProgress >= 1f)
        {
            if (_state == RescueState.Mashing)
                TransitionTo(RescueState.Success);
            return;
        }

        if (_mashTimeRemaining <= 0f)
            TransitionTo(RescueState.Cooldown);
    }

    private void TickCooldown()
    {
        _cooldownRemaining -= Time.deltaTime;
        OnCooldownTimeUpdated?.Invoke(_cooldownRemaining);

        if (_cooldownRemaining <= 0f)
        {
            _mashProgress = 0f;
            TransitionTo(RescueState.Triggered);
        }
    }

    // State machine
    private void TransitionTo(RescueState next)
    {
        PoTLog.Crumb(PoTCrumb.Rescue, $"{_state} → {next} (player={_activeTarget?.GrabbedPlayer?.name ?? "none"})");
        if (_debugRescue)
            Debug.Log($"[Rescue] {_state} → {next}  " +
                      $"(target={(_activeTarget as MonoBehaviour)?.name ?? "none"}, " +
                      $"player={_activeTarget?.GrabbedPlayer?.name ?? "none"}, " +
                      $"mash={_mashProgress:P0})");
        ExitState(_state);
        _state = next;
        CurrentRescueState = next;
        if (next == RescueState.Success) WasSuccessful = true;
        if (next == RescueState.Failed) WasFailed = true;   // latch — see WasFailed / ReviveTwinsForRetry
        EnterState(next);

        // Terminal states (Success/Failed) call CleanupRescueEvent() inside EnterState, which
        // resets _state to Idle and already fires OnRescueStateChanged(Idle). Firing the
        // terminal-state value here would stomp that Idle with a non-Idle value as the last
        // event subscribers see — leaving PoTWorldStateWriter's shared IsRescueActive stuck true
        // (enemies then never attack). Only fire if EnterState didn't already transition to Idle.
        if (_state == next)
            OnRescueStateChanged?.Invoke(next);
    }

    private void ExitState(RescueState exiting)
    {
        if (exiting == RescueState.Mashing)
        {
            _activeTarget?.ResumeTTK();
            _activeSoulAbility?.ResumeSoulTimer();
        }
    }

    public void RegisterSoulPlayer(SoulPlayer soul)
    {
        soul.OnSoulDied += HandleSoulDied;
    }

    private void HandleSoulDied()
    {
        if (_state == RescueState.Idle || _state == RescueState.Failed) return;

        _activeTarget?.ResumeTTK();
        _activeSoulAbility?.ResumeSoulTimer();

        TransitionTo(RescueState.SoulDied);
    }

    private void EnterState(RescueState entering)
    {
        switch (entering)
        {
            case RescueState.Triggered:
                // Do NOT reset _mashProgress â persists across soul leaving/returning.
                // Only Cooldown expiry and CleanupRescueEvent reset it.
                _mashWindowDuration = _activeTarget?.MashWindowDuration ?? 3f;
                _mashTimeRemaining = _mashWindowDuration;
                _requiredPressesTotal = (_activeTarget?.MashFrequency ?? 3f) * _mashWindowDuration;
                break;

            case RescueState.Mashing:
                _activeTarget?.PauseTTK();
                _activeSoulAbility?.PauseSoulTimer();
                _mashTimeRemaining = _mashWindowDuration;
                break;

            case RescueState.Cooldown:
                _cooldownRemaining = _activeTarget?.MashCooldown ?? 0.75f;
                break;

            case RescueState.Success:
                OnRescueResolved?.Invoke();
                Player rescued = _activeTarget?.GrabbedPlayer;
                IRescueTarget resolvedTarget = _activeTarget;

                if (_debugRescue)
                    Debug.Log($"[Rescue] SUCCESS — releasing '{rescued?.name}' " +
                              $"with heal={resolvedTarget?.PartialHealAmount:F0} → expect [DeathProxy]/[HealthRegen] next");
                resolvedTarget?.ReleasePlayer(resolvedTarget.PartialHealAmount); // FIRST
                _activeSoulAbility?.ResumeSoulTimer();

                if (rescued != null)
                {
                    bool isLeft = (rescued == leftTwin);
                    emergencyTeleportMonitor?.SetEmergencyOverride(isLeft, false);
                }

                CleanupRescueEvent(); // LAST
                break;

            case RescueState.Failed:
                if (_debugRescue) Debug.Log("[Rescue] FAILED — rescue lost, twin killed");
                OnRescueResolved?.Invoke();
                // Dedicated failure signal → game-over (GameOverController subscribes it). Suppressed
                // while the tutorial owns the rescue (BUG-103) so a failed tutorial rescue runs its own
                // fade → reset → retry instead. CurrentRescueState is already Failed (set in TransitionTo
                // before EnterState), so the tutorial step's Failed poll still fires.
                if (!SuppressFailGameOver)
                    OnRescueFailed?.Invoke();   // fires BEFORE cleanup resets state
                CleanupRescueEvent();
                break;

            case RescueState.SoulDied:
                if (_debugRescue)
                    Debug.Log("[Rescue] SoulDied — soul lost / too far; twin still trapped, awaiting retry");
                // Do NOT fire OnRescueStateChanged here â
                // TransitionTo already fires it after EnterState returns.
                // Player still trapped â _activeTarget preserved for retry.
                break;
        }
    }

    private void CleanupRescueEvent()
    {
        _struggleCapReached = false;
        _totalStruggleTimePaused = 0f;
        _activeTarget = null;
        LastBlowEnemy = null;
        var pulse = ActiveSoul?.GetComponent<SoulPulseSystem>();
        pulse?.ClearLastBlowEnemy();
        _activeGhostCount = 0;
        OnActiveTargetChanged?.Invoke(null);
        _activeSoulAbility = null;
        _mashProgress = 0f;
        _state = RescueState.Idle;
        CurrentRescueState = RescueState.Idle;
        OnRescueStateChanged?.Invoke(RescueState.Idle);
    }

    private bool IsSoulInMashRange()
    {
        var soul = ActiveSoulTransform;
        if (soul == null) return false;
        if (_activeTarget?.GrabbedPlayerTransform == null) return false;

        float dist = Vector3.Distance(
            soul.position,
            _activeTarget.GrabbedPlayerTransform.position);

        return dist <= mashProximityRadius;
    }
}