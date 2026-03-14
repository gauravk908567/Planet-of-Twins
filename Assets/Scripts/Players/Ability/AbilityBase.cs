using UnityEngine;

public abstract class AbilityBase : IAbility, IAbilityHUDSource
{
    protected GameObject owner;
    protected AbilityData data;

    private float _lastUseTime;
    protected float activeTimer;
    protected bool isActive;

    protected virtual float EffectiveCooldown => data.cooldown;

    // ── IAbilityHUDSource ─────────────────────────────────────
    // AbilityName: uses the AbilityData asset name by default.
    // Subclasses (TeleportAbility, StunAbility etc.) can override.
    public virtual string AbilityName => data?.name ?? "Ability";
    public virtual int CurrentCharges => 1;
    public virtual int MaxCharges => 1;
    public bool IsActive => isActive;

    /// <summary>
    /// 0 = fully on cooldown, 1 = ready to use.
    /// HUD ring fills from empty (0) to full (1) as cooldown expires.
    /// </summary>
    public float CooldownProgress
    {
        get
        {
            float cd = EffectiveCooldown;
            if (cd <= 0f) return 1f;
            float elapsed = Time.time - _lastUseTime;
            return Mathf.Clamp01(elapsed / cd);
        }
    }

    // ── Constructor ───────────────────────────────────────────
    public AbilityBase(AbilityData data)
    {
        this.data = data;
    }

    public virtual void Initialize(GameObject owner)
    {
        this.owner = owner;
    }

    // ── Core logic (unchanged) ─────────────────────────────────
    public void TryActivate()
    {
        if (Time.time < _lastUseTime + EffectiveCooldown) return;
        if (isActive) return;

        bool success = Activate();
        if (!success) return;

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
        if (!isActive) return;

        activeTimer += Time.deltaTime;

        if (activeTimer >= data.duration)
            End();
    }

    public float GetRange() => data.range;

    protected void ReduceCooldownByFraction(float fraction)
    {
        _lastUseTime -= EffectiveCooldown * fraction;
    }

    // Exposed so TeleportAbility can call GetCooldownProgress from its own override
    protected float GetCooldownProgress() => CooldownProgress;
}