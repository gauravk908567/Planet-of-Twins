using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// CoalesceSystem
//
// DEPENDENCIES (inject in Inspector):
//   ISkillUnlockState  — am I active?
//   IAbilityDataStore  — upgrade values
//   IStunEvents        — drag StunAbility GO here
//   IPossessEvents     — drag PossessAbility GO here
//
// No static events.
// ─────────────────────────────────────────────────────────────────────────────
public class CoalesceSystem : MonoBehaviour
{
    [Header("Inject")]
    [SerializeField] private MonoBehaviour _unlockStateMono;   // → ISkillUnlockState (SkillTreeManager)
    [SerializeField] private MonoBehaviour _dataStoreMono;     // → IAbilityDataStore  (SkillTreeManager)
    [SerializeField] private TwinAbilitySetup _abilitySetup;      // → TwinAbilitySetup on PlayerManager

    private ISkillUnlockState _unlockState;
    private IAbilityDataStore _dataStore;
    private IStunEvents _stunEvents;
    private IPossessEvents _possessEvents;

    [Header("Prefab")]
    [SerializeField] private GameObject _auraPrefab;

    [Header("Base values")]
    [SerializeField] private float _baseRadius = 1.5f;
    [SerializeField] private float _baseDps = 6f;
    [SerializeField] private float _baseLingerTime = 2.5f;

    private readonly Dictionary<GameObject, CoalesceAura> _activeAuras
        = new Dictionary<GameObject, CoalesceAura>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Awake()
    {
        _unlockState = _unlockStateMono as ISkillUnlockState;
        _dataStore = _dataStoreMono as IAbilityDataStore;

        if (_unlockState == null) Debug.LogError("[Coalesce] Missing ISkillUnlockState");
        if (_dataStore == null) Debug.LogError("[Coalesce] Missing IAbilityDataStore");
    }

    void Start() => StartCoroutine(LateStart());

    System.Collections.IEnumerator LateStart()
    {
        yield return null; // wait one frame — TwinAbilitySetup.Start() must run first
        _stunEvents = _abilitySetup?.StunEvents;
        _possessEvents = _abilitySetup?.PossessEvents;
        if (_stunEvents == null) Debug.LogError("[Coalesce] StunEvents null — check TwinAbilitySetup");
        if (_possessEvents == null) Debug.LogError("[Coalesce] PossessEvents null — check TwinAbilitySetup");
        if (_stunEvents != null) { _stunEvents.OnStunApplied += HandleApplied; _stunEvents.OnStunEnded += HandleEnded; }
        if (_possessEvents != null) { _possessEvents.OnPossessApplied += HandleApplied; _possessEvents.OnPossessEnded += HandleEnded; }
       // Debug.Log($"[Coalesce] LateStart() — stunEvents={(_stunEvents == null ? "NULL" : "OK")} possessEvents={(_possessEvents == null ? "NULL" : "OK")} auraPrefab={(_auraPrefab == null ? "NULL" : "OK")}");
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
        if (_stunEvents != null) { _stunEvents.OnStunApplied += HandleApplied; _stunEvents.OnStunEnded += HandleEnded; }
        if (_possessEvents != null) { _possessEvents.OnPossessApplied += HandleApplied; _possessEvents.OnPossessEnded += HandleEnded; }
    }

    void OnEnable()
    {
        // Only re-subscribe if Start has already run (re-enable after disable)
        if (_stunEvents != null) { _stunEvents.OnStunApplied += HandleApplied; _stunEvents.OnStunEnded += HandleEnded; }
        if (_possessEvents != null) { _possessEvents.OnPossessApplied += HandleApplied; _possessEvents.OnPossessEnded += HandleEnded; }
    }

    void OnDisable()
    {
        if (_stunEvents != null) { _stunEvents.OnStunApplied -= HandleApplied; _stunEvents.OnStunEnded -= HandleEnded; }
        if (_possessEvents != null) { _possessEvents.OnPossessApplied -= HandleApplied; _possessEvents.OnPossessEnded -= HandleEnded; }
    }

    // ── Handlers ──────────────────────────────────────────────────────────────
    void HandleApplied(GameObject enemy)
    {
        Debug.Log($"[Coalesce] HandleApplied called — enemy={enemy?.name} isUnlocked={_unlockState?.IsCoalesceUnlocked} auraPrefab={(_auraPrefab == null ? "NULL" : "OK")}");
        if (!(_unlockState?.IsCoalesceUnlocked ?? false)) { Debug.Log("[Coalesce] BLOCKED — Coalesce not unlocked in skill tree"); return; }

        if (_auraPrefab == null)
        {
            Debug.LogError("[Coalesce] _auraPrefab is NULL — drag CoalesceAura prefab into CoalesceSystem in Inspector");
            return;
        }

        if (enemy == null) return;
        if (_activeAuras.TryGetValue(enemy, out var existing) && existing != null) return;
        _activeAuras.Remove(enemy);

        var go = Instantiate(_auraPrefab, enemy.transform.position, Quaternion.identity, enemy.transform);
        var aura = go.GetComponent<CoalesceAura>();
        if (aura == null) { Debug.LogError("[Coalesce] CoalesceAura component missing from prefab"); return; }

        var d = _dataStore?.CoalesceData;
        float radius = 1.5f;
        float dps = 6f;
        float linger = 2.5f;
        if (d != null)
        {
            foreach (var node in d.nodes)
            {
                if (d.nodes.IndexOf(node) >= d.currentNodeIndex) break;
                radius += node.coalesceRadiusBonus;
                dps += node.coalesceDpsBonus;
                linger += node.coalesceDurationBonus;
            }
        }
        aura.Initialise(enemy, radius, dps, linger);
        _activeAuras[enemy] = aura;
        Debug.Log($"[Coalesce] Aura spawned on {enemy.name}");
    }

    void HandleEnded(GameObject enemy)
    {
        if (enemy == null) return;
        if (!_activeAuras.TryGetValue(enemy, out var aura)) return;
        _activeAuras.Remove(enemy);
        aura?.DetachAndLinger();
    }

    // ── Upgrade values ────────────────────────────────────────────────────────
    float CurrentRadius
    {
        get
        {
            var data = _dataStore?.CoalesceData;
            if (data == null) return _baseRadius;
            float v = _baseRadius;
            for (int i = 0; i < data.currentNodeIndex && i < data.nodes.Count; i++)
                v += data.nodes[i].coalesceRadiusBonus;
            return v;
        }
    }

    float CurrentDps
    {
        get
        {
            var data = _dataStore?.CoalesceData;
            if (data == null) return _baseDps;
            float v = _baseDps;
            for (int i = 0; i < data.currentNodeIndex && i < data.nodes.Count; i++)
                v += data.nodes[i].coalesceDpsBonus;
            return v;
        }
    }

    float CurrentLingerTime
    {
        get
        {
            var data = _dataStore?.CoalesceData;
            if (data == null) return _baseLingerTime;
            float v = _baseLingerTime;
            for (int i = 0; i < data.currentNodeIndex && i < data.nodes.Count; i++)
                v += data.nodes[i].coalesceDurationBonus;
            return v;
        }
    }

    bool IsActive() => _unlockState != null && _unlockState.IsCoalesceUnlocked;

    void LateUpdate()
    {
        var stale = new List<GameObject>();
        foreach (var kvp in _activeAuras)
            if (kvp.Key == null || kvp.Value == null) stale.Add(kvp.Key);
        foreach (var k in stale) _activeAuras.Remove(k);
    }
}