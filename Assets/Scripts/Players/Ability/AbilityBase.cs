using UnityEngine;

public abstract class AbilityBase : IAbility
{
    protected GameObject owner;
    protected AbilityData data;

    private float _lastUseTime;
    protected float activeTimer;
    protected bool isActive;

    // Subclasses override this to return their cached upgraded cooldown.
    // TryActivate and ReduceCooldownByFraction both use this — never data.cooldown directly.
    protected virtual float EffectiveCooldown => data.cooldown;

    public AbilityBase(AbilityData data)
    {
        this.data = data;
    }

    public virtual void Initialize(GameObject owner)
    {
        this.owner = owner;
    }

    public void TryActivate()
    {
        if (Time.time < _lastUseTime + EffectiveCooldown)
            return;

        if (isActive)
            return;

        bool success = Activate();
        if (!success)
            return;

        isActive = true;
        activeTimer = 0f;
    }

    protected abstract bool Activate();

    protected virtual void End()
    {
        isActive = false;
        _lastUseTime = Time.time;
    }

    public virtual void Tick()
    {
        if (!isActive)
            return;

        activeTimer += Time.deltaTime;

        if (activeTimer >= data.duration)
            End();
    }

    public float GetRange()
    {
        return data.range;
    }

    // Called by DualCastSystem on sync.
    // Pulls _lastUseTime back so cooldown expires sooner.
    // Uses EffectiveCooldown so upgraded cooldown values are respected.
    protected void ReduceCooldownByFraction(float fraction)
    {
        _lastUseTime -= EffectiveCooldown * fraction;
    }
}