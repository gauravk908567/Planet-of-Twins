using System.Collections;
using UnityEngine;

/// <summary>
/// Witness — support enemy. Buffs allies, shadows summoned ally, rituals to resummon.
/// Buff aura runs in Update — always active regardless of GOAP state.
/// All behaviour decisions driven by GOAP+BT.
/// </summary>
public class WitnessEnemy : Enemy
{
    // ── VFX cue (EnemyVfxLibrary, R4) — Witness has no basic melee/ranged (it throws bombs), but the aura
    //    and ritual beats play out of its own book; ThrowBomb/HandleDeath still resolve the same slot directly. ──
    public override CueBookData VfxBook => VfxLibraryProvider.Instance?.Enemy?.Witness;

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
    private CueHandle _auraHandle;   // HELD aura visual (Witness book) — on while ≥1 ally is in the field

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
 // Ally-buff visual is the Common on_AlliesBuff cue (self-limited); no EnemyVFXController stop.
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
            {
                // Shared "ally buffed" cue (Common book) on the newly-buffed enemy — the sole buff visual now
 // (: the parallel EnemyVFXController.PlayBuff is retired; buff is not a mood, on_AlliesBuff covers it).
                CommonFx.Play(FxIds.Common.Effects.on_AlliesBuff, CueContext.Follow(enemy.transform));
            }
        }

        _lastBuffed = newBuffed.ToArray();

        // Held aura visual (Witness book): on while ≥1 ally is in the field, off when the field empties. Rides the
        // Witness. IsPlaying-guarded so a self-ending cue re-arms while allies remain (a looping cue never re-fires).
        bool auraActive = newBuffed.Count > 0;
        if (auraActive)
        {
            if (_auraHandle.IsNone || FxManager.Instance == null || !FxManager.Instance.IsPlaying(_auraHandle))
                _auraHandle = PlayCue(FxIds.Enemy.Witness.On_WitnessAura, CueContext.Follow(transform, owner: this));
        }
        else if (!_auraHandle.IsNone)
        {
            StopAura();
        }
    }

    // Stop the held aura visual (idempotent; a stale handle is inert).
    private void StopAura()
    {
        if (_auraHandle.IsNone) return;
        FxManager.Instance?.Stop(_auraHandle);
        _auraHandle = CueHandle.None;
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

        // P16: the minion is a full Enemy — it goes through EnemyPool's canonical spawn
        // (Get→Warp→SetPoolProvider→ITimeAffected→deathNotifier), NOT a raw Instantiate, so it
        // gets on_enemyspawn, Setsuna participation, kill cues and pooled reuse like every enemy.
        var pool = EnemyPool.Instance;
        if (pool == null) { Debug.LogError("[Witness] EnemyPool.Instance unresolved — summon skipped.", this); return; }
        // playSpawnCue:false — a ritual-summoned ally appears because of the Witness's ritual/circle;
        // the generic on_enemyspawn effect would overlap it (and read as a wrong "witness spawn effect").
        var go = pool.SpawnReady(_meleePrefab, spawnPos, Quaternion.identity, playSpawnCue: false);
        var ally = go != null ? go.GetComponent<Enemy>() : null;
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

        // Ritual circle (Witness book) — held for the whole channel; the finally stops it on completion,
        // on interrupt (yield break), or if the coroutine is disposed (Witness dies / disabled mid-ritual).
        CueHandle ritualCue = PlayCue(FxIds.Enemy.Witness.On_WitnessRitualStart,
                                      CueContext.Follow(transform, owner: this));
        try
        {
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

            // Linger the ritual circle briefly after the ally lands so the player reads WHOSE power
            // spawned it (user call, playtest round 3). Success path only — interrupt (yield break)
            // and death/disable still stop the cue immediately via the finally. Scaled time.
            yield return new WaitForSeconds(0.75f);
        }
        finally
        {
            FxManager.Instance?.Stop(ritualCue);
        }
    }

    // ── Bomb — called by BTActionThrowBomb ─────────────────────
    public bool CanThrowBomb => !BombOnCooldown && !IsThrowing && !IsRetreating
                                && _bombPrefab != null && _witnessBombData != null;

    public void ThrowBomb(Transform target)
    {
        // Fail loud on broken authoring — a rebuilt bomb prefab silently nulled this slot once
        // (fileID reference into the old prefab; BUG-053) and CanThrowBomb just returned false.
        if (_bombPrefab == null || _witnessBombData == null)
        {
            Debug.LogError("[Witness] _bombPrefab/_witnessBombData unassigned — bomb throw impossible.", this);
            return;
        }
        if (!CanThrowBomb) return;
        StartCoroutine(BombThrowRoutine(target));
    }

    private IEnumerator BombThrowRoutine(Transform target)
    {
        IsThrowing = true;
        BombOnCooldown = true;
 // panic tell is Manpu-driven: enter Panicked so the Panicked vocabulary loopPrefab holds the
        // aura through the bomb throw + retreat. Ended (stomp-safe) at the end of RetreatRoutine.
        GetComponent<EnemyMoodSystem>()?.TransitionTo(EnemyMood.Panicked, 0f, EnemyMood.Normal);

        Vector3 dir = (target.position - transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);

        yield return new WaitForSeconds(_witnessData?.bombWindUpDuration ?? 0.75f);

        if (!Health.IsDead)
        {
            Vector3 spawnPos = _bombMuzzle != null ? _bombMuzzle.position : transform.position;
            Vector3 targetPos = target.position;
            targetPos.y = spawnPos.y;

            RegisterPooledPrefab(_bombPrefab, PoolCategory.Projectiles);   // refcounted warm pool, trimmed after death
            var go = GameplayPool.Spawn(_bombPrefab, PoolCategory.Projectiles, spawnPos, Quaternion.identity);
            var bomb = go != null ? go.GetComponent<BombProjectile>() : null;
            bomb?.ConfigureCues(VfxLibraryProvider.Instance?.Enemy?.WitnessBomb,
                FxIds.Enemy.WitnessBomb.On_WitnessBombFuseOn, FxIds.Enemy.WitnessBomb.On_WitnessBombExplode);
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
 // end the panic aura by leaving Panicked, but only if still Panicked (stomp-safe: a mood the
        // Witness legitimately entered meanwhile — wounded, etc. — is not clobbered).
        var mood = GetComponent<EnemyMoodSystem>();
        if (mood != null && mood.CurrentMood == EnemyMood.Panicked)
            mood.TransitionTo(EnemyMood.Normal, 0f, EnemyMood.Normal);
    }

    public void SetRitualColour(bool active)
    {
        if (_renderer != null)
            MaterialTint.SetColor(_renderer.material, active ? RitualColour : _originalColor);
    }

    protected override void HandleDeath()
    {
        GetComponent<WitnessAuraVFX>()?.StopAll();
        StopAura();                         // stop the held Witness-book aura visual
        ClearBuffs(_lastBuffed);

        if (_bombPrefab != null && _witnessBombData != null)
        {
            Vector3 spawnPos = _bombMuzzle != null ? _bombMuzzle.position : transform.position;
            RegisterPooledPrefab(_bombPrefab, PoolCategory.Projectiles);   // refcounted warm pool, trimmed after death
            var go = GameplayPool.Spawn(_bombPrefab, PoolCategory.Projectiles, spawnPos, Quaternion.identity);
            var bomb = go != null ? go.GetComponent<BombProjectile>() : null;
            bomb?.ConfigureCues(VfxLibraryProvider.Instance?.Enemy?.WitnessBomb,
                FxIds.Enemy.WitnessBomb.On_WitnessBombFuseOn, FxIds.Enemy.WitnessBomb.On_WitnessBombExplode);
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