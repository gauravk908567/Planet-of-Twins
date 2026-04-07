using System.Collections;
using UnityEngine;

/// <summary>
/// Penitent — slow approach enemy with escalating crush phases.
///
/// GRAB CYCLE:
///   Chase → within crushRange → wind-up (1.25s, player can dodge)
///   → grab → tick damage for crushDuration (2s)
///   → player mashes E to self-rescue (fills bar, breaks free early)
///   → if not freed: auto-release → postGrabCooldown (1.25s) → repeat
///
/// REFLECTION THRESHOLD HIT DURING GRAB:
///   Instantly drops player → rage phase (increased tick DPS, speed boost)
///   → rage ends → postGrabCooldown → repeat
///
/// RESCUE:
///   Only triggers if tick damage brings player health to 0
///   → PlayerDeathRescueProxy activates → soul travel → F mash rescue
///
/// REFLECTION:
///   At HP thresholds: 60% of incoming damage reflects to hitting twin (capped, never kills)
///   During possession: reflected damage hits nearby enemies instead
/// </summary>
public class PenitentEnemy : Enemy, IRescueTarget
{
    [Header("Penitent")]
    [SerializeField] private PenitentEnemyData _penitentData;

    // ── Colour constants (temp — replace with VFX) ─────────────
    private static readonly Color ReflectionColor = new Color(1f, 0.1f, 0.1f);
    private static readonly Color CrushColor = new Color(0.6f, 0f, 0.8f);
    private static readonly Color RageColor = new Color(1f, 0.4f, 0f);

    // ── Reflection ─────────────────────────────────────────────
    private bool _reflectionActive;
    private bool[] _thresholdTriggered;

    // ── Grab state ─────────────────────────────────────────────
    private bool _windingUp;
    private bool _crushing;
    private bool _inCooldown;
    private bool _inRage;
    private Player _crushTarget;
    private bool _crushResolved;
    private int _crushMashCount;
    private bool _ttkPaused;
    private bool _isFrozen;

    // ── IRescueTarget ──────────────────────────────────────────
    public Transform GrabbedPlayerTransform => _crushTarget?.transform;
    public Player GrabbedPlayer => _crushTarget;
    public float NormalisedTTK => 1f; // not used — Penitent has no TTK ring
    public float MashFrequency => 4f;
    public float MashWindowDuration => _penitentData?.crushDuration ?? 2f;
    public float MashCooldown => 0.75f;
    public float PartialHealAmount => 0f; // no heal on self-rescue
    public bool CanGrabbedPlayerStruggle => true;
    public float StrugglePauseDuration => 0.4f; // seconds TTK paused per E mash
    public float RescueProximityRadius => 1.5f;

    public event System.Action<Player> OnPlayerGrabbed;
    public event System.Action OnPlayerReleased;
    public event System.Action OnPlayerKilled;

    // ── Lifecycle ──────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        Health.OnDamageTaken += HandleDamageTaken;
    }

    private void OnDestroy()
    {
        if (Health != null) Health.OnDamageTaken -= HandleDamageTaken;
    }

    public override void OnEffectStarted() { base.OnEffectStarted(); _isFrozen = true; }
    public override void OnEffectEnded() { base.OnEffectEnded(); _isFrozen = false; }

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);
        if (data is PenitentEnemyData pd)
        {
            _penitentData = pd;
            _thresholdTriggered = new bool[pd.reflectionThresholds.Length];
        }
    }

    protected override void InitStates()
    {
        base.InitStates();
        // Standard states — grab triggered by proximity coroutine in Update
    }

    // ── Update — proximity check only ─────────────────────────
    private void Update()
    {
        // Proximity check — start wind-up when close enough
        // Skip if target already grabbed by another enemy
        if (_isFrozen) return;
        if (!_windingUp && !_crushing && !_inCooldown && Target != null)
        {
            var targetPlayer = Target.GetComponent<Player>();
            if (targetPlayer != null && !targetPlayer.IsGrabbed)
            {
                float dist = Vector3.Distance(transform.position, Target.position);
                if (dist <= (_penitentData?.crushRange ?? 1.5f))
                    StartCoroutine(GrabCycle());
            }
        }
    }

    // ── Crush tick damage coroutine ────────────────────────────
    private IEnumerator CrushTickRoutine()
    {
        while (_crushing)
        {
            while (_isFrozen || _ttkPaused) { yield return null; continue; }

            float interval = _inRage
                ? (_penitentData?.rageTickInterval ?? 0.15f)
                : (_penitentData?.crushTickInterval ?? 0.25f);

            // Wait for tick interval FIRST — first tick fires after delay, not at t=0
            yield return new WaitForSeconds(interval);

            if (!_crushing || _crushTarget == null) yield break;

            float dps = _inRage
                ? (_penitentData?.rageCrushDps ?? 25f)
                : (_penitentData?.crushDps ?? 15f);

            // Damage per tick = dps * interval
            float tickDamage = dps * interval;

            if (_crushTarget.Health != null)
            {
                float safe = Mathf.Min(tickDamage, _crushTarget.Health.CombatHealth - 1f);
                if (safe > 0f)
                    _crushTarget.Health.TakeDamage(
                        new DamageData(safe, DamageType.Environmental));

                // If health at 1 — trigger kill through proxy
                if (_crushTarget.Health.CombatHealth <= 1f)
                {
                    KillPlayer();
                    yield break;
                }
            }
        }
    }

    // ── Grab cycle ─────────────────────────────────────────────
    private IEnumerator GrabCycle()
    {
        _windingUp = true;

        // Wind-up — stop, face player, give dodge window
        StateMachine.Pause();
        Movement.Stop();

        if (Target != null)
        {
            Vector3 dir = (Target.position - transform.position);
            dir.y = 0f;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        while (_isFrozen) yield return null;
        yield return new WaitForSeconds(_penitentData?.grabWindUpDuration ?? 1.25f);
        _windingUp = false;

        // Check still in range after wind-up
        if (Target == null || Health.IsDead)
        {
            StateMachine.Resume();
            yield break;
        }

        float dist = Vector3.Distance(transform.position, Target.position);
        if (dist > (_penitentData?.crushRange ?? 1.5f) * 1.5f)
        {
            // Player dodged — resume chase
            StateMachine.Resume();
            yield break;
        }

        // Attempt grab
        var player = Target.GetComponent<Player>();
        if (player == null || player.IsGrabbed)
        {
            StateMachine.Resume();
            yield break;
        }

        StartCrush(player);

        // Wait for crush to resolve (TTK drain in Update handles it)
        float crushDuration = _penitentData?.crushDuration ?? 2f;
        yield return new WaitForSeconds(crushDuration);

        // Auto-release if still crushing
        if (_crushing)
            ReleasePlayer(0f);

        // Post-grab cooldown
        _inCooldown = true;
        yield return new WaitForSeconds(_penitentData?.postGrabCooldown ?? 1.25f);
        _inCooldown = false;

        StateMachine.Resume();
    }

    // ── Crush ──────────────────────────────────────────────────
    public void StartCrush(Player target)
    {
        if (_crushing || target == null || target.IsGrabbed) return;

        _crushTarget = target;
        _crushing = true;
        _crushResolved = false;
        _crushMashCount = 0;
        _ttkPaused = false;

        target.SetGrabbed(true);
        (target.Movement as IMovementFreezable)?.SetFrozen(true);

        if (_renderer != null) _renderer.material.color = CrushColor;
        OnPlayerGrabbed?.Invoke(target);
        StartCoroutine(CrushTickRoutine());
    }

    // ── IRescueTarget ──────────────────────────────────────────
    public void PauseTTK() => _ttkPaused = true;
    public void ResumeTTK() => _ttkPaused = false;

    public void OnStruggle()
    {
        if (!_crushing) return;
        _crushMashCount++;
        if (_crushMashCount >= (_penitentData?.crushMashThreshold ?? 8))
            ReleasePlayer(0f);
    }

    public void ReleasePlayer(float healAmount)
    {
        if (!_crushing || _crushResolved) return;
        _crushResolved = true;
        _crushing = false;

        (_crushTarget?.Movement as IMovementFreezable)?.SetFrozen(false);
        if (healAmount > 0f) _crushTarget?.Health?.Heal(healAmount);
        _crushTarget?.SetGrabbed(false);
        _crushTarget = null;

        if (_renderer != null) _renderer.material.color = _originalColor;
        OnPlayerReleased?.Invoke();
    }

    public void KillPlayer()
    {
        if (!_crushing || _crushResolved) return;
        _crushResolved = true;

        (_crushTarget?.Movement as IMovementFreezable)?.SetFrozen(false);
        _crushTarget?.SetGrabbed(false);

        var proxy = _crushTarget?.GetComponent<PlayerDeathRescueProxy>();
        _crushing = false;

        if (proxy != null && proxy.IsActive)
            proxy.KillPlayer();
        else
        {
            _crushTarget?.Health?.TakeDamage(
                new DamageData(9999f, DamageType.Environmental));
            OnPlayerKilled?.Invoke();
        }

        if (_renderer != null) _renderer.material.color = _originalColor;
        _crushTarget = null;
    }

    // ── Reflection ─────────────────────────────────────────────
    private void HandleDamageTaken(EnemyHealthComponent comp, float amount, Vector3 pos)
    {
        CheckReflectionThreshold();

        if (!_reflectionActive) return;

        if (!IsPossessed)
        {
            Player hittingTwin = FindNearestTwin(pos);
            if (hittingTwin?.Health != null)
            {
                float reflected = amount * (_penitentData?.reflectionFraction ?? 0.6f);
                float safe = Mathf.Min(reflected, hittingTwin.Health.CombatHealth - 1f);
                if (safe > 0f)
                    hittingTwin.Health.TakeDamage(
                        new DamageData(safe, DamageType.Environmental));
            }
        }
        else
        {
            float reflected = amount * (_penitentData?.reflectionFraction ?? 0.6f);
            var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            foreach (var e in enemies)
            {
                if (e == this) continue;
                if (Vector3.Distance(transform.position, e.transform.position) < 4f)
                    e.Health?.TakeDamage(new DamageData(reflected, DamageType.Ability));
            }
        }
    }

    private void CheckReflectionThreshold()
    {
        if (_penitentData == null || _thresholdTriggered == null) return;
        float hpFraction = Health.CurrentHealth / Health.MaxHealth;

        for (int i = 0; i < _penitentData.reflectionThresholds.Length; i++)
        {
            if (!_thresholdTriggered[i] &&
                hpFraction <= _penitentData.reflectionThresholds[i])
            {
                _thresholdTriggered[i] = true;

                // If grabbed mid-crush — drop player and enter rage
                if (_crushing)
                {
                    ReleasePlayer(0f);
                    StartCoroutine(RagePhase());
                }
                else
                {
                    StartCoroutine(ReflectionPhase());
                }
                break;
            }
        }
    }

    private IEnumerator ReflectionPhase()
    {
        _reflectionActive = true;
        float originalSpeed = Data?.moveSpeed ?? 3.5f;
        Movement.SetSpeed(originalSpeed * (_penitentData?.reflectionSpeedMultiplier ?? 1.4f));
        if (_renderer != null) _renderer.material.color = ReflectionColor;

        while (_isFrozen) yield return null;
        yield return new WaitForSeconds(_penitentData?.reflectionDuration ?? 2.2f);

        _reflectionActive = false;
        Movement.SetSpeed(originalSpeed);
        if (_renderer != null) _renderer.material.color = _originalColor;
    }

    private IEnumerator RagePhase()
    {
        _inRage = true;
        _reflectionActive = true;
        float originalSpeed = Data?.moveSpeed ?? 3.5f;
        float rageDuration = _penitentData?.rageDuration ?? 4f;
        Movement.SetSpeed(originalSpeed * (_penitentData?.reflectionSpeedMultiplier ?? 1.4f));
        if (_renderer != null) _renderer.material.color = RageColor;

        // UI + VFX
        var stateUI = GetComponentInChildren<EnemyStateUIController>();
        stateUI?.ShowRage(rageDuration);
        stateUI?.ShowIkariRage();
        GetComponentInChildren<EnemyVFXController>()?.PlayRage();

        StateMachine.Resume(); // resume chase at boosted speed

        while (_isFrozen) yield return null;
        yield return new WaitForSeconds(rageDuration);

        _inRage = false;
        _reflectionActive = false;
        Movement.SetSpeed(originalSpeed);
        if (_renderer != null) _renderer.material.color = _originalColor;
        GetComponentInChildren<EnemyStateUIController>()?.HideRage();
        GetComponentInChildren<EnemyVFXController>()?.StopRage();

        // Post-rage cooldown before resuming normal cycle
        _inCooldown = true;
        yield return new WaitForSeconds(_penitentData?.postGrabCooldown ?? 1.25f);
        _inCooldown = false;
    }

    protected override void HandleDeath()
    {
        if (_crushing) ReleasePlayer(0f);
        base.HandleDeath();
    }

    private Player FindNearestTwin(Vector3 hitPos)
    {
        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        Player nearest = null;
        float best = float.MaxValue;
        foreach (var p in players)
        {
            if (p is SoulPlayer) continue;
            float d = Vector3.Distance(hitPos, p.transform.position);
            if (d < best) { best = d; nearest = p; }
        }
        return nearest;
    }
}