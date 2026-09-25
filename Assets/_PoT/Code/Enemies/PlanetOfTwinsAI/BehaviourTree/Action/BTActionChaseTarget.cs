using BehaviourTree;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BT Action: Chase the detected twin.
/// Reads attackRange and moveSpeed from enemy Data SO at runtime.
/// Applies mood speedMultiplier from EnemyMoodSystem each tick.
/// Two-tier stuck handling — reroute at 0.8s, fallback at 2.5s.
/// No constructor parameters needed.
/// </summary>
public class BTActionChaseTarget : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Chase Target";

    private float _attackRange;
    private float _baseSpeed;

    // --- Stuck ---
    private float _stuckTimer = 0f;
    private float _stuckTimeThreshold = 0.8f;
    private float _stuckFallbackThreshold = 2.5f;
    private float _stuckMoveDelta = 0.08f;
    private Vector3 _lastPosition;
    private bool _fallbackTriggered = false;

    // --- Reroute ---
    private bool _isRerouting = false;
    private Vector3 _rerouteTarget;
    private float _rerouteRadius = 8f;
    private int _sampleCount = 12;
    private NavMeshPath _navPath;

    protected override void OnEnter()
    {
        base.OnEnter();
        if (_enemy == null) return;

        var tbData = _enemy.Data as TetherBreakerEnemyData;
        _attackRange = tbData != null ? tbData.chainAttackRange : _enemy.Data.attackRange;
        _baseSpeed = _enemy.Data.moveSpeed;

        _enemy.Movement.SetSpeed(MoodSpeed());
        _lastPosition = _enemy.transform.position;
        _stuckTimer = 0f;
        _isRerouting = false;
        _fallbackTriggered = false;
        _navPath = new NavMeshPath();
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        var target = GetBestTarget();
        if (target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        float dist = Vector3.Distance(_enemy.transform.position, target.transform.position);
        if (dist <= _attackRange)
        {
            _enemy.Movement.Stop();
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        _enemy.Movement.SetSpeed(MoodSpeed());

        float moved = Vector3.Distance(_enemy.transform.position, _lastPosition);
        if (moved >= _stuckMoveDelta)
        {
            _lastPosition = _enemy.transform.position;
            _stuckTimer = 0f;
            _isRerouting = false;
            _fallbackTriggered = false;
        }
        else
        {
            _stuckTimer += InDeltaTime;
            if (_stuckTimer >= _stuckTimeThreshold && !_isRerouting && !_fallbackTriggered)
                TryStartReroute(target.transform.position);
            if (_stuckTimer >= _stuckFallbackThreshold && !_fallbackTriggered)
                TriggerFallback();
        }

        if (_fallbackTriggered)
        {
            _enemy.Movement.Stop();
            FaceTarget(target);
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
        }

        if (_isRerouting)
        {
            if (Vector3.Distance(_enemy.transform.position, _rerouteTarget) <= 0.6f)
            {
                _isRerouting = false;
                _stuckTimer = 0f;
                _lastPosition = _enemy.transform.position;
            }
            else _enemy.Movement.MoveTowards(_rerouteTarget);
        }
        else
        {
            _enemy.Movement.MoveTowards(target.transform.position);
        }

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();
        _isRerouting = false; _stuckTimer = 0f; _fallbackTriggered = false;
    }

    private float MoodSpeed()
    {
        var ms = _enemy?.GetComponent<EnemyMoodSystem>();
        return _baseSpeed * (ms != null ? ms.CurrentModifiers.speedMultiplier : 1f);
    }

    private void TriggerFallback()
    {
        _fallbackTriggered = true;
        _isRerouting = false;
        _enemy?.Movement.Stop();
        (_enemy as TetherBreakerEnemy)?.TryChainAttack();
    }

    private void FaceTarget(Player target)
    {
        if (target == null || _enemy == null) return;
        Vector3 dir = (target.transform.position - _enemy.transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero) _enemy.transform.rotation = Quaternion.LookRotation(dir);
    }

    private void TryStartReroute(Vector3 targetPos)
    {
        Vector3 origin = _enemy.transform.position;
        Vector3 best = origin;
        float bestScore = float.MinValue;

        for (int i = 0; i < _sampleCount; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 360f / _sampleCount, 0f) * Vector3.forward;
            Vector3 candidate = origin + dir * _rerouteRadius;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _rerouteRadius * 0.5f, NavMesh.AllAreas)) continue;
            NavMesh.CalculatePath(origin, hit.position, NavMesh.AllAreas, _navPath);
            if (_navPath.status != NavMeshPathStatus.PathComplete) continue;

            float score = Vector3.Dot((targetPos - origin).normalized, (hit.position - origin).normalized);
            score -= Vector3.Dot((targetPos - origin).normalized, dir) * 0.3f;
            if (score > bestScore) { bestScore = score; best = hit.position; }
        }

        if (bestScore > float.MinValue && best != origin)
        {
            _rerouteTarget = best; _isRerouting = true;
            _stuckTimer = 0f; _lastPosition = origin;
        }
    }
}