using UnityEngine;

/// <summary>
/// Ritual Site POI — locations where Witness can perform rituals.
/// Multiple per zone — Witness picks safest one away from twins.
///
/// ATTACH: To ritual site GOs in each zone.
/// Place near walls/corners away from barrier.
/// No special setup needed — POIManager queries these automatically.
/// </summary>
public class RitualSitePOI : POIBase
{
    [Header("Ritual Site Config")]
    [Tooltip("Whether this site is currently occupied by a Witness performing ritual.")]
    public bool IsOccupied { get; private set; }
    public EnemyPOITracker Occupant { get; private set; }

    [Tooltip("Optional VFX for idle ritual site.")]
    [SerializeField] private ParticleSystem _idleVFX;

    protected override void Awake()
    {
        PoiType = POIType.RitualSite;
        base.Awake();
        if (_idleVFX != null) _idleVFX.Play();
    }

    public void Occupy(EnemyPOITracker occupant)
    {
        IsOccupied = true;
        Occupant = occupant;
        Debug.Log($"[RitualSite] {name} occupied by {occupant?.name}");
    }

    public void Vacate(EnemyPOITracker occupant)
    {
        if (Occupant != occupant) return; // ← only vacate if same occupant
        IsOccupied = false;
        Occupant = null;
        Debug.Log($"[RitualSite] {name} vacated");
    }
}