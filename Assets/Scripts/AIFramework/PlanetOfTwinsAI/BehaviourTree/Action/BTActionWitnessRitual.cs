using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Channel ritual to summon new ally.
/// Calls WitnessEnemy.StartRitual() and holds InProgress while channeling.
/// Returns Failed if ritual is interrupted by twin proximity.
/// Returns Succeeded when ritual completes and ally is summoned.
/// </summary>
public class BTActionWitnessRitual : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Witness Ritual";

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

        // Ritual running — hold InProgress
        if (_witness.IsRitualing)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);

        // Ritual just finished (ally summoned) — succeeded
        if (LastStatus == EBTNodeResult.InProgress && _witness.AllyIsAlive)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        // Ritual interrupted (ally still dead) — failed, flee goal will fire
        if (LastStatus == EBTNodeResult.InProgress && !_witness.AllyIsAlive)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Start ritual
        _witness.StartRitual();
        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }
}