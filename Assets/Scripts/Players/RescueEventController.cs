using System;
using System.Collections.Generic;
using UnityEngine;

public class RescueEventController : MonoBehaviour, IRescueActive
{
    [Header("Twin References")]
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private Transform soulTransform;

    [Header("Coordinators")]
    [SerializeField] private MonoBehaviour twinSelectorObject;   // ITwinSelector + ISelectionLock
    [SerializeField] private MonoBehaviour inputProviderObject;

    [Header("Proximity")]
    [SerializeField] private float rescueProximityRadius = 2.5f;

    [SerializeField] private SoulPlayer soulPlayer;

    [Header("Emergency Teleport")]
    [SerializeField] private EmergencyTeleportMonitor emergencyTeleportMonitor;

    [Tooltip("Soul must be within this radius of grabbed player to register F press")]
    [SerializeField] private float mashProximityRadius = 2.5f;

    // ââ Public events for UI âââââââââââââââââââââââââââââââââââ
    public event Action<RescueState> OnRescueStateChanged;
    public event Action<float> OnMashProgressUpdated;
    public event Action<float> OnMashTimeUpdated;
    public event Action<float> OnCooldownTimeUpdated;
    public event Action<Player> OnPlayerInDanger;
    public event Action<IRescueTarget> OnActiveTargetChanged;


    public Player ActiveGrabbedPlayer => _activeTarget?.GrabbedPlayer;
    public float ActiveMashWindowDuration => _mashWindowDuration;
    public IRescueTarget ActiveTarget => _activeTarget;

    // IRescueActive â read by SoulConvergenceSystem to block F-hold during rescue
    public bool IsRescueActive => _state != RescueState.Idle;
    public bool HasActiveRescueTarget => _activeTarget != null;

    // ââ Internal âââââââââââââââââââââââââââââââââââââââââââââââ
    private RescueState _state = RescueState.Idle;
    private IRescueTarget _activeTarget;
    private TeleportAbility _activeSoulAbility;

    private ITwinSelector _selector;
    private ISelectionLock _selectionLock;
    private IInputProvider _input;

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

    // ââ Unity lifecycle ââââââââââââââââââââââââââââââââââââââââ
    private void Awake()
    {
        _selector = twinSelectorObject as ITwinSelector;
        _selectionLock = twinSelectorObject as ISelectionLock;
        _input = inputProviderObject as IInputProvider;

        if (_selector == null) Debug.LogError("[RescueEventController] twinSelectorObject missing ITwinSelector.", this);
        if (_selectionLock == null) Debug.LogError("[RescueEventController] twinSelectorObject missing ISelectionLock.", this);
        if (_input == null) Debug.LogError("[RescueEventController] inputProviderObject missing IInputProvider.", this);
    }

    private void Start()
    {
        var traps = FindObjectsByType<SkeletonTrap>(FindObjectsSortMode.None);
        foreach (var trap in traps)
            RegisterTrap(trap);

        RegisterDeathProxies();

        if (leftTwin?.Health != null) leftTwin.Health.OnDeath += () => HandleTwinDeath(leftTwin);
        if (rightTwin?.Health != null) rightTwin.Health.OnDeath += () => HandleTwinDeath(rightTwin);
    }

    private void RegisterDeathProxies()
    {
        var leftProxy = leftTwin.GetComponent<PlayerDeathRescueProxy>();
        var rightProxy = rightTwin.GetComponent<PlayerDeathRescueProxy>();
        if (leftProxy != null) RegisterDyingProxy(leftProxy, leftTwin);
        if (rightProxy != null) RegisterDyingProxy(rightProxy, rightTwin);
    }

    // ââ Proxy registration âââââââââââââââââââââââââââââââââââââ
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
            _selectionLock?.UnlockSelection(); // unlock once â matched by single LockSelection in HandlePlayerGrabbed
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

        // Only unlock if selection was actually locked
        if (_state != RescueState.Idle && _state != RescueState.SoulDied)
            _selectionLock?.UnlockSelection();

        TransitionTo(RescueState.Failed);
    }

    private void OnDestroy()
    {
        foreach (var kvp in _trapDelegates)
        {
            kvp.Key.OnPlayerGrabbed -= kvp.Value.OnGrabbed;
            kvp.Key.OnPlayerReleased -= kvp.Value.OnReleased;
            kvp.Key.OnPlayerKilled -= kvp.Value.OnKilled;
        }
        _trapDelegates.Clear();
    }

    // ââ Trap registration ââââââââââââââââââââââââââââââââââââââ
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

    // ââ TeleportAbility registration âââââââââââââââââââââââââââ
    public void RegisterTeleportAbility(TeleportAbility ability)
    {
        ability.OnSoulArrived += HandleSoulArrived;
    }

    public void SetActiveSoulAbility(TeleportAbility ability)
    {
        _activeSoulAbility = ability;
    }

    // ââ Grab handlers ââââââââââââââââââââââââââââââââââââââââââ
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

        bool isLeft = (grabbedPlayer == leftTwin);
        emergencyTeleportMonitor?.SetEmergencyOverride(isLeft, true);

        var moveable = grabbedPlayer.Movement as IMovementFreezable;
        moveable?.SetFrozen(true);

        if (_selector?.SelectedTransform == grabbedPlayer.transform)
        {
            Player otherTwin = isLeft ? rightTwin : leftTwin;
            (_selector as TwinSelector)?.ForceSelect(otherTwin);
        }

        _selectionLock?.LockSelection();
        OnPlayerInDanger?.Invoke(grabbedPlayer);
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

        _selectionLock?.UnlockSelection();

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

        _selectionLock?.UnlockSelection();
        TransitionTo(RescueState.Failed);
    }

    private void HandleTwinDeath(Player deadTwin)
    {
        if (_activeTarget?.GrabbedPlayer == deadTwin) return;
        if (_state != RescueState.Idle) return;

        var moveable = deadTwin.Movement as IMovementFreezable;
        moveable?.SetFrozen(true);

        Player survivor = (deadTwin == leftTwin) ? rightTwin : leftTwin;
        if (survivor.Health.IsDead) return;

        (_selector as TwinSelector)?.ForceSelect(survivor);
        // NOTE: do NOT LockSelection here â HandlePlayerGrabbed fires immediately
        // after via OnPlayerGrabbed and locks it. Double-locking prevents unlock
        // after rescue since only one UnlockSelection is called in HandleDyingReleased.
    }

    private void HandleSoulArrived()
    {
        if (_state != RescueState.Idle && _state != RescueState.SoulDied) return;
        if (_activeTarget?.GrabbedPlayerTransform == null) return;

        float dist = Vector3.Distance(
            soulTransform.position,
            _activeTarget.GrabbedPlayerTransform.position);

        if (dist <= rescueProximityRadius)
            TransitionTo(RescueState.Triggered);
    }

    // ââ Update âââââââââââââââââââââââââââââââââââââââââââââââââ
    private void Update()
    {
        switch (_state)
        {
            case RescueState.Idle:
                CheckProximityForTrigger();
                break;

            case RescueState.Triggered:
                if (IsSoulInMashRange() && _input.GetRescueMash())
                {
                    TransitionTo(RescueState.Mashing);
                }
                else if (!IsSoulInMashRange() &&
                         soulTransform != null &&
                         _activeTarget?.GrabbedPlayerTransform != null)
                {
                    float dist = Vector3.Distance(
                        soulTransform.position,
                        _activeTarget.GrabbedPlayerTransform.position);
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
        }
    }

    private void CheckProximityForTrigger()
    {
        if (_activeTarget?.GrabbedPlayerTransform == null) return;
        if (soulTransform == null) return;

        float dist = Vector3.Distance(
            soulTransform.position,
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

        if (_input.GetRescueMash())
        {
            float fillPerPress = 1f / _requiredPressesTotal;
            _mashProgress = Mathf.Clamp01(_mashProgress + fillPerPress);
            OnMashProgressUpdated?.Invoke(_mashProgress);
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

    // ââ State machine ââââââââââââââââââââââââââââââââââââââââââ
    private void TransitionTo(RescueState next)
    {
        Debug.Log($"[RescueEventController] TransitionTo {next}");
        ExitState(_state);
        _state = next;
        EnterState(next);
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
                Player rescued = _activeTarget?.GrabbedPlayer;
                IRescueTarget resolvedTarget = _activeTarget;

                resolvedTarget?.ReleasePlayer(resolvedTarget.PartialHealAmount); // FIRST
                _activeSoulAbility?.ResumeSoulTimer();
                _selectionLock?.UnlockSelection();

                if (rescued != null)
                {
                    bool isLeft = (rescued == leftTwin);
                    emergencyTeleportMonitor?.SetEmergencyOverride(isLeft, false);
                }

                CleanupRescueEvent(); // LAST
                break;

            case RescueState.Failed:
                CleanupRescueEvent();
                break;

            case RescueState.SoulDied:
                // Do NOT fire OnRescueStateChanged here â
                // TransitionTo already fires it after EnterState returns.
                // Player still trapped â _activeTarget preserved for retry.
                break;
        }
    }

    private void CleanupRescueEvent()
    {
        _activeTarget = null;
        OnActiveTargetChanged?.Invoke(null);
        _activeSoulAbility = null;
        _mashProgress = 0f;
        _state = RescueState.Idle;
        OnRescueStateChanged?.Invoke(RescueState.Idle);
    }

    private bool IsSoulInMashRange()
    {
        if (soulTransform == null) return false;
        if (_activeTarget?.GrabbedPlayerTransform == null) return false;

        float dist = Vector3.Distance(
            soulTransform.position,
            _activeTarget.GrabbedPlayerTransform.position);

        return dist <= mashProximityRadius;
    }
}