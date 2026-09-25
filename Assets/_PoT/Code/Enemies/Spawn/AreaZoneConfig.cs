using UnityEngine;

/// <summary>
/// Zone configuration — thin container referencing split SOs.
/// Kept lean to avoid Unity Inspector lag.
///
/// SpawnSetupConfig     — left/right SideSpawnConfig + timing
/// ZoneEnvironmentConfig — dark energy, POI, clan war, threat sensitivity
/// GroupSpawnConfig     — commander group weights and probability
///
/// Passthrough accessors maintain backward compatibility with existing code
/// that reads fields directly from AreaZoneConfig.
///
/// CREATE: Right-click → Create → PlanetOfTwins/Area Zone Config
/// </summary>
[CreateAssetMenu(fileName = "AreaZoneConfig",
                 menuName = "PlanetOfTwins/Spawn/Area Zone Config")]
public class AreaZoneConfig : ScriptableObject
{
    public string areaName = "New Area";

    [Header("Spawn Setup")]
    public SpawnSetupConfig spawnSetup;

    [Header("Environment")]
    public ZoneEnvironmentConfig environment;

    [Header("Commander Groups")]
    public GroupSpawnConfig groupSpawn;

    // ── Spawn passthrough ──────────────────────────────────────
    public SideSpawnConfig leftSide => spawnSetup?.leftSide;
    public SideSpawnConfig rightSide => spawnSetup?.rightSide;
    public float spawnInterval => spawnSetup?.spawnInterval ?? 3f;

    public SideSpawnConfig GetSideConfig(SpawnSide side)
        => side == SpawnSide.Left ? leftSide : rightSide;

    // ── Environment passthrough ────────────────────────────────
    public bool crossClanBondingUnlocked => environment?.crossClanBondingUnlocked ?? false;
    public float comboPowerCeiling => environment?.comboPowerCeiling ?? 1f;
    public float darkEnergyGainMultiplier => environment?.darkEnergyGainMultiplier ?? 1f;
    public float spawnPointRespawnDuration => environment?.spawnPointRespawnDuration ?? 30f;
    public float ritualSiteSafeDistance => environment?.ritualSiteSafeDistance ?? 8f;
    public bool clanWarActive => environment?.clanWarActive ?? false;
    public float clanWarIntensity => environment?.clanWarIntensity ?? 0f;
    public float twinThreatRangeMultiplier => environment?.twinThreatRangeMultiplier ?? 1f;
}