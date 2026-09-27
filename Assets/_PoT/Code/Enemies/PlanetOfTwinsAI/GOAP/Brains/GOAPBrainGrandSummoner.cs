using CommonCore;
using UnityEngine;

/// <summary>
/// GOAP Brain: Grand Summoner
///
/// Goals on prefab:
///   GOAPGoalWander       — stays back, minimal direct engagement
///   GOAPGoalAttackTwin   — only if twin gets very close
///
/// Per-tick blackboard writes:
///   CommanderAlive    — synced to all soldiers
///   CommanderPosition — synced to all soldiers
///   TwinInDangerRange — true when twin within threat range
/// </summary>
public class GOAPBrainGrandSummoner : PoTGOAPBrainBase
{
    [SerializeField] private float _twinThreatRange = 6f;

    private GrandSummoner _commander;
    private ZoneEnemyTracker _zoneTracker;   // on the prefab; its HomeZone changes per spawn, the component doesn't

    protected override void OnConfigureBrain()
    {
        _commander = GetComponent<GrandSummoner>();
        if (_commander == null)
            Debug.LogError("[GOAPBrainGrandSummoner] No GrandSummoner component.", this);
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