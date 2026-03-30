using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AccordSpiritSystem — Accord Spirits ability (R hold during Accord State).
/// Unlocked via the AccordSpirit/DualCast skill tree node.
/// Completely independent from EmpowerSystem — does NOT require Empower to be purchased.
/// </summary>
public class AccordSpiritSystem : MonoBehaviour, IAbilityHUDSource
{
    [Header("Inject")]
    [SerializeField] private MonoBehaviour _unlockStateMono;
    [SerializeField] private MonoBehaviour _accordModeMono;
    [SerializeField] private MonoBehaviour _inputProviderMono;
    [SerializeField] private Player _leftTwin;
    [SerializeField] private Player _rightTwin;

    [Header("Spirit prefab")]
    [SerializeField] private GameObject _spiritPrefab;
    [SerializeField] private LayerMask _enemyLayer;

    [Header("Activation")]
    [SerializeField] private float _chargeHoldTime = 0.75f;

    [Header("Cooldown")]
    [SerializeField] private float _cooldown = 7f;

    [Header("Spirit stats")]
    [SerializeField] private float _cleansePulseRadius = 4f;
    [SerializeField] private float _portalDamagePerSec = 12f;
    [SerializeField] private float _portalDuration = 3.5f;
    [SerializeField] private float _portalRadius = 2f;

    [Header("Upgrade — open for count increase")]
    [SerializeField] private int _spiritsPerSide = 1;

    [Header("VFX")]
    [Tooltip("Charge effect on twins while holding R — same as SC charge prefab, tinted differently.")]
    [SerializeField] private GameObject _chargeEffectPrefab;
    [Tooltip("Knockback particle played on both twins at moment of summon.")]
    [SerializeField] private GameObject _knockbackParticlePrefab;
    [Tooltip("Arrival charge effect played on spirit when it reaches target.")]
    [SerializeField] private GameObject _arrivalChargeEffectPrefab;
    [Tooltip("Ring particle spawned at spirit arrival position for portal duration.")]
    [SerializeField] private GameObject _ringParticlePrefab;

    private ISkillUnlockState _unlockState;
    private IAccordModeProvider _accordMode;
    private IInputProvider _input;

    private float _chargeProgress = 0f;
    private float _cooldownTimer = 0f;
    private bool _isCharging = false;

    private ParticleSystem _leftChargeVFX;
    private ParticleSystem _rightChargeVFX;

    // ── IAbilityHUDSource ─────────────────────────────────────
    public string AbilityName => "";  // name set directly on TMP text by designer
    public int CurrentCharges => 1;
    public int MaxCharges => 1;
    public bool IsActive => _isCharging;
    public float CooldownProgress
    {
        get
        {
            if (_isCharging) return _chargeProgress;
            if (_cooldownTimer > 0f) return Mathf.Clamp01(1f - (_cooldownTimer / _cooldown));
            return 1f;
        }
    }

    public bool IsCharging => _isCharging;
    public float ChargeProgress => _chargeProgress;
    public bool IsReady => _cooldownTimer <= 0f;

    private void Awake()
    {
        _unlockState = _unlockStateMono as ISkillUnlockState;
        _accordMode = _accordModeMono as IAccordModeProvider;
        _input = _inputProviderMono as IInputProvider;

        if (_unlockState == null) Debug.LogError("[AccordSpiritSystem] Missing ISkillUnlockState", this);
        if (_accordMode == null) Debug.LogError("[AccordSpiritSystem] Missing IAccordModeProvider", this);
        if (_input == null) Debug.LogError("[AccordSpiritSystem] Missing IInputProvider", this);
    }

    private void Update()
    {
        // Unlocked via AccordSpirit node — independent of Empower
        if (!IsUnlocked()) return;

        // Only active during Accord State
        if (_accordMode == null || !_accordMode.IsAccordActive)
        {
            CancelCharge();
            return;
        }

        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
            CancelCharge();
            return;
        }

        HandleInput();
    }

    private void HandleInput()
    {
        if (_input.GetEmpowerHeld())
        {
            if (!_isCharging)
            {
                _isCharging = true;
                PlayChargeVFX();
            }
            _chargeProgress = Mathf.Clamp01(_chargeProgress + Time.deltaTime / _chargeHoldTime);
            if (_chargeProgress >= 1f)
            {
                _chargeProgress = 0f;
                _isCharging = false;
                StopChargeVFX();
                SummonSpirits();
            }
        }
        else
        {
            CancelCharge();
        }
    }

    private void CancelCharge()
    {
        if (_isCharging) StopChargeVFX();
        _isCharging = false;
        _chargeProgress = 0f;
    }

    private void SummonSpirits()
    {
        _cooldownTimer = _cooldown;

        // Knockback pulse VFX on both twins at summon
        PlayKnockbackVFX(_leftTwin);
        PlayKnockbackVFX(_rightTwin);

        // Shared claimed targets list — spirits pick different enemies
        var claimedTargets = new List<Transform>();

        for (int i = 0; i < _spiritsPerSide; i++)
            SpawnSpirit(_leftTwin, claimedTargets);

        for (int i = 0; i < _spiritsPerSide; i++)
            SpawnSpirit(_rightTwin, claimedTargets);

        Debug.Log($"[AccordSpiritSystem] Summoned {_spiritsPerSide * 2} spirits");
    }

    private void SpawnSpirit(Player twin, List<Transform> claimedTargets)
    {
        if (_spiritPrefab == null || twin == null) return;

        Vector3 pos = twin.transform.position;
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            pos = hit.position;

        var go = Instantiate(_spiritPrefab, pos, twin.transform.rotation);
        var agent = go.GetComponent<AccordSpiritAgent>();

        if (agent == null)
        {
            Debug.LogError("[AccordSpiritSystem] Prefab missing AccordSpiritAgent.", this);
            Destroy(go);
            return;
        }

        agent.Initialise(
            _enemyLayer,
            _cleansePulseRadius,
            _portalDamagePerSec,
            _portalDuration,
            _portalRadius,
            this,
            claimedTargets,
            _arrivalChargeEffectPrefab,
            _ringParticlePrefab);
    }

    // ── VFX helpers ───────────────────────────────────────────
    private void PlayChargeVFX()
    {
        _leftChargeVFX = SpawnAndPlay(_chargeEffectPrefab, _leftTwin);
        _rightChargeVFX = SpawnAndPlay(_chargeEffectPrefab, _rightTwin);
    }

    private void StopChargeVFX()
    {
        StopPS(_leftChargeVFX);
        StopPS(_rightChargeVFX);
        _leftChargeVFX = null;
        _rightChargeVFX = null;
    }

    private void PlayKnockbackVFX(Player twin)
    {
        if (_knockbackParticlePrefab == null || twin == null) return;
        var go = Instantiate(_knockbackParticlePrefab,
                             twin.transform.position,
                             _knockbackParticlePrefab.transform.rotation,
                             twin.transform);
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play(true);
            Destroy(go, ps.main.duration + ps.main.startLifetime.constantMax + 0.1f);
        }
        else Destroy(go, 2f);
    }

    private ParticleSystem SpawnAndPlay(GameObject prefab, Player twin)
    {
        if (prefab == null || twin == null) return null;
        var go = Instantiate(prefab, twin.transform.position,
                             prefab.transform.rotation, twin.transform);
        var ps = go.GetComponent<ParticleSystem>();
        ps?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps?.Play(true);
        return ps;
    }

    private void StopPS(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(ps.gameObject, ps.main.startLifetime.constantMax + 0.1f);
    }

    // Unlocked via DualCast/AccordSpirit node — no Empower dependency
    private bool IsUnlocked() =>
        _unlockState != null && _unlockState.IsAccordStateUnlocked;
}