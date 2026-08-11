using UnityEngine;

/// <summary>
/// event-driven reaction glyphs (relational Manpu). Lets a discrete world event flash a Manpu glyph on
/// a SPECIFIC enemy regardless of its mood, via the cue-book pulse path
/// (<see cref="ManpuSlot.PlayReaction"/> → <c>FxManager.PlayBook</c>). It owns the RULE, not the enemy — place
/// it on an always-active GO beside <c>ManpuAbilityListener</c>. Reactions are content: a `CueBook_Reactions`
/// with named effects (<c>"betrayed"</c>, <c>"ally_down"</c>); R1 drops a reaction if an ability owns the
/// victim's slot, and the pulse bypasses the R2 debounce (a deliberate accent, not state drift).
///
/// Start-set: (a) <b>betrayed</b> — an enemy attacked by a POSSESSED ally; (b) <b>ally_down</b> — an
/// enemy near an ally's combat death. Adding a reaction later = one `CueElement` + one rule here.
/// </summary>
public class ManpuReactionListener : MonoBehaviour
{
    [Tooltip("Cue Book of reaction effects — effect ids: \"betrayed\", \"ally_down\".")]
    [SerializeField] private CueBookData _reactionBook;

    [Tooltip("Enemies within this radius of an ally's combat death flash the \"ally_down\" reaction.")]
    [Min(0f)] [SerializeField] private float _allyDownRadius = 6f;

    [Tooltip("Layer(s) enemies live on (for the ally-down proximity sweep).")]
    [SerializeField] private LayerMask _enemyLayer = ~0;

    private EnemyDeathNotifier _deaths;
    private bool _subscribed;
    private readonly Collider[] _overlap = new Collider[16];

    // OnEnable can run before the notifier's Awake during Persistent's own load (Unity is
    // per-object Awake→OnEnable) — so the resolve retries in Start (R8: resolve others in Start)
    // and only fails loud THERE. The old OnEnable-only resolve permanently disabled reactions
    // for the whole session whenever the load order went the wrong way.
    private void OnEnable() => TrySubscribe(failLoud: false);
    private void Start()    => TrySubscribe(failLoud: true);

    private void TrySubscribe(bool failLoud)
    {
        if (_subscribed) return;
        _deaths = EnemyDeathNotifier.Instance;
        if (_deaths == null)
        {
            if (failLoud) Debug.LogError("[ManpuReactionListener] No EnemyDeathNotifier — reactions disabled.", this);
            return;
        }
        _deaths.OnEnemyDamaged += HandleDamaged;
        _deaths.OnEnemyCombatKill += HandleAllyDown;
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (!_subscribed || _deaths == null) return;
        _deaths.OnEnemyDamaged -= HandleDamaged;
        _deaths.OnEnemyCombatKill -= HandleAllyDown;
        _subscribed = false;
    }

    // "betrayed" — an enemy attacked by a POSSESSED ally flashes a shocked glyph on the VICTIM's slot.
    private void HandleDamaged(GameObject victim, DamageData data)
    {
        if (_reactionBook == null || victim == null) return;
        var src = data.Source;
        if (src == null || src == victim) return;                      // no source, or self-damage
        var attacker = src.GetComponentInParent<Enemy>();
        if (attacker == null || !attacker.IsPossessed) return;         // only a possessed-ally betrayal
        victim.GetComponentInChildren<ManpuSlot>(true)?.PlayReaction(_reactionBook, "betrayed");
    }

    // "ally_down" — enemies near a combat death flash a reaction (the dead one is despawning; its slot is cleared).
    private void HandleAllyDown(Vector3 deathPos, float _)   // size arg is for the death-helix auto-fit, unused here
    {
        if (_reactionBook == null) return;
        int n = Physics.OverlapSphereNonAlloc(deathPos, _allyDownRadius, _overlap, _enemyLayer);
        for (int i = 0; i < n; i++)
            _overlap[i].GetComponentInChildren<ManpuSlot>(true)?.PlayReaction(_reactionBook, "ally_down");
    }
}
