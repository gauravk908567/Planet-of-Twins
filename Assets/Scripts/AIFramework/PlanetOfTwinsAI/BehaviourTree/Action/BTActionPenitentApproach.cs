using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Penitent slow creeping approach.
/// Velocity curve — slows as it closes distance for threatening feel.
/// Applies mood speedMultiplier unless reflection or rage is active
/// (those states manage their own speed directly via coroutines).
/// Stops at crush range — Update() in PenitentEnemy triggers grab.
/// </summary>
public class BTActionPenitentApproach : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Penitent Approach";

    private PenitentEnemy _penitent;
    private float _crushRange;
    private float _baseSpeed;

    protected override void OnEnter()
    {
        base.OnEnter();
        _penitent = _enemy as PenitentEnemy;
        _crushRange = _penitent?.PenitentData?.crushRange ?? 1.5f;
        _baseSpeed = _enemy?.Data?.moveSpeed ?? 3.5f;
        _enemy?.Movement.SetSpeed(MoodSpeed());
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_penitent == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        if (_penitent.IsCrushing || _penitent.IsWindingUp || _penitent.IsInCooldown)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);

        var target = GetBestTarget();
        if (target == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        _enemy.SetTarget(target.transform);

        float dist = Vector3.Distance(_enemy.transform.position, target.transform.position);

        if (dist <= _crushRange)
        {
            _enemy.Movement.Stop();
            FaceTarget(target);
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        // Velocity curve — slow over last 3m before crush range
        // Only apply when not in reflection/rage (coroutines handle speed then)
        if (!_penitent.ReflectionActive && !_penitent.IsInRage)
        {
            float slowZone = _crushRange + 3f;
            float speedMult = dist <= slowZone
                ? Mathf.Lerp(0.3f, 1f, (dist - _crushRange) / 3f)
                : 1f;

            // Combine velocity curve with mood multiplier
            _enemy.Movement.SetSpeed(_baseSpeed * speedMult * MoodSpeedMult());
        }

        _enemy.Movement.MoveTowards(target.transform.position);
        FaceTarget(target);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        if (_enemy != null && !_penitent.IsCrushing)
            _enemy.Movement.Stop();
    }

    private float MoodSpeed()
    {
        return _baseSpeed * MoodSpeedMult();
    }

    private float MoodSpeedMult()
    {
        var ms = _enemy?.GetComponent<EnemyMoodSystem>();
        return ms != null ? ms.CurrentModifiers.speedMultiplier : 1f;
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