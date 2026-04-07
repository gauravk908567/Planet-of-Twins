using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyStateMachine))]
[RequireComponent(typeof(EnemyHealthComponent))]
public class Enemy : MonoBehaviour, ITimeAffected, IStunnable, IPossessable, IGrabbable, IAlertReceiver, IKnockbackReceiver, IFearReceiver, ISlowReceiver
{
    private bool _isStunned = false;
    private bool _isPossessed = false;
    private bool _isGrabbed = false;
    protected Renderer _renderer;
    protected Color _originalColor;
    private static readonly Color StunColor = new Color(0.2f, 0.6f, 1f);

    private Coroutine _stunCoroutine;
    private Coroutine _fearCoroutine;
    private Coroutine _slowCoroutine;
    private bool _isFeared = false;
    private float _baseSpeed = -1f; // cached on first slow, restored after

    [SerializeField] private EnemyData defaultData;
    public EnemyData Data { get; private set; }

    public bool IsStunned => _isStunned;
    public bool IsPossessed => _isPossessed;
    public bool IsGrabbedByTrap => _isGrabbed;

    // ── Components ─────────────────────────────────────────────
    public EnemyStateMachine StateMachine { get; private set; }
    public EnemyMovement Movement { get; private set; }
    public EnemyDetection Detection { get; private set; }
    public EnemyAttackController AttackController { get; private set; }
    public EnemyHealthComponent Health { get; private set; }
    public FactionComponent FactionComp { get; private set; }
    public StatusEffectController StatusEffects { get; private set; }
    public EnemyVFXController enemyVFXController { get; private set; }
    public EnemyStateUIController enemyStateUIController { get; private set; }

    // ── States ─────────────────────────────────────────────────
    public IEnemyState IdleState { get; protected set; }
    public IEnemyState ChaseState { get; protected set; }
    public IEnemyState AttackState { get; protected set; }
    public IEnemyState PossessedState { get; protected set; }

    [SerializeField] private float attackRange = 2f;
    [SerializeField] private MonoBehaviour timeFactorRegistryObject;
    [SerializeField] private float returnAnimDuration = 1.5f;
    [SerializeField] protected LayerMask possessedTargetLayer;

    public GameObject SourcePrefab { get; set; }
    private IEnemyPoolProvider _pool;

    public float AttackRange => attackRange;
    protected void SetAttackRange(float range) => attackRange = range;
    public Transform Target { get; private set; }
    public void SetTarget(Transform t) => Target = t;
    public void ClearTarget() => Target = null;

    private ITimeFactorRegistry _timeFactorRegistry;

    // ── Knockback duration — designer-tunable ─────────────────
    // How long (seconds) the NavMesh agent stays disabled during knockback.
    // Higher = pushed further. Exposed here so SOs don't need it if the
    // knockback force multiplier on EnemyData is sufficient granularity.
    [SerializeField] private float _knockbackDuration = 0.25f;

    protected virtual void Awake()
    {
        StateMachine = GetComponent<EnemyStateMachine>();
        Movement = GetComponent<EnemyMovement>();
        Detection = GetComponent<EnemyDetection>();
        AttackController = GetComponent<EnemyAttackController>();
        Health = GetComponent<EnemyHealthComponent>();
        FactionComp = GetComponent<FactionComponent>();
        StatusEffects = GetComponent<StatusEffectController>();

        _timeFactorRegistry = timeFactorRegistryObject as ITimeFactorRegistry;
        if (_timeFactorRegistry == null)
            _timeFactorRegistry = TimeFactorManager.Instance;
        if (_timeFactorRegistry == null)
            Debug.LogWarning($"[Enemy] {name}: no ITimeFactorRegistry found — freeze won't work", this);

        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null)
            _originalColor = _renderer.material.color;

        Health.OnDeath += HandleDeath;
        InitStates();
    }

    public virtual void ApplyData(EnemyData data)
    {
        if (data == null) return;
        Data = data;
        Health?.SetMaxHealth(data.maxHealth);
        Movement?.SetSpeed(data.moveSpeed);
        AttackController?.SetStats(data.attackRange, data.attackDamage,
                                   data.attackCooldown, data.attackWindup);
        Detection.SetRanges(data.detectionRange, data.possessedDetectionMultiplier);
        attackRange = data.attackRange;
        returnAnimDuration = data.returnAnimDuration;
    }

    protected virtual void InitStates()
    {
        IdleState = new EnemyIdleState(this);
        ChaseState = new EnemyChaseState(this);
        AttackState = new EnemyAttackState(this);
        PossessedState = new PossessedState(this, possessedTargetLayer);
    }

    protected void Start()
    {
        if (defaultData != null) ApplyData(defaultData);
    }

    private void OnEnable()
    {
        if (IdleState != null)
            StateMachine?.ChangeState(IdleState);

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

    // ── ITimeAffected ──────────────────────────────────────────
    public virtual void OnEffectStarted() { StateMachine.Pause(); Movement.OnFreeze(); }
    public virtual void OnEffectEnded() { StateMachine.Resume(); Movement.OnUnfreeze(); }

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
        StopAllCoroutines();
        StateMachine.Resume(); // CRITICAL — clear paused state from previous life
        Movement.SetSpeed(Data?.moveSpeed ?? 3.5f);
        Movement.Stop();
        FactionComp.CurrentFaction = Faction.Enemy;
        AttackController.ClearDamageMultiplier();
        AttackController.ClearAttackSlowdown();
        AttackController.ResetAttack();
        ClearTarget();
    }

    // ── IKnockbackReceiver ─────────────────────────────────────
    /// <summary>
    /// Base implementation: apply force scaled by EnemyData.knockbackForceMultiplier.
    /// Override in subclasses to add conditional blocking (e.g. SummonerEnemy,
    /// GroupGrabEnemy during active grab).
    /// </summary>
    public virtual void ReceiveKnockback(KnockbackData data)
    {
        // Read per-enemy resistance from SO. Default 1f if no data assigned.
        float multiplier = Data?.knockbackForceMultiplier ?? 1f;
        if (multiplier <= 0f) return; // fully immune — skip coroutine entirely

        StartCoroutine(KnockbackRoutine(data.Force * multiplier));
    }

    private IEnumerator KnockbackRoutine(Vector3 force)
    {
        // Disable NavMeshAgent so it doesn't fight against positional change
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>(); // routed through EnemyMovement — see note below
        if (agent != null) agent.enabled = false;

        float elapsed = 0f;

        while (elapsed < _knockbackDuration)
        {
            float t = 1f - (elapsed / _knockbackDuration); // dampen over time
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
        FactionComp.CurrentFaction = global::Faction.PossessedEnemy;
        AttackController.SetDamageMultiplier(damageMultiplier);
        StateMachine.ChangeState(PossessedState);

        float actualDuration = Data != null ? Data.possessionDuration : duration;
        StartCoroutine(PossessionDurationRoutine(actualDuration));
    }

    public void OnHitByPossessed(Enemy attacker)
    {
        if (_isPossessed) return;
        SetTarget(attacker.transform);
        StateMachine.ChangeState(AttackState);
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
        FactionComp.CurrentFaction = global::Faction.Enemy;
        ClearTarget();
        StateMachine.ChangeState(IdleState);
    }

    // ── IStunnable ─────────────────────────────────────────────
    public void ApplyStun(float duration)
    {
        if (_isPossessed) return;

        if (_stunCoroutine != null)
            StopCoroutine(_stunCoroutine);

        _stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        _isStunned = true;
        StateMachine.Pause();
        Movement.OnFreeze();

        if (_renderer != null) _renderer.material.color = StunColor;

        yield return new WaitForSeconds(duration);

        _isStunned = false;
        StateMachine.Resume();
        Movement.OnUnfreeze();

        if (_renderer != null)
            _renderer.material.color = _isPossessed
                ? new Color(0.5f, 0f, 1f)
                : _originalColor;

        _stunCoroutine = null;
    }

    // ── IGrabbable ─────────────────────────────────────────────
    public void GrabByTrap(float killDelay)
    {
        _isGrabbed = true;
        StateMachine.Pause();
        Movement.OnFreeze();
        StartCoroutine(TrapKillRoutine(killDelay));
    }

    public void ReleaseFromTrap()
    {
        _isGrabbed = false;
        StateMachine.Resume();
        Movement.OnUnfreeze();
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
        foreach (var c in combatants) { c.StateMachine.Pause(); c.Movement.OnFreeze(); }

        StateMachine.Pause();
        Movement.OnFreeze();

        yield return new WaitForSeconds(duration);

        StateMachine.Resume();
        Movement.OnUnfreeze();
        foreach (var c in combatants) { c.StateMachine.Resume(); c.Movement.OnUnfreeze(); }

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
        if (StateMachine.CurrentState != ChaseState) return;
        if (Target != grabbedPlayerTransform) return;
        StateMachine.ChangeState(ChaseState);
    }

    public void OnChaseAlert(Transform chasedPlayerTransform)
    {
        if (StateMachine.CurrentState == IdleState)
        {
            SetTarget(chasedPlayerTransform);
            StateMachine.ChangeState(ChaseState);
        }
    }

    // ── IFearReceiver ──────────────────────────────────────
    public void ApplyFear(Vector3 fleeFrom, float duration)
    {
        if (_isStunned || _isPossessed) return;

        if (_fearCoroutine != null)
            StopCoroutine(_fearCoroutine);

        _fearCoroutine = StartCoroutine(FearRoutine(fleeFrom, duration));
    }

    private IEnumerator FearRoutine(Vector3 fleeFrom, float duration)
    {
        _isFeared = true;
        StateMachine.Pause();
        enemyVFXController?.PlayFear();
        enemyStateUIController?.ShowIkariFear();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Move directly away from fleeFrom position
            Vector3 fleeDir = (transform.position - fleeFrom).normalized;
            fleeDir.y = 0f;
            Movement.MoveTowards(transform.position + fleeDir * 2f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _isFeared = false;
        StateMachine.Resume();
        enemyVFXController?.StopFear();
        _fearCoroutine = null;
    }

    // ── ISlowReceiver ──────────────────────────────────────
    public void ApplySlow(float speedMultiplier, float duration, string sourceKey)
    {
        if (_slowCoroutine != null)
            StopCoroutine(_slowCoroutine);

        _slowCoroutine = StartCoroutine(SlowRoutine(speedMultiplier, duration));
    }

    private IEnumerator SlowRoutine(float speedMultiplier, float duration)
    {
        // Cache base speed on first slow
        if (_baseSpeed < 0f)
            _baseSpeed = Data?.moveSpeed ?? 3f; // fallback if no data

        Movement.SetSpeed(_baseSpeed * speedMultiplier);

        yield return new WaitForSeconds(duration);

        // Restore base speed
        Movement.SetSpeed(_baseSpeed);
        _baseSpeed = -1f;
        _slowCoroutine = null;
    }

    private void OnDestroy()
    {
        if (_timeFactorRegistry != null)
        {
            _timeFactorRegistry.Unregister(this);
            _timeFactorRegistry.Unregister(Movement);
        }
    }
}