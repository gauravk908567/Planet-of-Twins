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

    [Header("VFX (Cue Book)")]
    // Book pulled from PlayerVfxLibrary.AccordSpirit via VfxLibraryProvider (R4, resolved lazily at play time);
    // passed to each spawned spirit. Charge = held charge on twins (R), Impact = knockback, Loop = arrival ring.
    private CueBookData _cueBook;
    [Tooltip("Optional explicit FxManager (R1). Resolves to .Instance otherwise (R4).")]
    [SerializeField] private FxManager _fx;

    private ISkillUnlockState _unlockState;
    private IAccordModeProvider _accordMode;
    private IInputProvider _input;

    private float _chargeProgress = 0f;
    private float _cooldownTimer = 0f;
    private bool _isCharging = false;

    private CueHandle _leftChargeHandle;
    private CueHandle _rightChargeHandle;

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

        var go = GameplayPool.Spawn(_spiritPrefab, PoolCategory.AbilityObjects, pos, twin.transform.rotation);
        var agent = go != null ? go.GetComponent<AccordSpiritAgent>() : null;

        if (agent == null)
        {
            Debug.LogError("[AccordSpiritSystem] Prefab missing AccordSpiritAgent.", this);
            GameplayPool.Despawn(go);
            return;
        }

        _cueBook ??= VfxLibraryProvider.Instance?.Player?.AccordSpirit;  // R4 — book from PlayerVfxLibrary
        agent.Initialise(
            _enemyLayer,
            _cleansePulseRadius,
            _portalDamagePerSec,
            _portalDuration,
            _portalRadius,
            this,
            claimedTargets,
            _cueBook,
            isKai: twin == _rightTwin);   // right = Kai (Vethara), left = Lyra (Luminari)
    }

 // ── VFX helpers (pooled cues — Tier 1) ──────────────
    private void PlayChargeVFX()
    {
        _fx ??= FxManager.Instance;   // R4
        _cueBook ??= VfxLibraryProvider.Instance?.Player?.AccordSpirit;  // R4 — book from PlayerVfxLibrary
        if (_fx == null || _cueBook == null) return;
        // Per-twin charge (Kai=right/Vethara, Lyra=left/Luminari) — book re-authored from the old shared "charge".
        if (_leftTwin != null)  _leftChargeHandle  = _fx.PlayBook(_cueBook, FxIds.Player.AccordSpirit.on_accspiritLyra, CueContext.Follow(_leftTwin.transform));
        if (_rightTwin != null) _rightChargeHandle = _fx.PlayBook(_cueBook, FxIds.Player.AccordSpirit.on_accspiritKai, CueContext.Follow(_rightTwin.transform));
    }

    private void StopChargeVFX()
    {
        _fx?.Stop(_leftChargeHandle);
        _fx?.Stop(_rightChargeHandle);
        _leftChargeHandle = CueHandle.None;
        _rightChargeHandle = CueHandle.None;
    }

    private void PlayKnockbackVFX(Player twin)
    {
        if (twin == null) return;
        _fx ??= FxManager.Instance;
        _cueBook ??= VfxLibraryProvider.Instance?.Player?.AccordSpirit;  // R4 — book from PlayerVfxLibrary
        if (_cueBook == null) return;
        // Summon knockback burst on the twin (visual only — no hitbox). simSpeed: fast.
        _fx?.PlayBook(_cueBook, FxIds.Player.AccordSpirit.on_accspiritknocback, CueContext.Follow(twin.transform, simSpeed: 2.5f));
    }

    // Unlocked via DualCast/AccordSpirit node — no Empower dependency
    private bool IsUnlocked() =>
        _unlockState != null && _unlockState.IsAccordStateUnlocked;
}