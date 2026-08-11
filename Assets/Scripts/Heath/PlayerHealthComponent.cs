using UnityEngine;
using System;

public class PlayerHealthComponent : MonoBehaviour,
        IDamageable, IDistanceAffected, IHealthTracker
{
    [SerializeField] private float maxCombatHealth = 100f;

    private float _currentCombatHealth;
    private float _distanceModifier = 1f;
    private float _overMaxDistanceDrain = 0f;
    private bool _isDead;
    private bool _deathFired;

    private readonly HealthRegenHandler _regenHandler = new HealthRegenHandler();

    [SerializeField] private SharedHealthPool _sharedPool;

    public float CombatHealth => _currentCombatHealth;
    public float MaxHealth => maxCombatHealth;
    public float CurrentModifier => _distanceModifier;
    public bool IsDead => _isDead;

    private bool _invincible;
    private bool _isRegenActive = false;  // tracks regen state to fire events only on change
    public void SetInvincible(bool value) => _invincible = value;

    // ── Regen diagnostics (TEMPORARY — health-regen investigation, 2026-08-10) ──────────
    // Logs this twin's REAL current HP as it regenerates, next to the distance-masked
    // DisplayHealth, so we can tell whether real regen is running while the HUD bar reads flat
    // (the masked-display / BUG-091 rollback hypothesis). Untick to silence; remove when closed.
    [SerializeField] private bool _debugRegen = true;
    private float _regenLogTimer;       // throttle for the healing-tick log
    private float _regenBlockLogTimer;  // throttle for the "NOT regenerating — why" log

    public float DisplayHealth
    {
        get
        {
            float compressed = _currentCombatHealth * _distanceModifier;
            float drained = _overMaxDistanceDrain * maxCombatHealth;
            return Mathf.Max(0f, compressed - drained);
        }
    }

    // ── Orthogonal split of DisplayHealth (world/HUD UI revamp) ────────────────
    // DisplayHealth above multiplies real health by the distance modifier, so walking apart
    // makes the bar FALL even though _currentCombatHealth never changed. Playtesters read that
    // as "I am dying" when they are merely stretched, which is the reported shared-health
    // confusion. The author's own variable name — "compressed" — says it is a suppression, not
    // damage.
    //
    // These two properties separate the signals so each drives ONE display channel:
    //   SurvivalHealth01 — how close to death you actually are. Real pool, minus the over-max
    //                      drain (which genuinely accumulates toward death and genuinely kills).
    //                      Drives bar FILL.
    //   BondWeakness01   — how weak the bond is right now. A pure instantaneous function of
    //                      distance, fully recoverable by walking back together, and it cannot
    //                      kill you on its own. Drives bar COLOUR (drain toward grey).
    //
    // DisplayHealth is deliberately left untouched so existing consumers keep their behaviour;
    // migrating them is a separate isolated change, not part of the UI revamp.
    public float SurvivalHealth01
    {
        get
        {
            if (maxCombatHealth <= 0f) return 0f;
            float real = _currentCombatHealth - _overMaxDistanceDrain * maxCombatHealth;
            return Mathf.Clamp01(real / maxCombatHealth);
        }
    }

    public float BondWeakness01 => Mathf.Clamp01(1f - _distanceModifier);

    public event Action<float> OnDisplayHealthChanged;

    /// <summary>Fires with BondWeakness01 whenever the distance modifier moves.</summary>
    public event Action<float> OnBondWeaknessChanged;
    public event Action OnDeath;
    public event Action<PlayerHealthComponent, float, Vector3> OnDamageTaken; // component, amount, hitPoint
    public event Action OnRegenStarted;   // fires once when regen begins
    public event Action OnRegenStopped;   // fires once when regen stops or is interrupted

    private void Awake()
    {
        _currentCombatHealth = maxCombatHealth;
    }

    private void Update()
    {
        if (_isDead) return;

        float regenAmount = _regenHandler.GetRegenThisFrame(
            _currentCombatHealth, maxCombatHealth, Time.deltaTime);

        bool regenActive = regenAmount > 0f;

        // Fire events only when state changes — not every frame
        if (regenActive && !_isRegenActive)
        {
            _isRegenActive = true;
            OnRegenStarted?.Invoke();
            if (_debugRegen)
            {
                _regenLogTimer = 0f;
                Debug.Log($"[HealthRegen] '{name}' regen STARTED — HP={_currentCombatHealth:F1}/{maxCombatHealth:F0} " +
                          $"(displayHP={DisplayHealth:F1}, distMod={_distanceModifier:F2})", this);
            }
        }
        else if (!regenActive && _isRegenActive)
        {
            _isRegenActive = false;
            OnRegenStopped?.Invoke();
            if (_debugRegen)
                Debug.Log($"[HealthRegen] '{name}' regen STOPPED — HP={_currentCombatHealth:F1}/{maxCombatHealth:F0} " +
                          $"(displayHP={DisplayHealth:F1}, distMod={_distanceModifier:F2})", this);
        }

        if (regenAmount > 0f)
        {
            _currentCombatHealth = Mathf.Clamp(
                _currentCombatHealth + regenAmount, 0f, maxCombatHealth);
            BroadcastDisplayHealth();

            // Throttled climb tick — every 0.5s so the rise is visible without 60 logs/sec.
            // displayHP vs HP here is the whole point: if HP climbs while displayHP stays low,
            // the bar is masking real regen by distance (distMod), not a regen failure.
            if (_debugRegen)
            {
                _regenLogTimer += Time.deltaTime;
                if (_regenLogTimer >= 0.5f)
                {
                    _regenLogTimer = 0f;
                    Debug.Log($"[HealthRegen] '{name}' healing → HP={_currentCombatHealth:F1}/{maxCombatHealth:F0} " +
                              $"(+{regenAmount / Mathf.Max(Time.deltaTime, 0.0001f):F1}/s, " +
                              $"displayHP={DisplayHealth:F1}, distMod={_distanceModifier:F2})", this);
                }
            }
        }
        else if (_debugRegen && _currentCombatHealth < maxCombatHealth)
        {
            // Alive + below max but NOT healing this frame → surface WHY. This is the probe for
            // the "health didn't regen (no crash, not under fire)" case: instead of silence, it
            // names the exact gate stopping regen. Throttled to once a second so it never floods.
            _regenBlockLogTimer += Time.deltaTime;
            if (_regenBlockLogTimer >= 1f)
            {
                _regenBlockLogTimer = 0f;
                Debug.Log($"[HealthRegen] '{name}' NOT regenerating at HP={_currentCombatHealth:F1}/{maxCombatHealth:F0} — " +
                          $"{_regenHandler.GetBlockReason(_currentCombatHealth, maxCombatHealth)} " +
                          $"(displayHP={DisplayHealth:F1}, distMod={_distanceModifier:F2}, overMaxDrain={_overMaxDistanceDrain:F2})", this);
            }
        }
    }

    public void TakeDamage(DamageData damageData)
    {
        if (_invincible) return;
        if (_isDead) return;

        float multipliedAmount = damageData.Amount *
            (_sharedPool != null ? _sharedPool.IncomingDamageMultiplier : 1f);

        _currentCombatHealth = Mathf.Clamp(
            _currentCombatHealth - multipliedAmount, 0f, maxCombatHealth);

        if (damageData.Type == DamageType.Combat)
            _regenHandler.OnCombatDamageTaken();

        if (_debugRegen)
            Debug.Log($"[HealthRegen] '{name}' took {multipliedAmount:F1} {damageData.Type} dmg → " +
                      $"HP={_currentCombatHealth:F1}/{maxCombatHealth:F0}" +
                      (damageData.Type == DamageType.Combat
                          ? " — COMBAT: regen delay timer RESET"
                          : " — non-combat: regen timer unaffected"), this);

        OnDamageTaken?.Invoke(this, multipliedAmount, damageData.HitPoint);
        BroadcastDisplayHealth();

        if (_currentCombatHealth <= 0f && !_deathFired)
        {
            _deathFired = true;
            _isDead = true;
            if (_debugRegen)
                Debug.Log($"[HealthRegen] '{name}' DIED (HP=0) — regen HALTED until ResetToAlive()", this);
            OnDeath?.Invoke();
        }
    }

    public void SetDistanceModifier(float modifier)
    {
        if (_isDead) return;
        bool changed = !Mathf.Approximately(_distanceModifier, modifier);
        _distanceModifier = modifier;
        BroadcastDisplayHealth();
        if (changed) OnBondWeaknessChanged?.Invoke(BondWeakness01);
    }

    public void SetOverMaxDistanceCalculator(float drainFraction)
    {
        if (_isDead) return;
        _overMaxDistanceDrain = drainFraction;
        BroadcastDisplayHealth();
    }

    public void SetRegenUpgradeNode(int node) =>
        _regenHandler.SetUpgradeNode(node);

    public void SetDisplayHealthDirectly(float value)
    {
        _currentCombatHealth = Mathf.Clamp(value, 0, maxCombatHealth);
        BroadcastDisplayHealth();
    }

    public void ResetToFull()
    {
        _currentCombatHealth = maxCombatHealth;
        BroadcastDisplayHealth();
    }

    public void ResetToAlive()
    {
        _isDead = false;
        _deathFired = false;
        if (_debugRegen)
            Debug.Log($"[HealthRegen] '{name}' ResetToAlive — isDead cleared, regen RE-ENABLED " +
                      "(begins after the post-hit delay)", this);
    }

    public void Heal(float amount)
    {
        _currentCombatHealth = Mathf.Min(maxCombatHealth, _currentCombatHealth + amount);
        if (_debugRegen)
            Debug.Log($"[HealthRegen] '{name}' Heal(+{amount:F1}) → HP={_currentCombatHealth:F1}/{maxCombatHealth:F0}", this);
        BroadcastDisplayHealth();
    }

    /// <summary>
    /// Full respawn restore — called by CheckpointManager on respawn.
    /// Restores HP to max, clears death flags, and resets distance drain.
    /// Safe to call in any state.
    /// </summary>
    public void RestoreToFull()
    {
        _isDead = false;
        _deathFired = false;
        _isRegenActive = false;
        _currentCombatHealth = maxCombatHealth;
        _overMaxDistanceDrain = 0f;
        _distanceModifier = 1f;
        _regenHandler.Reset();
        if (_debugRegen)
            Debug.Log($"[HealthRegen] '{name}' RestoreToFull — HP=max, drain/mod reset, regen handler Reset() " +
                      "(no regen until first hit again)", this);
        BroadcastDisplayHealth();
    }

    private float _previousDisplayHealth = -1f;

    private void BroadcastDisplayHealth()
    {
        float current = DisplayHealth;
        if (Mathf.Abs(current - _previousDisplayHealth) > 0.001f)
        {
            OnDisplayHealthChanged?.Invoke(current);
            _previousDisplayHealth = current;
        }
    }
}