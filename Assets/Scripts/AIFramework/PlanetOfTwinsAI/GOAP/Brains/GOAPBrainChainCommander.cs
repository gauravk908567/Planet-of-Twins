using CommonCore;
using UnityEngine;

/// <summary>
/// GOAP Brain: Chain Commander
///
/// Goals on prefab:
///   GOAPGoalAttackTwin   — engages twins when in range
///   GOAPGoalWander       — hangs back when idle
///
/// Per-tick blackboard writes:
///   CommanderAlive    — synced to all soldiers
///   CommanderPosition — synced to all soldiers
///   TwinInDangerRange — true when twin within threat range
/// </summary>
public class GOAPBrainChainCommander : PoTGOAPBrainBase
{
    [SerializeField] private float _twinThreatRange = 10f;

    private ChainCommander _commander;

    protected override void OnConfigureBrain()
    {
        _commander = GetComponent<ChainCommander>();
        if (_commander == null)
            Debug.LogError("[GOAPBrainChainCommander] No ChainCommander component.", this);
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

        // Sync to all governed soldiers
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