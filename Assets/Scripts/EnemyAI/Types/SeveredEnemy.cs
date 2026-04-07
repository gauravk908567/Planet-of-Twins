using System.Collections;
using UnityEngine;

/// <summary>
/// Severed — always spawns as a pair, one each side of the barrier.
///
/// LINKED HEALTH: 75% damage to self, 25% to partner.
/// DamageType.LinkedDamage bypasses the split to prevent infinite loops.
///
/// GRACE WINDOW: Partner dies → 1.2s window to kill survivor.
/// GRIEF RAGE: Grace missed → 8s rage → auto-die.
///
///
/// POOL SETUP:
///   Leave _partner EMPTY on prefab asset.
///   Spawner calls InitialisePair() on both instances after spawning.
///   SpawnZone.leftSpawnPoints[i] and rightSpawnPoints[i] are the two positions.
///   No barrier Transform needed — reach direction derived from partner position.
/// </summary>
public class SeveredEnemy : Enemy
{
    [Header("Severed — injected at spawn, leave empty on prefab")]
    [SerializeField] private SeveredEnemy _partner;

    private SeveredEnemyData _severedData;
    private bool _partnerDead;
    private bool _graceExpired;

    public SeveredGriefRageState RageState { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        Health.OnDeath += OnSeveredDied;
        Health.OnDamageTaken += HandleDamageTaken;
    }

    private void OnDestroy()
    {
        if (Health != null) Health.OnDamageTaken -= HandleDamageTaken;
    }

    /// <summary>
    /// Called by the spawner after both Severed instances are positioned.
    /// Wires the partner reference and starts the reach animation loop.
    /// Safe to call multiple times — stops previous coroutine first.
    /// </summary>
    public void InitialisePair(SeveredEnemy partner)
    {
        _partner = partner;
        _partnerDead = false;
        _graceExpired = false;
    }

    /// <summary>
    /// Called by partner when it detects a twin.
    /// Temporarily boosts this Severed's detection range so it finds
    /// the nearest twin on its own side even if normally out of range.
    /// </summary>
    /// <summary>
    /// Called when partner has detected a twin.
    /// Permanently boosts this Severed's detection range by 10x until it finds its own target.
    /// </summary>
    public void AlertFromPartner(Transform detectedTwin)
    {
        if (Target != null) return; // already has target, no boost needed
        StartCoroutine(BondedDetectionBoost());
    }

    private IEnumerator BondedDetectionBoost()
    {
        float originalRange = Detection?.DetectionRange ?? 8f;
        float boostedRange = originalRange * 10f;

        Detection?.SetRanges(boostedRange, Data?.possessedDetectionMultiplier ?? 0.7f);

        // Wait until target found or partner dies
        yield return new WaitUntil(() => Target != null || Health.IsDead || (_partner != null && _partner.Health.IsDead));

        Detection?.SetRanges(originalRange, Data?.possessedDetectionMultiplier ?? 0.7f);
    }

    /// <summary>Called by pool on return — clears partner so dormant instance is inert.</summary>
    public void Release()
    {
        StopAllCoroutines();
        _partner = null;
        _partnerDead = false;
        _graceExpired = false;
    }

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);
        if (data is SeveredEnemyData sd)
        {
            _severedData = sd;
            RageState = new SeveredGriefRageState(this, sd);
        }
    }

    protected override void InitStates()
    {
        base.InitStates();
        // RageState created after ApplyData
    }

    // ── Linked damage ──────────────────────────────────────────
    private void HandleDamageTaken(EnemyHealthComponent comp, float amount, Vector3 pos)
    {
        // Skip linked damage — prevents infinite loop between partners
        if (comp.LastDamageType == DamageType.LinkedDamage) return;
        if (_partner == null || _partner.Health.IsDead) return;

        float partnerAmount = amount * (1f - (_severedData?.directDamageFraction ?? 0.75f));
        if (partnerAmount <= 0f) return;

        _partner.Health?.TakeDamage(new DamageData(partnerAmount, DamageType.LinkedDamage));
    }

    // ── Bonded detection — alert partner when this Severed detects a twin ──
    // Called by EnemyIdleState when DetectTarget returns non-null.
    // We hook into ApplyData to override chase behaviour.
    // Simpler approach: override OnChaseAlert to propagate to partner.
    public new void OnChaseAlert(Transform target)
    {
        // Call base behaviour
        base.OnChaseAlert(target);

        // Alert partner so it chases nearest twin on its side
        if (_partner != null && !_partner.Health.IsDead)
            _partner.AlertFromPartner(target);
    }

    // ── Partner death ──────────────────────────────────────────
    private void OnSeveredDied()
    {
        if (_partner != null && !_partner.Health.IsDead)
            _partner.OnPartnerDied();
    }

    public void OnPartnerDied()
    {
        _partnerDead = true;
        StartCoroutine(GraceWindow());
    }

    private IEnumerator GraceWindow()
    {
        float duration = _severedData?.graceWindowDuration ?? 1.2f;
        yield return new WaitForSeconds(duration);

        if (!Health.IsDead)
        {
            _graceExpired = true;
            // If no target yet, boost detection range before entering rage
            if (Target == null)
                StartCoroutine(RageDetectionBoost());
            else
                EnterGriefRage();
        }
    }

    private IEnumerator RageDetectionBoost()
    {
        // Partner died and we still have no target — boost range and search
        float originalRange = Detection?.DetectionRange ?? 8f;
        float boostedRange = originalRange * 10f;
        Detection?.SetRanges(boostedRange, Data?.possessedDetectionMultiplier ?? 0.7f);

        // Wait up to 2s to find target, then enter rage regardless
        float elapsed = 0f;
        while (Target == null && elapsed < 2f)
        {
            elapsed += UnityEngine.Time.deltaTime;
            yield return null;
        }

        Detection?.SetRanges(originalRange, Data?.possessedDetectionMultiplier ?? 0.7f);
        EnterGriefRage();
    }

    private void EnterGriefRage()
    {
        if (RageState == null)
            RageState = new SeveredGriefRageState(this, _severedData);

        GetComponentInChildren<EnemyVFXController>()?.PlayRage();
        StateMachine.ChangeState(RageState);
    }

    protected override void HandleDeath()
    {
        StopAllCoroutines();
        base.HandleDeath();
    }
}