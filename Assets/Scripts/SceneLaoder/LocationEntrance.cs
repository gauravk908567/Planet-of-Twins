using UnityEngine;

/// <summary>
/// Marks a named arrival point inside an area scene.
/// Place one per entry direction (e.g. one for "arriving from Park", one for "arriving from Streets").
/// A null comesFrom = default entrance (used when no specific direction is known).
///
/// No strings — identity is the WorldLocationSO asset reference.
/// SoftResetController calls LocationEntrance.GetFor(fromLocation) to place twins on arrival.
///
/// SETUP (per area scene):
///   1. Create an empty GO at the spawn-in position.
///   2. Add this component.
///   3. Set comesFrom = the WorldLocationSO of the area the twins are arriving FROM.
///      Leave null for the default/fallback entrance.
///   4. Rotate the transform to face the direction twins should look on arrival.
/// </summary>
public class LocationEntrance : MonoBehaviour
{
    [Tooltip("The location twins are arriving FROM. " +
             "Null = this is the default entrance (used when direction is unknown).")]
    [SerializeField] private WorldLocationSO comesFrom;

    public WorldLocationSO ComesFrom => comesFrom;
    public Transform SpawnTransform   => transform;

    // ── Static query ──────────────────────────────────────────────────────────

    /// <summary>
    /// Find the entrance for twins arriving from <paramref name="from"/>.
    /// Falls back to the default entrance (comesFrom == null) if no exact match.
    /// Returns null if no entrance exists in the currently loaded scenes.
    /// </summary>
    public static LocationEntrance GetFor(WorldLocationSO from)
    {
        var all = FindObjectsByType<LocationEntrance>(FindObjectsSortMode.None);
        LocationEntrance fallback = null;

        foreach (var e in all)
        {
            if (e.comesFrom == from) return e;          // exact match
            if (e.comesFrom == null) fallback = e;      // default candidate
        }

        return fallback;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.2f);

        string label = comesFrom != null
            ? $"Entrance ← {comesFrom.name}"
            : "Entrance (default)";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, label);
    }
#endif
}
