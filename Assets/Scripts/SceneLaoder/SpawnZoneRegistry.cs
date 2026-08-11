using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent singleton. SpawnZones self-register here on OnEnable/OnDisable,
/// so EnemySpawner (in Persistent) never needs a [SerializeField] link to
/// SpawnZones (which live in area scenes).
///
/// WIRING:
///   • Lives in Persistent.unity (under ------SYSTEM------).
///   • EnemySpawner subscribes to OnZoneRegistered / OnZoneUnregistered in its OnEnable.
///   • SpawnZone calls Register/Unregister in its OnEnable/OnDisable.
///
/// This replaces the broken [SerializeField] SpawnZone[] allZones pattern
/// that cannot cross scene boundaries.
/// </summary>
public class SpawnZoneRegistry : MonoBehaviour
{
    public static SpawnZoneRegistry Instance { get; private set; }

    private readonly List<SpawnZone> _zones = new();

    /// <summary>Fires on the frame a SpawnZone's scene loads and OnEnable runs.</summary>
    public event System.Action<SpawnZone> OnZoneRegistered;
    /// <summary>Fires when a SpawnZone is disabled/unloaded.</summary>
    public event System.Action<SpawnZone> OnZoneUnregistered;

    public IReadOnlyList<SpawnZone> RegisteredZones => _zones;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Register(SpawnZone zone)
    {
        if (zone == null || _zones.Contains(zone)) return;
        _zones.Add(zone);
        OnZoneRegistered?.Invoke(zone);
    }

    public void Unregister(SpawnZone zone)
    {
        if (zone == null || !_zones.Remove(zone)) return;
        OnZoneUnregistered?.Invoke(zone);
    }
}
