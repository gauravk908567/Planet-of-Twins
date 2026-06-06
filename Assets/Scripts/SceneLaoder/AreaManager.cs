// ═══════════════════════════════════════════════════════════════════════
// FILE 1 — AreaNode.cs
// Add this to the same GameObject as your SpawnZone (AreaParkZone etc.)
// The SpawnZone GameObject NEVER gets deactivated — only areaContent does.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using UnityEngine;
// ═══════════════════════════════════════════════════════════════════════
// FILE 2 — AreaManager.cs
// Singleton. Lives on a persistent GameObject in your world scene.
// Tracks which areas are occupied and refreshes active state accordingly.
// ═══════════════════════════════════════════════════════════════════════

public class AreaManager : MonoBehaviour
{
    public static AreaManager Instance { get; private set; }

    // All registered area nodes in the current level
    private readonly List<AreaNode> _allAreas = new List<AreaNode>();

    // Areas that currently have at least one twin inside them
    private readonly HashSet<AreaNode> _occupiedAreas = new HashSet<AreaNode>();

    // ── Lifecycle ───────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Registration ────────────────────────────────────────

    /// <summary>Called by each AreaNode.Start() — builds the full area list.</summary>
    public void RegisterArea(AreaNode area)
    {
        if (area != null && !_allAreas.Contains(area))
            _allAreas.Add(area);
    }

    /// <summary>Called by AreaNode.OnDestroy() — cleans up on scene unload.</summary>
    public void UnregisterArea(AreaNode area)
    {
        _allAreas.Remove(area);
        _occupiedAreas.Remove(area);
    }

    // ── Occupation API (called by AreaNode) ─────────────────

    /// <summary>
    /// A twin entered this area.
    /// Rebuilds the active set immediately — no delay, no buffer.
    /// </summary>
    public void OnAreaOccupied(AreaNode area)
    {
        if (area == null) return;

        _occupiedAreas.Add(area);
        RefreshActiveAreas();

        Debug.Log($"[AreaManager] Occupied: {area.areaID} | " +
                  $"Total occupied: {_occupiedAreas.Count}");
    }

    /// <summary>
    /// The last twin left this area.
    /// Rebuilds the active set — area may deactivate if no adjacent twin needs it.
    /// </summary>
    public void OnAreaVacated(AreaNode area)
    {
        if (area == null) return;

        _occupiedAreas.Remove(area);
        RefreshActiveAreas();

        Debug.Log($"[AreaManager] Vacated: {area.areaID} | " +
                  $"Total occupied: {_occupiedAreas.Count}");
    }

    // ── Core logic ──────────────────────────────────────────

    private void RefreshActiveAreas()
    {
        // Step 1: Build the set of areas that MUST be active right now.
        // Rule: any occupied area + all of its declared adjacents.
        var shouldBeActive = new HashSet<AreaNode>();

        foreach (var occupied in _occupiedAreas)
        {
            if (occupied == null) continue;

            shouldBeActive.Add(occupied);

            if (occupied.adjacentAreas == null) continue;
            foreach (var adj in occupied.adjacentAreas)
                if (adj != null) shouldBeActive.Add(adj);
        }

        // Step 2: Apply to every registered area.
        // Areas not in shouldBeActive get deactivated.
        // Areas in shouldBeActive get activated.
        foreach (var area in _allAreas)
        {
            if (area == null) continue;
            area.SetContentActive(shouldBeActive.Contains(area));
        }
    }

    // ── Debug helpers ────────────────────────────────────────

    /// <summary>Call from a debug UI or console to see current state.</summary>
    public void LogCurrentState()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("─── AreaManager State ───");
        sb.AppendLine($"Registered areas ({_allAreas.Count}):");
        foreach (var a in _allAreas)
            sb.AppendLine($"  [{(IsActive(a) ? "ON " : "off")}] {a.areaID}" +
                          $"{(_occupiedAreas.Contains(a) ? " ← OCCUPIED" : "")}");
        Debug.Log(sb.ToString());
    }

    private bool IsActive(AreaNode area)
    {
        if (area?.areaContent == null || area.areaContent.Length == 0) return true;
        return area.areaContent[0] != null && area.areaContent[0].activeSelf;
    }
}

