using System.Collections;
using UnityEngine;

/// <summary>
/// Severed — always spawns as a pair, one each side of the barrier.
///
/// LINKED HEALTH: 75% damage to self, 25% to partner.
/// GRACE WINDOW: Partner dies → 1.2s window to kill survivor.
/// GRIEF RAGE: Grace missed → 8s rage → faster attacks → auto-die.
///
/// CHANGES FROM OLD VERSION:
/// - Removed InitStates override
/// - Added public PartnerDead and IsInGriefRage for Blackboard sync
/// - EnterGriefRage now applies attack cooldown multiplier directly
/// - RageDetectionBoost now injects detection via PerceptionManager
/// </summary>
public class SeveredEnemy : Enemy
{
    // ── VFX cue (EnemyVfxLibrary, R4) ──
    public override CueBookData VfxBook => VfxLibraryProvider.Instance?.Enemy?.Severed;
    protected override string MeleeAttackCueId => FxIds.Enemy.Severed.On_SevererdAttack;

    [Header("Severed — injected at spawn, leave empty on prefab")]
    [SerializeField] private SeveredEnemy _partner;

    private SeveredEnemyData _severedData;
    private bool _partnerDead;
    private bool _graceExpired;
    private bool _isInGriefRage;

    // Read by GOAPBrainSeveredEnemy to sync Blackboard
    public bool PartnerDead => _partnerDead;
    public bool IsInGriefRage => _isInGriefRage;
    public SeveredEnemyData SeveredData => _severedData;

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

    public void InitialisePair(SeveredEnemy partner)
    {
        _partner = partner;
        _partnerDead = false;
        _graceExpired = false;
        _isInGriefRage = false;
    }
    private void Update()
    {
        if (_isInGriefRage && !Health.IsDead)
            FactionEnergySystem.Instance?.OnSeveredGrieving();
    }
    public void AlertFromPartner(Transform detectedTwin)
    {
        if (Target != null) return;
        // Inject detection so PerceptionListener picks up the twin
        var perceivable = detectedTwin?.GetComponent<CommonCore.Perceivable>();
        if (perceivable != null)
            CommonCore.PerceptionManager.Instance?.InjectDetection(
                perceivable, typeof(CommonCore.ProximitySensor), 1f);
    }

    public void Release()
    {
        StopAllCoroutines();
        _partner = null;
        _partnerDead = false;
        _graceExpired = false;
        _isInGriefRage = false;
    }

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);
        if (data is SeveredEnemyData sd)
            _severedData = sd;
    }

    // ── Linked damage ──────────────────────────────────────────
    private void HandleDamageTaken(EnemyHealthComponent comp, float amount, Vector3 pos)
    {
        if (comp.LastDamageType == DamageType.LinkedDamage) return;
        if (_partner == null || _partner.Health.IsDead) return;

        float partnerAmount = amount * (1f - (_severedData?.directDamageFraction ?? 0.75f));
        if (partnerAmount <= 0f) return;

        _partner.Health?.TakeDamage(new DamageData(partnerAmount, DamageType.LinkedDamage));
    }

    public new void OnChaseAlert(Transform target)
    {
        base.OnChaseAlert(target);
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
            if (Target == null)
                StartCoroutine(RageDetectionBoost());
            else
                EnterGriefRage();
        }
    }

    private IEnumerator RageDetectionBoost()
    {
        float elapsed = 0f;
        while (Target == null && elapsed < 2f)
        {
            elapsed += UnityEngine.Time.deltaTime;
            yield return null;
        }
        EnterGriefRage();
    }

    private void EnterGriefRage()
    {
        _isInGriefRage = true;
        MoodEventBus.AllyEnteredRage(gameObject);
        // Apply rage attack speed — GOAP brain reads IsInGriefRage from Blackboard
        // and GoalGriefRage fires at Critical priority
        float mult = _severedData?.rageCooldownMultiplier ?? 0.4f;
        AttackController.SetAttackSlowdown(mult);

        // Rage-entry burst (Severed book) — fires once when the survivor reaches grief rage after its pair dies.
        PlayCue(FxIds.Enemy.Severed.On_SeveredRage, CueContext.Follow(transform));
 // Sustained rage AURA is now Manpu-driven: partner death already drives this survivor into the
        // Grieving→Enraged mood via EnemySocialBond, and the Enraged vocabulary loopPrefab is the held aura —
        // no parallel EnemyVFXController call needed.

        // Auto-die after rage duration
        StartCoroutine(RageExpiry());
    }

    private IEnumerator RageExpiry()
    {
        yield return new UnityEngine.WaitForSeconds(_severedData?.rageDuration ?? 8f);
        if (!Health.IsDead)
            Health.TakeDamage(new DamageData(9999f, DamageType.Environmental));
    }

    protected override void HandleDeath()
    {
        StopAllCoroutines();
        base.HandleDeath();
    }
}