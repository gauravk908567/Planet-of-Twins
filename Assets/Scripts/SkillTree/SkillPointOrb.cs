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
public class SkillPointOrb : MonoBehaviour
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

    private Vector3 _basePosition;
    private bool _collected = false;

    void Awake()
    {
        _pointBank = _pointBankMono as IPointBank;   // optional same-scene slot (R1)
        _basePosition = transform.position;
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

        // TODO: play collect sound / VFX here
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.7f, 0.35f);
        Gizmos.DrawSphere(transform.position, _collectRadius);
        Gizmos.color = new Color(0.4f, 1f, 0.7f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, _collectRadius);
    }
}