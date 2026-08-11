using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Move toward nearest pact member when too far.
/// Fires when IsRallying=true (reused as "needs regroup").
/// Returns Succeeded when close enough or twin found.
/// </summary>
public class BTActionStayInPact : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "StayInPact";

    private EnemyProximityPower _proximityPower;
    private const float ArrivalDistance = 3f;

    protected override void OnEnter()
    {
        base.OnEnter();
        _proximityPower = _enemy?.GetComponent<EnemyProximityPower>();
        _enemy?.Movement.SetSpeed(_enemy.Data?.moveSpeed ?? 3.5f);
        Debug.Log($"[StayInPact] {_enemy?.name} regrouping with pact");
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_proximityPower == null || !_proximityPower.InPact)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Twin found — abandon regroup
        if (HasTarget)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        float dist = _proximityPower.DistanceToNearestPactMember;
        float pactRange = ComboReadyRegistry.Instance?.PactRange ?? 12f;

        if (dist <= pactRange)
        {
            _enemy.Movement.Stop();
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        Vector3 dest = _proximityPower.NearestPactMemberPosition;
        _enemy.Movement.SetSpeed(_enemy.Data?.moveSpeed ?? 3.5f);
        _enemy.Movement.MoveTowards(dest);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();
        if (_enemy?.Data != null)
            _enemy.Movement.SetSpeed(_enemy.Data.moveSpeed);
    }
}