// ═══════════════════════════════════════════════════════════════════════
// FILE 1 — AreaNode.cs
// Add this to the same GameObject as your SpawnZone (AreaParkZone etc.)
// The SpawnZone GameObject NEVER gets deactivated — only areaContent does.
// ═══════════════════════════════════════════════════════════════════════

using UnityEngine;

/// <summary>
/// Defines one area's content and adjacency.
/// Sits on the same GameObject as SpawnZone — the trigger stays active always.
/// Only the objects in areaContent get toggled by AreaManager.
/// </summary>
public class AreaNode : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Unique ID for this area — used for debug and lookup")]
    public string areaID = "Area_Unnamed";

    [Header("Detection")]
    [Tooltip("The SpawnZone on this same GameObject (or a child trigger)")]
    public SpawnZone spawnZone;

    [Header("Adjacency")]
    [Tooltip("Areas that must stay active while this area is occupied. " +
             "Drag in any areas visible or reachable from here. " +
             "Order and count don't matter — fully data-driven.")]
    public AreaNode[] adjacentAreas;

    [Header("Content To Toggle")]
    [Tooltip("GameObjects to activate/deactivate with this area. " +
             "Do NOT put CommonStatic or always-on objects here — " +
             "anything not in any AreaNode's list is simply never touched.")]
    public GameObject[] areaContent;

    [Header("Startup")]
    [Tooltip("Check this on the area where the player spawns. " +
             "Treats this area as occupied from the first frame.")]
    public bool activeOnStart = false;

    // ── Lifecycle ───────────────────────────────────────────

    private void Awake()
    {
        if (spawnZone == null)
        {
            spawnZone = GetComponent<SpawnZone>();
            if (spawnZone == null)
                Debug.LogWarning($"[AreaNode] {areaID} has no SpawnZone reference. " +
                                 "Trigger-based activation will not work.");
        }

        if (spawnZone != null)
        {
            spawnZone.OnZoneEntered += HandleZoneEntered;
            spawnZone.OnZoneExited += HandleZoneExited;
        }
    }

    private void Start()
    {
        AreaManager.Instance?.RegisterArea(this);

        if (activeOnStart)
            AreaManager.Instance?.OnAreaOccupied(this);
    }

    private void OnDestroy()
    {
        if (spawnZone != null)
        {
            spawnZone.OnZoneEntered -= HandleZoneEntered;
            spawnZone.OnZoneExited -= HandleZoneExited;
        }

        AreaManager.Instance?.UnregisterArea(this);
    }

    // ── Zone callbacks ──────────────────────────────────────

    private void HandleZoneEntered(SpawnZone zone)
        => AreaManager.Instance?.OnAreaOccupied(this);

    private void HandleZoneExited(SpawnZone zone)
        => AreaManager.Instance?.OnAreaVacated(this);

    // ── Content control (called by AreaManager only) ────────

    public void SetContentActive(bool active)
    {
        if (areaContent == null) return;
        foreach (var obj in areaContent)
            if (obj != null) obj.SetActive(active);
    }

    // ── Editor gizmos ───────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (adjacentAreas == null) return;

        UnityEditor.Handles.color = new Color(1f, 0.85f, 0f, 0.8f);
        foreach (var adj in adjacentAreas)
        {
            if (adj == null) continue;
            UnityEditor.Handles.DrawDottedLine(transform.position,
                                               adj.transform.position, 4f);
            UnityEditor.Handles.Label(
                (transform.position + adj.transform.position) * 0.5f,
                $"{areaID} ↔ {adj.areaID}");
        }

        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawSphere(transform.position, 0.8f);
    }
#endif
}

