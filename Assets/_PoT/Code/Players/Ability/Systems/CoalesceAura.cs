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
public class CoalesceAura : MonoBehaviour, ISpawnPoolable
{
    // ── ISpawnPoolable (P16 — pooled via GameplayPool.AbilityObjects) ──────────
    public void OnSpawned(GameplayPool pool) { }
    public void OnDespawned()
    {
        StopAuraCue();
        UnsubscribeHostDeath();   // the old host's death must never detach the NEXT aura
        _host = null;
        _lingering = false;
        _lingerTimer = -1f;
        _tickTimer = 0f;
        // Reparent is the pool's job (an aura parented to an enemy returns to the pool root).
    }

    private EnemyHealthComponent _hostHealth;   // kept so despawn can unsubscribe the named handler

    private void UnsubscribeHostDeath()
    {
        if (_hostHealth != null) _hostHealth.OnDeath -= HandleHostDied;
        _hostHealth = null;
    }

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

    // Aura visual is the held cue on_aura — the embedded ParticleSystem is removed from the prefab. Cue Follows this
    // transform: rides the host while parented, stays in world on linger. Book from PlayerVfxLibrary (R4).
    // (on_burningaura is just the name of the Coalesce upgrade — not a separate visual, so only on_aura is played.)
    private CueBookData _cueBook;
    private CueHandle _auraHandle;

    // ── Public init ───────────────────────────────────────────────────────────
    public void Initialise(GameObject host, float radius, float dps, float lingerDuration)
    {
        _host = host;
        Radius = radius;
        DamagePerSec = dps;
        LingerDuration = lingerDuration;

        // Held aura visual — Follows this transform (rides the host while parented, stays put on linger).
        _cueBook ??= VfxLibraryProvider.Instance?.Player?.Coalesce;   // R4
        if (_cueBook != null)
 // Tier-resolved: plays on_aura_t[n] when authored in the book, else the base id
            // (the on_burningaura upgrade = the natural first tier variant). Data via the R4
            // singleton — the aura is a spawned prefab with no serialized store slot.
            // emitterScale: aura footprint tracks the upgraded damage radius (art authored for the
            // 1.5 m base). Interim per the sizing ruling — the big radius node later gets a ring
            // LAYER in the book instead of pure scale, but until then the footprint must not lie.
            _auraHandle = FxManager.Instance?.PlayBook(_cueBook,
                UpgradeCueResolver.Resolve(_cueBook, SkillTreeManager.Instance?.CoalesceData, FxIds.Player.Coalesce.on_aura),
                CueContext.Follow(transform, emitterScale: radius / 1.5f)) ?? CueHandle.None;
        else
            // Fail loud (rule 4): an unresolved library = an invisible aura with working damage —
            // exactly the BUG-065 symptom. Never let this path stay silent.
            Debug.LogError("[CoalesceAura] Coalesce cue book unresolved (VfxLibraryProvider/PlayerVfxLibrary) — aura visual will NOT play.", this);

        // FIX: subscribe to host death so we detach and linger immediately if the
        // enemy dies while the aura is still active. Without this, StunAbility prunes
        // the dead enemy from _stunnedThisWindow so OnStunEnded never fires for it,
        // CoalesceSystem.HandleEnded is never called, and the aura stays parented to
        // the enemy — which then returns to pool and reuses the aura on the next spawn.
        UnsubscribeHostDeath();   // pooled reuse — drop any stale subscription first
        _hostHealth = host?.GetComponent<EnemyHealthComponent>();
        if (_hostHealth != null)
            _hostHealth.OnDeath += HandleHostDied;

        Debug.Log($"[CoalesceAura] Initialised on {host?.name} radius={radius} dps={dps} linger={lingerDuration}");
    }

    private void HandleHostDied()
    {
        // Host died while aura was active — detach and linger in world space
        if (!_lingering)
            DetachAndLinger();
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
            if (_lingerTimer <= 0f)
            {
                StopAuraCue();          // held aura cue off (author its own fade-out on the cue element)
                GameplayPool.Despawn(gameObject);
                return;
            }
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
            if (health != null) health.TakeDamage(new DamageData(dmgThisTick, DamageType.Ability, gameObject, col.transform.position));
        }
    }

    private void StopAuraCue()
    {
        FxManager.Instance?.Stop(_auraHandle);
        _auraHandle = CueHandle.None;
    }

    // Pool / scene-unload safety — a held cue must never outlive the aura GameObject.
    private void OnDestroy() => StopAuraCue();

    // ── Gizmo — visible in Scene view ────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.2f, 1f, 0.3f);
        Gizmos.DrawSphere(transform.position, Radius);
        Gizmos.color = new Color(0.5f, 0.2f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}