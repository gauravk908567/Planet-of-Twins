using UnityEngine;

public class PossessEffect : StatusEffectBase
{
    private Enemy enemy;
    private Faction originalFaction;
    private IEnemyState previousState;

    public bool IsPossessing { get; private set; }

    public PossessEffect(GameObject target, float duration)
        : base(target, duration)
    {
        enemy = target.GetComponent<Enemy>();
    }

    public override void OnApply()
    {
        base.OnApply();

        if (enemy == null) return;

        // Prevent double possession
        if (enemy.Faction.CurrentFaction == Faction.PossessedEnemy)
        {
           // timer = duration; // instantly finish
            return;
        }

        IsPossessing = true;

        originalFaction = enemy.Faction.CurrentFaction;
        previousState = enemy.StateMachine.CurrentState;

        enemy.Faction.CurrentFaction = Faction.PossessedEnemy;

        enemy.StateMachine.ChangeState(new PossessedState(enemy));
    }

    public override void OnRemove()
    {
        if (enemy == null) return;

        enemy.Faction.CurrentFaction = originalFaction;

        enemy.StateMachine.ChangeState(enemy.IdleState);

        IsPossessing = false;
    }
}