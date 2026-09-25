using UnityEngine;

/// <summary>
/// Possess effect — switches enemy to attack allies for duration.
/// Replaces old FactionComp + StateMachine.ChangeState pattern.
/// GOAP brain reads IsPossessed from Blackboard and switches to
/// PoT_GOAPGoal_Possessed which drives attacking other enemies.
/// </summary>
public class PossessEffect : StatusEffectBase
{
    private Enemy _enemy;
    private readonly LayerMask _possessedTargetLayer;

    public bool IsPossessing { get; private set; }

    public PossessEffect(GameObject target, float duration, LayerMask possessedTargetLayer)
        : base(target, duration)
    {
        _enemy = target.GetComponent<Enemy>();
        _possessedTargetLayer = possessedTargetLayer;
    }

    public override void OnApply()
    {
        base.OnApply();
        if (_enemy == null) return;
        if (_enemy.IsPossessed) return;

        IsPossessing = true;
        _enemy.ApplyPossession(duration, 1f);
        // GOAP brain reads IsPossessed via Blackboard sync each frame
        // PoT_GOAPGoal_Possessed fires at Maximum priority automatically
    }

    public override void OnRemove()
    {
        if (_enemy == null) return;
        IsPossessing = false;
        // ApplyPossession's coroutine calls OnPossessionEnded on expiry
        // GOAP brain re-evaluates goals naturally next tick
    }
}