using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Search at LastKnownPosition after losing twin.
/// Moves to last known position, looks around (360 scan), then gives up.
/// Speed scales with confidence from PoTPerceptionMemory.
/// Pause/resume safe — speed reset on OnExit.
/// </summary>
public class BTActionSearch : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Search";

    private PoTPerceptionMemory _memory;
    private float _searchTimer;
    private bool _arrived;
    private float _scanAngle;

    protected override void OnEnter()
    {
        base.OnEnter();
        _memory = _enemy?.GetComponent<PoTPerceptionMemory>();
        _searchTimer = 0f;
        _arrived = false;
        _scanAngle = 0f;

        if (_memory == null || !_memory.HasMemory) return;

        float speed = (_enemy.Data?.moveSpeed ?? 3.5f) * _memory.SpeedMultiplier;
        _enemy.Movement.SetSpeed(speed);

    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_memory == null || !_memory.HasMemory)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        if (HasTarget)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        if (_memory.Confidence <= 0.05f)
        {
            _enemy.Movement.Stop();
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        Vector3 dest = _memory.LastKnownPosition;

        if (!_arrived)
        {
            float dist = Vector3.Distance(_enemy.transform.position, dest);
            if (dist <= 1.5f)
            {
                _arrived = true;
                _enemy.Movement.Stop();
                _memory.OnSearchedLastKnownPosition();
            }
            else
            {
                // Re-apply speed every tick — pause/resume safe
                float speed = (_enemy.Data?.moveSpeed ?? 3.5f) * _memory.SpeedMultiplier;
                _enemy.Movement.SetSpeed(speed);
                _enemy.Movement.MoveTowards(dest);
            }
        }
        else
        {
            _searchTimer += InDeltaTime;
            _scanAngle += InDeltaTime * 90f;
            _enemy.transform.rotation = Quaternion.Euler(0f, _scanAngle, 0f);

            if (_searchTimer >= _memory.SearchDuration)
            {
                _enemy.Movement.Stop();
                return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
            }
        }

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();

        _arrived = false;
        _searchTimer = 0f;
    }
}