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

    // Spawn lead: the shared on_enemyspawn cue LEADS while the enemy is hidden (child Renderers off)
    // + brain-held, then it pops in and the brain resumes. NO material float involved — reveal
    // materials are a world-object thing (Witness ritual site), enemies don't carry them.
    [Tooltip("Seconds the enemy stays hidden + brain-held while the spawn cue leads (cue is 1.8 s, " +
             "enemy appears at 1.2 s). 0 = appear instantly.")]
    [SerializeField] private float _spawnRevealDelay = 1.2f;
    private Renderer[] _spawnHideRenderers;

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
    public EnemyStateUIController enemyStateUIController { get; private set; }

    private float attackRange;
    [Tooltip("Muzzle/hand transform projectiles and ranged cues fire from. Optional — falls back to the root.")]
    [SerializeField] protected Transform firePoint;
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

    // ── Refcounted projectile/bomb/chain pools (GameplayPool) ──────────────────
    // Anything this enemy will throw registers here once per life; OnDisable releases everything,
    // so the pooled instances follow the enemies on screen instead of accumulating forever.
    private readonly List<GameObject> _pooledPrefabUsers = new List<GameObject>();

    /// <summary>Declare "this live enemy spawns this prefab" — warms the GameplayPool and holds it
    /// open until this enemy despawns. Deduped per life; call freely at configure or throw time.</summary>
    public void RegisterPooledPrefab(GameObject prefab, PoolCategory category, int warmCount = 2)
    {
        if (prefab == null || _pooledPrefabUsers.Contains(prefab)) return;
        _pooledPrefabUsers.Add(prefab);
        GameplayPool.AddUser(prefab, category, warmCount);
    }

    private void ReleasePooledPrefabs()
    {
        for (int i = 0; i < _pooledPrefabUsers.Count; i++)
            GameplayPool.RemoveUser(_pooledPrefabUsers[i]);
        _pooledPrefabUsers.Clear();
    }

    /// <summary>True while this enemy is actively engaging or being acted on — chasing/fighting a
    /// target, stunned, possessed, feared, grabbed, or brain-held (freeze service / QTE). POI energy
    /// feeding (PoiEnergyEmitter) pauses while true: enemies only drink when they are not engaging.</summary>
    public bool IsEngaged =>
        Target != null || _isStunned || _isPossessed || _isGrabbed || _isFeared || IsBrainPaused;

    // ── VFX cue (archetype book from EnemyVfxLibrary, R4) ──────
    // Overridden per archetype to point at its EnemyVfxLibrary slot + basic-attack id.
    // Null book/id = plays nothing (support types, commanders, dropped Penitent) — fails safe.
    public virtual CueBookData VfxBook => null;
    protected virtual string MeleeAttackCueId => null;
    protected virtual string RangedAttackCueId => null;

    /// <summary>Archetype override to mute the basic attack slash/hit-spark while a held state owns the
    /// visual language (GroupGrab: the warden is ABSORBING the grabbed twin, not slashing them).</summary>
    public virtual bool SuppressBasicAttackCues => false;

    /// <summary>Play the archetype's basic melee-attack cue on this enemy (called by EnemyAttackController).</summary>
    public void PlayMeleeAttackCue()
    {
        if (SuppressBasicAttackCues) return;
        var book = VfxBook;
        if (book != null && !string.IsNullOrEmpty(MeleeAttackCueId))
            FxManager.Instance?.PlayBook(book, MeleeAttackCueId, CueContext.Follow(transform));
    }

    /// <summary>Play the archetype's basic ranged-attack cue at the fire point (called by EnemyAttackController).</summary>
    public void PlayRangedAttackCue(Transform origin)
    {
        var book = VfxBook;
        if (book == null) return;
        // Melee archetypes given a projectile via data have no ranged id — fall back to their melee tell
        // so the attack is never silent.
        string id = !string.IsNullOrEmpty(RangedAttackCueId) ? RangedAttackCueId : MeleeAttackCueId;
        if (string.IsNullOrEmpty(id)) return;
        var o = origin != null ? origin : transform;
        FxManager.Instance?.PlayBook(book, id, new CueContext(o.position, o.rotation));
    }

    /// <summary>Play any of THIS enemy's archetype cues (from its <see cref="VfxBook"/>) by id — used by type-specific
    /// lifecycle beats (summon, ritual, rage, grab…). No-op if the book or id is missing (fail-safe). Returns the
    /// <see cref="CueHandle"/> so a HELD cue (a drain / aura) can be stopped later; a fire-and-forget caller ignores it.</summary>
    public CueHandle PlayCue(string id, CueContext ctx)
    {
        var book = VfxBook;
        if (book == null || string.IsNullOrEmpty(id)) return CueHandle.None;
        return FxManager.Instance?.PlayBook(book, id, ctx) ?? CueHandle.None;
    }

    /// <summary>Deep-search fallback for the muzzle transform (see Awake). Name match is
    /// case-insensitive contains: "firepoint" / "muzzle" / "tip". Null when nothing matches —
    /// callers already fall back to the enemy root.</summary>
    private static Transform FindFirePointDeep(Transform root)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == root) continue;
            string n = all[i].name.ToLowerInvariant();
            if (n.Contains("firepoint") || n.Contains("muzzle") || n.Contains("tip"))
                return all[i];
        }
        return null;
    }

    private ITimeFactorRegistry _timeFactorRegistry;

    [SerializeField] private float _knockbackDuration = 0.25f;

    protected virtual void Awake()
    {
        Movement = GetComponent<EnemyMovement>();
        AttackController = GetComponent<EnemyAttackController>();
        Health = GetComponent<EnemyHealthComponent>();
        StatusEffects = GetComponent<StatusEffectController>();
        enemyStateUIController = GetComponentInChildren<EnemyStateUIController>();

        _timeFactorRegistry = timeFactorRegistryObject as ITimeFactorRegistry;
        if (_timeFactorRegistry == null)
            _timeFactorRegistry = TimeFactorManager.Instance;
        if (_timeFactorRegistry == null)
            Debug.LogWarning($"[Enemy] {name}: no ITimeFactorRegistry — freeze won't work", this);

        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null)
            _originalColor = MaterialTint.GetColor(_renderer.material);

        _spawnHideRenderers = GetComponentsInChildren<Renderer>(true);   // toggled off during the spawn lead

        // firePoint fallback: the serialized slot wins (industry pattern — the muzzle is a
        // per-shooter child transform; the shared projectile prefab needs nothing). If unassigned,
        // find a descendant named *FirePoint*/*Muzzle*/*Tip* so ranged prefabs work out of the box.
        if (firePoint == null)
            firePoint = FindFirePointDeep(transform);

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
        AttackController?.SetProjectile(data.useProjectile, data.projectilePrefab,
                                        firePoint, data.projectileSpeed);
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
        ReleasePooledPrefabs();   // last live user gone → GameplayPool trims that prefab's pool
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

    public void SetPoolProvider(IEnemyPoolProvider pool, GameObject sourcePrefab, bool playSpawnCue = true)
    {
        _pool = pool;
        SourcePrefab = sourcePrefab;

        // Shared spawn effect (Common book) at the spawn position. Every spawn path (regular / partner / group /
        // soldier / summoner) funnels through here AFTER positioning, so this is the one generic hook. World-anchored
        // (the enemy is already placed). The cue LEADS: the enemy stays hidden + brain-held for _spawnRevealDelay,
        // then materialises (below). Runs BEFORE ApplyData, so the delay is a per-prefab field, not from Data.
        // playSpawnCue=false: a SUMMONED/ritual minion — the summoner's own channel/circle cue IS its spawn tell,
        // so the generic effect (and its reveal-delay hide) is skipped and the minion appears immediately.
        if (!playSpawnCue) return;

        CommonFx.Play(FxIds.Common.Effects.on_enemyspawn, new CueContext(transform.position));

        if (_spawnRevealDelay > 0f)
            StartCoroutine(SpawnRevealRoutine());
    }

    // Hold the enemy hidden (renderers off) + brain-paused while the spawn cue leads, then show + resume.
    private IEnumerator SpawnRevealRoutine()
    {
        SetSpawnRenderersVisible(false);
        PauseBrain();
        yield return new WaitForSeconds(_spawnRevealDelay);   // scaled — a gameplay anticipation beat (R10)
        SetSpawnRenderersVisible(true);
        ResumeBrain();
    }

    private void SetSpawnRenderersVisible(bool visible)
    {
        if (_spawnHideRenderers == null) return;
        for (int i = 0; i < _spawnHideRenderers.Length; i++)
            if (_spawnHideRenderers[i] != null)
                _spawnHideRenderers[i].enabled = visible;
    }

    public void ResetForPool()
    {
        _isPossessed = false;
        _isStunned = false;
        _isGrabbed = false;
        _isFeared = false;
        IsBrainPaused = false;
        // NOTE: health is deliberately NOT reset here — ResetForPool runs INSIDE the OnDeath event
        // (HandleDeath → pool Return), and resetting health/LastDamageType mid-event made every kill
        // read as Environmental to the LATER OnDeath subscribers (EnemyDeathNotifier: no accord
        // charge, no souls, no kill helix). The pool resets health at ISSUE time instead
        // (EnemyPool.Get → ResetToFull, BUG-058).
        StopAllCoroutines();
        SetSpawnRenderersVisible(true);   // returned mid-spawn-lead → never re-enter the pool hidden
        // Body tint back to authored — every state tint (stun cyan, possess purple, Witness ritual,
        // Penitent crush/rage, TetherBreaker rage) writes this renderer against _originalColor, and an
        // enemy killed MID-STATE re-entered the pool tinted and respawned tinted (BUG-057).
        if (_renderer != null) MaterialTint.SetColor(_renderer.material, _originalColor);
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
        if (_renderer != null) MaterialTint.SetColor(_renderer.material, StunColor);

        yield return new WaitForSeconds(duration);

        _isStunned = false;
        ResumeBrain();
        if (_renderer != null)
            MaterialTint.SetColor(_renderer.material, _isPossessed ? new Color(0.5f, 0f, 1f) : _originalColor);

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
 // fear flee reads as Panicked (erratic). Manpu shows the Panicked aura; the flee BEHAVIOUR is the
        // forced routine here (brain paused), so the mood modifiers can't fight it. Paired stop below (stomp-safe).
        GetComponent<EnemyMoodSystem>()?.TransitionTo(EnemyMood.Panicked, 0f, EnemyMood.Normal);

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
 // leave Panicked only if still Panicked (stomp-safe; overlapping fears clear at the last one's end).
        var fearMood = GetComponent<EnemyMoodSystem>();
        if (fearMood != null && fearMood.CurrentMood == EnemyMood.Panicked)
            fearMood.TransitionTo(EnemyMood.Normal, 0f, EnemyMood.Normal);
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