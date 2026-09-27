using CommonCore;
using UnityEngine;

/// <summary>
/// GOAP Brain: Penitent Commander
///
/// Goals on prefab:
///   GOAPGoalAttackTwin  — engages twins with arrogant personality
///   GOAPGoalWander      — slow patrol when idle
///
/// Per-tick blackboard writes:
///   CommanderAlive    — synced to all soldiers
///   CommanderPosition — synced to all soldiers
///   TwinInDangerRange — true when twin within threat range
///
/// Also wires soldier damage notification so DarkShield fires correctly.
/// Call WireSoldierDamage() from EnemySpawner after RegisterSoldier().
/// </summary>
public class GOAPBrainPenitentCommander : PoTGOAPBrainBase
{
    [SerializeField] private float _twinThreatRange = 12f;

    private PenitentCommander _commander;
    private ZoneEnemyTracker _zoneTracker;   // on the prefab; its HomeZone changes per spawn, the component doesn't

    protected override void OnConfigureBrain()
    {
        _commander = GetComponent<PenitentCommander>();
        if (_commander == null)
            Debug.LogError("[GOAPBrainPenitentCommander] No PenitentCommander component.", this);
        _zoneTracker = GetComponent<ZoneEnemyTracker>();
    }

    protected override void OnConfigureBlackboard()
    {
        LinkedBlackboard.Set(PoTNames.CommanderAlive, true);
        LinkedBlackboard.Set(PoTNames.CommanderPosition, Vector3.zero);
        LinkedBlackboard.Set(PoTNames.TwinInDangerRange, false);
    }

    protected override void OnPreTickBrain(float InDeltaTime)
    {
        if (_commander == null) return;

        LinkedBlackboard.Set(PoTNames.CommanderAlive, _commander.IsAlive);
        LinkedBlackboard.Set(PoTNames.CommanderPosition, transform.position);

        var soldiers = _commander.Soldiers;
        for (int i = 0; i < soldiers.Count; i++)   // indexed: a foreach over the interface allocates every tick
        {
            var s = soldiers[i];
            if (s == null || s.Health.IsDead) continue;
            var brain = s.Brain;
            if (brain?.LinkedBlackboard == null) continue;
            brain.LinkedBlackboard.Set(PoTNames.CommanderAlive, _commander.IsAlive);
            brain.LinkedBlackboard.Set(PoTNames.CommanderPosition, transform.position);
        }

        LinkedBlackboard.Set(PoTNames.TwinInDangerRange, IsTwinInRange());
    }

    /// <summary>
    /// Called by EnemySpawner after each soldier is registered.
    /// Subscribes to soldier health so DarkShield fires on big hits.
    /// </summary>
    public void WireSoldierDamage(Enemy soldier)
    {
        if (soldier == null || _commander == null) return;
        float maxHP = soldier.Health.MaxHealth;
        soldier.Health.OnDamageTaken += (comp, amount, pos) =>
            _commander.NotifyDamageTaken(amount, maxHP);
    }

    private bool IsTwinInRange()
    {
        float zone = _zoneTracker
            ?.HomeZone?.areaConfig?.twinThreatRangeMultiplier ?? 1f;
        float range = _twinThreatRange * zone;
        foreach (var p in PlayerRoster.Twins)
        {
            if (Vector3.Distance(transform.position, p.transform.position) <= range)
                return true;
        }
        return false;
    }
}