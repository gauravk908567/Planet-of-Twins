using UnityEngine;

/// <summary>
/// Plays a "soul absorbed" cue at each combat-kill position (Tier 1). The cue's prefab carries
/// a <see cref="SoulParticleAttractor"/> which now self-resolves the nearest twin and is pool-safe,
/// so this spawner is just: subscribe to the death notifier, play one cue at the death point.
///
/// ARCHITECTURE NOTE: subscribes <c>EnemyDeathNotifier.OnEnemyCombatKill</c> (death world position).
/// No scene scan, no per-kill Instantiate.
/// </summary>
public class KillParticleSpawner : MonoBehaviour
{
    [Header("Cue")]
    [Tooltip("Cue Book — plays FxEvent.Death at each combat-kill position (its element's prefab carries " +
             "SoulParticleAttractor).")]
    [SerializeField] private CueBookData _cueBook;
    [Tooltip("Optional explicit FxManager (R1). Resolves to .Instance otherwise (R4).")]
    [SerializeField] private FxManager _fx;

    [Header("Death notifier")]
    [SerializeField] private EnemyDeathNotifier deathNotifier;

    private void Start()
    {
        deathNotifier ??= EnemyDeathNotifier.Instance;
        if (deathNotifier == null)
        {
            Debug.LogError("[KillParticleSpawner] EnemyDeathNotifier unresolved — is Persistent loaded?", this);
            return;
        }
        // Re-subscribe in case OnEnable fired before deathNotifier was resolved.
        deathNotifier.OnEnemyCombatKill -= HandleCombatKill;
        deathNotifier.OnEnemyCombatKill += HandleCombatKill;
    }

    private void OnEnable()
    {
        if (deathNotifier != null)
            deathNotifier.OnEnemyCombatKill += HandleCombatKill;
    }

    private void OnDisable()
    {
        if (deathNotifier != null)
            deathNotifier.OnEnemyCombatKill -= HandleCombatKill;
    }

    private void HandleCombatKill(Vector3 deathPosition, float sizeScale)
    {
        if (_cueBook == null) return;
        _fx ??= FxManager.Instance;   // R4
        // sizeScale (enemy bounds vs 2 m humanoid) auto-fits the death helix + burst to the body.
        _fx?.PlayBook(_cueBook, "kill_seq", new CueContext(deathPosition, scale: sizeScale));
    }
}
