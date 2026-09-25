using UnityEngine;

public abstract class StatusEffectBase : IStatusEffect
{
    protected float duration;
    protected float timer;
    protected GameObject target;

    public bool IsFinished => timer >= duration;

    public StatusEffectBase(GameObject target, float duration)
    {
        this.target = target;
        this.duration = duration;
    }

    public virtual void OnApply()
    {
        timer = 0f;
    }

    public virtual void OnUpdate()
    {
        timer += Time.deltaTime;
    }

    public virtual void OnRemove()
    {
    }
}