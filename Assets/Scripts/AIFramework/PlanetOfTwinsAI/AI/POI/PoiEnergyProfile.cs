using UnityEngine;

/// <summary>
/// Config for one POI's energy feed (R7 — config only). Assign to a PoiEnergyEmitter on a
/// ritual site / spawn point / barrier POI. Per-POI amounts are the point: a major ritual
/// site can feed harder than a roadside barrier by giving each its own profile asset.
/// </summary>
[CreateAssetMenu(fileName = "PoiEnergyProfile",
                 menuName = "PlanetOfTwins/AI/POI Energy Profile")]
public class PoiEnergyProfile : ScriptableObject
{
    [Header("Who gets fed")]
    [Tooltip("Enemies below this fraction of max health are eligible (0.5 = below 50% HP).")]
    [Range(0f, 1f)] public float healthThresholdPct = 0.5f;

    [Tooltip("Feed range in metres. 0 = use the POI's own InfluenceRadius.")]
    public float feedRadius = 0f;

    [Header("Per feed (one tick, per enemy)")]
    [Tooltip("Dark energy added per feed (normalised 0..1 scale — 0.03 is 'very less but still some').")]
    public float energyPerFeed = 0.03f;

    [Tooltip("Health restored per feed.")]
    public float healthPerFeed = 6f;

    [Header("Cadence (per enemy, scaled time)")]
    [Tooltip("Seconds between feeds for one enemy.")]
    public float feedInterval = 12f;

    [Tooltip("Once an enemy's dark energy crosses this, its interval drops to fastFeedInterval.")]
    [Range(0f, 1f)] public float fastIntervalEnergyThreshold = 0.6f;

    [Tooltip("Reduced interval for enemies past the dark-energy threshold.")]
    public float fastFeedInterval = 8f;
}
