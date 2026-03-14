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
    public void LockAbilities() => _abilityLockCounter++;
    public void UnlockAbilities() => _abilityLockCounter = Mathf.Max(0, _abilityLockCounter - 1);

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
        if (AbilitiesLocked)
        {
            Debug.LogWarning($"[AbilityController] {gameObject.name}: primary blocked " +
                $"(lockCounter={_abilityLockCounter})", this);
            return;
        }
        primaryAbility?.TryActivate();
    }

    // ── Teleport — normal path (barrier check applies) ────────
    // Reserved for future non-emergency gate usage.
    public void ActivateTeleport()
    {
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
}