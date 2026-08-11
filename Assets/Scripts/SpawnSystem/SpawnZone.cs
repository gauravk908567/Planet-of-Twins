using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zone that activates when a player enters.
/// Holds handplaced scene references for spawn points, ritual sites, barriers.
/// AreaZoneConfig SO holds settings/config only — no scene refs.
/// </summary>
public class SpawnZone : MonoBehaviour
{
    [Header("Config")]
    public AreaZoneConfig areaConfig;

    [Header("Spawn Points — handplace in this area")]
    public Transform[] leftSpawnPoints;
    public Transform[] rightSpawnPoints;

    [Header("Phase 4 — POIs (handplace in scene)")]
    [Tooltip("Breakable spawn point POIs in this zone.")]
    public SpawnPointPOI[] spawnPoints;

    [Tooltip("Ritual sites available in this zone — Witness picks safest.")]
    public RitualSitePOI[] ritualSites;

    [Tooltip("Barrier segments in this zone — enemies gain DE when near any of these.")]
    public BarrierPOI[] barriers;

    [Header("Detection")]
    [Tooltip("Layer mask for players — zone activates when any player enters")]
    [SerializeField] private LayerMask playerLayer;

    // Notifies EnemySpawner
    public event System.Action<SpawnZone> OnZoneEntered;
    public event System.Action<SpawnZone> OnZoneExited;

    private readonly HashSet<GameObject> _playersInZone = new HashSet<GameObject>();

    // ── POI helpers ────────────────────────────────────────
    public bool HasSpawnPoints => spawnPoints != null && spawnPoints.Length > 0;
    public bool HasRitualSites => ritualSites != null && ritualSites.Length > 0;
    public bool HasBarriers => barriers != null && barriers.Length > 0;

    public SpawnPointPOI GetNearestSpawnPoint(Vector3 position)
    {
        if (!HasSpawnPoints) return null;
        SpawnPointPOI nearest = null;
        float minDist = float.MaxValue;
        foreach (var sp in spawnPoints)
        {
            if (sp == null || !sp.IsActive) continue;
            float d = Vector3.Distance(position, sp.transform.position);
            if (d < minDist) { minDist = d; nearest = sp; }
        }
        return nearest;
    }

    public bool IsNearBarrier(Vector3 position)
    {
        if (!HasBarriers) return false;
        foreach (var b in barriers)
        {
            if (b == null || !b.IsActive) continue;
            if (Vector3.Distance(position, b.transform.position) <= b.InfluenceRadius)
                return true;
        }
        return false;
    }

    public RitualSitePOI GetNearestRitualSite(Vector3 position)
    {
        if (!HasRitualSites) return null;
        RitualSitePOI nearest = null;
        float minDist = float.MaxValue;
        foreach (var rs in ritualSites)
        {
            if (rs == null || !rs.IsActive) continue;
            float d = Vector3.Distance(position, rs.transform.position);
            if (d < minDist) { minDist = d; nearest = rs; }
        }
        return nearest;
    }

    /// <summary>
    /// Find safest ritual site — furthest from all threat positions,
    /// reachable via NavMesh, not currently occupied.
    /// </summary>
    public RitualSitePOI GetSafestRitualSite(Vector3 fromPosition,
                                           Vector3[] threatPositions)
    {
        if (!HasRitualSites) return null;

        float minSafeDist = areaConfig?.ritualSiteSafeDistance ?? 8f;
        RitualSitePOI safest = null;
        RitualSitePOI fallback = null;
        float bestScore = -1f;
        float fallbackDist = float.MaxValue;

        foreach (var site in ritualSites)
        {
            Debug.Log($"[RitualSite] checking {site?.name} " +
              $"active={site?.IsActive} occupied={site?.IsOccupied}");
            if (site == null || !site.IsActive || site.IsOccupied) continue;

            Vector3 sitePos = site.transform.position;

            var path = new UnityEngine.AI.NavMeshPath();
            if (!UnityEngine.AI.NavMesh.CalculatePath(
                fromPosition, sitePos,
                UnityEngine.AI.NavMesh.AllAreas, path)) continue;
            if (path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) continue;

            // Track nearest reachable as fallback
            float selfDist = Vector3.Distance(fromPosition, sitePos);
            if (selfDist < fallbackDist) { fallbackDist = selfDist; fallback = site; }

            float minThreatDist = float.MaxValue;
            foreach (var threat in threatPositions)
            {
                float d = Vector3.Distance(sitePos, threat);
                if (d < minThreatDist) minThreatDist = d;
            }
            if (minThreatDist < minSafeDist) continue;

            float score = minThreatDist - selfDist * 0.3f;
            if (score > bestScore) { bestScore = score; safest = site; }
        }

        return safest ?? fallback; // fallback if no safe site found
    }

    // ── Self-registration (multi-scene) ────────────────────
    private void OnEnable()
    {
        SpawnZoneRegistry.Instance?.Register(this);
        if (SoftResetController.Instance != null)
            SoftResetController.Instance.OnSoftReset += ClearOccupants;
    }

    private void OnDisable()
    {
        SpawnZoneRegistry.Instance?.Unregister(this);
        if (SoftResetController.Instance != null)
            SoftResetController.Instance.OnSoftReset -= ClearOccupants;
    }

    // Clears zone occupancy — called only on soft reset (twins teleport, trigger exit never fires)
    // or when this zone's area scene unloads (via EnemySpawner.DespawnZone).
    public void ClearOccupants() => _playersInZone.Clear();

    // ── Zone detection ─────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        // Ignore non-player colliders (the old log fired for EVERYTHING, before this layer check).
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

        bool firstPlayer = _playersInZone.Add(other.gameObject) && _playersInZone.Count == 1;
        Debug.Log($"[SpawnZone] Player '{other.name}' ENTERED zone '{name}' " +
                  $"(playersInZone={_playersInZone.Count}, firstEntry={firstPlayer}, " +
                  $"config={(areaConfig != null ? areaConfig.name : "NULL — this zone will not spawn!")}).", this);

        if (firstPlayer)
            OnZoneEntered?.Invoke(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

        bool lastPlayer = _playersInZone.Remove(other.gameObject) && _playersInZone.Count == 0;
        Debug.Log($"[SpawnZone] Player '{other.name}' LEFT zone '{name}' " +
                  $"(playersInZone={_playersInZone.Count}, lastExit={lastPlayer}).", this);

        if (lastPlayer)
            OnZoneExited?.Invoke(this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
        var col = GetComponent<BoxCollider>();
        if (col != null)
            Gizmos.DrawCube(
                transform.position + col.center,
                Vector3.Scale(col.size, transform.lossyScale));

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.7f);
        if (leftSpawnPoints != null)
            foreach (var p in leftSpawnPoints)
                if (p != null) Gizmos.DrawWireSphere(p.position, 0.4f);

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.7f);
        if (rightSpawnPoints != null)
            foreach (var p in rightSpawnPoints)
                if (p != null) Gizmos.DrawWireSphere(p.position, 0.4f);

        // POI gizmos
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        if (spawnPoints != null)
            foreach (var sp in spawnPoints)
                if (sp != null) Gizmos.DrawWireSphere(sp.transform.position, 0.6f);

        Gizmos.color = new Color(0.5f, 0f, 1f, 0.5f);
        if (ritualSites != null)
            foreach (var rs in ritualSites)
                if (rs != null) Gizmos.DrawWireSphere(rs.transform.position, 0.8f);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        if (barriers != null)
            foreach (var b in barriers)
                if (b != null) Gizmos.DrawWireSphere(
                    b.transform.position, b.InfluenceRadius);
    }
}