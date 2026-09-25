using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Flee from nearest twin.
/// Used after ritual interrupt or bomb throw.
/// Calls WitnessEnemy.StartRetreat() and holds InProgress until safe distance.
/// Returns Succeeded when retreat distance reached.
/// </summary>
public class BTActionWitnessRetreat : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Witness Retreat";

    private WitnessEnemy _witness;

    protected override void OnEnter()
    {
        base.OnEnter();
        _witness = _enemy as WitnessEnemy;
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_witness == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        if (_witness.IsRetreating)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);

        // Not retreating and wasn't retreating — start it
        if (LastStatus != EBTNodeResult.InProgress)
        {
            _witness.StartRetreat();
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
        }

        // Retreat just finished
        return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
    }
}