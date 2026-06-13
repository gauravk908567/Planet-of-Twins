using UnityEngine;

/// <summary>
/// Place a BoxCollider trigger at the boundary between two world chunks.
/// When either twin enters/exits, notifies SceneFlowManager to recalculate
/// which scenes should be loaded.
///
/// SETUP (per boundary):
///   1. Create an empty GameObject at the area boundary, sized to span the passage.
///   2. Add BoxCollider (Is Trigger = true).
///   3. Add this component. Assign targetLocation = the WorldLocationSO for the area
///      the twin is entering when they cross INTO this trigger.
///   4. Set playerLayer to the Player layer mask.
///   5. (Optional) Set entranceId to match an EntranceDefinition in targetLocation
///      if there are multiple entry points (e.g. "GateLeft" vs "GateRight").
///
/// PLACEMENT TIP:
///   Put the trigger fully inside the target area (not straddling the line).
///   A twin is "in area X" once they are inside X's trigger. For seamless
///   streaming, overlap the previous area's trigger slightly so load starts
///   a little before the seam is visible.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneLoadTrigger : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The location the twin is entering when they cross into this trigger volume.")]
    [SerializeField] private WorldLocationSO targetLocation;

    [Tooltip("The location the twins are coming FROM when crossing this trigger. " +
             "LocationEntrance.GetFor(comesFrom) resolves the spawn point on the other side. " +
             "Leave null to use the target area's default entrance.")]
    [SerializeField] private WorldLocationSO comesFrom;

    [Header("Detection")]
    [SerializeField] private LayerMask playerLayer;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            Debug.LogWarning($"[SceneLoadTrigger] Collider on '{name}' was not set to Trigger — fixing automatically.");
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerLayer(other)) return;
        var actor = other.GetComponentInParent<Player>();
        if (actor == null || actor is SoulPlayer) return; // SoulPlayer tracked separately
        SceneFlowManager.Instance?.NotifyTwinEntered(targetLocation, actor);
        Debug.Log($"[SceneLoadTrigger] {actor.name} entered '{targetLocation?.scene.Name}'" +
                  (comesFrom != null ? $" from '{comesFrom.name}'" : "") + ".");
    }

    // OnTriggerExit intentionally removed — transition model (3.7a): exits are never tracked;
    // assignment of a new location implicitly vacates the previous one.

    private bool IsPlayerLayer(Collider other) =>
        ((1 << other.gameObject.layer) & playerLayer) != 0;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (targetLocation == null) return;
        var col = GetComponent<BoxCollider>();
        if (col == null) return;

        UnityEditor.Handles.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        UnityEditor.Handles.DrawSolidRectangleWithOutline(
            new[]
            {
                transform.TransformPoint(col.center + new Vector3(-col.size.x, -col.size.y,  col.size.z) * 0.5f),
                transform.TransformPoint(col.center + new Vector3( col.size.x, -col.size.y,  col.size.z) * 0.5f),
                transform.TransformPoint(col.center + new Vector3( col.size.x,  col.size.y,  col.size.z) * 0.5f),
                transform.TransformPoint(col.center + new Vector3(-col.size.x,  col.size.y,  col.size.z) * 0.5f),
            },
            new Color(0.2f, 0.8f, 1f, 0.1f),
            new Color(0.2f, 0.8f, 1f, 0.9f));

        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f,
            $"→ {targetLocation.scene.Name}" +
            (comesFrom != null ? $" (from {comesFrom.name})" : ""));
    }
#endif
}
