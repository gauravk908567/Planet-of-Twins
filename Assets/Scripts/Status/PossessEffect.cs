using UnityEngine;

public class PossessEffect : StatusEffectBase
{
    private Enemy enemy;
    private Faction originalFaction;
    private IEnemyState previousState;

    private readonly LayerMask _possessedTargetLayer;

    public bool IsPossessing { get; private set; }

    public PossessEffect(GameObject target, float duration, LayerMask possessedTargetLayer)
        : base(target, duration)
    {
        enemy = target.GetComponent<Enemy>();
        _possessedTargetLayer = possessedTargetLayer;
    }

    public override void OnApply()
    {
        base.OnApply();

        if (enemy == null) return;

        // Prevent double possession
        if (enemy.FactionComp.CurrentFaction == Faction.PossessedEnemy)
        {
           // timer = duration; // instantly finish
            return;
        }

        IsPossessing = true;

        originalFaction = enemy.FactionComp.CurrentFaction;
        previousState = enemy.StateMachine.CurrentState;

        enemy.FactionComp.CurrentFaction = Faction.PossessedEnemy;

        enemy.StateMachine.ChangeState(new PossessedState(enemy, _possessedTargetLayer));
    }

    public override void OnRemove()
    {
        if (enemy == null) return;

        enemy.FactionComp.CurrentFaction = originalFaction;

        enemy.StateMachine.ChangeState(enemy.IdleState);

        IsPossessing = false;
    }
}