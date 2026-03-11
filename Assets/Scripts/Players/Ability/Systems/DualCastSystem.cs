using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// DualCastSystem
//
// DEPENDENCIES (inject all three in Inspector):
//   ISkillUnlockState  — am I active?
//   IStunEvents        — drag StunAbility GameObject here
//   IPossessEvents     — drag PossessAbility GameObject here
//
// No static events. No FindObjectOfType. No singletons.
// ─────────────────────────────────────────────────────────────────────────────
public class DualCastSystem : MonoBehaviour
{
    [Header("Inject")]
    [SerializeField] private MonoBehaviour _unlockStateMono;   // → ISkillUnlockState (SkillTreeManager)
    [SerializeField] private TwinAbilitySetup _abilitySetup;   // → TwinAbilitySetup on PlayerManager

    private ISkillUnlockState _unlockState;
    private IStunEvents _stunEvents;
    private IPossessEvents _possessEvents;

    [Header("Settings")]
    [SerializeField] private float _syncWindow = 0.5f;
    [SerializeField] private float _cooldownBonus = 0.20f;
    [SerializeField] private float _syncCooldown = 1.5f;

    private float _lastStunTime = -99f;
    private float _lastPossessTime = -99f;
    private float _lastSyncTime = -99f;

    // Instance event — fine, not static
    public event System.Action OnSynced;

    void Awake()
    {
        _unlockState = _unlockStateMono as ISkillUnlockState;
        if (_unlockState == null) Debug.LogError("[DualCast] Missing ISkillUnlockState");
    }

    void Start() => StartCoroutine(LateStart());

    System.Collections.IEnumerator LateStart()
    {
        yield return null; // wait one frame — TwinAbilitySetup.Start() must run first
        _stunEvents = _abilitySetup?.StunEvents;
        _possessEvents = _abilitySetup?.PossessEvents;
        if (_stunEvents == null) Debug.LogError("[DualCast] StunEvents null — check TwinAbilitySetup");
        if (_possessEvents == null) Debug.LogError("[DualCast] PossessEvents null — check TwinAbilitySetup");
        if (_stunEvents != null) _stunEvents.OnStunCast += HandleStunCast;
        if (_possessEvents != null) _possessEvents.OnPossessCast += HandlePossessCast;
        Debug.Log($"[DualCast] LateStart() — stunEvents={(_stunEvents == null ? "NULL" : "OK")} possessEvents={(_possessEvents == null ? "NULL" : "OK")}");
    }


    System.Collections.IEnumerator RetrySubscribe()
    {
        // Wait until TwinAbilitySetup has initialised
        for (int i = 0; i < 10; i++)
        {
            yield return null;
            _stunEvents = _abilitySetup?.StunEvents;
            _possessEvents = _abilitySetup?.PossessEvents;
            if (_stunEvents != null && _possessEvents != null) break;
        }

        if (_stunEvents == null || _possessEvents == null)
        { Debug.LogError($"[{GetType().Name}] Still no events after retry — check TwinAbilitySetup"); yield break; }

        Debug.Log($"[{GetType().Name}] Retry succeeded — subscribing to events");
        if (_stunEvents != null) _stunEvents.OnStunCast += HandleStunCast;
        if (_possessEvents != null) _possessEvents.OnPossessCast += HandlePossessCast;
    }

    void OnEnable()
    {
        // Only re-subscribe if Start has already run (re-enable after disable)
        if (_stunEvents != null) _stunEvents.OnStunCast += HandleStunCast;
        if (_possessEvents != null) _possessEvents.OnPossessCast += HandlePossessCast;
    }

    void OnDisable()
    {
        if (_stunEvents != null) _stunEvents.OnStunCast -= HandleStunCast;
        if (_possessEvents != null) _possessEvents.OnPossessCast -= HandlePossessCast;
    }

    void HandleStunCast()
    {
        if (!IsActive()) return;
        _lastStunTime = Time.time;
        TrySync();
    }

    void HandlePossessCast()
    {
        if (!IsActive()) return;
        _lastPossessTime = Time.time;
        TrySync();
    }

    void TrySync()
    {
        if (Mathf.Abs(_lastStunTime - _lastPossessTime) > _syncWindow) return;
        if (Time.time - _lastSyncTime < _syncCooldown) return;

        _lastSyncTime = Time.time;
        _stunEvents?.ReduceCurrentCooldown(_cooldownBonus);
        _possessEvents?.ReduceCurrentCooldown(_cooldownBonus);
        OnSynced?.Invoke();
        Debug.Log("[DualCast] Synced.");
    }

    bool IsActive() => _unlockState != null && _unlockState.IsDualCastUnlocked;
}