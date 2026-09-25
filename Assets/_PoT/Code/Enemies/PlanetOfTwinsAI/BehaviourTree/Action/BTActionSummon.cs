using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Trigger a summon.
/// Calls SummonerEnemy.TriggerSummon() and holds InProgress while summoning.
/// Returns Succeeded when summon completes.
/// Returns Failed if summon not ready or enemy not a summoner.
/// </summary>
public class BTActionSummon : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Summon";

    private SummonerEnemy _summoner;

    protected override void OnEnter()
    {
        base.OnEnter();
        _summoner = _enemy as SummonerEnemy;
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_summoner == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Already summoning — hold InProgress
        if (_summoner.IsSummoning)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);

        // Summon just completed — succeeded
        if (LastStatus == EBTNodeResult.InProgress)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        // Can't summon yet
        if (!_summoner.CanSummon)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Face target before summoning
        var target = GetBestTarget();
        if (target != null)
        {
            Vector3 dir = (target.transform.position - _enemy.transform.position);
            dir.y = 0f;
            if (dir != Vector3.zero)
                _enemy.transform.rotation = Quaternion.LookRotation(dir);
        }

        _summoner.TriggerSummon();
        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }
}