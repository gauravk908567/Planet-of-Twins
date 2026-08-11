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

    protected override void OnConfigureBrain()
    {
        _commander = GetComponent<GrandSummoner>();
        if (_commander == null)
            Debug.LogError("[GOAPBrainGrandSummoner] No GrandSummoner component.", this);
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