using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry for all Points of Interest.
/// Enemies query for nearest POI of a given type.
/// 
/// ATTACH: To AIManager GO alongside FactionEnergySystem, SoundEventSystem.
/// </summary>
public class POIManager : MonoBehaviour
{
    public static POIManager Instance { get; private set; }

    private readonly List<POIBase> _pois = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Registration ───────────────────────────────────────
    public void Register(POIBase poi)
    {
        if (!_pois.Contains(poi))
            _pois.Add(poi);
    }

    public void Unregister(POIBase poi)
        => _pois.Remove(poi);

    // ── Queries ────────────────────────────────────────────

    /// <summary>Find nearest active POI of given type to position.</summary>
    public POIBase GetNearest(Vector3 position, POIType type, bool activeOnly = true)
    {
        POIBase nearest = null;
        float minDist = float.MaxValue;

        foreach (var poi in _pois)
        {
            if (poi == null) continue;
            if (poi.PoiType != type) continue;
            if (activeOnly && !poi.IsActive) continue;

            float d = Vector3.Distance(position, poi.transform.position);
            if (d < minDist) { minDist = d; nearest = poi; }
        }
        return nearest;
    }

    /// <summary>
    /// Find safest POI of given type — furthest from all twins
    /// while still reachable via NavMesh.
    /// Used by Witness for ritual site selection.
    /// </summary>
    public POIBase GetSafest(Vector3 fromPosition, POIType type,
                              Vector3[] threatPositions,
                              float minDistFromThreats = 8f)
    {
        POIBase safest = null;
        float bestScore = -1f;

        foreach (var poi in _pois)
        {
            if (poi == null || !poi.IsActive || poi.PoiType != type) continue;

            Vector3 poiPos = poi.transform.position;

            // Check reachable via NavMesh
            var path = new UnityEngine.AI.NavMeshPath();
            if (!UnityEngine.AI.NavMesh.CalculatePath(
                fromPosition, poiPos,
                UnityEngine.AI.NavMesh.AllAreas, path)) continue;
            if (path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) continue;

            // Score = min distance from all threats
            // Higher = safer
            float minThreatDist = float.MaxValue;
            foreach (var threat in threatPositions)
            {
                float d = Vector3.Distance(poiPos, threat);
                if (d < minThreatDist) minThreatDist = d;
            }

            // Must be at least minDistFromThreats away
            if (minThreatDist < minDistFromThreats) continue;

            // Also prefer closer to self (less travel time)
            float selfDist = Vector3.Distance(fromPosition, poiPos);
            float score = minThreatDist - selfDist * 0.3f;

            if (score > bestScore) { bestScore = score; safest = poi; }
        }
        return safest;
    }

    /// <summary>Get all active POIs of given type within radius.</summary>
    public List<POIBase> GetAllInRadius(Vector3 position, POIType type, float radius)
    {
        var result = new List<POIBase>();
        foreach (var poi in _pois)
        {
            if (poi == null || !poi.IsActive || poi.PoiType != type) continue;
            if (Vector3.Distance(position, poi.transform.position) <= radius)
                result.Add(poi);
        }
        return result;
    }

    public int CountActive(POIType type)
    {
        int count = 0;
        foreach (var poi in _pois)
            if (poi != null && poi.IsActive && poi.PoiType == type) count++;
        return count;
    }
}