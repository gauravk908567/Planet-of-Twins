using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared base of every commander archetype (<see cref="ChainCommander"/>, <see cref="PenitentCommander"/>,
/// <see cref="GrandSummoner"/>): the squad, its formation slots and the death cascade. An archetype adds only its
/// signature ability, and hears about hits on its soldiers through <see cref="OnSoldierDamaged"/>.
///
/// Each soldier is tracked by a <see cref="SquadMember"/> whose named handlers are removed again when the soldier
/// dies or the squad is released (R8): a pooled soldier must never keep reporting to a commander it no longer follows.
/// </summary>
public abstract class CommanderEnemy : Enemy, ICommander, IEnemyReuseReset
{
    [Header("Commander")]
    [SerializeField] private float _commandRadius = 15f;
    [SerializeField] private float _deathRageDuration = 4.5f;

    private readonly List<Enemy> _soldiers = new();
    private readonly List<SquadMember> _members = new();   // same order as _soldiers; owns each soldier's handlers
    private bool _dead = false;

    // ── ICommander ─────────────────────────────────────────────
    public bool IsAlive => !_dead && !Health.IsDead;
    public float CommandRadius => _commandRadius;
    public IReadOnlyList<Enemy> Soldiers => _soldiers;

    public Vector3 GetSlotWorldPosition(Vector3 localOffset)
        => transform.position + transform.TransformDirection(localOffset);

    public void RegisterSoldier(Enemy soldier)
    {
        if (soldier == null || _soldiers.Contains(soldier)) return;
        var member = new SquadMember(this, soldier);
        _soldiers.Add(soldier);
        _members.Add(member);
        member.Subscribe();
        soldier.GetComponent<GOAPGoalHoldFormation>()?.SetCommander(this);
    }

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

    // BUG-135 — a pooled commander comes back alive with no squad. The pool deactivated it inside the death
    // event, which cut DeathCascade short, so release any old soldiers still following it first.
    // An archetype resets its own ability state in an override that calls this.
    public virtual void ResetForReuse()
    {
        ReleaseSquad();
        _dead = false;
    }

    /// <summary>A soldier of this squad took a hit (only reported while the commander is alive).</summary>
    protected virtual void OnSoldierDamaged(Enemy soldier, float amount) { }

    // Unhooks every soldier: the formation goal lets go (only if it still follows THIS commander, BUG-135) and the
    // soldier's handlers are removed.
    private void ReleaseSquad()
    {
        for (int i = 0; i < _members.Count; i++)
        {
            _members[i].Unsubscribe();
            var s = _soldiers[i];
            if (s != null) s.GetComponent<GOAPGoalHoldFormation>()?.ReleaseFrom(this);
        }
        _soldiers.Clear();
        _members.Clear();
    }

    private void RemoveMember(SquadMember member)
    {
        int index = _members.IndexOf(member);
        if (index < 0) return;
        member.Unsubscribe();
        _members.RemoveAt(index);
        _soldiers.RemoveAt(index);
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
        ReleaseSquad();
    }

    // ── Squad member ───────────────────────────────────────────
    // One per registered soldier: its death and damage handlers are named methods, so they can be removed (R8).
    private sealed class SquadMember
    {
        private readonly CommanderEnemy _commander;
        private readonly Enemy _soldier;

        public SquadMember(CommanderEnemy commander, Enemy soldier)
        {
            _commander = commander;
            _soldier = soldier;
        }

        public void Subscribe()
        {
            _soldier.Health.OnDeath += HandleSoldierDeath;
            _soldier.Health.OnDamageTaken += HandleSoldierDamaged;
        }

        public void Unsubscribe()
        {
            if (_soldier == null) return;   // destroyed (not pooled): its events went with it
            _soldier.Health.OnDeath -= HandleSoldierDeath;
            _soldier.Health.OnDamageTaken -= HandleSoldierDamaged;
        }

        private void HandleSoldierDeath() => _commander.RemoveMember(this);

        private void HandleSoldierDamaged(EnemyHealthComponent health, float amount, Vector3 hitPoint)
        {
            if (_commander.IsAlive) _commander.OnSoldierDamaged(_soldier, amount);
        }
    }
}
