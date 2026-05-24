using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for all Planet of Twins enemies.
///
/// REWORKED: Removed EnemyStateMachine, EnemyDetection, OldFactionComponent,
/// and all state machine references. Brain is now handled by PoTGOAPBrainBase.
/// Detection is handled by PerceptionListener + FactionComponent.
///
/// What stays: Movement, AttackController, Health, all status effects
/// (stun, fear, slow, possession, knockback), pool support, time factor.
///
/// Brain pause/resume: instead of StateMachine.Pause/Resume, sets _brainPaused
/// flag. PoTGOAPBrainBase checks this flag in OnPreTickBrain and skips ticking
/// when true. This keeps all existing stun/fear/grab logic working unchanged.
/// </summary>
[RequireComponent(typeof(EnemyHealthComponent))]
public class Enemy : MonoBehaviour, ITimeAffected, IStunnable, IPossessable, IGrabbable,
                     IAlertReceiver, IKnockbackReceiver, IFearReceiver, ISlowReceiver
{
    private bool _isStunned = false;
    private bool _isPossessed = false;
    private bool _isGrabbed = false;
    private bool _isFeared = false;
    private float _baseSpeed = -1f;

    // Brain pause flag — replaces StateMachine.Pause/Resume
    // PoTGOAPBrainBase reads this in OnPreTickBrain
    public bool IsBrainPaused { get; private set; } = false;

    protected Renderer _renderer;
    protected Color _originalColor;
    private static readonly Color StunColor = new Color(0.2f, 0.6f, 1f);

    private Coroutine _stunCoroutine;
    private Coroutine _fearCoroutine;
    private Coroutine _slowCoroutine;

    [SerializeField] private EnemyData defaultData;
    public EnemyData Data { get; private set; }

    public bool IsStunned => _isStunned;
    public bool IsPossessed => _isPossessed;
    public bool IsGrabbedByTrap => _isGrabbed;

    // ── Components ─────────────────────────────────────────────
    public EnemyMovement Movement { get; private set; }
    public EnemyAttackController AttackController { get; private set; }
    public EnemyHealthComponent Health { get; private set; }
    public StatusEffectController StatusEffects { get; private set; }
    public EnemyVFXController enemyVFXController { get; private set; }
    public EnemyStateUIController enemyStateUIController { get; private set; }

    private float attackRange;
    [SerializeField] private MonoBehaviour timeFactorRegistryObject;
    [SerializeField] private float returnAnimDuration = 1.5f;
    [SerializeField] protected LayerMask possessedTargetLayer;

    public GameObject SourcePrefab { get; set; }
    private IEnemyPoolProvider _pool;

    public float AttackRange => attackRange;
    protected void SetAttackRange(float range) => attackRange = range;

    // Target is now written by GOAP/BT via Blackboard — kept for compatibility
    // with existing systems (rescue, alerts, possessed targeting)
    public Transform Target { get; private set; }
    public void SetTarget(Transform t) => Target = t;
    public void ClearTarget() => Target = null;

    private ITimeFactorRegistry _timeFactorRegistry;

    [SerializeField] private float _knockbackDuration = 0.25f;

    protected virtual void Awake()
    {
        Movement = GetComponent<EnemyMovement>();
        AttackController = GetComponent<EnemyAttackController>();
        Health = GetComponent<EnemyHealthComponent>();
        StatusEffects = GetComponent<StatusEffectController>();
        enemyVFXController = GetComponentInChildren<EnemyVFXController>();
        enemyStateUIController = GetComponentInChildren<EnemyStateUIController>();

        _timeFactorRegistry = timeFactorRegistryObject as ITimeFactorRegistry;
        if (_timeFactorRegistry == null)
            _timeFactorRegistry = TimeFactorManager.Instance;
        if (_timeFactorRegistry == null)
            Debug.LogWarning($"[Enemy] {name}: no ITimeFactorRegistry — freeze won't work", this);

        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null)
            _originalColor = _renderer.material.color;

        Health.OnDeath += HandleDeath;
    }

    public virtual void ApplyData(EnemyData data)
    {
        if (data == null) return;
        Data = data;
        Health?.SetMaxHealth(data.maxHealth);
        Movement?.SetSpeed(data.moveSpeed);
        AttackController?.SetStats(data.attackRange, data.attackDamage,
                                   data.attackCooldown, data.attackWindup);
        attackRange = data.attackRange;
        returnAnimDuration = data.returnAnimDuration;
    }

    protected void Start()
    {
        if (defaultData != null) ApplyData(defaultData);
    }

    private void OnEnable()
    {
        if (_timeFactorRegistry != null)
        {
            _timeFactorRegistry.Register(this);
            _timeFactorRegistry.Register(Movement);
        }
    }

    private void OnDisable()
    {
        _timeFactorRegistry?.Unregister(this);
        _timeFactorRegistry?.Unregister(Movement);
    }

    // ── Brain pause (replaces StateMachine.Pause/Resume) ───────
    public void PauseBrain()
    {
        IsBrainPaused = true;
        Movement.OnFreeze();
    }

    public void ResumeBrain()
    {
        IsBrainPaused = false;
        Movement.OnUnfreeze();
    }

    // ── ITimeAffected ──────────────────────────────────────────
    public virtual void OnEffectStarted() => PauseBrain();
    public virtual void OnEffectEnded() => ResumeBrain();

    // ── Death ──────────────────────────────────────────────────
    protected virtual void HandleDeath()
    {
        if (_timeFactorRegistry != null)
        {
            _timeFactorRegistry.Unregister(this);
            _timeFactorRegistry.Unregister(Movement);
        }

        if (_pool != null && SourcePrefab != null)
            _pool.Return(SourcePrefab, gameObject);
        else
            Destroy(gameObject, 0.1f);
    }

    public void SetPoolProvider(IEnemyPoolProvider pool, GameObject sourcePrefab)
    {
        _pool = pool;
        SourcePrefab = sourcePrefab;
    }

    public void ResetForPool()
    {
        _isPossessed = false;
        _isStunned = false;
        _isGrabbed = false;
        _isFeared = false;
        IsBrainPaused = false;
        StopAllCoroutines();
        Movement.SetSpeed(Data?.moveSpeed ?? 3.5f);
        Movement.Stop();
        AttackController.ClearDamageMultiplier();
        AttackController.ClearAttackSlowdown();
        AttackController.ResetAttack();
        ClearTarget();
    }

    // ── IKnockbackReceiver ─────────────────────────────────────
    public virtual void ReceiveKnockback(KnockbackData data)
    {
        float multiplier = Data?.knockbackForceMultiplier ?? 1f;
        if (multiplier <= 0f) return;
        StartCoroutine(KnockbackRoutine(data.Force * multiplier));
    }

    private IEnumerator KnockbackRoutine(Vector3 force)
    {
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        float elapsed = 0f;
        while (elapsed < _knockbackDuration)
        {
            float t = 1f - (elapsed / _knockbackDuration);
            transform.position += force * t * Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (agent != null) agent.enabled = true;
    }

    // ── IPossessable ───────────────────────────────────────────
    public void ApplyPossession(float duration, float damageMultiplier)
    {
        if (Data != null && !Data.canBePossessed) return;
        if (_isPossessed || _isStunned) return;

        _isPossessed = true;

        // Update faction via new FactionComponent
        var factionComp = GetComponent<CommonCore.FactionComponent>();
        // Possessed faction logic — GOAP goal reads IsPossessed from Blackboard
        // FactionComponent faction swap handled when GOAP brain writes to Blackboard

        AttackController.SetDamageMultiplier(damageMultiplier);

        float actualDuration = Data != null ? Data.possessionDuration : duration;
        StartCoroutine(PossessionDurationRoutine(actualDuration));
    }

    public void OnHitByPossessed(Enemy attacker)
    {
        if (_isPossessed) return;
        SetTarget(attacker.transform);
        // GOAP brain will detect target change via Blackboard next tick
    }

    private IEnumerator PossessionDurationRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (!_isPossessed) yield break;

        _isPossessed = false;
        AttackController.ClearDamageMultiplier();
        StartReturnAnimation(returnAnimDuration);
    }

    public void OnPossessionEnded()
    {
        ClearTarget();
        // GOAP brain re-evaluates goals next tick — AttackTwin goal will fire
    }

    // ── IStunnable ─────────────────────────────────────────────
    public void ApplyStun(float duration)
    {
        if (_isPossessed) return;
        if (_stunCoroutine != null) StopCoroutine(_stunCoroutine);
        _stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        _isStunned = true;
        PauseBrain();
        if (_renderer != null) _renderer.material.color = StunColor;

        yield return new WaitForSeconds(duration);

        _isStunned = false;
        ResumeBrain();
        if (_renderer != null)
            _renderer.material.color = _isPossessed ? new Color(0.5f, 0f, 1f) : _originalColor;

        _stunCoroutine = null;
    }

    // ── IGrabbable ─────────────────────────────────────────────
    public void GrabByTrap(float killDelay)
    {
        _isGrabbed = true;
        PauseBrain();
        StartCoroutine(TrapKillRoutine(killDelay));
    }

    public void ReleaseFromTrap()
    {
        _isGrabbed = false;
        ResumeBrain();
    }

    private IEnumerator TrapKillRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_isGrabbed)
            Health.TakeDamage(new DamageData(9999f, DamageType.Environmental));
    }

    // ── Possession return animation ────────────────────────────
    public void StartReturnAnimation(float duration)
    {
        StartCoroutine(ReturnAnimationRoutine(duration));
    }

    private IEnumerator ReturnAnimationRoutine(float duration)
    {
        var combatants = FindDirectCombatants();
        foreach (var c in combatants) c.PauseBrain();

        PauseBrain();
        yield return new WaitForSeconds(duration);
        ResumeBrain();

        foreach (var c in combatants) c.ResumeBrain();
        OnPossessionEnded();
    }

    private List<Enemy> FindDirectCombatants()
    {
        var result = new List<Enemy>();
        var all = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var e in all)
            if (e != this && e.Target == transform)
                result.Add(e);
        return result;
    }

    // ── IAlertReceiver ─────────────────────────────────────────
    public void OnGrabAlert(Transform grabbedPlayerTransform)
    {
        // GOAP brain handles this via Blackboard — no direct state machine call needed
    }

    public void OnChaseAlert(Transform chasedPlayerTransform)
    {
        // PerceptionListener will detect the twin — GOAP re-evaluates naturally
        // Keep SetTarget for systems that still read it directly
        SetTarget(chasedPlayerTransform);
    }

    // ── IFearReceiver ──────────────────────────────────────────
    public void ApplyFear(Vector3 fleeFrom, float duration)
    {
        if (_isStunned || _isPossessed) return;
        if (_fearCoroutine != null) StopCoroutine(_fearCoroutine);
        _fearCoroutine = StartCoroutine(FearRoutine(fleeFrom, duration));
    }

    private IEnumerator FearRoutine(Vector3 fleeFrom, float duration)
    {
        _isFeared = true;
        PauseBrain();
        enemyVFXController?.PlayFear();
        enemyStateUIController?.ShowIkariFear();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            Vector3 fleeDir = (transform.position - fleeFrom).normalized;
            fleeDir.y = 0f;
            Movement.MoveTowards(transform.position + fleeDir * 2f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _isFeared = false;
        ResumeBrain();
        enemyVFXController?.StopFear();
        _fearCoroutine = null;
    }

    // ── ISlowReceiver ──────────────────────────────────────────
    public void ApplySlow(float speedMultiplier, float duration, string sourceKey)
    {
        if (_slowCoroutine != null) StopCoroutine(_slowCoroutine);
        _slowCoroutine = StartCoroutine(SlowRoutine(speedMultiplier, duration));
    }

    private IEnumerator SlowRoutine(float speedMultiplier, float duration)
    {
        if (_baseSpeed < 0f)
            _baseSpeed = Data?.moveSpeed ?? 3f;

        Movement.SetSpeed(_baseSpeed * speedMultiplier);
        yield return new WaitForSeconds(duration);
        Movement.SetSpeed(_baseSpeed);
        _baseSpeed = -1f;
        _slowCoroutine = null;
    }

    private void OnDestroy()
    {
        _timeFactorRegistry?.Unregister(this);
        _timeFactorRegistry?.Unregister(Movement);
    }
}