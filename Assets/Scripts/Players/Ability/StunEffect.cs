using UnityEngine;

/// <summary>
/// Stun effect — disables movement and pauses GOAP brain for duration.
/// Replaces old StateMachine.enabled = false pattern.
/// </summary>
public class StunEffect : StatusEffectBase
{
    private Enemy _enemy;

    public StunEffect(GameObject target, float duration) : base(target, duration)
    {
        _enemy = target.GetComponent<Enemy>();
    }

    public override void OnApply()
    {
        base.OnApply();
        if (_enemy == null) return;

        _enemy.ApplyStun(duration);
    }

    public override void OnRemove()
    {
        // ApplyStun handles its own cleanup via coroutine
        // Nothing to do here — stun coroutine resumes brain on expiry
    }
}