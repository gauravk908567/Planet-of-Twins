using UnityEngine;

public class PossessedState : IEnemyState
{
    private readonly Enemy _enemy;
    private readonly LayerMask _targetLayer;
    private readonly float _searchRadius;

    private readonly Collider[] _hitBuffer = new Collider[10];

    // Colour state
    private Renderer _renderer;
    private Color _originalColor;
    private static readonly Color PossessedColor = new Color(0.5f, 0f, 1f); // purple glow

    public PossessedState(Enemy enemy, LayerMask targetLayer, float searchRadius = 12f)
    {
        _enemy = enemy;
        _targetLayer = targetLayer;
        _searchRadius = searchRadius;

        // Cache renderer once — GetComponentInChildren is expensive per-frame
        _renderer = enemy.GetComponentInChildren<Renderer>();
    }

    public void Enter()
    {
        _enemy.ClearTarget();

        // Cache original colour and apply possessed glow
        if (_renderer != null)
        {
            _originalColor = _renderer.material.color;
            _renderer.material.color = PossessedColor;
        }
    }

    public void Update()
    {
        Transform target = FindTarget();
        _enemy.SetTarget(target);

        if (target == null) return;

        float dist = Vector3.Distance(_enemy.transform.position, target.position);

        if (dist > _enemy.AttackRange)
            _enemy.Movement.MoveTowards(target.position);
        else
            _enemy.AttackController.TryAttack(isPossessed: true);
    }

    public void Exit()
    {
        _enemy.ClearTarget();

        // Restore original colour when possession ends
        if (_renderer != null)
            _renderer.material.color = _originalColor;
    }

    private Transform FindTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(
            _enemy.transform.position, _searchRadius, _hitBuffer, _targetLayer);

        Transform nearestUnpossessed = null;
        Transform nearestTrap = null;
        Transform nearestOtherPossessed = null;
        float distUnpossessed = float.MaxValue;
        float distTrap = float.MaxValue;
        float distOtherPossessed = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var col = _hitBuffer[i];
            if (col.gameObject == _enemy.gameObject) continue;

            float d = Vector3.Distance(_enemy.transform.position, col.transform.position);

            var otherEnemy = col.GetComponent<Enemy>();
            if (otherEnemy != null && !otherEnemy.IsPossessed && d < distUnpossessed)
            {
                distUnpossessed = d;
                nearestUnpossessed = col.transform;
                continue;
            }

            var trap = col.GetComponent<IStunnable>();
            if (trap != null && otherEnemy == null && d < distTrap)
            {
                distTrap = d;
                nearestTrap = col.transform;
                continue;
            }

            if (otherEnemy != null && otherEnemy.IsPossessed && d < distOtherPossessed)
            {
                distOtherPossessed = d;
                nearestOtherPossessed = col.transform;
            }
        }

        if (nearestUnpossessed != null) return nearestUnpossessed;
        if (nearestTrap != null) return nearestTrap;
        if (nearestOtherPossessed != null) return nearestOtherPossessed;

        return _enemy.transform; // hit self if no targets
    }
}