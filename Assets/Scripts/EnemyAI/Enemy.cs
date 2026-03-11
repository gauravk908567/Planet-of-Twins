using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyStateMachine))]
[RequireComponent(typeof(EnemyHealthComponent))]
public class Enemy : MonoBehaviour, ITimeAffected, IStunnable, IPossessable, IGrabbable, IAlertReceiver
{
    private bool _isStunned = false;
    private bool _isPossessed = false;
    private bool _isGrabbed = false;
    private float _stunDuration = 0f;
    private float _stunTimer = 0f;
    private Renderer _renderer;
    private Color _originalColor;
    private static readonly Color StunColor = new Color(0.2f, 0.6f, 1f);

    private Coroutine _stunCoroutine;

    [SerializeField] private EnemyData defaultData; // fallback if spawner doesn't inject
    public EnemyData Data { get; private set; }

    public bool IsStunned => _isStunned;
    public bool IsPossessed => _isPossessed;
    public bool IsGrabbedByTrap => _isGrabbed;

    // ── Components (set in Awake) ──────────────────────────────
    public EnemyStateMachine StateMachine { get; private set; }
    public EnemyMovement Movement { get; private set; }
    public EnemyDetection Detection { get; private set; }
    public EnemyAttackController AttackController { get; private set; }
    public EnemyHealthComponent Health { get; private set; }
    public FactionComponent FactionComp { get; private set; }
    public StatusEffectController StatusEffects { get; private set; }

    // ── States (created in subclass Awake via InitStates) ─────
    public IEnemyState IdleState { get; protected set; }
    public IEnemyState ChaseState { get; protected set; }
    public IEnemyState AttackState { get; protected set; }
    public IEnemyState PossessedState { get; protected set; }

    // ── Config ─────────────────────────────────────────────────
    [SerializeField] private float attackRange = 2f;

    // FIX: injected — no FindAnyObjectByType
    [SerializeField] private MonoBehaviour timeFactorRegistryObject;
    [SerializeField] private float returnAnimDuration = 1.5f;

    [SerializeField] protected LayerMask possessedTargetLayer;

    public GameObject SourcePrefab { get; set; }
    private IEnemyPoolProvider _pool;

    public float AttackRange => attackRange;

    // ── Target — only settable through SetTarget() ────────────
    public Transform Target { get; private set; }

    public void SetTarget(Transform t) => Target = t;
    public void ClearTarget() => Target = null;

    // ── Internal ───────────────────────────────────────────────
    private ITimeFactorRegistry _timeFactorRegistry;

    protected virtual void Awake()
    {
        StateMachine = GetComponent<EnemyStateMachine>();
        Movement = GetComponent<EnemyMovement>();
        Detection = GetComponent<EnemyDetection>();
        AttackController = GetComponent<EnemyAttackController>();
        Health = GetComponent<EnemyHealthComponent>();
        FactionComp = GetComponent<FactionComponent>();
        StatusEffects = GetComponent<StatusEffectController>();

        // MOVED from Start() — must be set before OnEnable() runs
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
    // Subclasses override this to create type-specific states
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
        // REMOVED: InitStates() — already called in Awake(), calling twice overwrites states
    }

    private void OnEnable()
    {
        // Runs on every pool reuse — restart behaviour
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
        // Unregister when returned to pool
        _timeFactorRegistry?.Unregister(this);
        _timeFactorRegistry?.Unregister(Movement);
    }

    // ── ITimeAffected ──────────────────────────────────────────
    public void OnEffectStarted()
    {
        // FIX: don't toggle .enabled — use the proper freeze path
        StateMachine.Pause();     // stops state Update() cleanly
        Movement.OnFreeze();      // stops NavMeshAgent
    }

    public void OnEffectEnded()
    {
        StateMachine.Resume();
        Movement.OnUnfreeze();
    }

    // ── Death ──────────────────────────────────────────────────
    protected virtual void HandleDeath()
    {
        if (_timeFactorRegistry != null)
        {
            _timeFactorRegistry.Unregister(this);
            _timeFactorRegistry.Unregister(Movement);
        }

        // Return to pool instead of destroying
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
        StopAllCoroutines();
        FactionComp.CurrentFaction = Faction.Enemy;
        AttackController.ClearDamageMultiplier();
        ClearTarget();
    }

    // IPossessable
    public void ApplyPossession(float duration, float damageMultiplier)
    {
        // Check data first — some enemy types are immune
        if (Data != null && !Data.canBePossessed) return; // ADD
        if (_isPossessed || _isStunned) return;

        _isPossessed = true;
        FactionComp.CurrentFaction = global::Faction.PossessedEnemy;
        AttackController.SetDamageMultiplier(damageMultiplier);
        StateMachine.ChangeState(PossessedState);

        // Use data duration if available, otherwise use passed duration as fallback
        float actualDuration = Data != null ? Data.possessionDuration : duration; // ADD
        StartCoroutine(PossessionDurationRoutine(actualDuration)); // CHANGE
    }

    public void OnHitByPossessed(Enemy attacker)
    {
        // This enemy was struck by a possessed enemy — switch target to fight back
        if (_isPossessed) return; // don't react if we're also possessed
        SetTarget(attacker.transform);
        StateMachine.ChangeState(AttackState);
    }

    private IEnumerator PossessionDurationRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (!_isPossessed) yield break; // already ended (e.g. enemy died)

        _isPossessed = false;
        AttackController.ClearDamageMultiplier();

        // Start return animation (coroutine on Enemy — pauses self + combatants)
        StartReturnAnimation(returnAnimDuration);
    }

    // Called at end of return animation coroutine (see ReturnAnimationState note)
    public void OnPossessionEnded()
    {
        FactionComp.CurrentFaction = global::Faction.Enemy;
        ClearTarget();
        StateMachine.ChangeState(IdleState);
    }
    private void OnDestroy()
    {
        if (_timeFactorRegistry != null)
        {
            _timeFactorRegistry.Unregister(this);
            _timeFactorRegistry.Unregister(Movement);
        }
    }

    public void ApplyStun(float duration)
    {
        // Can't stun while possessed — possession takes priority
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

        if (_renderer != null)
            _renderer.material.color = StunColor;

        yield return new WaitForSeconds(duration);

        _isStunned = false;
        StateMachine.Resume();
        Movement.OnUnfreeze();

        // Restore — possessed purple takes priority if still possessed
        if (_renderer != null)
            _renderer.material.color = _isPossessed
                ? new Color(0.5f, 0f, 1f)
                : _originalColor;

        _stunCoroutine = null;
    }

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
        if (_isGrabbed) // still grabbed when timer ends
            Health.TakeDamage(new DamageData(9999f, DamageType.Environmental));
    }

    public void StartReturnAnimation(float duration)
    {
        StartCoroutine(ReturnAnimationRoutine(duration));
    }
    private IEnumerator ReturnAnimationRoutine(float duration)
    {
        // Pause self and direct combatants (same logic as ReturnAnimationState.Enter)
        var combatants = FindDirectCombatants();
        foreach (var c in combatants) { c.StateMachine.Pause(); c.Movement.OnFreeze(); }

        StateMachine.Pause();
        Movement.OnFreeze();
        // TODO: trigger animator

        yield return new WaitForSeconds(duration);

        // Resume
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

    public void OnGrabAlert(Transform grabbedPlayerTransform)
    {
        // FIX: only respond if already chasing the same player
        // Idle enemies, attacking enemies, possessed enemies don't pile on
        if (StateMachine.CurrentState != ChaseState) return;
        if (Target != grabbedPlayerTransform) return; // chasing different target

        // Already chasing this player — switch to pile-on (target stays same)
        StateMachine.ChangeState(ChaseState); // refresh chase to grabbed position
    }

    public void OnChaseAlert(Transform chasedPlayerTransform)
    {
        // Only idle enemies respond to chase alerts
        if (StateMachine.CurrentState == IdleState)
        {
            SetTarget(chasedPlayerTransform);
            StateMachine.ChangeState(ChaseState);
        }
    }
}