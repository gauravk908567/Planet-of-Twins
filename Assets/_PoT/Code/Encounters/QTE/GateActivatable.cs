using UnityEngine;

/// <summary>
/// Attach to a gate GameObject that has an Animator.
/// The Animator must have a bool parameter named "IsOpen".
///
/// Can be made permanent (prototype) or resettable via _isPermanent flag.
/// </summary>
public class GateActivatable : MonoBehaviour, IActivatable, WorldFlagRegistry.IWorldFlagObject
{
    [SerializeField] private Animator gateAnimator;
    [SerializeField] private string openParameter = "IsOpen";

    [Tooltip("If true, Deactivate() is a no-op � gate stays open for the session.")]
    [SerializeField] private bool isPermanent = true;

    [Tooltip("Save-state (§11.1): OPTIONAL override key. When the gate opens its key is stored in the " +
             "WorldFlagRegistry and written into the save slot; on Continue/respawn a set key re-opens the gate " +
             "WITHOUT replaying the QTE. Empty = a PERMANENT gate auto-keys itself from scene + authored position " +
             "(like SkillPointOrb); a non-permanent (resettable) gate with no key opts out of persistence.")]
    [SerializeField] private string _worldFlagKey = "";

    public bool IsActivated { get; private set; } = false;

    private string _key;   // resolved in Awake: authored key, else auto-key for permanent gates, else null (opt-out)

    private void Awake()
    {
        if (gateAnimator == null)
            gateAnimator = GetComponent<Animator>();

        // BUG-119: the only gate in the game had no authored key, so opening it was never saved. Permanent gates are
        // progression → persist by default; the authored position (captured before any open animation) keeps the
        // key stable across sessions.
        _key = !string.IsNullOrEmpty(_worldFlagKey) ? _worldFlagKey
             : isPermanent ? WorldFlagRegistry.PositionKey("gate", gameObject, transform.position)
             : null;
    }

    // R5: self-register / self-apply on stream-in (covers a restore that ran BEFORE this area streamed);
    // the registry's Restore() re-applies to gates already live (the Continue order). Only persistent
    // (keyed or permanent) gates participate.
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(_key)) return;
        WorldFlagRegistry.Instance?.Register(this);
        ApplyWorldFlags();
    }

    private void OnDisable()
    {
        if (string.IsNullOrEmpty(_key)) return;
        WorldFlagRegistry.Instance?.Unregister(this);
    }

    public void Activate()
    {
        if (IsActivated) return;
        IsActivated = true;
        Debug.Log($"[GateActivatable] Activate called � animator={gateAnimator?.name ?? "NULL"}, param='{openParameter}', IsOpen={gateAnimator?.GetBool(openParameter)}");
        gateAnimator?.SetBool(openParameter, true);
        Debug.Log($"[GateActivatable] {name} opened.");

        // Persist this one-shot world event so a save/respawn re-opens the gate without the QTE (§11.1).
        if (!string.IsNullOrEmpty(_key))
        {
            WorldFlagRegistry.Instance?.Set(_key);
            WorldFlagRegistry.Instance?.Register(this);   // idempotent — covers direct-play (OnEnable before Persistent)
        }
    }

    /// <summary>WorldFlagRegistry restore hook — if this gate's flag is set, land it open (no QTE, no beat replay).
    /// Invariant: flag == live world. A soft-reset respawn never re-closes an opened gate, so when a restore hands
    /// us an OLDER flag set (gate opened after that checkpoint) the still-open gate re-asserts its flag — otherwise
    /// the next checkpoint would save it closed and Continue would shut it again.</summary>
    public void ApplyWorldFlags()
    {
        if (string.IsNullOrEmpty(_key)) return;
        if (IsActivated) { WorldFlagRegistry.Instance?.Set(_key); return; }
        if (WorldFlagRegistry.Instance != null && WorldFlagRegistry.Instance.IsSet(_key))
        {
            IsActivated = true;
            gateAnimator?.SetBool(openParameter, true);   // animator eases to the open state; the load fade covers it
        }
    }

    public void Deactivate()
    {
        if (isPermanent)
        {
            Debug.Log($"[GateActivatable] {name} is permanent � Deactivate ignored.");
            return;
        }
        IsActivated = false;
        gateAnimator?.SetBool(openParameter, false);
    }
}