using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// ProjectileBase
// Inherit from this for any arrow/bullet projectile.
// Handles movement, collision, damage, and death proxy activation.
// ─────────────────────────────────────────────────────────────────────────────
public class ProjectileBase : MonoBehaviour, IProjectileData
{
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private LayerMask hitLayers;

    private Vector3 _direction;
    private float _speed;
    private float _damage;
    private Enemy _owner;
    private bool _hasHit;

    public void Initialise(Vector3 direction, float speed)
    {
        _direction = direction;
        _speed = speed;
        Destroy(gameObject, lifetime);
    }

    // Called by ProjectileAttackLauncher after Initialise()
    public void SetOwnerAndDamage(Enemy owner, float damage)
    {
        _owner = owner;
        _damage = damage;
    }

    private void Update()
    {
        if (_hasHit) return;
        transform.position += _direction * _speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        if (((1 << other.gameObject.layer) & hitLayers) == 0) return;

        _hasHit = true;

        var playerHealth = other.GetComponent<PlayerHealthComponent>();
        if (playerHealth != null && playerHealth.IsDead)
        {
            Destroy(gameObject);
            return;
        }

        var damageable = other.GetComponent<IDamageable>();
        damageable?.TakeDamage(new DamageData(
            _damage, DamageType.Combat, gameObject, transform.position));

        // Trigger rescue proxy if killing blow — mirrors EnemyAttackController
        if (playerHealth != null && playerHealth.IsDead)
        {
            Debug.Log($"[ProjectileBase] Killing blow on {other.gameObject.name}");
            other.GetComponent<PlayerDeathRescueProxy>()?.Activate(_owner);
        }

        Destroy(gameObject);
    }
}