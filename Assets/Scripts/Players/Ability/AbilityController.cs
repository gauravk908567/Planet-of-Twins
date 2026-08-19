using UnityEngine;

public class AbilityController : MonoBehaviour, IAbilityLock
{
    private IAbility primaryAbility;
    private IAbility teleportAbility;
    private AbilityRadiusPreview radiusPreview;
    private TeleportMarkerPreview teleportMarkPreview;

    private Transform _barrierTransform;
    private float _minCastDistanceFromBarrier = 40f;

    private int _abilityLockCounter = 0;
    public bool AbilitiesLocked => _abilityLockCounter > 0;

    // Events for UI — fired when suppression starts/ends
    public event System.Action OnSuppressed;
    public event System.Action OnSuppressionLifted;

    public void LockAbilities()
    {
        _abilityLockCounter++;
        if (_abilityLockCounter == 1) OnSuppressed?.Invoke();
    }

    // ── Primary-only lock (for Q-only suppression from bombs) ─
    private int _primaryLockCounter = 0;
    public bool PrimaryLocked => _primaryLockCounter > 0 || AbilitiesLocked;

    public event System.Action OnPrimarySuppressed;
    public event System.Action OnPrimarySuppressionLifted;

    public void LockPrimaryOnly()
    {
        _primaryLockCounter++;
        if (_primaryLockCounter == 1) OnPrimarySuppressed?.Invoke();
    }
    public void UnlockPrimaryOnly()
    {
        _primaryLockCounter = Mathf.Max(0, _primaryLockCounter - 1);
        if (_primaryLockCounter == 0) OnPrimarySuppressionLifted?.Invoke();
    }
    public void UnlockAbilities()
    {
        _abilityLockCounter = Mathf.Max(0, _abilityLockCounter - 1);
        if (_abilityLockCounter == 0) OnSuppressionLifted?.Invoke();
    }

    private void Awake()
    {
        radiusPreview = GetComponent<AbilityRadiusPreview>();
        teleportMarkPreview = GetComponent<TeleportMarkerPreview>();
    }

    // ── Setup ─────────────────────────────────────────────────
    public void SetPrimaryAbility(IAbility ability)
    {
        primaryAbility = ability;
        primaryAbility.Initialize(gameObject);
    }

    public void SetTeleportAbility(IAbility ability)
    {
        teleportAbility = ability;
        teleportAbility.Initialize(gameObject);

        if (ability is TeleportAbility ta && teleportMarkPreview != null)
        {
            var caster = GetComponent<Player>();
            teleportMarkPreview.Initialise(ta, caster.transform, ta.GetTeleportPos());
        }
    }

    public void SetBarrierReference(Transform barrier, float minCastDistance)
    {
        _barrierTransform = barrier;
        _minCastDistanceFromBarrier = minCastDistance;
    }

    public void SetMinCastDistance(float distance) =>
        _minCastDistanceFromBarrier = distance;

    // ── Primary ───────────────────────────────────────────────
    public void ActivatePrimary()
    {
        if (PrimaryLocked)
        {
            Debug.LogWarning($"[AbilityController] {gameObject.name}: primary blocked " +
                $"(abilityLock={_abilityLockCounter} primaryLock={_primaryLockCounter})", this);
            return;
        }
        primaryAbility?.TryActivate();
    }

    // ── Teleport — normal path (barrier check applies) ────────
    // Reserved for future non-emergency gate usage.
    public void ActivateTeleport()
    {
        // Lazy-resolve barrier from BarrierPOI so TwinAbilitySetup
        // doesn't need a cross-scene serialized Transform reference.
        if (_barrierTransform == null)
        {
            var bp = POIManager.Instance?.GetNearest(transform.position, POIType.Barrier);
            if (bp != null) _barrierTransform = bp.transform;
        }

        if (_barrierTransform != null)
        {
            float dist = Vector3.Distance(transform.position, _barrierTransform.position);
            if (dist > _minCastDistanceFromBarrier)
            {
                Debug.Log($"[AbilityController] {gameObject.name}: too far from barrier " +
                    $"({dist:F1} > {_minCastDistanceFromBarrier})");
                return;
            }
        }
        teleportAbility?.TryActivate();
    }

    // ── Teleport — emergency path (barrier check bypassed) ────
    // Called by TwinAbilityDispatcher when EmergencyTeleportMonitor.IsEmergencyAvailable.
    // During rescue the caster can be anywhere — barrier distance is irrelevant.
    public void ActivateTeleportEmergency()
    {
        // Double teleport guard — if this controller's teleport is already active, block
        var ta = teleportAbility as TeleportAbility;
        if (ta != null && ta.IsActive) return;
        teleportAbility?.TryActivate();
    }

    // ── Preview ───────────────────────────────────────────────
    // No barrier check — marker should always show during emergency.
    public void ShowTeleportPreview()
    {
        if (AbilitiesLocked) return;
        teleportMarkPreview?.Show(GetTeleportPos());
    }

    public void HideTeleportPreview() => teleportMarkPreview?.Hide();

    /// <summary>Bench cheat (GameDebuggerV2): both abilities instantly ready. Not for gameplay code.</summary>
    public void DebugClearCooldowns()
    {
        (primaryAbility as AbilityBase)?.DebugClearCooldown();
        (teleportAbility as AbilityBase)?.DebugClearCooldown();
    }

    public void ShowPrimaryPreview(float radius)
    {
        if (AbilitiesLocked) return;
        radiusPreview?.Show(radius);
    }

    public void HidePrimaryPreview() => radiusPreview?.Hide();

    // ── Tick ──────────────────────────────────────────────────
    private void Update()
    {
        primaryAbility?.Tick();
        teleportAbility?.Tick();
    }

    // ── Accessors ─────────────────────────────────────────────
    public TeleportAbility GetTeleportAbility() => teleportAbility as TeleportAbility;
    public Transform GetTeleportPos() => (teleportAbility as TeleportAbility)?.GetTeleportPos();
    public float GetPrimaryRange() => primaryAbility is AbilityBase b ? b.GetRange() : 0f;
    public IAbilityHUDSource GetPrimaryHUDSource() => primaryAbility as IAbilityHUDSource;
    public IAbilityHUDSource GetTeleportHUDSource() => teleportAbility as IAbilityHUDSource;

    // ── Couch S2: shared-cooldown readiness ───────────────────
    // Primary (Q) and emergency Teleport (C) are shared: TwinAbilityDispatcher gates on BOTH twins'
    // readiness, so casting either (Kai=Stun/VoidStrike, Lyra=Possess/RadiantSeeker) puts that ability
    // on cooldown → the AND becomes false → the slot is locked for both until it replenishes (the used
    // ability's own cooldown; a whiff never starts a cooldown, so it never false-locks). Readiness is
    // read through IAbilityHUDSource so it works for every ability type without a concrete-type check.
    // COOLDOWN-based only — per-twin PrimaryLocked (bomb suppression) is enforced inside ActivatePrimary,
    // so it blocks that twin's cast without locking the shared slot.
    public bool IsPrimaryReady => IsAbilityReady(primaryAbility);
    public bool IsTeleportReady => IsAbilityReady(teleportAbility);

    private static bool IsAbilityReady(IAbility ability) =>
        ability is IAbilityHUDSource hud && !hud.IsActive && hud.CooldownProgress >= 1f;
}