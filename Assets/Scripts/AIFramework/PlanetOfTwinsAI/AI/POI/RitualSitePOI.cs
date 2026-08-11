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

    [Header("VFX")]
    [Tooltip("Ritual Site Cue Book — On_Occupy plays while a Witness performs the ritual here (site glow). " +
             "Held from Occupy → Vacate. This is the SITE's own visual; the Witness caster's circle is the " +
             "enemy Witness book (On_WitnessRitualStart).")]
    [SerializeField] private CueBookData _cueBook;

    // Held handle for the On_Occupy cue — stopped on Vacate / area unload (stale handle stop is inert).
    private CueHandle _occupyHandle;

    protected override void Awake()
    {
        PoiType = POIType.RitualSite;
        base.Awake();
    }

    public void Occupy(EnemyPOITracker occupant)
    {
        IsOccupied = true;
        Occupant = occupant;
        Debug.Log($"[RitualSite] {name} occupied by {occupant?.name}");

        // Site activation cue — held; World-anchored at the (static) site. Guard against double-play.
        var fx = FxManager.Instance;
        if (_cueBook != null && fx != null && !fx.IsPlaying(_occupyHandle))
            _occupyHandle = fx.PlayBook(
                _cueBook, FxIds.Unsorted.RitualSiteCueBook.On_Occupy, new CueContext(transform.position));
    }

    public void Vacate(EnemyPOITracker occupant)
    {
        if (Occupant != occupant) return; // ← only vacate if same occupant
        IsOccupied = false;
        Occupant = null;
        Debug.Log($"[RitualSite] {name} vacated");
        StopOccupyCue();
    }

    // Area unload / disable safety — a held cue must not outlive the site (base handles POI unregister).
    protected override void OnDisable()
    {
        base.OnDisable();
        StopOccupyCue();
    }

    private void StopOccupyCue()
    {
        FxManager.Instance?.Stop(_occupyHandle);
        _occupyHandle = CueHandle.None;
    }
}