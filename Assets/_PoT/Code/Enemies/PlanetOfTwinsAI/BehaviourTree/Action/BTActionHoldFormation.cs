using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Move to and hold formation slot position.
/// Reads slot world position from GOAPGoalHoldFormation on this enemy.
/// Returns Succeeded when within tolerance of slot.
/// Returns Failed if goal missing or commander dead.
/// </summary>
public class BTActionHoldFormation : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Hold Formation";

    private GOAPGoalHoldFormation _formGoal;
    private const float Tolerance = 1.5f;

    protected override void OnEnter()
    {
        base.OnEnter();
        _formGoal = _enemy?.GetComponent<GOAPGoalHoldFormation>();
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_formGoal == null || _enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        Vector3 slot = _formGoal.GetFormationPosition();
        if (slot == Vector3.zero)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        float dist = Vector3.Distance(_enemy.transform.position, slot);
        if (dist <= Tolerance)
        {
            _enemy.Movement.Stop();
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        _enemy.Movement.MoveTowards(slot);
        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();
    }
}