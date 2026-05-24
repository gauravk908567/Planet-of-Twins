using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Attack nearest enemy of opposite clan during clan war.
/// Used by GOAPActionPossessed (possession) and GOAPActionAttackEnemy (clan war).
///
/// POSSESSION context: isPossessed=true, no clan filter — targets any non-possessed enemy.
/// CLAN WAR context:   isPossessed=false, targetEnemyLayer=true — filters to opposite clan only.
///   Opposite clan determined via ClanSoldier component. Enemies without ClanSoldier
///   are not clan soldiers and are skipped entirely.
///
/// Prioritises enemies actively targeting a twin — most impactful disruption.
/// Falls back to nearest opposite-clan enemy if none are targeting twins.
/// Rescans every 0.5s — targets may move or die.
///
/// WAIT-IT-OUT PREVENTION:
/// If a valid target exists but no attack lands for StaleThreshold seconds,
/// the enemy is flagged to keep moving regardless of hesitation — prevents
/// two enemies circling each other indefinitely.
/// </summary>
public class BTActionAttackEnemy : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Attack Enemy";

    private float _attackRange;
    private Enemy _target;
    private float _scanTimer;
    private const float ScanInterval = 0.5f;
    private const float DetectionMult = 6f;

    // ── Wait-it-out prevention ────────────────────────────────
    private float _staleTimer;
    private const float StaleThreshold = 3f;
    private bool _forceApproach;

    // ── Context — set by GOAP action caller ──────────────────
    // Default = clan war mode (no possession).
    // GOAPActionPossessed calls SetPossessionContext(true).
    private bool _isPossessionContext;
    public void SetPossessionContext(bool isPossession) => _isPossessionContext = isPossession;

    protected override void OnEnter()
    {
        base.OnEnter();
        _attackRange = _enemy?.Data?.attackRange ?? 2f;
        _scanTimer = ScanInterval;
        _staleTimer = 0f;
        _forceApproach = false;
        _target = null;
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        _scanTimer += InDeltaTime;
        if (_scanTimer >= ScanInterval || _target == null || _target.Health.IsDead)
        {
            _target = FindBestTarget();
            _scanTimer = 0f;
            _forceApproach = false;
            _staleTimer = 0f;
        }

        if (_target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        _enemy.SetTarget(_target.transform);

        float dist = Vector3.Distance(_enemy.transform.position, _target.transform.position);

        if (dist <= _attackRange)
        {
            FaceTarget();
            // Clan war:  isPossessed=false, targetEnemyLayer=true  → 0.3x reduction applies
            // Possession: isPossessed=true, targetEnemyLayer=false → reduction bypassed
            _enemy.AttackController.TryAttack(
                isPossessed: _isPossessionContext,
                targetEnemyLayer: !_isPossessionContext);

            _staleTimer = 0f;
            _forceApproach = false;
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
        }

        // ── Wait-it-out prevention ────────────────────────────
        _staleTimer += InDeltaTime;
        if (_staleTimer >= StaleThreshold)
            _forceApproach = true;

        // MoveFast doesn't exist — MoveTowards with forceApproach flag
        // ensures the enemy stays committed rather than drifting.
        // If EnemyMovement gains a speed-boost method later, swap it here.
        _enemy.Movement.MoveTowards(_target.transform.position);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();
        _enemy?.ClearTarget();
        _target = null;
        _staleTimer = 0f;
        _forceApproach = false;
    }

    private Enemy FindBestTarget()
    {
        if (_enemy == null) return null;

        float detectionRange = _attackRange * DetectionMult;
        Enemy best = null;
        float bestScore = float.MinValue;

        // Clan war mode — cache own clan for opposite-clan filter
        ClanSoldier mySoldier = _isPossessionContext
            ? null
            : _enemy.GetComponent<ClanSoldier>();

        foreach (var candidate in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
        {
            if (candidate == _enemy) continue;
            if (candidate.Health.IsDead) continue;
            if (candidate.IsPossessed) continue;

            // ── Clan war filter ───────────────────────────────
            if (!_isPossessionContext)
            {
                // Skip if own clan is unknown — can't determine opposite
                if (mySoldier == null) continue;

                var candidateSoldier = candidate.GetComponent<ClanSoldier>();

                // Skip non-soldiers (no ClanSoldier component)
                if (candidateSoldier == null) continue;

                // Skip same clan — only attack opposite
                if (candidateSoldier.Clan == mySoldier.Clan) continue;
            }

            float dist = Vector3.Distance(_enemy.transform.position, candidate.transform.position);
            if (dist > detectionRange) continue;

            // Base score — closer is better
            float score = 1f - (dist / detectionRange);

            // Bonus — candidate is targeting a twin (most impactful to disrupt)
            if (candidate.Target != null)
            {
                var p = candidate.Target.GetComponent<Player>();
                if (p != null && !(p is SoulPlayer)) score += 1f;
            }

            if (score > bestScore) { bestScore = score; best = candidate; }
        }

        return best;
    }

    private void FaceTarget()
    {
        if (_target == null || _enemy == null) return;
        Vector3 dir = _target.transform.position - _enemy.transform.position;
        dir.y = 0f;
        if (dir != Vector3.zero)
            _enemy.transform.rotation = Quaternion.LookRotation(dir);
    }
}