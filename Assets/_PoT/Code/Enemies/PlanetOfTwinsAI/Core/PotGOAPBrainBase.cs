using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Abstract base for all Planet of Twins GOAP enemy brains.
/// Reads Enemy.IsBrainPaused to skip ticking during stun/fear/grab.
///
/// Phase 6D — ClanWar state and TwinInDangerRange are wired directly in
/// Update() (not OnPreTickBrain) so they sync regardless of whether child
/// brains call base.OnPreTickBrain. Cheap per-tick: one shared BB read,
/// two sqr-distance checks.
/// </summary>
public abstract class PoTGOAPBrainBase : GOAPBrainBase
{
    protected Enemy LinkedEnemy { get; private set; }

    protected override void ConfigureBlackboard()
    {
        LinkedBlackboard.Set(PoTNames.IsRescueActive, false);
        LinkedBlackboard.Set(PoTNames.HasRescueTarget, false);
        LinkedBlackboard.Set(PoTNames.AccordStateActive, false);
        LinkedBlackboard.Set(PoTNames.AccordBarFill, 0f);
        LinkedBlackboard.Set(PoTNames.SharedHealthNorm, 1f);
        LinkedBlackboard.Set(PoTNames.SoulIsActive, false);
        LinkedBlackboard.Set(PoTNames.SoulPosition, CommonCore.Constants.InvalidVector3Position);
        LinkedBlackboard.Set(PoTNames.SpawnUnderAttack, false);
        LinkedBlackboard.Set(PoTNames.BarrierWidening, false);
        LinkedBlackboard.Set(PoTNames.DarkEnergyAvailable, false);
        LinkedBlackboard.Set(PoTNames.EnemyHealthNorm, 1f);
        LinkedBlackboard.Set(PoTNames.EnemyIsPossessed, false);
        LinkedBlackboard.Set(PoTNames.EnemyIsStunned, false);
        LinkedBlackboard.Set(PoTNames.EnemyIsGrabbing, false);
        LinkedBlackboard.Set(PoTNames.BombOnCooldown, false);
        LinkedBlackboard.Set(PoTNames.ReflectionActive, false);
        LinkedBlackboard.Set(PoTNames.EnemyAllyAlive, false);

        // ── Phase 6D ──────────────────────────────────────────
        LinkedBlackboard.Set(PoTNames.ClanWarActive, false);
        LinkedBlackboard.Set(PoTNames.ClanWarIntensity, 0f);
        LinkedBlackboard.Set(PoTNames.TwinInDangerRange, false);

        OnConfigureBlackboard();
    }

    protected override void ConfigureBrain()
    {
        LinkedEnemy = GetComponent<Enemy>();

        if (LinkedEnemy == null)
            Debug.LogError($"[PoTGOAPBrainBase] No Enemy on {gameObject.name}.", this);

        OnConfigureBrain();
    }

    protected virtual void OnConfigureBlackboard() { }
    protected virtual void OnConfigureBrain() { }

    protected override void OnPreTickBrain(float InDeltaTime)
    {
        if (LinkedEnemy != null && LinkedEnemy.IsBrainPaused)
        {
            // Cancel tick entirely by making all goals invalid this frame
            // We do this by returning early — but GOAPBrainBase still ticks
            // So instead we use OnPostTickBrain pattern — see below
            return;
        }

        SyncEnemyStateToBlackboard();
        SyncSharedStateToBlackboard();
    }

    private void SyncEnemyStateToBlackboard()
    {
        if (LinkedEnemy == null) return;

        LinkedBlackboard.Set(PoTNames.EnemyHealthNorm,
            LinkedEnemy.Health.CurrentHealth / LinkedEnemy.Health.MaxHealth);
        LinkedBlackboard.Set(PoTNames.EnemyIsPossessed, LinkedEnemy.IsPossessed);
        LinkedBlackboard.Set(PoTNames.EnemyIsStunned, LinkedEnemy.IsStunned);
    }

    private void SyncSharedStateToBlackboard()
    {
        var shared = BlackboardManager.GetSharedBlackboard(PoTNames.SharedBlackboardID);
        if (shared == null) return;

        bool val = false; float fval = 1f;

        shared.TryGet(PoTNames.IsRescueActive, out val, false); LinkedBlackboard.Set(PoTNames.IsRescueActive, val);
        shared.TryGet(PoTNames.AccordStateActive, out val, false); LinkedBlackboard.Set(PoTNames.AccordStateActive, val);
        shared.TryGet(PoTNames.SharedHealthNorm, out fval, 1f); LinkedBlackboard.Set(PoTNames.SharedHealthNorm, fval);
        shared.TryGet(PoTNames.SpawnUnderAttack, out val, false); LinkedBlackboard.Set(PoTNames.SpawnUnderAttack, val);
        shared.TryGet(PoTNames.SoulIsActive, out val, false); LinkedBlackboard.Set(PoTNames.SoulIsActive, val);
        shared.TryGet(PoTNames.DarkEnergyAvailable, out val, false); LinkedBlackboard.Set(PoTNames.DarkEnergyAvailable, val);
    }

    // Overrides GOAPBrainBase.Update()
    private void Update()
    {
        if (LinkedEnemy != null && LinkedEnemy.IsBrainPaused)
            return; // skip entire GOAP tick while stunned/feared/grabbed

        // ── Phase 6D — wired in Update, not OnPreTickBrain.
        // These run for every brain regardless of whether children call base.OnPreTickBrain.
        SyncClanWarToLocalBlackboard();
        SyncTwinInDangerRange();

        LinkedBlackboard?.Set(CommonCore.Names.CurrentLocation, transform.position);
        TickBrain(Time.deltaTime);
    }

    /// <summary>
    /// Phase 6D — mirror shared ClanWar state onto the per-enemy blackboard
    /// so local GOAP goals (GOAPGoalAttackEnemy) can read without re-fetching
    /// the shared board every goal evaluation.
    /// </summary>
    private void SyncClanWarToLocalBlackboard()
    {
        var shared = BlackboardManager.GetSharedBlackboard(PoTNames.SharedBlackboardID);
        if (shared == null) return;

        bool clanWarActive = false;
        float clanWarIntensity = 0f;
        shared.TryGet(PoTNames.ClanWarActive, out clanWarActive, false);
        shared.TryGet(PoTNames.ClanWarIntensity, out clanWarIntensity, 0f);

        LinkedBlackboard.Set(PoTNames.ClanWarActive, clanWarActive);
        LinkedBlackboard.Set(PoTNames.ClanWarIntensity, clanWarIntensity);
    }

    /// <summary>
    /// Phase 6D — compute per-enemy TwinInDangerRange each tick.
    /// True if either twin is within Data.twinThreatRange (sqr-compared).
    /// Consumed by GOAPGoalAttackEnemy as a hard gate — twin in range = yield to
    /// GOAPGoalAttackTwin. When twinThreatRange <= 0 the flag is forced false
    /// (designer-disabled threat switching).
    /// </summary>
    private void SyncTwinInDangerRange()
    {
        if (LinkedEnemy == null || LinkedEnemy.Data == null)
        {
            LinkedBlackboard.Set(PoTNames.TwinInDangerRange, false);
            return;
        }

        float threatRange = LinkedEnemy.Data.twinThreatRange;
        if (threatRange <= 0f)
        {
            LinkedBlackboard.Set(PoTNames.TwinInDangerRange, false);
            return;
        }

        float sqrThreat = threatRange * threatRange;
        Vector3 myPos = transform.position;
        bool inRange = false;

        var left = TwinAnticipator.Left;
        if (left != null)
        {
            if ((left.transform.position - myPos).sqrMagnitude <= sqrThreat)
                inRange = true;
        }

        if (!inRange)
        {
            var right = TwinAnticipator.Right;
            if (right != null)
            {
                if ((right.transform.position - myPos).sqrMagnitude <= sqrThreat)
                    inRange = true;
            }
        }

        LinkedBlackboard.Set(PoTNames.TwinInDangerRange, inRange);
    }
}