using UnityEngine;

public class AbilityController : MonoBehaviour, IAbilityLock
{
    private IAbility primaryAbility;
    private IAbility teleportAbility;
    private AbilityRadiusPreview radiusPreview;
    private TeleportMarkerPreview teleportMarkPreview;

    // Injected by TwinAbilitySetup
    private Transform _barrierTransform;
    private float _minCastDistanceFromBarrier = 40f; // upgradeable

    private int _abilityLockCounter = 0;

    public bool AbilitiesLocked => _abilityLockCounter > 0;
    public void LockAbilities() => _abilityLockCounter++;
    public void UnlockAbilities()
        => _abilityLockCounter = Mathf.Max(0, _abilityLockCounter - 1);

    // Called by TwinAbilitySetup after building abilities
    public void SetBarrierReference(Transform barrier, float minCastDistance)
    {
        _barrierTransform = barrier;
        _minCastDistanceFromBarrier = minCastDistance;
    }

    private void Awake()
    {
        radiusPreview = GetComponent<AbilityRadiusPreview>();
        teleportMarkPreview = GetComponent<TeleportMarkerPreview>();
    }

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

    public TeleportAbility GetTeleportAbility() => teleportAbility as TeleportAbility;

    // FIX: added diagnostic log — remove once confirmed working
    public void ActivatePrimary()
    {
        if (AbilitiesLocked)
        {
            Debug.LogWarning($"[AbilityController] {gameObject.name}: " +
                             $"ActivatePrimary blocked — AbilitiesLocked=true " +
                             $"(counter={_abilityLockCounter})", this);
            return;
        }
        if (primaryAbility == null)
        {
            Debug.LogWarning($"[AbilityController] {gameObject.name}: primaryAbility is null.", this);
            return;
        }
        primaryAbility.TryActivate();
    }

    public void ActivateTeleport()
    {
        // Check minimum distance from barrier before allowing cast
        if (_barrierTransform != null)
        {
            float dist = Vector3.Distance(
                transform.position, _barrierTransform.position);

            if (dist < _minCastDistanceFromBarrier)
            {
                Debug.Log($"[AbilityController] {gameObject.name}: " +
                          $"Too far from barrier to teleport. " +
                          $"Distance={dist:F1}, required<={_minCastDistanceFromBarrier}");
                // TODO: show "Move closer to barrier" UI feedback here
                return;
            }
        }
        teleportAbility?.TryActivate();
    }

    public void ShowTeleportPreview()
    {
        if (AbilitiesLocked) return;

        // Same distance check for preview
        if (_barrierTransform != null)
        {
            float dist = Vector3.Distance(
                transform.position, _barrierTransform.position);
            if (dist < _minCastDistanceFromBarrier) return;
        }

        teleportMarkPreview?.Show(GetTeleportPos());
    }

    public void HideTeleportPreview() => teleportMarkPreview?.Hide();

    private void Update()
    {
        primaryAbility?.Tick();
        teleportAbility?.Tick();
    }

    public void ShowPrimaryPreview(float radius)
    {
        if (AbilitiesLocked) return;
        radiusPreview?.Show(radius);
    }

    public void HidePrimaryPreview() => radiusPreview?.Hide();

    public float GetPrimaryRange()
    {
        if (primaryAbility is AbilityBase b) return b.GetRange();
        return 0f;
    }

    public Transform GetTeleportPos()
        => (teleportAbility as TeleportAbility)?.GetTeleportPos();

    // Upgradeable via skill tree
    public void SetMinCastDistance(float distance)
        => _minCastDistanceFromBarrier = distance;
}
