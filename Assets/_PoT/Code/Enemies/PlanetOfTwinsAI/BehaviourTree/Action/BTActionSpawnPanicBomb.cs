using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Spawn panic bomb when twin gets too close.
/// Reads panicRange and windUpDuration from SiphonEnemyData.
/// Has internal cooldown — won't fire again until cooldown expires.
/// Returns Succeeded after bomb spawns so BT continues to flee.
/// Returns Failed if not a siphon, no target, or on cooldown.
/// </summary>
public class BTActionSpawnPanicBomb : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Panic Bomb";

    private SiphonEnemy _siphon;
    private float _panicRange;
    private float _windUpDuration;
    private float _lastBombTime = -99f;
    private float _bombCooldown = 5f;
    private bool _windingUp;
    private float _windUpElapsed;

    protected override void OnEnter()
    {
        base.OnEnter();
        _siphon = _enemy as SiphonEnemy;

        var siphonData = _enemy?.Data as SiphonEnemyData;
        _panicRange = siphonData?.panicRange ?? 2.5f;
        _windUpDuration = siphonData?.panicBombWindUpDuration ?? 0.25f;
        _bombCooldown = _windUpDuration + (siphonData?.panicBombTravelDuration ?? 1f) + 2f;
        _windingUp = false;
        _windUpElapsed = 0f;
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_siphon == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        var target = GetBestTarget();
        if (target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // On cooldown
        if (Time.time < _lastBombTime + _bombCooldown)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        float dist = Vector3.Distance(_enemy.transform.position, target.transform.position);

        // Twin not close enough — not panicking
        if (dist > _panicRange)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Wind up
        if (!_windingUp)
        {
            _windingUp = true;
            _windUpElapsed = 0f;
            _enemy.Movement.Stop();
            FaceTarget(target);
        }

        _windUpElapsed += InDeltaTime;

        if (_windUpElapsed >= _windUpDuration)
        {
            _enemy.SetTarget(target.transform);
            _siphon.SpawnPanicBomb();
            _lastBombTime = Time.time;
            _windingUp = false;
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _windingUp = false;
    }

    private void FaceTarget(Player target)
    {
        if (target == null || _enemy == null) return;
        Vector3 dir = (target.transform.position - _enemy.transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero)
            _enemy.transform.rotation = Quaternion.LookRotation(dir);
    }
}