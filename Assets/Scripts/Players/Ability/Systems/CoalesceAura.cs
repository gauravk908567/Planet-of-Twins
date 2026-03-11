using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// CoalesceAura  — attaches to a stunned/possessed enemy and drains nearby enemies
//
// PREFAB SETUP:
//   1. Create empty GameObject → name "CoalesceAura"
//   2. Add a child with a SphereRenderer (semi-transparent energy VFX)
//      or use a particle system — purely visual, not part of gameplay logic
//   3. Attach THIS script to the root
//   4. Save as prefab at Assets/Prefabs/CoalesceAura.prefab
//   5. Drag into CoalesceSystem.AuraPrefab slot
//
// HOW IT WORKS:
//   - CoalesceSystem parents this to the stunned/possessed enemy on ability start
//   - While the ability is active:  aura follows the enemy (parented, so free)
//   - When ability ends:           CoalesceSystem calls DetachAndLinger()
//   - Aura stays in world position for LingerDuration, then destroys itself
//
// FACTION CHECK:
//   Only damages GameObjects on the "Enemy" layer (set in Inspector).
//   NEVER damages the host enemy (the one it was attached to).
// ─────────────────────────────────────────────────────────────────────────────
public class CoalesceAura : MonoBehaviour
{
    [Header("Settings — overridden at spawn by CoalesceSystem")]
    public float Radius = 1.5f;
    public float DamagePerSec = 6f;
    public float LingerDuration = 2.5f;

    [Header("Layers")]
    [Tooltip("Only damage objects on this layer")]
    public LayerMask EnemyLayer;

    [Tooltip("Tick interval in seconds — 0.25 = 4 times per second")]
    public float TickInterval = 0.25f;

    // ── Internal ──────────────────────────────────────────────────────────────
    private GameObject _host;        // the enemy we're attached to — never damage this one
    private float _tickTimer;
    private float _lingerTimer = -1f;  // -1 means still attached (ability active)
    private bool _lingering = false;
    private Vector3 _worldPos;    // cached when detached

    // ── Public init ───────────────────────────────────────────────────────────
    public void Initialise(GameObject host, float radius, float dps, float lingerDuration)
    {
        _host = host;
        Radius = radius;
        DamagePerSec = dps;
        LingerDuration = lingerDuration;

        // Scale the visual sphere child to match the damage radius
        // Sphere mesh has radius 0.5 at scale 1, so scale = radius * 2
        var visual = transform.GetChild(0);
        if (visual != null)
        {
            float s = radius * 2f;
            visual.localScale = new Vector3(s, s, s);
        }

        Debug.Log($"[CoalesceAura] Initialised on {host?.name} radius={radius} dps={dps} linger={lingerDuration}");
    }

    // ── Called by CoalesceSystem when the stun/possess ends ──────────────────
    public void DetachAndLinger()
    {
        if (_lingering) return;
        _lingering = true;
        _worldPos = transform.position;
        _lingerTimer = LingerDuration;

        // Detach from enemy so it stays in world space
        transform.SetParent(null);
        transform.position = _worldPos;
    }

    // ── Update ────────────────────────────────────────────────────────────────
    void Update()
    {
        // Linger countdown
        if (_lingering)
        {
            _lingerTimer -= Time.deltaTime;
            if (_lingerTimer <= 0f) { Destroy(gameObject); return; }
        }

        // Damage tick
        _tickTimer += Time.deltaTime;
        if (_tickTimer >= TickInterval)
        {
            _tickTimer -= TickInterval;
            DamageTick();
        }
    }

    // ── Damage ────────────────────────────────────────────────────────────────
    void DamageTick()
    {
        float dmgThisTick = DamagePerSec * TickInterval;
        Collider[] hits = Physics.OverlapSphere(transform.position, Radius, EnemyLayer);

        foreach (var col in hits)
        {
            // Skip the host enemy
            if (_host != null && col.gameObject == _host) continue;

            var health = col.GetComponent<EnemyHealthComponent>();
            if (health == null) health = col.GetComponentInParent<EnemyHealthComponent>();
            if (health != null) health.TakeDamage(new DamageData(dmgThisTick, DamageType.Environmental, gameObject, col.transform.position));
        }
    }

    // ── Gizmo — visible in Scene view ────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.2f, 1f, 0.3f);
        Gizmos.DrawSphere(transform.position, Radius);
        Gizmos.color = new Color(0.5f, 0.2f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}