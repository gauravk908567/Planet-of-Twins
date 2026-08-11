using BehaviourTree;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BT Action: Kite — maintain desired range from target.
/// Reads minEngageRange and desiredRange from RangedEnemyData/SiphonEnemyData SO.
/// Applies mood speedMultiplier from EnemyMoodSystem each tick.
/// Two-tier stuck handling — reroute at 0.8s, stand-and-shoot fallback at 2.5s.
/// No constructor parameters needed.
/// </summary>
public class BTActionKite : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Kite";

    private float _minEngageRange;
    private float _desiredRange;
    private float _baseSpeed;

    // --- Stuck ---
    private float _stuckTimer = 0f;
    private float _stuckTimeThreshold = 0.8f;
    private float _stuckFallbackThreshold = 2.5f;
    private float _stuckMoveDelta = 0.08f;
    private Vector3 _lastPosition;
    private bool _cornered = false;

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

        if (_enemy.Data is SiphonEnemyData sd)
        {
            _minEngageRange = sd.panicRange;
            _desiredRange = sd.preferredRange;
        }
        else if (_enemy.Data is RangedEnemyData rd)
        {
            _minEngageRange = rd.minEngageRange;
            _desiredRange = rd.desiredRange;
        }
        else
        {
            _minEngageRange = 3f;
            _desiredRange = 8f;
        }

        _baseSpeed = _enemy.Data?.moveSpeed ?? 3.5f;
        _lastPosition = _enemy.transform.position;
        _stuckTimer = 0f;
        _isRerouting = false;
        _cornered = false;
        _navPath = new NavMeshPath();

        _enemy.Movement.SetSpeed(MoodSpeed());
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        var target = GetBestTarget();
        if (target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        float dist = Vector3.Distance(_enemy.transform.position, target.transform.position);

        float lower = _desiredRange - 1.5f;
        float upper = _desiredRange + 1.5f;

        if (dist >= lower && dist <= upper)
        {
            _enemy.Movement.Stop();
            FaceTarget(target);
            _isRerouting = false; _stuckTimer = 0f; _cornered = false;
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        bool isFleeing = dist < _desiredRange;
        _enemy.Movement.SetSpeed(MoodSpeed());

        float moved = Vector3.Distance(_enemy.transform.position, _lastPosition);
        if (moved >= _stuckMoveDelta)
        {
            _lastPosition = _enemy.transform.position;
            _stuckTimer = 0f; _isRerouting = false; _cornered = false;
        }
        else
        {
            _stuckTimer += InDeltaTime;
            if (_stuckTimer >= _stuckTimeThreshold && !_isRerouting && !_cornered)
                TryStartReroute(target.transform.position, isFleeing);
            if (_stuckTimer >= _stuckFallbackThreshold && !_cornered)
            {
                _cornered = true; _isRerouting = false;
                _enemy.Movement.Stop();
            }
        }

        if (_cornered)
        {
            FaceTarget(target);
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        if (_isRerouting)
        {
            if (Vector3.Distance(_enemy.transform.position, _rerouteTarget) <= 0.6f)
            {
                _isRerouting = false; _stuckTimer = 0f;
                _lastPosition = _enemy.transform.position;
            }
            else _enemy.Movement.MoveTowards(_rerouteTarget);
        }
        else if (isFleeing)
        {
            Vector3 flee = (_enemy.transform.position - target.transform.position).normalized;
            flee.y = 0f;
            _enemy.Movement.MoveTowards(_enemy.transform.position + flee * 5f);
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
        _isRerouting = false; _stuckTimer = 0f; _cornered = false;
    }

    private float MoodSpeed()
    {
        var ms = _enemy?.GetComponent<EnemyMoodSystem>();
        return _baseSpeed * (ms != null ? ms.CurrentModifiers.speedMultiplier : 1f);
    }

    private void FaceTarget(Player target)
    {
        if (target == null || _enemy == null) return;
        Vector3 dir = (target.transform.position - _enemy.transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero) _enemy.transform.rotation = Quaternion.LookRotation(dir);
    }

    private void TryStartReroute(Vector3 targetPos, bool fleeing)
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

            float distFromTarget = Vector3.Distance(hit.position, targetPos);
            float score = fleeing ? distFromTarget : -distFromTarget;
            Vector3 currentDir = fleeing ? (origin - targetPos).normalized : (targetPos - origin).normalized;
            score -= Vector3.Dot(currentDir, dir) * 0.4f;
            if (score > bestScore) { bestScore = score; best = hit.position; }
        }

        if (bestScore > float.MinValue && best != origin)
        {
            _rerouteTarget = best; _isRerouting = true;
            _stuckTimer = 0f; _lastPosition = origin;
        }
    }
}