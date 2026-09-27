using CommonCore;
using PoT.Diagnostics;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Listens to existing Planet of Twins game events and writes
/// the results to the shared Blackboard.
///
/// This is the ONLY place existing PoT systems touch the AI framework.
/// No changes needed to RescueEventController, AccordStateSystem,
/// SharedHealthPool, or any other existing system.
///
/// SETUP: Add this component to your scene manager or a persistent GO.
/// Wire references in Inspector or leave null to auto-find on Start.
/// </summary>
public class PoTWorldStateWriter : MonoBehaviour
{
    public static PoTWorldStateWriter Instance { get; private set; }

    // Profiler marker (P8.5, game.md §28): the per-frame shared-blackboard sync.
    private static readonly ProfilerMarker PerfUpdate = PerfMarkers.Create("PoT.AI.WorldStateWriter");

    [Header("Wire in Inspector or leave null to auto-find")]
    [SerializeField] private RescueEventController _rescueController;
    [SerializeField] private AccordStateSystem _accordSystem;
    [SerializeField] private SharedHealthPool _sharedHealthPool;

    private Blackboard<FastName> _shared;

    // Cached delegate — needed to unsubscribe correctly
    private System.Action<RescueState> _onRescueStateChanged;

    // BUG-082: the enemy soft-freeze is (rescue state active) OR (a rescue soul is still deployed
    // and travelling home). _rescueActive is event-driven; the soul-deployed half is polled each
    // frame in Update(). _lastRescueFreeze de-dupes so we only write the blackboard on a change.
    private bool _rescueActive;
    private bool _lastRescueFreeze;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // The sealed AI core marks its perception tick itself but can't reference PoT.Diagnostics; this bridge (the
        // game's one contact point with the framework) lists it so GameDebuggerV2's Perf section shows it (P8.5).
        PerfMarkers.Watch("CommonCore.Perception.Tick");
    }

    private void Start()
    {
        _shared = BlackboardManager.GetSharedBlackboard(PoTNames.SharedBlackboardID);
        if (_shared == null)
        {
            Debug.LogError("[PoTWorldStateWriter] Could not get shared blackboard. Ensure BlackboardManager is in the scene.", this);
            return;
        }

        InitialiseBlackboard();
        FindMissingReferences();
        SubscribeToEvents();
    }

    private void OnDestroy() => UnsubscribeFromEvents();

    private void InitialiseBlackboard()
    {
        _shared.Set(PoTNames.IsRescueActive, false);
        _shared.Set(PoTNames.HasRescueTarget, false);
        _shared.Set(PoTNames.AccordStateActive, false);
        _shared.Set(PoTNames.AccordBarFill, 0f);
        _shared.Set(PoTNames.SharedHealthNorm, 1f);
        _shared.Set(PoTNames.SoulIsActive, false);
        _shared.Set(PoTNames.SoulPosition, CommonCore.Constants.InvalidVector3Position);
        _shared.Set(PoTNames.SpawnUnderAttack, false);
        _shared.Set(PoTNames.BarrierWidening, false);
        _shared.Set(PoTNames.DarkEnergyAvailable, false);
        _shared.Set(PoTNames.TargetIsEngaged, false);
        _shared.Set(PoTNames.AllyGrabbed, false);
        _shared.Set(PoTNames.ActiveGhostCount, 0);
    }

    private void FindMissingReferences()
    {
        if (_rescueController == null) _rescueController = FindAnyObjectByType<RescueEventController>();
        if (_accordSystem == null) _accordSystem = FindAnyObjectByType<AccordStateSystem>();
        if (_sharedHealthPool == null) _sharedHealthPool = FindAnyObjectByType<SharedHealthPool>();

        if (_rescueController == null) Debug.LogWarning("[PoTWorldStateWriter] RescueEventController not found.");
        if (_accordSystem == null) Debug.LogWarning("[PoTWorldStateWriter] AccordStateSystem not found.");
        if (_sharedHealthPool == null) Debug.LogWarning("[PoTWorldStateWriter] SharedHealthPool not found.");
    }

    private void SubscribeToEvents()
    {
        if (_rescueController != null)
        {
            _onRescueStateChanged = OnRescueStateChanged;
            _rescueController.OnRescueStateChanged += _onRescueStateChanged;
        }

        if (_accordSystem != null)
        {
            _accordSystem.OnAccordActivated += OnAccordActivated;
            _accordSystem.OnAccordDeactivated += OnAccordDeactivated;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (_rescueController != null && _onRescueStateChanged != null)
            _rescueController.OnRescueStateChanged -= _onRescueStateChanged;

        if (_accordSystem != null)
        {
            _accordSystem.OnAccordActivated -= OnAccordActivated;
            _accordSystem.OnAccordDeactivated -= OnAccordDeactivated;
        }
    }

    // Event handlers
    private void OnRescueStateChanged(RescueState state)
    {
        _rescueActive = state != RescueState.Idle;
        _shared?.Set(PoTNames.HasRescueTarget, _rescueActive);
        UpdateRescueFreeze();   // BUG-082: fold the state change into the combined freeze flag
    }

    // BUG-082: enemies must stay frozen not only while the rescue state machine is active, but
    // through the rescuing soul's RETURN flight home — which runs in TeleportAbility.ReturnSequence
    // AFTER the state has gone Idle at Success. So the shared freeze flag is (rescue state active)
    // OR (a soul is still deployed). IsAnySoulDeployed is polled live from Update() so the flag
    // self-clears even if the ability is cancelled, re-cast, or destroyed mid-flight.
    private void UpdateRescueFreeze()
    {
        if (_shared == null) return;
        bool freeze = _rescueActive
            || (_rescueController != null && _rescueController.IsAnySoulDeployed);
        if (freeze == _lastRescueFreeze) return;
        _lastRescueFreeze = freeze;
        _shared.Set(PoTNames.IsRescueActive, freeze);
    }

    private void OnAccordActivated() => _shared?.Set(PoTNames.AccordStateActive, true);
    private void OnAccordDeactivated() => _shared?.Set(PoTNames.AccordStateActive, false);

    private void Update()
    {
        if (_shared == null) return;
        using var perf = PerfUpdate.Auto();

        // Shared health
        if (_sharedHealthPool != null)
        {
            float norm = _sharedHealthPool.MaxCombinedHealth > 0f
                ? _sharedHealthPool.CombinedHealth / _sharedHealthPool.MaxCombinedHealth
                : 0f;
            _shared.Set(PoTNames.SharedHealthNorm, norm);
        }

        // BUG-082: poll the soul-deployed half of the freeze flag (writes only on change).
        UpdateRescueFreeze();
    }

    // Public API called by other PoT systems
    public void NotifySpawnUnderAttack(GameObject spawnPoint, bool isUnderAttack)
    {
        _shared?.Set(PoTNames.SpawnUnderAttack, isUnderAttack);
        _shared?.Set(PoTNames.AttackedSpawnPoint, spawnPoint);
    }

    public void NotifySoulActive(bool isActive, Vector3 position)
    {
        _shared?.Set(PoTNames.SoulIsActive, isActive);
        _shared?.Set(PoTNames.SoulPosition, isActive ? position : CommonCore.Constants.InvalidVector3Position);
    }

    public void NotifyBarrierWidening(bool isWidening)
    {
        _shared?.Set(PoTNames.BarrierWidening, isWidening);
        _shared?.Set(PoTNames.DarkEnergyAvailable, isWidening);
    }

    public void NotifyTargetEngaged(GameObject target, bool isEngaged)
    {
        _shared?.Set(PoTNames.TargetIsEngaged, isEngaged);
        _shared?.Set(PoTNames.EngagedTarget, target);
    }
}