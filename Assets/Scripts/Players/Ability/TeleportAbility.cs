using System;
using System.Collections;
using UnityEngine;

public class TeleportAbility : AbilityBase
{
    private readonly Player _caster;
    private readonly Player _target;
    private readonly SoulPlayer _soul;
    private readonly ITimeFactorController _timeFactorController;
    private readonly ICoroutineRunner _coroutineRunner;
    private readonly ISelectionLock _selectionLock;
    private IAbilityLock _casterAbilityLock;  // ADD
    private IAbilityLock _targetAbilityLock;  // ADD

    private CharacterController _soulCC;
    private bool _soulHasArrived = false;
    private bool _soulTimerPaused = false;
    private float _activationTime = 0f;
    private Coroutine _activeTeleportCoroutine = null;
    private Vector3 _markerPosition;
    private bool _markerSet = false;

    public event Action<float> OnSoulTimerUpdated;
    public event Action OnSoulArrived;

    [SerializeField] private float minTravelDistance = 3f;
    private float _distanceTravelled = 0f;

    public TeleportAbility(
        AbilityData data,
        Player caster,
        Player target,
        Player soulPlayer,
        ITimeFactorController timeFactorController,
        ICoroutineRunner coroutineRunner,
        ISelectionLock selectionLock)
        : base(data)
    {
        _caster = caster;
        _target = target;
        _soul = soulPlayer as SoulPlayer;
        _timeFactorController = timeFactorController;
        _coroutineRunner = coroutineRunner;
        _selectionLock = selectionLock;

        if (_soul == null)
            UnityEngine.Debug.LogError("[TeleportAbility] soulPlayer is not SoulPlayer.");
        else
        {
            _soul.SetLinkedPlayers(caster, target);
            _soulCC = _soul.GetComponent<CharacterController>();
            _soul.OnSoulDied += HandleSoulDied;
        }

        // ADD: cache ability locks from both twins
        _casterAbilityLock = caster.GetComponent<AbilityController>();
        _targetAbilityLock = target.GetComponent<AbilityController>();
    }

    public void SetMarkerPosition(Vector3 worldPosition)
    {
        _markerPosition = worldPosition;
        _markerSet = true;
    }

    public Transform GetTeleportPos() => _target.transform;

    public void PauseSoulTimer() => _soulTimerPaused = true;
    public void ResumeSoulTimer() => _soulTimerPaused = false;

    public void ForceEnd() { if (isActive) End(); }

    protected override bool Activate()
    {
        if (_soul == null || _caster == null || _coroutineRunner == null)
        {
            UnityEngine.Debug.LogError("[TeleportAbility] Null ref — check Inspector slots.");
            return false;
        }

        if (_activeTeleportCoroutine != null)
        {
            _coroutineRunner.StopCoroutine(_activeTeleportCoroutine);
            _activeTeleportCoroutine = null;
        }

        _timeFactorController?.TriggerEffect();
        _soulHasArrived = false;
        _soulTimerPaused = false;

        _selectionLock?.LockSelection();
        _casterAbilityLock?.LockAbilities();   // FIX: block primary ability on caster
        _targetAbilityLock?.LockAbilities();   // FIX: block primary ability on target

        _soul.ShouldSoulSleep(false);

        // Disable CC to allow direct position set
        if (_soulCC != null) _soulCC.enabled = false;
        _soul.transform.position = _caster.transform.position;
        if (_soulCC != null) _soulCC.enabled = true;

        _soul.Movement?.SetMovementLocked(true);

        Vector3 destination = _markerSet ? _markerPosition : _target.transform.position;

        _activeTeleportCoroutine = _coroutineRunner.StartCoroutine(
       TravelToTarget(_soul.transform, destination, speed: 40f,
           requireMinDistance: true,   // ADD
           onArrival: () =>
           {
               _soulHasArrived = true;
               _activationTime = UnityEngine.Time.time;
               _activeTeleportCoroutine = null;
               _soul.Movement?.SetMovementLocked(false);
               OnSoulArrived?.Invoke();
           }));

        return true;
    }

    public override void Tick()
    {
        if (!_soulHasArrived) return;
        if (!_soulTimerPaused)
        {
            base.Tick();
            float elapsed = UnityEngine.Time.time - _activationTime;
            float remaining = Mathf.Max(0f, data.duration - elapsed);
            OnSoulTimerUpdated?.Invoke(remaining / data.duration);
        }
    }

    protected override void End()
    {
        _soulHasArrived = false;
        _soulTimerPaused = false;
        _markerSet = false;

        _soul?.Movement?.SetMovementLocked(true);

        if (_activeTeleportCoroutine != null)
        {
            _coroutineRunner.StopCoroutine(_activeTeleportCoroutine);
            _activeTeleportCoroutine = null;
        }

        _timeFactorController?.ResolveEffect();

        _selectionLock?.UnlockSelection();
        _casterAbilityLock?.UnlockAbilities();   // FIX: unblock on end
        _targetAbilityLock?.UnlockAbilities();   // FIX: unblock on end

        _activeTeleportCoroutine = _coroutineRunner.StartCoroutine(
            TravelToTarget(_soul.transform, _caster.transform.position, speed: 40f,
                onArrival: () =>
                {
                    _soul?.ShouldSoulSleep(true);
                    _soul?.Movement?.SetMovementLocked(false);
                    _activeTeleportCoroutine = null;
                }));

        base.End();
    }

    private void HandleSoulDied() { if (isActive) ForceEnd(); }

    private IEnumerator TravelToTarget(
        Transform origin, Vector3 destination, float speed,
        Action onArrival, bool requireMinDistance = false)
    {
        _distanceTravelled = 0f;
        Vector3 lastPos = origin.position;

        while (Vector3.Distance(origin.position, destination) > 0.05f)
        {
            if (_soulCC != null) _soulCC.enabled = false;

            Vector3 newPos = Vector3.MoveTowards(
                origin.position, destination, speed * UnityEngine.Time.deltaTime);

            _distanceTravelled += Vector3.Distance(origin.position, newPos);
            origin.position = newPos;

            if (_soulCC != null) _soulCC.enabled = true;
            yield return null;
        }

        // Only fire arrival if min distance travelled (prevents instant trigger)
        if (requireMinDistance && _distanceTravelled < minTravelDistance)
        {
            // Soul arrived too fast — wait at destination until min distance met
            // This handles the case where partner is very close
            yield return new WaitForSeconds(0.3f);
        }

        onArrival?.Invoke();
    }
}

