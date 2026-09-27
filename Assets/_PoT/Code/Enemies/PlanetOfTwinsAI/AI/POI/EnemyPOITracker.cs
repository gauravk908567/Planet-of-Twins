using UnityEngine;

/// <summary>
/// Per-enemy POI awareness — queries SpawnZone directly (not AreaZoneConfig).
/// SpawnZone holds all handplaced scene POI refs.
/// AreaZoneConfig holds config/settings only.
///
/// ATTACH: To every enemy prefab.
/// </summary>
public class EnemyPOITracker : MonoBehaviour, IEnemyReuseReset
{
    [SerializeField] private float _updateInterval = 1f;

    private Enemy _enemy;
    private EnemyDarkEnergy _darkEnergy;
    private ZoneEnemyTracker _zoneTracker;
    private float _timer;

    // ── Cached state ───────────────────────────────────────
    public SpawnPointPOI NearestSpawnPoint { get; private set; }
    public RitualSitePOI NearestRitualSite { get; private set; }
    public bool NearBarrier { get; private set; }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _darkEnergy = GetComponent<EnemyDarkEnergy>();
        _zoneTracker = GetComponent<ZoneEnemyTracker>();
    }

    // BUG-135 — drop the POIs cached from the previous life's zone (possibly an unloaded area).
    public void ResetForReuse()
    {
        _timer = 0f;
        NearestSpawnPoint = null;
        NearestRitualSite = null;
        NearBarrier = false;
    }

    private void Update()
    {
        if (_enemy == null || _enemy.Health.IsDead) return;

        _timer += Time.deltaTime;
        if (_timer < _updateInterval) return;
        _timer = 0f;

        UpdatePOICache();
        TickBarrierEnergy();
    }

    private SpawnZone GetHomeZone()
    {
        var zone = _zoneTracker?.HomeZone;
        if (zone != null) return zone;

        // Fallback: the nearest loaded SpawnZone, for an enemy with no home zone (a GDV2 bench spawn, a Witness-summoned
        // ally). Read from SpawnZoneRegistry (R5), not a scene search: this runs every POI update for each such enemy
        // (P8.7 audit). Same set: zones live only in area scenes, which load after Persistent's registry.
        var registry = SpawnZoneRegistry.Instance;
        if (registry == null) return null;
        var zones = registry.RegisteredZones;
        SpawnZone nearest = null;
        float minDist = float.MaxValue;
        for (int i = 0; i < zones.Count; i++)   // indexed: no enumerator garbage
        {
            var z = zones[i];
            if (z == null) continue;
            float d = Vector3.Distance(transform.position, z.transform.position);
            if (d < minDist) { minDist = d; nearest = z; }
        }
        return nearest;
    }

    private void UpdatePOICache()
    {
        var zone = GetHomeZone();
        //Debug.Log($"[POITracker] {_enemy?.name} zone={zone?.name} " +
        //          $"hasRitual={zone?.HasRitualSites} " +
        //          $"hasSites={zone?.ritualSites?.Length} " +
        //          $"nearBarrier={NearBarrier}");
        if (zone == null) return;

        Vector3 pos = transform.position;

        NearestSpawnPoint = zone.GetNearestSpawnPoint(pos);
        NearestRitualSite = zone.GetNearestRitualSite(pos);
        NearBarrier = zone.IsNearBarrier(pos);
    }

    private void TickBarrierEnergy()
    {
        if (NearBarrier && _darkEnergy != null)
            _darkEnergy.OnNearBarrier();
    }

    public RitualSitePOI GetSafestRitualSite(Vector3[] threatPositions)
    {
        var zone = GetHomeZone();
        if (zone == null) return null;
        return zone.GetSafestRitualSite(transform.position, threatPositions);
    }

    public bool HasRitualSites => GetHomeZone()?.HasRitualSites ?? false;
}