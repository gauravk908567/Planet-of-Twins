using System.Collections;
using UnityEngine;

/// <summary>
/// Witness — support enemy. Buffs allies, shadows summoned ally, rituals to resummon.
/// Buff aura runs in Update — always active regardless of GOAP state.
/// All behaviour decisions driven by GOAP+BT.
/// </summary>
public class WitnessEnemy : Enemy
{
    [Header("Witness — project assets")]
    [SerializeField] private WitnessEnemyData _witnessData;
    [SerializeField] private GameObject _bombPrefab;
    [SerializeField] private BombEffectData _witnessBombData;
    [SerializeField] private Transform _bombMuzzle;
    [SerializeField] private GameObject _meleePrefab;
    [SerializeField] private LayerMask _playerLayer;
    [SerializeField] private LayerMask _enemyLayer;

    // ── Public state — read by GOAP brain ─────────────────────
    public Enemy FollowTarget { get; private set; }
    public bool AllyIsAlive => FollowTarget != null
    && FollowTarget.gameObject != null           // ← add
    && FollowTarget.gameObject.activeInHierarchy // ← add
    && !FollowTarget.Health.IsDead;
    public bool IsRitualing { get; private set; }
    public bool BombOnCooldown { get; private set; }
    public bool IsRetreating { get; private set; }
    public bool IsThrowing { get; private set; }
    public WitnessEnemyData WitnessData => _witnessData;
    public bool RitualBombDropped { get; set; } = false;

    // ── Aura ───────────────────────────────────────────────────
    private readonly Collider[] _auraBuffer = new Collider[16];
    private Enemy[] _lastBuffed = new Enemy[0];

    private static readonly Color RitualColour = new Color(0.5f, 0f, 1f);

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
            StartCoroutine(InitialSummonRoutine());
        }
    }

    private IEnumerator InitialSummonRoutine()
    {
        yield return null;
        SummonAlly();
    }

    // ── Update — aura only (passive, always runs) ──────────────
    private void Update()
    {
        UpdateBuffAura();
    }

    // ── Buff Aura ──────────────────────────────────────────────
    private void UpdateBuffAura()
    {
        float radius = _witnessData?.buffAuraRadius ?? 6f;
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, radius, _auraBuffer, _enemyLayer);

        if (count > 0)
            FactionEnergySystem.Instance?.OnWitnessAuraActive();

        var newBuffed = new System.Collections.Generic.List<Enemy>();
        for (int i = 0; i < count; i++)
        {
            var enemy = _auraBuffer[i].GetComponent<Enemy>()
                     ?? _auraBuffer[i].GetComponentInParent<Enemy>();
            if (enemy == null || enemy == this) continue;
            newBuffed.Add(enemy);
        }

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

        foreach (var enemy in newBuffed)
        {
            if (!IsPossessed)
            {
                enemy.AttackController.SetDamageMultiplier(_witnessData?.buffDamageMultiplier ?? 1.4f);
                enemy.AttackController.SetAttackSlowdown(_witnessData?.buffCooldownMultiplier ?? 0.5f);
            }
            else
            {
                enemy.AttackController.SetDamageMultiplier(0.6f);
                enemy.AttackController.SetAttackSlowdown(2f);
            }

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
        RitualBombDropped = false;
        if (_meleePrefab == null) return;

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
    }

    // ── Ritual — called by BTActionWitnessRitual ───────────────
    public void StartRitual()
    {
        if (IsRitualing) return;
        StartCoroutine(RitualRoutine());
    }

    private IEnumerator RitualRoutine()
    {
        IsRitualing = true;
        SetRitualColour(true);
        Movement.Stop();

        float elapsed = 0f;
        float duration = _witnessData?.ritualDuration ?? 4f;

        while (elapsed < duration)
        {
            // Check for interrupt — twin too close or took damage
            var twin = GetNearestTwin();
            if (twin != null)
            {
                float dist = Vector3.Distance(transform.position, twin.transform.position);
                if (dist <= (_witnessData?.ritualInterruptRange ?? 5f))
                {
                    IsRitualing = false;
                    SetRitualColour(false);
                    yield break; // GOAP will re-evaluate, flee goal fires
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ritual complete — summon ally
        SummonAlly();
        IsRitualing = false;
        SetRitualColour(false);
    }

    // ── Bomb — called by BTActionThrowBomb ─────────────────────
    public bool CanThrowBomb => !BombOnCooldown && !IsThrowing && !IsRetreating
                                && _bombPrefab != null && _witnessBombData != null;

    public void ThrowBomb(Transform target)
    {
        if (!CanThrowBomb) return;
        StartCoroutine(BombThrowRoutine(target));
    }

    private IEnumerator BombThrowRoutine(Transform target)
    {
        IsThrowing = true;
        BombOnCooldown = true;
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

        IsThrowing = false;
        yield return StartCoroutine(RetreatRoutine());
        BombOnCooldown = false;
    }

    // ── Retreat — called by BTActionWitnessRetreat ─────────────
    public void StartRetreat()
    {
        if (IsRetreating) return;
        StartCoroutine(RetreatRoutine());
    }

    private IEnumerator RetreatRoutine()
    {
        IsRetreating = true;

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
        IsRetreating = false;
        GetComponentInChildren<EnemyVFXController>()?.StopPanic();
    }

    public void SetRitualColour(bool active)
    {
        if (_renderer != null)
            _renderer.material.color = active ? RitualColour : _originalColor;
    }

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
                _witnessBombData, _playerLayer, LayerMask.GetMask("Enemy"));
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