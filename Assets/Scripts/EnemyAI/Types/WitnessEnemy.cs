using System.Collections;
using UnityEngine;

/// <summary>
/// Witness — support enemy that summons a melee ally and buffs nearby enemies.
///
/// FLOW:
///   Spawn → immediately summon melee ally → shadow it (ShadowState)
///   Ally dies → WitnessRitualState (4s channel to summon new ally)
///   Player interrupts ritual (proximity or damage) → flee → restart ritual
///   Ritual completes → new ally spawned → back to ShadowState
///
/// BUFF AURA: Always active. Nearby enemies get +40% damage, halved cooldowns.
///   On possession: inverts to debuff aura.
///
/// BOMB: If twin enters bombTriggerRange while shadowing → wind-up → roll bomb.
///
/// DEATH BOMB: On death, drops bomb at feet.
///
/// IMPORTANT: Uses Awake subscription (not OnEnable) to avoid hiding Enemy.OnEnable.
/// </summary>
public class WitnessEnemy : Enemy
{
    [Header("Witness — project assets, safe to wire on prefab")]
    [SerializeField] private WitnessEnemyData _witnessData;
    [SerializeField] private GameObject _bombPrefab;
    [SerializeField] private BombEffectData _witnessBombData;
    [SerializeField] private Transform _bombMuzzle;
    [SerializeField] private GameObject _meleePrefab; // ally to summon
    [SerializeField] private LayerMask _playerLayer;
    [SerializeField] private LayerMask _enemyLayer;

    // ── States ─────────────────────────────────────────────────
    public WitnessShadowState ShadowState { get; private set; }
    public WitnessRitualState RitualState { get; private set; }

    // ── Follow target ──────────────────────────────────────────
    public Enemy FollowTarget { get; private set; }

    // ── Bomb state ─────────────────────────────────────────────
    private bool _bombOnCooldown;
    private bool _isThrowing;
    private bool _isRetreating;

    // ── Aura tracking ──────────────────────────────────────────
    private readonly Collider[] _auraBuffer = new Collider[16];
    private Enemy[] _lastBuffed = new Enemy[0];

    // ── Colour ─────────────────────────────────────────────────
    private static readonly Color RitualColour = new Color(0.5f, 0f, 1f);

    // ── Shadow follow distance (not in SO, keep simple) ────────
    private const float ShadowFollowDist = 2.5f;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);
        if (data is WitnessEnemyData wd)
        {
            _witnessData = wd;
            ShadowState = new WitnessShadowState(this, wd);
            RitualState = new WitnessRitualState(this, wd);
            AttackState = RitualState; // never standard attack

            // Summon first ally and start shadowing
            StartCoroutine(InitialSummonRoutine());
        }
    }

    protected override void InitStates()
    {
        base.InitStates();
        // States created in ApplyData
    }

    private IEnumerator InitialSummonRoutine()
    {
        yield return null; // wait one frame for pool/spawner setup
        SummonAlly();
        // SummonAlly sets FollowTarget and transitions to ShadowState
    }

    // ── Update — aura only ─────────────────────────────────────
    private void Update()
    {
        UpdateBuffAura();
    }

    // ── Aura ───────────────────────────────────────────────────
    private void UpdateBuffAura()
    {
        float radius = _witnessData?.buffAuraRadius ?? 6f;
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, radius, _auraBuffer, _enemyLayer);

        // Build new buffed list
        var newBuffed = new System.Collections.Generic.List<Enemy>();
        for (int i = 0; i < count; i++)
        {
            var enemy = _auraBuffer[i].GetComponent<Enemy>()
                     ?? _auraBuffer[i].GetComponentInParent<Enemy>();
            if (enemy == null || enemy == this) continue;
            newBuffed.Add(enemy);
        }

        // Unbuff enemies that LEFT the range (were buffed last frame, not in new list)
        foreach (var e in _lastBuffed)
        {
            if (e == null) continue;
            if (!newBuffed.Contains(e))
            {
                e.AttackController.ClearDamageMultiplier();
                e.AttackController.ClearAttackSlowdown();
                e.GetComponentInChildren<EnemyVFXController>()?.StopBuff();
            }
        }

        // Apply buffs to enemies currently in range
        foreach (var enemy in newBuffed)
        {
            if (!IsPossessed)
            {
                enemy.AttackController.SetDamageMultiplier(
                    _witnessData?.buffDamageMultiplier ?? 1.4f);
                enemy.AttackController.SetAttackSlowdown(
                    _witnessData?.buffCooldownMultiplier ?? 0.5f);
            }
            else
            {
                enemy.AttackController.SetDamageMultiplier(0.6f);
                enemy.AttackController.SetAttackSlowdown(2f);
            }

            // Only notify buffed for NEW entries (not already tracked)
            bool wasAlreadyBuffed = System.Array.Exists(_lastBuffed, e => e == enemy);
            if (!wasAlreadyBuffed)
                enemy.GetComponentInChildren<EnemyVFXController>()?.PlayBuff();
        }

        _lastBuffed = newBuffed.ToArray();
    }

    private void ClearBuffs(Enemy[] enemies)
    {
        if (enemies == null) return;
        foreach (var e in enemies)
        {
            if (e == null) continue;
            e.AttackController.ClearDamageMultiplier();
            e.AttackController.ClearAttackSlowdown();
        }
    }

    // ── Summon ─────────────────────────────────────────────────
    public void SummonAlly()
    {
        if (_meleePrefab == null) return;

        // Spawn near Witness position
        Vector3 spawnPos = transform.position +
                           transform.forward * 1.5f +
                           new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));

        var go = Instantiate(_meleePrefab, spawnPos, Quaternion.identity);
        var ally = go.GetComponent<Enemy>();
        if (ally == null) return;

        SetFollowTarget(ally);
        Debug.Log($"[Witness] Summoned {ally.name}");
    }

    public void SetFollowTarget(Enemy target)
    {
        FollowTarget = target;
        if (target != null && ShadowState != null)
            StateMachine?.ChangeState(ShadowState);
    }

    // ── Bomb ───────────────────────────────────────────────────
    public void CheckBombThrow()
    {
        if (_bombOnCooldown || _isThrowing || _isRetreating) return;
        if (_bombPrefab == null || _witnessBombData == null) return;

        var twin = GetNearestTwin();
        if (twin == null) return;

        float dist = Vector3.Distance(transform.position, twin.transform.position);
        if (dist <= (_witnessData?.bombTriggerRange ?? 5f))
            StartCoroutine(BombThrowRoutine(twin.transform));
    }

    private IEnumerator BombThrowRoutine(Transform target)
    {
        _isThrowing = true;
        _bombOnCooldown = true;
        GetComponentInChildren<EnemyVFXController>()?.PlayPanic();

        Vector3 dir = (target.position - transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);

        yield return new WaitForSeconds(_witnessData?.bombWindUpDuration ?? 0.75f);

        if (!Health.IsDead)
        {
            Vector3 spawnPos = _bombMuzzle != null ? _bombMuzzle.position : transform.position;
            Vector3 targetPos = target.position;
            targetPos.y = spawnPos.y;

            var go = Instantiate(_bombPrefab, spawnPos, Quaternion.identity);
            var bomb = go.GetComponent<BombProjectile>();
            bomb?.Roll(spawnPos, targetPos, 1.0f,
                _witnessData?.bombDetonationDelay ?? 0.75f,
                _witnessData?.bombAoeRadius ?? 1.5f,
                _witnessBombData, _playerLayer, LayerMask.GetMask("Enemy"));
        }

        _isThrowing = false;
        yield return StartCoroutine(RetreatRoutine());
        _bombOnCooldown = false;
    }

    private IEnumerator RetreatRoutine()
    {
        _isRetreating = true;
        StateMachine.Pause();

        float originalSpeed = Data?.moveSpeed ?? 3.5f;
        Movement.SetSpeed(originalSpeed * (_witnessData?.fleeSpeedMultiplier ?? 1.6f));

        float retreatDist = _witnessData?.ritualFleeDistance ?? 10f;
        var twin = GetNearestTwin();

        while (twin != null)
        {
            float dist = Vector3.Distance(transform.position, twin.transform.position);
            if (dist >= retreatDist) break;
            Vector3 fleeDir = (transform.position - twin.transform.position).normalized;
            fleeDir.y = 0f;
            Movement.MoveTowards(transform.position + fleeDir * 5f);
            yield return null;
        }

        Movement.SetSpeed(originalSpeed);
        Movement.Stop();
        StateMachine.Resume();
        _isRetreating = false;
        GetComponentInChildren<EnemyVFXController>()?.StopPanic();
    }

    // ── Ritual colour (called by WitnessRitualState) ───────────
    public void SetRitualColour(bool active)
    {
        if (_renderer != null)
            _renderer.material.color = active ? RitualColour : _originalColor;
    }

    // ── Death bomb ─────────────────────────────────────────────
    protected override void HandleDeath()
    {
        GetComponent<WitnessAuraVFX>()?.StopAll();
        ClearBuffs(_lastBuffed);

        if (_bombPrefab != null && _witnessBombData != null)
        {
            Vector3 spawnPos = _bombMuzzle != null ? _bombMuzzle.position : transform.position;
            var go = Instantiate(_bombPrefab, spawnPos, Quaternion.identity);
            var bomb = go.GetComponent<BombProjectile>();
            bomb?.Initialise(
                _witnessData?.bombDetonationDelay ?? 0.75f,
                _witnessData?.bombAoeRadius ?? 1.5f,
                _witnessBombData, _playerLayer,
                LayerMask.GetMask("Enemy"));
        }

        base.HandleDeath();
    }

    // ── Helpers ────────────────────────────────────────────────
    public Player GetNearestTwin()
    {
        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        Player nearest = null;
        float best = float.MaxValue;
        foreach (var p in players)
        {
            if (p is SoulPlayer) continue;
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < best) { best = d; nearest = p; }
        }
        return nearest;
    }

    public Enemy FindFollowTarget()
    {
        var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        Enemy best = null;
        float bestDist = float.MaxValue;
        foreach (var e in enemies)
        {
            if (e == this || e is WitnessEnemy || e.Health.IsDead) continue;
            float priority = e.Target != null
                ? Vector3.Distance(transform.position, e.transform.position) * 0.5f
                : Vector3.Distance(transform.position, e.transform.position);
            if (priority < bestDist) { bestDist = priority; best = e; }
        }
        return best;
    }

    private void OnDrawGizmosSelected()
    {
        if (_witnessData == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, _witnessData.buffAuraRadius);
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, _witnessData.bombTriggerRange);
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, _witnessData.ritualInterruptRange);
    }
}