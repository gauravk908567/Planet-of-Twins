using System;
using System.Collections;
using UnityEngine;

public class GroupGrabEnemy : Enemy, IRescueTarget
{
    [Header("Grab Config")]
    [SerializeField] private float behindTimeRequired = 1.5f;
    [SerializeField] private float behindDotThreshold = -0.3f;
    [SerializeField] private float pileRadius = 4f;   // nearby allies join pile
    [SerializeField] private LayerMask enemyLayer;

    [Header("IRescueTarget values")]
    [SerializeField] private float _timeToKill = 5f;
    [SerializeField] private float _mashFrequency = 4f;
    [SerializeField] private float _mashWindowDuration = 3f;
    [SerializeField] private float _mashCooldown = 0.75f;
    [SerializeField] private float _partialHealAmount = 20f;
    [SerializeField] private int _trapTier = 2;    // grabber = fully frozen

    [Header("Struggle")]
    [SerializeField] private float strugglePauseDuration = 0.4f;
    private Coroutine _strugglePauseCoroutine;

    [Header("Alert Ranges")]
    [SerializeField] private float chaseAlertRange = 6f;   // while chasing
    [SerializeField] private float grabAlertRange = 14f;  // after grabbing — much larger

    private bool _isResolved;

    // Grab cooldown — prevents re-grab after rescue
    private bool _grabOnCooldown;
    [SerializeField] private float grabCooldownAfterRescue = 3f;

    public IEnemyState GrabState { get; private set; }
    public IEnemyState BehindTimer { get; private set; }
    public IEnemyState FrontAttackState { get; private set; }
    public PileState PileStateObj { get; private set; }

    public float ChaseAlertRange => chaseAlertRange;
    public LayerMask EnemyLayer => enemyLayer;
    public void AlertNearby(Transform playerTransform, bool isGrab)
        => AlertNearbyEnemies(playerTransform, isGrab);
    // ── IRescueTarget ──────────────────────────────────────────
    public Transform GrabbedPlayerTransform => _grabbedPlayer?.transform;
    public Player GrabbedPlayer => _grabbedPlayer;
    public float NormalisedTTK => _ttkRemaining / _timeToKill;
    public float MashFrequency => _mashFrequency;
    public float MashWindowDuration => _mashWindowDuration;
    public float MashCooldown => _mashCooldown;
    public float PartialHealAmount => _partialHealAmount;
    public bool CanGrabbedPlayerStruggle => _trapTier == 1;
    public float RescueProximityRadius => 1.5f;

    public event Action<Player> OnPlayerGrabbed;
    public event Action OnPlayerReleased;
    public event Action OnPlayerKilled;

    private Player _grabbedPlayer;
    private float _ttkRemaining;
    private bool _ttkPaused;
    private bool _isGrabbing;

    // Pre-allocated for pile detection
    private readonly Collider[] _pileBuffer = new Collider[8];

    protected override void InitStates()
    {
        BehindTimer = new BehindTimerState(this, behindTimeRequired, behindDotThreshold);
        FrontAttackState = new FrontAttackState(this, behindDotThreshold); // NEW
        GrabState = new GrabState(this);
        PileStateObj = new PileState(this);
        IdleState = new EnemyIdleState(this);
        ChaseState = new EnemyChaseState(this);

        // FIX: AttackState now points to FrontAttackState — NOT BehindTimerState
        // This breaks the infinite loop
        AttackState = FrontAttackState;

        PossessedState = new PossessedState(this, possessedTargetLayer);
    }

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);
        if (data is GroupGrabEnemyData grabData)
            behindTimeRequired = grabData.behindTimeRequired;
    }
    private void Update()
    {
        if (_isGrabbing && !_ttkPaused)
        {
            _ttkRemaining -= Time.deltaTime;
            if (_ttkRemaining <= 0f)
                KillPlayer();
        }
    }

    // ── Called by GrabState.Enter() ───────────────────────────
    public void StartGrab()
    {
        if (Target == null) return;
        if (_grabOnCooldown) return;

        var player = Target.GetComponent<Player>();
        if (player == null) return;
        if (player.IsGrabbed) return;

        _isResolved = false;   // ADD — reset on new grab
        _grabbedPlayer = player;
        _grabbedPlayer.SetGrabbed(true);  // ADD
        _ttkRemaining = _timeToKill;
        _ttkPaused = false;
        _isGrabbing = true;

        if (_trapTier >= 2)
            (player.Movement as IMovementFreezable)?.SetFrozen(true);

        OnPlayerGrabbed?.Invoke(player);
        AlertNearbyEnemies(player.transform, isGrab: true);
    }

    public void EndGrab()
    {
        _grabbedPlayer?.SetGrabbed(false);  // ADD
        _isGrabbing = false;
        _grabbedPlayer = null;
        StateMachine.ChangeState(IdleState);
    }

    public void AlertNearbyEnemies(Transform playerTransform, bool isGrab)
    {
        float range = isGrab ? grabAlertRange : chaseAlertRange;
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, range, _pileBuffer, enemyLayer);

        for (int i = 0; i < count; i++)
        {
            var receiver = _pileBuffer[i].GetComponent<IAlertReceiver>();
            if (receiver == null || (object)receiver == this) continue;

            if (isGrab)
                receiver.OnGrabAlert(playerTransform);
            else
                receiver.OnChaseAlert(playerTransform);
        }
    }

    public void JoinPile(Transform pileTarget)
    {
        PileStateObj.SetPileTarget(pileTarget);
        StateMachine.ChangeState(PileStateObj);
    }

    // ── IRescueTarget ──────────────────────────────────────────
    public void PauseTTK() => _ttkPaused = true;
    public void ResumeTTK() => _ttkPaused = false;

    public void ReleasePlayer(float healAmount)
    {
        if (!_isGrabbing || _isResolved) return;  // ADD _isResolved check
        _isResolved = true;

        if (_trapTier >= 2)
            (_grabbedPlayer?.Movement as IMovementFreezable)?.SetFrozen(false);

        _grabbedPlayer?.Health?.Heal(healAmount);
        _grabbedPlayer?.SetGrabbed(false);  // ADD
        _isGrabbing = false;

        // ADD: grab cooldown so rescued player can't be immediately re-grabbed
        StartCoroutine(GrabCooldownRoutine());

        _grabbedPlayer = null;
        OnPlayerReleased?.Invoke();
        StateMachine.ChangeState(IdleState);
    }

    public void KillPlayer()
    {
        if (!_isGrabbing || _isResolved) return;
        _isResolved = true;

        // Unfreeze player regardless
        if (_trapTier >= 2)
            (_grabbedPlayer?.Movement as IMovementFreezable)?.SetFrozen(false);

        _grabbedPlayer?.SetGrabbed(false);

        var proxy = _grabbedPlayer?.GetComponent<PlayerDeathRescueProxy>();
        _isGrabbing = false;

        // Route through proxy if it owns the death state — avoids double-activation
        // and prevents player getting stuck when ranged enemy also dealt damage
        if (proxy != null && proxy.IsActive)
        {
            // Proxy already running — just kill through it cleanly
            proxy.KillPlayer();
        }
        else
        {
            // Proxy not active — TTK expired naturally, fire event directly
            _grabbedPlayer?.Health?.TakeDamage(
                new DamageData(9999f, DamageType.Environmental));
            OnPlayerKilled?.Invoke();
        }

        _grabbedPlayer = null;
        StateMachine.ChangeState(IdleState);
    }

    protected override void HandleDeath()
    {
        if (_isGrabbing)
        {
            _grabbedPlayer?.SetGrabbed(false);  // ADD
            ReleasePlayer(0f);
        }
        base.HandleDeath();
    }

    public void OnStruggle()
    {
        if (!CanGrabbedPlayerStruggle) return;  // tier 2 = fully frozen
        if (!_isGrabbing) return;

        if (_strugglePauseCoroutine != null)
            StopCoroutine(_strugglePauseCoroutine);
        _strugglePauseCoroutine = StartCoroutine(StrugglePauseRoutine());
    }

    private IEnumerator StrugglePauseRoutine()
    {
        PauseTTK();
        yield return new WaitForSeconds(strugglePauseDuration);
        ResumeTTK();
        _strugglePauseCoroutine = null;
    }

    private IEnumerator GrabCooldownRoutine()
    {
        _grabOnCooldown = true;
        yield return new WaitForSeconds(grabCooldownAfterRescue);
        _grabOnCooldown = false;
    }

    //Knockback──────────────────────────────────────────
    public override void ReceiveKnockback(KnockbackData data)
    {
        if (_isGrabbing) return;
        base.ReceiveKnockback(data);
    }
}