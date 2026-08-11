using System.Collections;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Tracks this enemy's social bond — partner reference and bond type.
/// Death bond: when partner dies, this enemy dies too UNLESS BondBroken.
/// Set partner via SetBondPartner() after spawning.
///
/// ATTACH: To every enemy prefab.
/// </summary>
public class EnemySocialBond : MonoBehaviour
{
    public enum BondType
    {
        SeveredPair,        // Already built — shared health, grief rage
        ComboPartner,       // Compatible cross-clan combo partner (dynamic)
    }

    [Header("Bond Config")]
    [SerializeField] private BondType _bondType = BondType.ComboPartner;
    [Tooltip("Max distance for combo power activation.")]
    [SerializeField] private float _comboBondRange = 8f;
    [Tooltip("Max distance for death bond — further = not linked.")]
    [SerializeField] private float _deathBondRange = 30f;

    private Enemy _enemy;
    private Enemy _bondPartner;
    private EnemyDarkEnergy _darkEnergy;
    private bool _partnerDead;

    // ── Public ─────────────────────────────────────────────
    public Enemy BondPartner => _bondPartner;
    public bool PartnerDead => _partnerDead;
    public bool HasPartner => _bondPartner != null && !_bondPartner.Health.IsDead;
    public BondType Type => _bondType;
    public float ComboBondRange => _comboBondRange;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _darkEnergy = GetComponent<EnemyDarkEnergy>();
    }

    private void Start()
    {
        _enemy = GetComponent<Enemy>();
        _darkEnergy = GetComponent<EnemyDarkEnergy>();

        if (_enemy != null)
            _enemy.Health.OnDeath += OnSelfDied;
    }
    private void Update()
    {
        if (_bondPartner == null || _enemy == null) return;
        WriteToBlackboard();
    }

    // ── Setup ──────────────────────────────────────────────
    public void SetBondPartner(Enemy partner, BondType type)
    {
        _bondPartner = partner;
        _bondType = type;
        _partnerDead = false;

        // Subscribe to partner death
        if (_bondPartner != null)
            _bondPartner.Health.OnDeath += OnPartnerDied;

        UnityEngine.Debug.Log($"[Bond] {_enemy?.name} bonded to {partner?.name} ({type})");
    }

    public void ClearBond()
    {
        if (_bondPartner != null)
            _bondPartner.Health.OnDeath -= OnPartnerDied;
        _bondPartner = null;
        _partnerDead = false;
    }

    // ── Death bond ─────────────────────────────────────────
    private void OnPartnerDied()
    {
        if (_enemy == null || _enemy.Health.IsDead) return;
        _partnerDead = true;
        UnityEngine.Debug.Log($"[Bond] {_enemy?.name} partner died — BondBroken={_darkEnergy?.BondBroken}");

        MoodEventBus.AllyDied(_bondPartner?.gameObject);

        // If dark energy below threshold — this enemy dies too
        if (_darkEnergy != null && !_darkEnergy.BondBroken)
        {
            // Check range — only linked if close enough
            if (_bondPartner != null)
            {
                float dist = Vector3.Distance(transform.position, _bondPartner.transform.position);
                if (dist <= _deathBondRange)
                {
                    UnityEngine.Debug.Log($"[Bond] {_enemy?.name} dying with partner (energy below threshold)");
                    StartCoroutine(DelayedDeath());
                    return;
                }
            }
        }

        // Bond broken or out of range — survive, mood change
        var moodSystem = GetComponent<EnemyMoodSystem>();
        if (_darkEnergy != null && _darkEnergy.BondBroken)
        {
            // Survived because of dark energy — become more dangerous
            moodSystem?.TransitionTo(EnemyMood.Enraged, 0f, EnemyMood.Aggressive);
            UnityEngine.Debug.Log($"[Bond] {_enemy?.name} SURVIVED partner death — bond broken, entering rage");
        }
        else
        {
            moodSystem?.TransitionTo(EnemyMood.Grieving, 3f, EnemyMood.Enraged);
        }
    }

    private void OnSelfDied()
    {
        // Notify partner
        if (_bondPartner != null && !_bondPartner.Health.IsDead)
        {
            var partnerBond = _bondPartner.GetComponent<EnemySocialBond>();
            partnerBond?.OnPartnerDied();
        }
    }

    private IEnumerator DelayedDeath()
    {
        float duration = _enemy?.Data?.deathBondGraceDuration ?? 1.2f;
        yield return new WaitForSeconds(duration);
        if (!_enemy.Health.IsDead)
            _enemy.Health.TakeDamage(new DamageData(9999f, DamageType.Environmental));
    }

    // ── Blackboard ─────────────────────────────────────────
    private void WriteToBlackboard()
    {
        var brain = GetComponent<PoTGOAPBrainBase>();
        if (brain?.LinkedBlackboard == null) return;
        brain.LinkedBlackboard.Set(PoTNames.BondPartnerAlive, HasPartner);
        brain.LinkedBlackboard.Set(PoTNames.BondBroken, _darkEnergy?.BondBroken ?? false);
    }

    private void OnDestroy()
    {
        if (_enemy != null)
            _enemy.Health.OnDeath -= OnSelfDied;
    }
}