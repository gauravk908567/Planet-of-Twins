using UnityEngine;

public abstract class AbilityBase : IAbility
{
    protected GameObject owner;
    protected AbilityData data;

    private float lastUseTime;
    protected float activeTimer;
    protected bool isActive;

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
        if (Time.time < lastUseTime + data.cooldown)
            return;

        if (isActive)
            return;

        bool success = Activate();

        if (!success)
            return;

        isActive = true;
        activeTimer = 0f; 
    }

    // Now returns bool
    protected abstract bool Activate();

    protected virtual void End()
    {
        isActive = false;
        lastUseTime = Time.time;
    }

    public virtual void Tick()
    {
        if (!isActive)
            return;

        activeTimer += Time.deltaTime;

        if (activeTimer >= data.duration)
        {
            End();
        }
    }

    public float GetRange()
    {
        return data.range;
    }
}