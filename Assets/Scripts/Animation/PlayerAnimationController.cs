using UnityEngine;

public class PlayerAnimationController : MonoBehaviour, IAnimationController
{
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private Animator _animator;
    private PlayerAttackController _attackController;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _attackController = GetComponent<PlayerAttackController>();

        if (_animator == null)
            Debug.LogError("[PlayerAnimationController] No Animator found.", this);
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
