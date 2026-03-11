using UnityEngine;

public class PlayerAttackController : MonoBehaviour
{
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private LayerMask enemyLayer;

    [Tooltip("Dot product threshold for hit cone. 0.5=120°, 0.7=90°, 0.85=60°")]
    [SerializeField] private float hitAngle = 0.5f;

    [Header("Soul Attack")]
    [Tooltip("Check this on the Soul twin's PlayerAttackController")]
    [SerializeField] private bool isSoul = false;
    [SerializeField] private LayerMask soulAttackLayer;
    [SerializeField] private float soulDamageMultiplier = 0.5f;

    [Tooltip("Inject — drag SoulConvergenceSystem GameObject here")]
    [SerializeField] private MonoBehaviour _dmgMultiplierMono;

    private IAttackStrategy _attackStrategy;
    private IAnimationController _animController;
    private AttackRangeIndicator _rangeIndicator;
    private bool _soulMode;

    private void Awake()
    {
        LayerMask effectiveLayer = isSoul ? soulAttackLayer : enemyLayer;
        float effectiveDmg = isSoul ? attackDamage * soulDamageMultiplier : attackDamage;
        var dmgMultiplier = _dmgMultiplierMono as IDamageMultiplier;

        _attackStrategy = new MeleeAttackStrategy(
            transform, attackRange, effectiveDmg, effectiveLayer, hitAngle,
            damageMultiplier: dmgMultiplier);
        _animController = GetComponent<IAnimationController>();
        _rangeIndicator = GetComponentInChildren<AttackRangeIndicator>();
    }

    public void PerformAttack()
    {
        if (_soulMode) return;
        _animController?.PlayAttack();
    }

    public void ExecuteHitDetection()
    {
        if (_soulMode) return;

        _rangeIndicator?.Show(attackRange);
        _attackStrategy?.ExecuteAttack();

        // Stun — only for non-soul attacks
        if (!isSoul)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);
            foreach (var hit in hits)
            {
                Vector3 dir = (hit.transform.position - transform.position).normalized;
                if (Vector3.Dot(transform.forward, dir) < hitAngle) continue;
                hit.GetComponent<StatusEffectController>()
                   ?.ApplyEffect(new StunEffect(hit.gameObject, 2f));
            }
        }

        _rangeIndicator?.Hide();
    }

    public void SetSoulMode(bool value) => _soulMode = value;
}