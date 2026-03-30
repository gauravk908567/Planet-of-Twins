using UnityEngine;

/// <summary>
/// Composite IDamageMultiplier pattern:
/// PlayerAttackController implements IDamageMultiplier and passes 'this'
/// to MeleeAttackStrategy. DamageOutMultiplier composites two sources:
///   1. SoulConvergenceSystem (injected via _dmgMultiplierMono)
///   2. EmpowerSystem's empowered-twin bonus (set via SetEmpowerMultiplier)
/// Neither system knows about the other. MeleeAttackStrategy reads one
/// interface and gets both multipliers automatically.
/// </summary>
public class PlayerAttackController : MonoBehaviour, IDamageMultiplier
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

    [Header("VFX")]
    [SerializeField] private GameObject _slashPrefab;
    [SerializeField] private GameObject _hitPrefab;

    // ── IDamageMultiplier (composite) ─────────────────────────
    private IDamageMultiplier _scMultiplier;
    private float _empowerDamageMultiplier = 1f;

    /// <summary>
    /// Composite: SC multiplier × Empower multiplier.
    /// MeleeAttackStrategy reads this on every hit.
    /// </summary>
    public float DamageOutMultiplier =>
        (_scMultiplier?.DamageOutMultiplier ?? 1f) * _empowerDamageMultiplier;

    /// <summary>
    /// Required by IDamageMultiplier. Incoming damage reduction is
    /// owned by SoulConvergenceSystem and SharedHealthPool — not here.
    /// </summary>
    public float DamageInMultiplier => 1f;

    /// <summary>
    /// Called by EmpowerSystem when empowering this twin (e.g. 1.35f).
    /// Call with 1f to remove the buff on Empower end.
    /// </summary>
    public void SetEmpowerMultiplier(float multiplier) =>
        _empowerDamageMultiplier = Mathf.Max(0f, multiplier);

    // ── Internals ─────────────────────────────────────────────
    private IAttackStrategy _attackStrategy;
    private IAnimationController _animController;
    private AttackRangeIndicator _rangeIndicator;
    private bool _soulMode;

    private void Awake()
    {
        _scMultiplier = _dmgMultiplierMono as IDamageMultiplier;

        LayerMask effectiveLayer = isSoul ? soulAttackLayer : enemyLayer;
        float effectiveDmg = isSoul ? attackDamage * soulDamageMultiplier : attackDamage;

        // Pass 'this' — MeleeAttackStrategy calls DamageOutMultiplier which
        // composites SC + Empower automatically.
        _attackStrategy = new MeleeAttackStrategy(
            transform, attackRange, effectiveDmg, effectiveLayer, hitAngle,
            damageMultiplier: this,
            slashPrefab: _slashPrefab,
            hitPrefab: _hitPrefab);

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

        if (!isSoul)
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position, attackRange, enemyLayer);

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