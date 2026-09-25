using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: Witness finds safest ritual site and runs to it.
/// Uses zone config (AreaZoneConfig.ritualSites) not global POIManager.
///
/// Smart pathing:
///   1. Query zone config for safest RitualSite (away from twins, reachable)
///   2. Sprint to site
///   3. Drop slowing bomb on escape path after moving BombDropDistance units
///   4. Perform ritual on arrival (calls StartRitual)
///
/// Returns Succeeded when arrived at ritual site.
/// Returns Failed if no ritual sites in zone or all occupied.
/// </summary>
public class BTActionWitnessRitualPath : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "WitnessRitualPath";

    private EnemyPOITracker _poiTracker;
    private RitualSitePOI _targetSite;
    private Vector3 _destination;

    private const float SprintMultiplier = 1.3f;
    private const float ArrivalDistance = 2.0f;

    protected override void OnEnter()
    {
        base.OnEnter();
        _poiTracker = _enemy?.GetComponent<EnemyPOITracker>();

        if (_poiTracker == null || !_poiTracker.HasRitualSites)
        {
            Debug.Log($"[WitnessRitualPath] {_enemy?.name} no ritual sites in zone");
            return;
        }

        // Get twin positions for safe path selection
        var threatPositions = GetTwinPositions();

        // Get safest site from zone config
        _targetSite = _poiTracker.GetSafestRitualSite(threatPositions);

        if (_targetSite == null)
        {
            Debug.Log($"[WitnessRitualPath] {_enemy?.name} no safe ritual site found");
            return;
        }

        var tracker = _enemy?.GetComponent<EnemyPOITracker>();
        _targetSite?.Vacate(tracker);

        _destination = _targetSite.transform.position;

        _enemy?.Movement.SetSpeed((_enemy.Data?.moveSpeed ?? 3.5f) * SprintMultiplier);
        Debug.Log($"[WitnessRitualPath] {_enemy?.name} → {_targetSite.name}");
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_targetSite == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        if (_enemy == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        float dist = Vector3.Distance(_enemy.transform.position, _destination);
        if (dist <= ArrivalDistance)
        {
            _enemy.Movement.Stop();
            Debug.Log($"[WitnessRitualPath] {_enemy?.name} arrived — starting ritual");

            // Mark the site occupied (was never called — GetSafestRitualSite skips IsOccupied, so this both
            // stops two Witnesses sharing one site AND fires the site's On_Occupy cue). Released in OnExit.Vacate.
            _targetSite?.Occupy(_poiTracker);

            // Start ritual at this site
            var witness = _enemy as WitnessEnemy;
            witness?.StartRitual();

            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        _enemy.Movement.SetSpeed((_enemy.Data?.moveSpeed ?? 3.5f) * SprintMultiplier);
        _enemy.Movement.MoveTowards(_destination);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();
        if (_enemy?.Data != null)
            _enemy.Movement.SetSpeed(_enemy.Data.moveSpeed);
        var tracker = _enemy?.GetComponent<EnemyPOITracker>();
        _targetSite?.Vacate(tracker);
    }

    private Vector3[] GetTwinPositions()
    {
        var players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        var positions = new System.Collections.Generic.List<Vector3>();
        foreach (var p in players)
        {
            if (p is SoulPlayer) continue;
            positions.Add(p.transform.position);
        }
        return positions.ToArray();
    }
}