using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Rush to defend nearest spawn point.
/// Moves to spawn point at full speed.
/// Returns Succeeded when arrived or spawn no longer under attack.
/// </summary>
public class BTActionDefendSpawn : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "DefendSpawn";

    private EnemyPOITracker _poiTracker;
    private Vector3 _defendTarget;
    private const float ArrivalDist = 2.5f;

    protected override void OnEnter()
    {
        base.OnEnter();
        _poiTracker = _enemy?.GetComponent<EnemyPOITracker>();

        var spawn = _poiTracker?.NearestSpawnPoint;
        _defendTarget = spawn != null
            ? spawn.transform.position
            : _enemy.transform.position;

        _enemy?.Movement.SetSpeed((_enemy.Data?.moveSpeed ?? 3.5f) * 1.2f); // rush speed
        Debug.Log($"[DefendSpawn] {_enemy?.name} rushing to {_defendTarget}");
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Spawn no longer under attack
        bool spawnUnderAttack = false;
        _enemy.GetComponent<PoTGOAPBrainBase>()?.LinkedBlackboard
            ?.TryGet(PoTNames.SpawnUnderAttack, out spawnUnderAttack, false);
        if (!spawnUnderAttack)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        float dist = Vector3.Distance(_enemy.transform.position, _defendTarget);
        if (dist <= ArrivalDist)
        {
            _enemy.Movement.Stop();
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        _enemy.Movement.SetSpeed((_enemy.Data?.moveSpeed ?? 3.5f) * 1.2f);
        _enemy.Movement.MoveTowards(_defendTarget);

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