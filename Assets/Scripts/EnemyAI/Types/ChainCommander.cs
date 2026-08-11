using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chain Commander — Vethara
/// Commands: 2 TetherBreaker + 2 BasicMelee
///
/// Signature — ChainStrike (STUB):
///   When a TetherBreaker in the group has a twin pinned,
///   swings spiked chain at immobilised twin for high damage.
///
/// Brain: GOAPBrainChainCommander
/// </summary>
public class ChainCommander : Enemy, ICommander
{
    [Header("Chain Strike")]
    [SerializeField] private float _strikeRange = 6f;
    [SerializeField] private float _strikeDamage = 40f;
    [SerializeField] private float _strikeCooldown = 12f;

    [Header("Commander")]
    [SerializeField] private float _commandRadius = 15f;
    [SerializeField] private float _deathRageDuration = 4.5f;

    private readonly List<Enemy> _soldiers = new();
    private float _lastStrike = -99f;
    private bool _dead = false;

    // ── ICommander ─────────────────────────────────────────────
    public bool IsAlive => !_dead && !Health.IsDead;
    public float CommandRadius => _commandRadius;

    public Vector3 GetSlotWorldPosition(Vector3 localOffset)
        => transform.position + transform.TransformDirection(localOffset);

    public void RegisterSoldier(Enemy soldier)
    {
        if (soldier == null || _soldiers.Contains(soldier)) return;
        _soldiers.Add(soldier);
        soldier.Health.OnDeath += () => _soldiers.Remove(soldier);
        soldier.GetComponent<GOAPGoalHoldFormation>()?.SetCommander(this);
    }

    public IReadOnlyList<Enemy> Soldiers => _soldiers;

    // ── Init ───────────────────────────────────────────────────
    public void InitialiseCommander(float commandRadius, float deathRageDuration)
    {
        _commandRadius = commandRadius;
        _deathRageDuration = deathRageDuration;
    }

    protected override void Awake()
    {
        base.Awake();
        Health.OnDeath += OnCommanderDied;
    }

    // ── ChainStrike ────────────────────────────────────────────
    private void Update()
    {
        if (Health.IsDead) return;
        if (Time.time - _lastStrike < _strikeCooldown) return;
        if (!AnyTwinPinned()) return;
        TryChainStrike();
    }

    private bool AnyTwinPinned()
    {
        foreach (var s in _soldiers)
            if (s is TetherBreakerEnemy tb && tb.IsSprinting) return true;
        return false;
    }

    private void TryChainStrike()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p is SoulPlayer) continue;
            if (!p.IsGrabbed) continue;
            if (Vector3.Distance(transform.position, p.transform.position) > _strikeRange) continue;
            _lastStrike = Time.time;
            Debug.Log($"[ChainCommander] STUB ChainStrike → {p.name} dmg={_strikeDamage}");
 // TODO: ChainStrike commander-ability VFX retired with EnemyVFXController; re-express via a
            // Manpu reaction cue or an Enraged mood transition when the commander archetypes are finished.
            // TODO: p.Health.TakeDamage(new DamageData(_strikeDamage, DamageType.Physical));
            return;
        }
    }

    // ── Death cascade ──────────────────────────────────────────
    private void OnCommanderDied()
    {
        if (_dead) return;
        _dead = true;
        StartCoroutine(DeathCascade());
        MoodEventBus.AllyDied(gameObject);
    }

    private IEnumerator DeathCascade()
    {
        foreach (var s in _soldiers)
        {
            if (s == null || s.Health.IsDead) continue;
 // Enraged mood transition drives the soldier's Manpu rage aura autonomously — the parallel
            // EnemyVFXController.PlayRage is retired.
            s.GetComponent<EnemyMoodSystem>()
             ?.TransitionTo(EnemyMood.Enraged, _deathRageDuration, EnemyMood.Aggressive);
        }
        yield return new WaitForSeconds(_deathRageDuration);
        foreach (var s in _soldiers)
        {
            if (s == null || s.Health.IsDead) continue;
            s.GetComponent<GOAPGoalHoldFormation>()?.ClearCommander();
        }
        _soldiers.Clear();
    }
}