using BehaviourTree;
using CommonCore;
using UnityEngine;

/// <summary>
/// Base class for all Planet of Twins BT action nodes.
///
/// Provides direct access to:
///   - The Enemy component (movement, health, attack, VFX)
///   - The Blackboard (perception results, game state)
///   - Common PoT state checks as properties
///
/// USAGE:
///   Extend this for every custom PoT action node.
///   Call base.OnEnter() first in your override to cache Enemy.
///
/// MOVEMENT:
///   Call _enemy.Movement.MoveTowards() directly.
///   Do NOT use NavigationInterface — Option B for enemy combat.
///   NavigationInterface is reserved for environmental BTs.
/// </summary>
public abstract class PoTBTActionBase : BTActionNodeBase
{
    // ── Cached references ──────────────────────────────────────
    protected Enemy _enemy;
    protected Blackboard<FastName> Board => LinkedBlackboard;

    protected override void OnEnter()
    {
        base.OnEnter();

        if (_enemy == null)
        {
            var self = LinkedBlackboard.GetGameObject(CommonCore.Names.Self);
            if (self != null)
                _enemy = self.GetComponent<Enemy>();

            if (_enemy == null)
                Debug.LogError($"[PoT_BTActionBase] {GetType().Name} could not find Enemy component on Self.");
        }
    }

    // ── Perception helpers ─────────────────────────────────────

    /// <summary>Best detected twin from PerceptionListener.</summary>
    protected Player GetBestTarget()
    {
        GameObject go = null;
        if (Board.TryGet(CommonCore.Names.Awareness_BestTarget, out go, null))
            return go?.GetComponent<Player>();
        return null;
    }

    /// <summary>Position of best detected twin. InvalidVector3Position if none.</summary>
    protected Vector3 GetBestTargetPosition()
    {
        var target = GetBestTarget();
        return target != null ? target.transform.position : CommonCore.Constants.InvalidVector3Position;
    }

    protected bool HasTarget => GetBestTarget() != null;

    // ── Game state helpers ─────────────────────────────────────

    protected bool IsRescueActive
    {
        get
        {
            bool val = false;
            Board.TryGet(PoTNames.IsRescueActive, out val, false);
            return val;
        }
    }

    protected bool IsAccordActive
    {
        get
        {
            bool val = false;
            Board.TryGet(PoTNames.AccordStateActive, out val, false);
            return val;
        }
    }

    protected bool IsSoulActive
    {
        get
        {
            bool val = false;
            Board.TryGet(PoTNames.SoulIsActive, out val, false);
            return val;
        }
    }

    protected float SharedHealthNorm
    {
        get
        {
            float val = 1f;
            Board.TryGet(PoTNames.SharedHealthNorm, out val, 1f);
            return val;
        }
    }

    protected bool SpawnUnderAttack
    {
        get
        {
            bool val = false;
            Board.TryGet(PoTNames.SpawnUnderAttack, out val, false);
            return val;
        }
    }

    protected bool DarkEnergyAvailable
    {
        get
        {
            bool val = false;
            Board.TryGet(PoTNames.DarkEnergyAvailable, out val, false);
            return val;
        }
    }

    // ── Enemy state helpers ────────────────────────────────────

    protected float EnemyHealthNorm
    {
        get
        {
            float val = 1f;
            Board.TryGet(PoTNames.EnemyHealthNorm, out val, 1f);
            return val;
        }
    }

    protected bool EnemyIsPossessed
    {
        get
        {
            bool val = false;
            Board.TryGet(PoTNames.EnemyIsPossessed, out val, false);
            return val;
        }
    }

    // ── Distance helpers ───────────────────────────────────────

    protected float DistanceToTarget()
    {
        var target = GetBestTarget();
        if (target == null || _enemy == null) return float.MaxValue;
        return Vector3.Distance(_enemy.transform.position, target.transform.position);
    }

    protected bool IsInRange(float range) => DistanceToTarget() <= range;

    // ── Movement helpers ───────────────────────────────────────

    /// <summary>
    /// Move toward a world position using EnemyMovement directly.
    /// Returns true when within stoppingDistance.
    /// </summary>
    protected bool MoveToward(Vector3 destination, float stoppingDistance = 0.5f)
    {
        if (_enemy == null) return false;

        float dist = Vector3.Distance(_enemy.transform.position, destination);
        if (dist <= stoppingDistance)
        {
            _enemy.Movement.Stop();
            return true;
        }

        _enemy.Movement.MoveTowards(destination);
        return false;
    }

    /// <summary>
    /// Move away from a position (flee / kite).
    /// Returns true when beyond safeDistance.
    /// </summary>
    protected bool FleeFrom(Vector3 origin, float safeDistance)
    {
        if (_enemy == null) return false;

        float dist = Vector3.Distance(_enemy.transform.position, origin);
        if (dist >= safeDistance)
        {
            _enemy.Movement.Stop();
            return true;
        }

        Vector3 fleeDir = (_enemy.transform.position - origin).normalized;
        _enemy.Movement.MoveTowards(_enemy.transform.position + fleeDir * 5f);
        return false;
    }
}