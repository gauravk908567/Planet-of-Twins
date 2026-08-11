using UnityEngine;

public class EnemyAnimationController : MonoBehaviour, IAnimationController
{
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private Animator _animator;
    private EnemyAttackController _attackController;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _attackController = GetComponent<EnemyAttackController>();

        if (_animator == null)
            Debug.LogError("[EnemyAnimationController] No Animator found.", this);
    }

    public void PlayAttack()
    {
        _animator?.SetTrigger(AttackHash);
    }

    // ── Called by Animation Event at the hit frame ─────────────
    public void OnAttackHitFrame()
    {
        _attackController?.ExecuteHitDetection();
    }
}