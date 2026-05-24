using BehaviourTree;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BT Action: Smart wander within wanderRadius.
/// Uses scored NavMesh sampling — picks reachable, varied points.
/// Stuck detection uses displacement over time window (not velocity)
/// to handle slow diagonal movement correctly.
///
/// Stuck recovery: expands search radius and picks new point immediately.
/// Never picks unreachable points — path validity checked before moving.
/// </summary>
public class BTActionWander : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Wander";

    // ── Wander state (EnemyAmbientState component) ────────
    private EnemyAmbientState _ambient;

    // ── Stuck detection ────────────────────────────────────
    private Vector3 _stuckCheckPosition;
    private float _stuckCheckTimer;
    private float _expandedRadius;
    private bool _radiusExpanded;

    private const float StuckCheckInterval = 3.0f;  // check every 3s
    private const float StuckMinDisplacement = 0.5f; // must move at least 0.5 units in 3s
    private const float RadiusExpandFactor = 1.5f; // expand radius by 50% when stuck
    private const int MaxSampleAttempts = 8;    // candidate points to evaluate

    // ── Recent positions (avoid revisiting) ────────────────
    private readonly Vector3[] _recentPoints = new Vector3[4];
    private int _recentIdx;
    private bool _recentInit;

    protected override void OnEnter()
    {
        base.OnEnter();
        if (_enemy == null) return;

        _ambient = _enemy.GetComponent<EnemyAmbientState>();
        if (_ambient == null)
        {
            Debug.LogWarning($"[Wander] No EnemyAmbientState on {_enemy.name} — add component.");
            return;
        }

        float wanderSpeed = (_enemy.Data?.moveSpeed ?? 3.5f) * 0.5f;
        _enemy.Movement.SetSpeed(wanderSpeed);

        if (!_ambient.WanderInit)
        {
            _ambient.WanderInit = true;
            _ambient.WanderWaiting = false;
            _ambient.WanderTimer = 0f;
            _ambient.WanderDuration = RandomWaitDuration();

            _stuckCheckPosition = _enemy.transform.position;
            _stuckCheckTimer = 0f;
            _expandedRadius = 0f;
            _radiusExpanded = false;

            PickNewWanderTarget();
        }
        else
        {
            // Resuming — restore speed only
            _enemy.Movement.SetSpeed(wanderSpeed);

            // Re-apply destination in case agent lost it
            if (!_ambient.WanderWaiting && _ambient.WanderTarget != Vector3.zero)
                _enemy.Movement.MoveTowards(_ambient.WanderTarget);
        }
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_ambient == null || _enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        if (_ambient.WanderWaiting)
        {
            _ambient.WanderTimer += InDeltaTime;
            if (_ambient.WanderTimer >= _ambient.WanderDuration)
            {
                _ambient.WanderWaiting = false;
                _ambient.WanderTimer = 0f;
                _ambient.WanderDuration = RandomWaitDuration();
                _radiusExpanded = false;
                _expandedRadius = 0f;
                PickNewWanderTarget();
            }
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
        }

        float dist = Vector3.Distance(_enemy.transform.position, _ambient.WanderTarget);

        // Arrived
        if (dist <= 1.5f)
        {
            _enemy.Movement.Stop();
            RecordVisitedPoint(_ambient.WanderTarget);
            _ambient.WanderWaiting = true;
            _ambient.WanderTimer = 0f;
            _radiusExpanded = false;
            _expandedRadius = 0f;
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
        }

        // Stuck detection — displacement over time window
        _stuckCheckTimer += InDeltaTime;
        if (_stuckCheckTimer >= StuckCheckInterval)
        {
            float displacement = Vector3.Distance(
                _enemy.transform.position, _stuckCheckPosition);

            if (displacement < StuckMinDisplacement)
            {
                // Stuck — expand radius and pick new point
                if (!_radiusExpanded)
                {
                    _expandedRadius = (_enemy.Data?.wanderRadius ?? 5f) * RadiusExpandFactor;
                    _radiusExpanded = true;
                }
                Debug.Log($"[Wander] {_enemy.name} stuck (moved {displacement:F2}) — rerouting");
                PickNewWanderTarget();
            }

            _stuckCheckPosition = _enemy.transform.position;
            _stuckCheckTimer = 0f;
        }

        // Re-apply movement every tick — pause/resume safe
        float wanderSpeed = (_enemy.Data?.moveSpeed ?? 3.5f) * 0.5f;
        _enemy.Movement.SetSpeed(wanderSpeed);
        _enemy.Movement.MoveTowards(_ambient.WanderTarget);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();
        if (_enemy?.Data != null)
            _enemy.Movement.SetSpeed(_enemy.Data.moveSpeed);
        // Do NOT reset _ambient — state persists across GOAP replans
    }

    // ── Smart point picking ────────────────────────────────
    private void PickNewWanderTarget()
    {
        if (_enemy == null || _ambient == null) return;

        float radius = _radiusExpanded
            ? _expandedRadius
            : (_enemy.Data?.wanderRadius ?? 5f);

        Vector3 origin = _enemy.transform.position;
        Vector3 bestPoint = Vector3.zero;
        float bestScore = -1f;

        for (int i = 0; i < MaxSampleAttempts; i++)
        {
            // Sample random point within radius
            Vector2 rand = Random.insideUnitCircle * radius;
            Vector3 candidate = origin + new Vector3(rand.x, 0f, rand.y);

            // Must be on NavMesh
            if (!NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas))
                continue;

            Vector3 point = hit.position;

            // Must have valid path from current position
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, point, NavMesh.AllAreas, path))
                continue;
            if (path.status != NavMeshPathStatus.PathComplete)
                continue;

            // Score this candidate
            float score = ScoreCandidate(point, origin);
            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = point;
            }
        }

        if (bestScore > -1f)
        {
            _ambient.WanderTarget = bestPoint;
            //Debug.Log($"[Wander] {_enemy.name} → {bestPoint} (score={bestScore:F2} radius={radius:F1})");
        }
        else
        {
            // All candidates failed — stay near origin
            _ambient.WanderTarget = origin;
            Debug.Log($"[Wander] {_enemy.name} no valid point found — staying near origin");
        }
    }

    private float ScoreCandidate(Vector3 point, Vector3 origin)
    {
        float score = 0f;

        // Prefer points farther from current position (explore not revisit)
        float dist = Vector3.Distance(origin, point);
        float radius = _enemy.Data?.wanderRadius ?? 5f;
        score += Mathf.Clamp01(dist / radius) * 0.5f;

        // Penalise recently visited points
        if (_recentInit)
        {
            float minRecentDist = float.MaxValue;
            foreach (var p in _recentPoints)
                if (p != Vector3.zero)
                    minRecentDist = Mathf.Min(minRecentDist,
                        Vector3.Distance(point, p));

            // Recent penalty — closer to recent = lower score
            float recentPenalty = 1f - Mathf.Clamp01(minRecentDist / (radius * 0.5f));
            score -= recentPenalty * 0.4f;
        }

        return score;
    }

    private void RecordVisitedPoint(Vector3 point)
    {
        _recentPoints[_recentIdx % _recentPoints.Length] = point;
        _recentIdx++;
        _recentInit = true;
    }

    private float RandomWaitDuration()
        => Random.Range(_enemy?.Data?.wanderTimerMin ?? 2f,
                        _enemy?.Data?.wanderTimerMax ?? 4f);
}