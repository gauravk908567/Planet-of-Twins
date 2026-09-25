using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// SkillPointOrb
//
// A physical collectible placed on the map by the designer.
// Either twin walks within CollectRadius → awards 1 skill point → destroys itself.
//
// PREFAB SETUP:
//   Root:  Empty GO + SkillPointOrb.cs
//   Child: Glowing orb mesh or particle system (visual only)
//   No Collider needed — proximity check is OverlapSphere in Update.
//
// UNITY SETUP:
//   1. Create the prefab
//   2. Place instances in the scene wherever you want collectibles
//   3. Drag SkillTreeManager into _pointBankMono on each instance
//      OR: if you have a lot of orbs, create a child of SkillTreeManager called
//      "OrbParent" and use a simple manager that injects IPointBank into all
//      children on Awake (see OrbParent note below).
//
// PLACEMENT ADVICE:
//   - Put them in locations that require the twins to move, not stand still
//   - Near enemy clusters = risk/reward
//   - On the far side of the arena from spawn = forces traversal
//   - Never directly at the twins' starting position
// ─────────────────────────────────────────────────────────────────────────────
//
// SAVE-STATE (game.md §11.1): collecting sets a world flag (auto-keyed scene+position, or _worldFlagKey
// override). Collect HIDES the orb (renderers/colliders off) instead of destroying it, so it stays
// registered: Continue / area re-stream keeps a collected orb hidden (no double point), and a respawn
// that rolls points back to the checkpoint also rolls the orb back (re-shown — the point isn't lost).
// ─────────────────────────────────────────────────────────────────────────────
public class SkillPointOrb : MonoBehaviour, WorldFlagRegistry.IWorldFlagObject
{
    [Header("Inject — OPTIONAL same-scene slot. Leave empty in area scenes (resolves at runtime, R4).")]
    [Tooltip("SkillTreeManager lives in Persistent now — do NOT drag it across scenes (R2). " +
             "Left empty, the orb resolves SkillTreeManager.Instance in Start().")]
    [SerializeField] private MonoBehaviour _pointBankMono;
    private IPointBank _pointBank;

    [Header("Settings")]
    [Tooltip("Radius within which either twin collects this orb")]
    [SerializeField] private float _collectRadius = 1.0f;

    [Tooltip("Layer mask — must include both twin player objects")]
    [SerializeField] private LayerMask _playerLayer;

    [Header("Visual — optional bob")]
    [SerializeField] private float _bobAmplitude = 0.15f;
    [SerializeField] private float _bobSpeed = 2.0f;

    [Header("Save-state (§11.1)")]
    [Tooltip("OPTIONAL override. Empty = auto-key from scene + authored position (recommended for bulk orbs). " +
             "Set only if this orb must keep its identity after being moved in the editor.")]
    [SerializeField] private string _worldFlagKey = "";

    private Vector3 _basePosition;
    private bool _collected = false;
    private string _key;
    private Renderer[] _renderers;   // incl. the root VFXRenderer
    private Collider[] _colliders;

    void Awake()
    {
        _pointBank = _pointBankMono as IPointBank;   // optional same-scene slot (R1)
        _basePosition = transform.position;
        _key = string.IsNullOrEmpty(_worldFlagKey)
            ? WorldFlagRegistry.PositionKey("orb", gameObject, _basePosition)
            : _worldFlagKey;
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
    }

    // R5: self-register + self-apply on stream-in (covers restore-before-stream); Restore() re-applies live orbs.
    void OnEnable()
    {
        WorldFlagRegistry.Instance?.Register(this);
        ApplyWorldFlags();
    }

    void OnDisable() => WorldFlagRegistry.Instance?.Unregister(this);

    /// <summary>WorldFlagRegistry hook — collected iff the flag is set. Two-way: a respawn that restores an
    /// older flag set re-shows an orb collected after the checkpoint (its point was rolled back too).</summary>
    public void ApplyWorldFlags()
    {
        bool collected = WorldFlagRegistry.Instance != null && WorldFlagRegistry.Instance.IsSet(_key);
        _collected = collected;
        SetPresent(!collected);
    }

    void SetPresent(bool present)
    {
        foreach (var r in _renderers) if (r != null) r.enabled = present;
        foreach (var c in _colliders) if (c != null) c.enabled = present;
    }

    void Start()
    {
        // R4: SkillTreeManager is a Persistent singleton — resolve at runtime rather than via a
        // cross-scene drag (R2). Persistent loads before any area scene, so Instance exists by Start.
        _pointBank ??= SkillTreeManager.Instance;
        if (_pointBank == null)
            Debug.LogError($"[SkillPointOrb] {name}: no IPointBank — is SkillTreeManager (Persistent) loaded?", this);
    }

    void Update()
    {
        if (_collected) return;

        // Bob
        float y = _basePosition.y + Mathf.Sin(Time.time * _bobSpeed) * _bobAmplitude;
        transform.position = new Vector3(_basePosition.x, y, _basePosition.z);

        // Either twin within range → collect
        if (Physics.CheckSphere(transform.position, _collectRadius, _playerLayer))
            Collect();
    }

    void Collect()
    {
        if (_collected) return;
        _collected = true;

        _pointBank?.AddPoints(1);
        var reg = WorldFlagRegistry.Instance;
        reg?.Set(_key);        // persist the pickup (§11.1)
        reg?.Register(this);   // idempotent — covers direct-play, where OnEnable ran before Persistent existed

        // TODO: play collect sound / VFX here
        SetPresent(false);   // hide, don't Destroy — must stay registered so a respawn can roll it back
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.7f, 0.35f);
        Gizmos.DrawSphere(transform.position, _collectRadius);
        Gizmos.color = new Color(0.4f, 1f, 0.7f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, _collectRadius);
    }
}