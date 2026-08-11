using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Throw chain at target.
/// Sets target on enemy, calls TryChainAttack(), holds InProgress while chain resolves.
/// Returns Succeeded when chain sequence completes (hit or miss).
/// Returns Failed if no target or enemy busy.
/// </summary>
public class BTActionChainAttack : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Chain Attack";

    private TetherBreakerEnemy _tbEnemy;
    private float _chainRange;

    protected override void OnEnter()
    {
        base.OnEnter();
        _tbEnemy = _enemy as TetherBreakerEnemy;

        var tbData = _enemy?.Data as TetherBreakerEnemyData;  // ADD
        _chainRange = tbData?.chainAttackRange ?? 8f;          // ADD

        var target = GetBestTarget();
        if (target != null) _enemy.SetTarget(target.transform);
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_tbEnemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        var target = GetBestTarget();
        if (target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Update target continuously
        _enemy.SetTarget(target.transform);

        // Chain is active � hold InProgress
        if (_tbEnemy.IsThrowing || _tbEnemy.IsSprinting || _tbEnemy.ChainActive)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);

        // Reel done but throw still on cooldown (recovery window) � fail so the Selector falls to
        // ChaseTarget and he repositions instead of standing frozen until the cooldown clears.
        if (_tbEnemy.ChainOnCooldown)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        float dist = DistanceToTarget();
        if (dist > _chainRange)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Not yet started � try to throw
        _tbEnemy.TryChainAttack();
        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
    }
}