using UnityEngine;

/// <summary>
/// Environmental and gameplay settings for a zone.
/// Split from AreaZoneConfig to keep it lean.
///
/// CREATE: Right-click → Create → PlanetOfTwins/Spawn/Zone Environment Config
/// </summary>
[CreateAssetMenu(fileName = "ZoneEnvironmentConfig",
                 menuName = "PlanetOfTwins/Spawn/Zone Environment Config")]
public class ZoneEnvironmentConfig : ScriptableObject
{
    [Header("Dark Energy")]
    public bool crossClanBondingUnlocked = false;
    [Range(0.5f, 2f)] public float comboPowerCeiling = 1.0f;
    [Range(0.5f, 3f)] public float darkEnergyGainMultiplier = 1.0f;

    [Header("POI")]
    public float spawnPointRespawnDuration = 30f;
    public float ritualSiteSafeDistance = 8f;

    [Header("Clan War")]
    public bool clanWarActive = false;
    [Range(0f, 1f)] public float clanWarIntensity = 0f;

    [Header("Twin Threat Sensitivity")]
    [Tooltip("Zone-wide multiplier on per-enemy twinThreatRange.\n" +
             "Prevents wait-it-out during clan war.")]
    [Range(0.5f, 3f)] public float twinThreatRangeMultiplier = 1f;
}