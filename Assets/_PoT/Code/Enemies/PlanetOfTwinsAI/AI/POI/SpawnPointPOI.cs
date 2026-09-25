using System.Collections;
using UnityEngine;

/// <summary>
/// Spawn Point POI — breakable enemy spawn location.
/// When destroyed: visually disabled, enemies react, respawns after timer.
/// Enemies defend spawn points when attacked.
///
/// ATTACH: To each spawn point transform in the scene.
/// Wire _visualRenderer to show disabled state.
/// </summary>
public class SpawnPointPOI : POIBase
{
    [Header("Spawn Point Config")]
    [Tooltip("Time in seconds before spawn point reactivates after being destroyed.")]
    [SerializeField] private float _respawnDuration = 30f;

    [Tooltip("Cue Book asset — the transient 'spawn_hit' reaction fired when the player damages the point. " +
             "Persistent state visuals (portal / recharge / broken husk / material intensity) are owned by " +
             "SpawnPointVisualDriver, not this book.")]
    [SerializeField] private CueBookData _cueBook;

    [Tooltip("How much HP this spawn point has.")]
    [SerializeField] private float _maxHP = 100f;

    private float _currentHP;
    private bool _isRespawning;
    private float _respawnStartTime;   // scaled Time.time captured when destroyed

    // ── Events ─────────────────────────────────────────────
    public event System.Action<SpawnPointPOI> OnSpawnPointDestroyed;
    public event System.Action<SpawnPointPOI> OnSpawnPointRespawned;

    public bool IsRespawning => _isRespawning;

    /// <summary>0 at the moment of destruction → 1 when fully recharged. Presentation reads this to ramp
    /// visuals. Scaled time (matches the WaitForSeconds respawn wait) — freezes under pause/Setsuna (R10).</summary>
    public float RechargeProgress =>
        _isRespawning ? Mathf.Clamp01((Time.time - _respawnStartTime) / _respawnDuration)
                      : (IsActive ? 1f : 0f);

    protected override void Awake()
    {
        PoiType = POIType.SpawnPoint;
        _currentHP = _maxHP;
        base.Awake();
    }

    // ── Damage ─────────────────────────────────────────────
    public void TakeDamage(float amount)
    {
        if (!IsActive || _isRespawning) return;

        _currentHP -= amount;

        // Notify world state — enemies will defend
        PoTWorldStateWriter.Instance?.NotifySpawnUnderAttack(gameObject, true);
        MoodEventBus.Fire(EnemySocialEvent.SpawnUnderAttack, gameObject);

        // Transient reaction — the one true cue here (momentary impact, no live params).
        if (_cueBook != null)
            FxManager.Instance?.PlayBook(_cueBook, FxIds.Unsorted.SpawnPointCueBook.spawn_hit, CueContext.Follow(transform));

        if (_currentHP <= 0f)
            StartCoroutine(DestroyAndRespawn());
    }

    /// <summary>Debug bench (GameDebuggerV2): force the point into its destroyed state with exactly
    /// <paramref name="remaining"/> seconds of recharge left, so the full recharge ramp is watchable
    /// without waiting out the whole timer. Re-fires the destroyed event if the point was healthy.</summary>
    public void DebugSetRechargeRemaining(float remaining)
    {
        StopAllCoroutines();
        StartCoroutine(DestroyAndRespawn(Mathf.Clamp(remaining, 0.1f, _respawnDuration)));
    }

    private IEnumerator DestroyAndRespawn() => DestroyAndRespawn(_respawnDuration);

    private IEnumerator DestroyAndRespawn(float remaining)
    {
        // Destroyed state. RechargeProgress is (now − start) / _respawnDuration, so a shortened wait
        // back-dates the start — progress lands at the right fraction and ramps to 1 over `remaining`.
        IsActive = false;
        _isRespawning = true;
        _currentHP = 0f;
        _respawnStartTime = Time.time - (_respawnDuration - remaining);   // scaled — drives RechargeProgress (R10)

        POIManager.Instance?.Unregister(this);
        PoTWorldStateWriter.Instance?.NotifySpawnUnderAttack(gameObject, false);

        // Visuals (portal off, recharge converge, husk shrink, material dim→bright) are owned by
        // SpawnPointVisualDriver — it listens to these events and reads RechargeProgress each frame.
        OnSpawnPointDestroyed?.Invoke(this);
        Debug.Log($"[SpawnPoint] {name} destroyed — respawning in {remaining}s");

        yield return new WaitForSeconds(remaining);

        // Respawn
        IsActive = true;
        _isRespawning = false;
        _currentHP = _maxHP;

        POIManager.Instance?.Register(this);

        OnSpawnPointRespawned?.Invoke(this);
        Debug.Log($"[SpawnPoint] {name} respawned");
    }
}