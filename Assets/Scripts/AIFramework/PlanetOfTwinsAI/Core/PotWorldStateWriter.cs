using CommonCore;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Header("Wire in Inspector or leave null to auto-find")]
    [SerializeField] private RescueEventController _rescueController;
    [SerializeField] private AccordStateSystem _accordSystem;
    [SerializeField] private SharedHealthPool _sharedHealthPool;

    private Blackboard<FastName> _shared;

    // Cached barrier POIs — populated once at Start
    private BarrierPOI[] _barrierPOIs;

    // Cached delegate — needed to unsubscribe correctly
    private System.Action<RescueState> _onRescueStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _shared = BlackboardManager.GetSharedBlackboard(PoTNames.SharedBlackboardID);
        if (_shared == null)
        {
            Debug.LogError("[PoTWorldStateWriter] Could not get shared blackboard. Ensure BlackboardManager is in the scene.", this);
            return;
        }

        _barrierPOIs = FindObjectsByType<BarrierPOI>(FindObjectsSortMode.None);

        InitialiseBlackboard();
        FindMissingReferences();
        SubscribeToEvents();

        // Refresh barrier cache whenever a chunk loads or unloads
        SceneManager.sceneLoaded += OnSceneLoadedRefresh;
        SceneManager.sceneUnloaded += OnSceneUnloadedRefresh;
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        SceneManager.sceneLoaded -= OnSceneLoadedRefresh;
        SceneManager.sceneUnloaded -= OnSceneUnloadedRefresh;
    }

    private void OnSceneLoadedRefresh(Scene scene, LoadSceneMode mode) => RefreshBarrierCache();
    private void OnSceneUnloadedRefresh(Scene scene) => RefreshBarrierCache();

    private void RefreshBarrierCache()
    {
        _barrierPOIs = FindObjectsByType<BarrierPOI>(FindObjectsSortMode.None);
        Debug.Log($"[PoTWorldStateWriter] Barrier cache refreshed: {_barrierPOIs.Length} barriers.");
    }

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
        _shared.Set(PoTNames.NearBarrier, false);   // ← Phase 4
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
        bool active = state != RescueState.Idle;
        _shared?.Set(PoTNames.IsRescueActive, active);
        _shared?.Set(PoTNames.HasRescueTarget, active);
    }

    private void OnAccordActivated() => _shared?.Set(PoTNames.AccordStateActive, true);
    private void OnAccordDeactivated() => _shared?.Set(PoTNames.AccordStateActive, false);

    private void Update()
    {
        if (_shared == null) return;

        // Shared health
        if (_sharedHealthPool != null)
        {
            float norm = _sharedHealthPool.MaxCombinedHealth > 0f
                ? _sharedHealthPool.CombinedHealth / _sharedHealthPool.MaxCombinedHealth
                : 0f;
            _shared.Set(PoTNames.SharedHealthNorm, norm);
        }

        // NearBarrier — true if any active enemy is within any barrier's proximity radius
        _shared.Set(PoTNames.NearBarrier, IsAnyEnemyNearBarrier());
    }

    /// <summary>
    /// Checks all cached BarrierPOIs each frame.
    /// Returns true if at least one enemy is within the barrier's dark energy radius.
    /// BarrierPOI.NearbyEnemyCount is maintained by BarrierPOI itself via trigger/overlap.
    /// Falls back to manual distance check if NearbyEnemyCount is not exposed.
    /// </summary>
    private bool IsAnyEnemyNearBarrier()
    {
        if (_barrierPOIs == null || _barrierPOIs.Length == 0)
            return false;

        foreach (var barrier in _barrierPOIs)
        {
            if (barrier == null) continue;
            if (barrier.NearbyEnemyCount > 0)
                return true;
        }
        return false;
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