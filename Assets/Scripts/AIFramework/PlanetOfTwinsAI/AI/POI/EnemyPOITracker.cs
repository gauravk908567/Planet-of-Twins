using UnityEngine;

/// <summary>
/// Per-enemy POI awareness — queries SpawnZone directly (not AreaZoneConfig).
/// SpawnZone holds all handplaced scene POI refs.
/// AreaZoneConfig holds config/settings only.
///
/// ATTACH: To every enemy prefab.
/// </summary>
public class EnemyPOITracker : MonoBehaviour
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

        // Fallback — find nearest SpawnZone in scene
        // (for manually placed enemies not spawned by EnemySpawner)
        var allZones = Object.FindObjectsByType<SpawnZone>(FindObjectsSortMode.None);
        SpawnZone nearest = null;
        float minDist = float.MaxValue;
        foreach (var z in allZones)
        {
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
        NearestRitualSite = GetNearestRitualSite(zone, pos);
        NearBarrier = zone.IsNearBarrier(pos);
    }

    private RitualSitePOI GetNearestRitualSite(SpawnZone zone, Vector3 pos)
    {
        if (!zone.HasRitualSites) return null;
        RitualSitePOI nearest = null;
        float minDist = float.MaxValue;
        foreach (var site in zone.ritualSites)
        {
            if (site == null || !site.IsActive) continue;
            float d = Vector3.Distance(pos, site.transform.position);
            if (d < minDist) { minDist = d; nearest = site; }
        }
        return nearest;
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