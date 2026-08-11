using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Investigate a sound position.
/// Moves to sound location, looks around briefly, returns to wander.
/// Pause/resume safe — speed reset on OnExit.
/// </summary>
public class BTActionInvestigate : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Investigate";

    private PoTPerceptionMemory _memory;
    private float _investigateTimer;
    private bool _arrived;
    private float _scanAngle;

    private const float InvestigateDuration = 2.5f;
    private const float InvestigateSpeed = 0.55f;

    protected override void OnEnter()
    {
        base.OnEnter();
        _memory = _enemy?.GetComponent<PoTPerceptionMemory>();
        _investigateTimer = 0f;
        _arrived = false;
        _scanAngle = 0f;

        float speed = (_enemy?.Data?.moveSpeed ?? 3.5f) * InvestigateSpeed;
        _enemy?.Movement.SetSpeed(speed);

    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_memory == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        if (_memory.SearchState != EnemySearchState.Investigating)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        if (HasTarget)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        Vector3 dest = _memory.LastKnownPosition;

        if (!_arrived)
        {
            float dist = Vector3.Distance(_enemy.transform.position, dest);
            if (dist <= 1.5f)
            {
                _arrived = true;
                _enemy.Movement.Stop();
            }
            else
            {
                // Re-apply speed every tick — pause/resume safe
                float speed = (_enemy?.Data?.moveSpeed ?? 3.5f) * InvestigateSpeed;
                _enemy.Movement.SetSpeed(speed);
                _enemy.Movement.MoveTowards(dest);
            }
        }
        else
        {
            _investigateTimer += InDeltaTime;
            _scanAngle += InDeltaTime * 120f;
            _enemy.transform.rotation = Quaternion.Euler(0f, _scanAngle, 0f);

            if (_investigateTimer >= InvestigateDuration)
            {
                _memory.OnSearchedLastKnownPosition();
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
        _investigateTimer = 0f;
    }
}