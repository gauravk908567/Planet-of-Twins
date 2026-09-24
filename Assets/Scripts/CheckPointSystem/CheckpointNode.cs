using UnityEngine;
using TMPro;

/// <summary>
/// One node (A or B) of a <see cref="DualCheckpoint"/> (game.md §11.2). A trigger volume that tracks the
/// twin currently standing in it (SoulPlayer excluded). The parent <see cref="DualCheckpoint"/> reads both
/// nodes' occupants to decide when both twins are present, and drives this node's own <see cref="Prompt"/>.
/// The per-node burst VFX (clan colour) will read <see cref="IsOccupied"/> + the parent's state in the visual
/// pass — not wired yet.
///
/// <para>R1: a same-scene child of its DualCheckpoint. Occupancy uses the same trigger mechanism as the
/// legacy <see cref="CheckpointTrigger"/> (the twin's CharacterController generates the enter/exit events).</para>
/// </summary>
[RequireComponent(typeof(Collider))]
public class CheckpointNode : MonoBehaviour
{
    [Tooltip("This node's 'Hold [button] to save' text — a CheckpointNodePrompt prefab instance placed above the " +
             "node (child of it). Driven by the parent DualCheckpoint.")]
    [SerializeField] private TMP_Text _prompt;

    /// <summary>The twin currently standing on this node, or null. Set by trigger enter/exit.</summary>
    public Player Occupant { get; private set; }

    public bool IsOccupied => Occupant != null;

    /// <summary>This node's prompt text (may be null if not authored — the DualCheckpoint warns).</summary>
    public TMP_Text Prompt => _prompt;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[CheckpointNode] '{name}' collider is not a trigger — set isTrigger for occupancy to work.", this);
    }

    private void OnDisable() => Occupant = null;   // area unload / disable clears occupancy

    private void OnTriggerEnter(Collider other)
    {
        var p = other.GetComponentInParent<Player>();
        if (p == null || p is SoulPlayer) return;
        Occupant = p;
    }

    private void OnTriggerExit(Collider other)
    {
        var p = other.GetComponentInParent<Player>();
        if (p != null && p == Occupant) Occupant = null;
    }
}
