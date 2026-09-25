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

    protected override void OnConfigureBrain()
    {
        _commander = GetComponent<PenitentCommander>();
        if (_commander == null)
            Debug.LogError("[GOAPBrainPenitentCommander] No PenitentCommander component.", this);
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

        foreach (var s in _commander.Soldiers)
        {
            if (s == null || s.Health.IsDead) continue;
            var brain = s.GetComponent<PoTGOAPBrainBase>();
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
        float zone = GetComponent<ZoneEnemyTracker>()
            ?.HomeZone?.areaConfig?.twinThreatRangeMultiplier ?? 1f;
        float range = _twinThreatRange * zone;
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p is SoulPlayer) continue;
            if (Vector3.Distance(transform.position, p.transform.position) <= range)
                return true;
        }
        return false;
    }
}