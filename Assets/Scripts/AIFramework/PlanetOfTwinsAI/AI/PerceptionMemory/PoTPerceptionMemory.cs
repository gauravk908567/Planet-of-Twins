using CommonCore;
using UnityEngine;

/// <summary>
/// Tracks enemy perception memory — where twin was last seen,
/// how confident the enemy is of twin's position, and current search state.
///
/// ATTACH: To every enemy prefab alongside GOAPBrain.
///
/// Confidence decay:
///   1.0 = certain (actively seeing twin)
///   0.7 = pursuing (twin left sight recently)
///   0.5 = searching (lost sight, heading to last known position)
///   0.3 = suspecting (searched, nothing found)
///   0.0 = gave up (return to wander)
///
/// Speed varies with confidence — feeds into BTActionChaseTarget.
/// Search state written to Blackboard — GOAP goals read it.
/// </summary>
public class PoTPerceptionMemory : MonoBehaviour
{
    [Header("Confidence Decay")]
    [Tooltip("Seconds to decay from 1.0 to 0.0 after losing sight.")]
    [SerializeField] private float _decayDuration = 8f;
    [Tooltip("Confidence below which enemy gives up and returns to wander.")]
    [SerializeField] private float _giveUpThreshold = 0.05f;
    [Tooltip("Confidence below which enemy switches from pursuing to searching.")]
    [SerializeField] private float _searchThreshold = 0.5f;
    [Tooltip("Confidence below which enemy switches from searching to suspecting.")]
    [SerializeField] private float _suspectThreshold = 0.25f;

    [Header("Search")]
    [Tooltip("How long enemy looks around at LastKnownPosition before giving up.")]
    [SerializeField] private float _searchDuration = 4f;
    [Tooltip("How far enemy wanders during search scan.")]
    [SerializeField] private float _searchRadius = 3f;

    // ── State ──────────────────────────────────────────────
    private float _confidence;
    private Vector3 _lastKnownPosition;
    private float _lastSeenTime;
    private EnemySearchState _searchState;
    private bool _hasMemory;

    private Enemy _enemy;
    private PoTGOAPBrainBase _brain;

    // ── Public accessors ───────────────────────────────────
    public float Confidence => _confidence;
    public Vector3 LastKnownPosition => _lastKnownPosition;
    public EnemySearchState SearchState => _searchState;

    /// <summary>Fires when the search state changes (old → new). Manpu uses the entry into Pursuing
    /// as the "!" player-detected pulse (the single highest-value perception read).</summary>
    public event System.Action<EnemySearchState, EnemySearchState> OnSearchStateChanged;
    private EnemySearchState _lastNotifiedState = EnemySearchState.Idle;

    public bool HasMemory => _hasMemory;
    public float SearchDuration => _searchDuration;
    public float SearchRadius => _searchRadius;

    /// <summary>
    /// Speed multiplier based on confidence.
    /// Confident = full speed, Searching = 60%, Suspecting = 40%.
    /// </summary>
    public float SpeedMultiplier
    {
        get
        {
            if (_confidence >= 0.7f) return 1.0f;   // Pursuing — full speed
            if (_confidence >= 0.5f) return 0.85f;  // Lost sight — slightly cautious
            if (_confidence >= 0.3f) return 0.6f;   // Searching — deliberate
            if (_confidence >= 0.1f) return 0.4f;   // Suspecting — slow creep
            return 1.0f;                             // Gave up — wander speed
        }
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _brain = GetComponent<PoTGOAPBrainBase>();
        _confidence = 0f;
        _lastKnownPosition = CommonCore.Constants.InvalidVector3Position;
        _searchState = EnemySearchState.Idle;
        _hasMemory = false;
    }

    private void Update()
    {
        if (_enemy == null || _enemy.Health.IsDead) return;

        bool hasTarget = HasCurrentTarget();

        if (hasTarget)
        {
            // Actively seeing twin — full confidence, update last known
            _confidence = 1.0f;
            _lastKnownPosition = GetCurrentTargetPosition();
            _lastSeenTime = Time.time;
            _hasMemory = true;
            _searchState = EnemySearchState.Pursuing;
        }
        else if (_hasMemory)
        {
            // Lost sight — decay confidence
            float elapsed = Time.time - _lastSeenTime;
            _confidence = Mathf.Clamp01(1f - (elapsed / _decayDuration));

            // Update search state based on confidence
            if (_confidence >= 0.7f)
                _searchState = EnemySearchState.Pursuing;
            else if (_confidence >= _searchThreshold)
                _searchState = EnemySearchState.Searching;
            else if (_confidence >= _suspectThreshold)
                _searchState = EnemySearchState.Suspecting;
            else if (_confidence <= _giveUpThreshold)
            {
                _searchState = EnemySearchState.Idle;
                _hasMemory = false;
                _confidence = 0f;
                _lastKnownPosition = CommonCore.Constants.InvalidVector3Position;
            }
        }

        WriteToBlackboard();

        if (_searchState != _lastNotifiedState)
        {
            var prev = _lastNotifiedState;
            _lastNotifiedState = _searchState;
            OnSearchStateChanged?.Invoke(prev, _searchState);
        }
    }

    // ── Public API ─────────────────────────────────────────

    /// <summary>Called by BTActionSearch when enemy arrives at last known position.</summary>
    public void OnSearchedLastKnownPosition()
    {
        // Reduce confidence — searched and found nothing
        _confidence = Mathf.Max(0f, _confidence - 0.3f);
        Debug.Log($"[Perception] {_enemy.name} searched LastKnownPos — confidence now {_confidence:F2}");
    }

    /// <summary>Called by SoundEventSystem when sound is heard.</summary>
    public void InjectSoundMemory(Vector3 soundPosition, float confidenceBoost = 0.5f)
    {
        // Sound gives partial confidence — not certain but worth investigating
        if (_confidence < confidenceBoost)
        {
            _confidence = confidenceBoost;
            _lastKnownPosition = soundPosition;
            _lastSeenTime = Time.time;
            _hasMemory = true;
            _searchState = EnemySearchState.Investigating;
            Debug.Log($"[Perception] {_enemy.name} heard sound at {soundPosition} — confidence={_confidence:F2}");
        }
    }

    /// <summary>Called when twin is spotted fresh — instant confidence restore.</summary>
    public void OnTwinSpotted(Vector3 position)
    {
        _confidence = 1.0f;
        _lastKnownPosition = position;
        _lastSeenTime = Time.time;
        _hasMemory = true;
        _searchState = EnemySearchState.Pursuing;
        MoodEventBus.Fire(EnemySocialEvent.TwinSpotted, _enemy.gameObject);
    }

    /// <summary>Called when twin is lost — starts decay.</summary>
    public void OnTwinLost()
    {
        _lastSeenTime = Time.time;
        MoodEventBus.Fire(EnemySocialEvent.TwinLost, null);
        Debug.Log($"[Perception] {_enemy.name} lost twin — starting decay from {_confidence:F2}");
    }

    // ── Helpers ────────────────────────────────────────────
    private bool HasCurrentTarget()
    {
        if (_brain?.LinkedBlackboard == null) return false;
        GameObject go = null;
        _brain.LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out go, null);
        return go != null;
    }

    private Vector3 GetCurrentTargetPosition()
    {
        if (_brain?.LinkedBlackboard == null) return CommonCore.Constants.InvalidVector3Position;
        GameObject go = null;
        _brain.LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out go, null);
        return go != null ? go.transform.position : CommonCore.Constants.InvalidVector3Position;
    }

    private void WriteToBlackboard()
    {
        if (_brain?.LinkedBlackboard == null) return;
        _brain.LinkedBlackboard.Set(PoTNames.PerceptionConfidence, _confidence);
        _brain.LinkedBlackboard.Set(PoTNames.LastKnownPosition, _lastKnownPosition);
        _brain.LinkedBlackboard.Set(PoTNames.HasPerceptionMemory, _hasMemory);
        _brain.LinkedBlackboard.Set(PoTNames.EnemySearchState, (int)_searchState);
    }
}

/// <summary>Enemy search state — written to Blackboard for GOAP goals.</summary>
public enum EnemySearchState
{
    Idle,           // No memory — wander
    Pursuing,       // Actively chasing — full confidence
    Searching,      // Lost sight — heading to LastKnownPosition
    Suspecting,     // Searched — low confidence patrol
    Investigating,  // Sound heard — checking sound position
}