using UnityEngine;

public class Arrow : MonoBehaviour, IProjectileData
{
    private Vector3 _dir;
    private float _speed;
    private EnemyAttackController _controller;
    private bool _hasHit;

    [SerializeField] private float lifetime = 5f;
    [SerializeField] private LayerMask hitLayers;

    // Called by EnemyAttackController.FireProjectile()
    public void Initialise(Vector3 direction, float speed, EnemyAttackController controller)
    {
        _dir = direction;
        _speed = speed;
        _controller = controller;
        Destroy(gameObject, lifetime);
    }

    // IProjectileData legacy support — controller path preferred
    public void Initialise(Vector3 direction, float speed)
        => Initialise(direction, speed, null);

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

        // Hand collider back to controller — it owns all damage logic
        _controller?.OnProjectileHit(other);

        Destroy(gameObject);
    }
}