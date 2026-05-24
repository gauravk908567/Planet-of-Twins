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

    [Tooltip("Renderer to toggle for visual disabled state.")]
    [SerializeField] private Renderer _visualRenderer;

    [Tooltip("VFX to play when destroyed.")]
    [SerializeField] private GameObject _destroyVFXPrefab;

    [Tooltip("VFX to play when respawning.")]
    [SerializeField] private GameObject _respawnVFXPrefab;

    [Tooltip("How much HP this spawn point has.")]
    [SerializeField] private float _maxHP = 100f;

    private float _currentHP;
    private bool _isRespawning;

    // ── Events ─────────────────────────────────────────────
    public event System.Action<SpawnPointPOI> OnSpawnPointDestroyed;
    public event System.Action<SpawnPointPOI> OnSpawnPointRespawned;

    public float RespawnProgress => _isRespawning
        ? 1f - (_currentHP / _maxHP) : 0f;

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

        if (_currentHP <= 0f)
            StartCoroutine(DestroyAndRespawn());
    }

    private IEnumerator DestroyAndRespawn()
    {
        // Destroyed state
        IsActive = false;
        _isRespawning = true;
        _currentHP = 0f;

        POIManager.Instance?.Unregister(this);
        PoTWorldStateWriter.Instance?.NotifySpawnUnderAttack(gameObject, false);

        if (_destroyVFXPrefab != null)
        {
            var vfx = Instantiate(_destroyVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        // Visual disabled state
        if (_visualRenderer != null)
            _visualRenderer.material.color = Color.gray;

        OnSpawnPointDestroyed?.Invoke(this);
        Debug.Log($"[SpawnPoint] {name} destroyed — respawning in {_respawnDuration}s");

        yield return new WaitForSeconds(_respawnDuration);

        // Respawn
        IsActive = true;
        _isRespawning = false;
        _currentHP = _maxHP;

        POIManager.Instance?.Register(this);

        if (_respawnVFXPrefab != null)
        {
            var vfx = Instantiate(_respawnVFXPrefab, transform.position, Quaternion.identity);
            Destroy(vfx, 3f);
        }

        if (_visualRenderer != null)
            _visualRenderer.material.color = Color.white;

        OnSpawnPointRespawned?.Invoke(this);
        Debug.Log($"[SpawnPoint] {name} respawned");
    }
}