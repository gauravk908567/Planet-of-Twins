using UnityEngine;

/// <summary>
/// Routes the standard DamageData pipeline into a breakable spawn point. Twin melee and abilities
/// resolve targets via GetComponent&lt;IDamageable&gt; on what their overlap hits — SpawnPointPOI's
/// TakeDamage(float) is not on that interface, so without this adapter the player can never damage
/// a spawn point.
///
/// ATTACH: the same GameObject as the SpawnPointPOI (or a collider child under it). The collider
/// must sit on a layer the attack strategies scan (the Enemy layer is the safe default).
/// </summary>
public class PoiDamageAdapter : MonoBehaviour, IDamageable
{
    [Tooltip("Auto-resolved from parents in Awake when left empty.")]
    [SerializeField] private SpawnPointPOI _spawnPoint;

    private void Awake()
    {
        if (_spawnPoint == null) _spawnPoint = GetComponentInParent<SpawnPointPOI>();
        if (_spawnPoint == null)
        {
            Debug.LogError("[PoiDamageAdapter] No SpawnPointPOI on or above this object — disabling.", this);
            enabled = false;
        }
    }

    public void TakeDamage(DamageData damageData)
    {
        if (!enabled || _spawnPoint == null) return;
        _spawnPoint.TakeDamage(damageData.Amount);
    }
}
