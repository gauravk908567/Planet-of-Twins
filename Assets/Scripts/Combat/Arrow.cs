using UnityEngine;

public class Arrow : MonoBehaviour, IProjectileData
{
    private Vector3 _dir;
    private float _speed;
    private EnemyAttackController _controller;
    private bool _hasHit;

    [SerializeField] private float lifetime = 5f;
    [SerializeField] private LayerMask hitLayers;

    // Called by EnemyAttackController.FireProjectile() — canonical path
    public void Initialise(Vector3 direction, float speed, EnemyAttackController controller)
    {
        _dir = direction;
        _speed = speed;
        _controller = controller;
        Destroy(gameObject, lifetime);
    }

    // IProjectileData legacy path — controller is null, so OnTriggerEnter will
    // call _controller?.OnProjectileHit which is a no-op: the arrow hits but
    // deals ZERO damage silently. This is almost always a wiring mistake.
    public void Initialise(Vector3 direction, float speed)
    {
        Debug.LogError(
            $"[Arrow] Legacy Initialise called on {gameObject.name} — " +
            $"controller is null, hit will deal NO damage. " +
            $"Use Initialise(direction, speed, controller) instead.",
            this);
        Initialise(direction, speed, null);
    }

    private void Update()
    {
        if (_hasHit) return;
        transform.position += _dir * _speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        if (((1 << other.gameObject.layer) & hitLayers.value) == 0) return;

        _hasHit = true;

        // Controller owns all damage logic — arrow is pure movement + collision reporter
        _controller?.OnProjectileHit(other);

        Destroy(gameObject);
    }
}