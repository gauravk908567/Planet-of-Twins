using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FrontAttackState — GroupGrabEnemy behaviour when at attack range.
///
/// ENCIRCLEMENT: enemies distribute themselves evenly around the player.
/// On Enter(), each enemy registers with a shared static slot coordinator.
/// Slots are assigned at equal angular intervals (360° / enemy count).
/// Each enemy strafes toward its assigned angle around the player.
///
/// With 2 enemies: 180° apart (opposite sides)
/// With 3 enemies: 120° apart (triangle)
/// With 4 enemies: 90° apart (square)
///
/// When one enemy reaches the rear arc it transitions to BehindTimerState.
/// Remaining enemies re-distribute their slots automatically.
///
/// Solo (no nearby ally): attack directly, never grab.
/// </summary>
public class FrontAttackState : IEnemyState
{
    private readonly GroupGrabEnemy _enemy;
    private readonly float _behindDotThreshold;

    private float _assignedAngle = 0f;
    private const float StrafeSpeed = 2.5f;
    private const float AngleTolerance = 15f; // degrees — close enough to assigned slot

    // ── Shared slot coordinator (static per scene) ─────────────────────
    private static readonly Dictionary<GroupGrabEnemy, FrontAttackState>
        _activeStates = new();

    public FrontAttackState(GroupGrabEnemy enemy, float behindDotThreshold = -0.3f)
    {
        _enemy = enemy;
        _behindDotThreshold = behindDotThreshold;
    }

    public void Enter()
    {
        // Register and redistribute all active slots
        _activeStates[_enemy] = this;
        RedistributeSlots();
    }

    public void Exit()
    {
        // Unregister and redistribute remaining enemies
        _activeStates.Remove(_enemy);
        RedistributeSlots();
    }

    public void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(_enemy.IdleState);
            return;
        }

        float distance = Vector3.Distance(
            _enemy.transform.position, _enemy.Target.position);

        // Lost target — chase again
        if (distance > _enemy.AttackRange * 1.5f)
        {
            _enemy.StateMachine.ChangeState(_enemy.ChaseState);
            return;
        }

        _enemy.AlertNearby(_enemy.Target, isGrab: false);

        // Solo — attack only, never grab
        if (!HasNearbyAlly())
        {
            _enemy.AttackController?.TryAttack();
            return;
        }

        // Behind player — start grab timer
        if (IsEnemyBehindPlayer())
        {
            _enemy.StateMachine.ChangeState(_enemy.BehindTimer);
            return;
        }

        // Strafe toward assigned slot angle
        StrafeTowardSlot(distance);
    }

    // ── Slot redistribution ────────────────────────────────────────────
    private static void RedistributeSlots()
    {
        // Clean up any null entries first (destroyed enemies)
        var toRemove = new List<GroupGrabEnemy>();
        foreach (var key in _activeStates.Keys)
            if (key == null) toRemove.Add(key);
        foreach (var key in toRemove)
            _activeStates.Remove(key);

        int count = _activeStates.Count;
        if (count == 0) return;

        float step = 360f / count;
        int index = 0;
        foreach (var state in _activeStates.Values)
        {
            state._assignedAngle = index * step;
            index++;
        }
    }

    // ── Strafe toward assigned angle around player ─────────────────────
    private void StrafeTowardSlot(float currentDistance)
    {
        if (_enemy.Target == null) return;

        // Current angle of this enemy around the player (XZ plane)
        Vector3 toEnemy = _enemy.transform.position - _enemy.Target.position;
        toEnemy.y = 0f;
        float currentAngle = Mathf.Atan2(toEnemy.x, toEnemy.z) * Mathf.Rad2Deg;

        // Angle difference — which direction to strafe
        float angleDiff = Mathf.DeltaAngle(currentAngle, _assignedAngle);

        if (Mathf.Abs(angleDiff) <= AngleTolerance)
        {
            // At assigned slot — hold position, face player
            _enemy.Movement.Stop();
            return;
        }

        // Strafe direction: positive angleDiff = go clockwise
        float strafeSign = angleDiff > 0f ? 1f : -1f;
        Vector3 toEnemyNorm = toEnemy.normalized;
        Vector3 strafeDir = Vector3.Cross(toEnemyNorm, Vector3.up) * strafeSign;

        // Maintain distance while strafing
        float distanceError = currentDistance - _enemy.AttackRange;
        Vector3 correctionDir = toEnemyNorm * Mathf.Clamp(distanceError, -1f, 1f);

        Vector3 moveDir = (strafeDir + correctionDir * 0.5f).normalized;
        _enemy.Movement.MoveTowards(
            _enemy.transform.position + moveDir * StrafeSpeed * Time.deltaTime);
    }

    // ── Helpers ────────────────────────────────────────────────────────
    private bool IsEnemyBehindPlayer()
    {
        Vector3 playerToEnemy = (_enemy.transform.position - _enemy.Target.position).normalized;
        float dot = Vector3.Dot(playerToEnemy, _enemy.Target.forward);
        return dot < _behindDotThreshold;
    }

    private bool HasNearbyAlly()
    {
        var buffer = new Collider[4];
        int count = Physics.OverlapSphereNonAlloc(
            _enemy.transform.position, _enemy.ChaseAlertRange, buffer,
            _enemy.EnemyLayer);

        for (int i = 0; i < count; i++)
        {
            var other = buffer[i].GetComponent<Enemy>();
            if (other != null && other != _enemy) return true;
        }
        return false;
    }
}