using CommonCore;
using UnityEngine;

/// <summary>
/// GOAP Brain for SiphonGhost.
///
/// Lightweight brain � no perception sensor, no NavMesh, no dark energy by default.
/// All those components are plug-and-play on the prefab if needed later.
///
/// Goals (Components on prefab):
///   GOAPGoalGhostPursuit  � High (75)  � chase soul when not binding
///   GOAPGoalGhostBind     � Critical (90) � bind when soul in range
///
/// Actions (Components on prefab):
///   GOAPActionGhostPursuit
///   GOAPActionGhostBind
///
/// Syncs ghost state to Blackboard each frame for goals to read.
/// </summary>
public class GOAPBrainSiphonGhost : PoTGOAPBrainBase
{
    private SiphonGhost _ghost;

    protected override void OnConfigureBrain()
    {
        _ghost = GetComponent<SiphonGhost>();
        if (_ghost == null)
            Debug.LogError("[GOAPBrainSiphonGhost] No SiphonGhost component.", this);
    }

    protected override void OnConfigureBlackboard()
    {
        LinkedBlackboard.Set(PoTNames.GhostIsBinding, false);
        LinkedBlackboard.Set(PoTNames.GhostIsRetreating, false);
        LinkedBlackboard.Set(PoTNames.GhostKillWindow, true);
        LinkedBlackboard.Set(PoTNames.GhostSoulInRange, false);
    }

    protected override void OnPreTickBrain(float InDeltaTime)
    {
        if (_ghost == null || _ghost.IsResolved) return;

        LinkedBlackboard.Set(PoTNames.GhostIsBinding, _ghost.IsBinding);
        LinkedBlackboard.Set(PoTNames.GhostIsRetreating, _ghost.IsRetreating);
        LinkedBlackboard.Set(PoTNames.GhostKillWindow, _ghost.KillWindowOpen);

        // Soul in THROW range (the ghost binds by throwing a chain, not by contact)
        bool soulInRange = false;
        if (_ghost.Soul != null)
        {
            float throwRange = _ghost.GhostData?.ghostThrowRange ?? 2.5f;
            float dist = Vector3.Distance(transform.position, _ghost.Soul.transform.position);
            soulInRange = dist <= throwRange && !_ghost.IsBinding && !_ghost.IsRetreating && !_ghost.IsThrowing;
        }
        LinkedBlackboard.Set(PoTNames.GhostSoulInRange, soulInRange);
    }
}