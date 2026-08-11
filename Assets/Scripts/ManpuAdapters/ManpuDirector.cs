using UnityEngine;

/// <summary>
/// Per-enemy Manpu director. Subscribes to THIS enemy's mood + perception
/// transitions and the global Setsuna signal, and forwards them to the enemy's <c>ManpuSlot</c>.
/// Pure presentation glue — it owns no mood/perception state, it only routes transitions (R8 named
/// handlers, unsubscribed on disable so pooled reuse is clean). The slot applies R1/R2/R3; this just
/// decides *when to ask*.
/// </summary>
public class ManpuDirector : MonoBehaviour
{
    [SerializeField] private ManpuSlot _slot;
    [SerializeField] private EnemyMoodSystem _moodSystem;
    [SerializeField] private PoTPerceptionMemory _perception;

    private void Awake()
    {
        if (_slot == null) _slot = GetComponentInChildren<ManpuSlot>(true);
        if (_moodSystem == null) _moodSystem = GetComponent<EnemyMoodSystem>();
        if (_perception == null) _perception = GetComponent<PoTPerceptionMemory>();
    }

    private void OnEnable()
    {
        if (_moodSystem != null) _moodSystem.OnMoodChanged += HandleMoodChanged;
        if (_perception != null) _perception.OnSearchStateChanged += HandleSearchChanged;
        SetsunaSystem.OnActiveChanged += HandleSetsuna;
    }

    private void OnDisable()
    {
        if (_moodSystem != null) _moodSystem.OnMoodChanged -= HandleMoodChanged;
        if (_perception != null) _perception.OnSearchStateChanged -= HandleSearchChanged;
        SetsunaSystem.OnActiveChanged -= HandleSetsuna;
    }

    // Game glue: converts the game's enums to the package's mirror enums (int-identical — see ManpuEnums).
    private void HandleMoodChanged(EnemyMood oldMood, EnemyMood newMood)
        => _slot?.RequestMoodPulse((ManpuMood)(int)oldMood, (ManpuMood)(int)newMood);

    private void HandleSearchChanged(EnemySearchState oldState, EnemySearchState newState)
        => _slot?.RequestPerceptionPulse((ManpuSearchState)(int)newState);

    private void HandleSetsuna(bool active)
        => _slot?.SetSlowMode(active);
}
