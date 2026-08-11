using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Shadow the summoned ally in formation.
///
/// Positioning: Witness stays _shadowDist behind the ally
/// relative to the nearest twin — formation is W → Ally → Twin.
/// Witness actively repositions at 1.5x speed to maintain this line
/// as the twin moves.
///
/// Bomb triggering is handled by GOAPGoalThrowBomb (Critical priority)
/// preempting this goal via the GOAP planner — no bomb check needed here.
///
/// Returns InProgress while shadowing.
/// Returns Failed if ally is dead or null.
/// </summary>
public class BTActionShadowAlly : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Shadow Ally";

    private WitnessEnemy _witness;
    private float _shadowDist;
    private float _baseSpeed;
    private float _repositionSpeed;

    protected override void OnEnter()
    {
        base.OnEnter();
        _witness = _enemy as WitnessEnemy;

        var wd = _enemy?.Data as WitnessEnemyData;
        _shadowDist = wd?.shadowDistance ?? 2.5f;
        _baseSpeed = _enemy?.Data?.moveSpeed ?? 3.5f;
        _repositionSpeed = _baseSpeed * 1.5f;

        _enemy?.Movement.SetSpeed(_baseSpeed);
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_witness == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        var ally = _witness.FollowTarget;
        if (ally == null || ally.Health.IsDead)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        Vector3 shadowTarget = CalculateShadowPosition(ally);

        float dist = Vector3.Distance(_enemy.transform.position, shadowTarget);

        if (dist > 0.4f)
        {
            // Reposition faster the further out of formation we are
            float speed = dist > _shadowDist ? _repositionSpeed : _baseSpeed;
            _enemy.Movement.SetSpeed(speed);
            _enemy.Movement.MoveTowards(shadowTarget);
        }
        else
        {
            _enemy.Movement.Stop();
        }

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();
        _enemy?.Movement.SetSpeed(_baseSpeed);
    }

    /// <summary>
    /// Returns the ideal shadow position: _shadowDist behind the ally
    /// on the axis pointing away from the nearest twin.
    /// If no twin is found, falls back to directly behind ally
    /// based on ally's current facing direction.
    /// </summary>
    private Vector3 CalculateShadowPosition(Enemy ally)
    {
        Vector3 allyPos = ally.transform.position;

        var twin = _witness.GetNearestTwin();
        Vector3 awayFromTwin;

        if (twin != null)
        {
            // Direction from twin toward ally — Witness goes further along this line
            awayFromTwin = (allyPos - twin.transform.position).normalized;
        }
        else
        {
            // Fallback: use ally's back direction
            awayFromTwin = -ally.transform.forward;
        }

        awayFromTwin.y = 0f;

        if (awayFromTwin == Vector3.zero)
            awayFromTwin = -ally.transform.forward;

        return allyPos + awayFromTwin * _shadowDist;
    }
}