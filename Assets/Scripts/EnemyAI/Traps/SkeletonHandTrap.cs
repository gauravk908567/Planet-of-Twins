// State machine: Dormant → Arming → Active → Dragging → Released/Killed
//
// Inspector variables (all tunable for difficulty and skill tree):
//   trapTier          1 = can struggle (both tier 1+2), 2 = fully frozen
//   armingDelay       how long after trigger before it can grab (1s base)
//   grabWindow        how long the grab attempt lasts (2s base)
//   timeToKill        how long player has before death once grabbed (5s base)
//   mashFrequency     button presses/second required to fill rescue bar
//   mashWindowDuration how long rescuer has to fill the bar (3s base)
//   mashCooldown      wait after failed mash before retry (0.75s base)
//   partialHealAmount HP restored to rescued player
//   requiredDamage    HP the trap must take before it starts dragging (40)
//   rescueProximityRadius how close soul must be to trigger mash (1.5u)
//
// Implements IRescueTarget so RescueEventController only knows the interface.
// ───────────────────────────────────────────────────────────────────────
using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SkeletonTrap : MonoBehaviour, IRescueTarget, IDamageable, IStunnable, IPossessable
{
    // ── Tunable in Inspector ───────────────────────────────────
    [Header("Trap Config")]
    [Tooltip("1 = trapped player can struggle (slows TTK). 2 = fully frozen.")]
    [SerializeField] private int trapTier = 1;

    [SerializeField] private float armingDelay = 1f;
    [SerializeField] private float grabWindow = 2f;
    [SerializeField] private float timeToKill = 5f;

    [Header("Rescue Config")]
    [SerializeField] private float mashFrequency = 3f;   // presses/sec needed
    [SerializeField] private float mashWindowDuration = 3f;   // seconds to fill bar
    [SerializeField] private float mashCooldown = 0.75f;
    [SerializeField] private float partialHealAmount = 25f;
    [SerializeField] private float rescueProximityRadius = 1.5f;

    [Header("Damage to Break Free")]
    [Tooltip("Damage required to transition to Dragging state (before grab completes).")]
    [SerializeField] private float requiredDamage = 40f;

    [Header("Layer")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Struggle")]
    [SerializeField] private float strugglePauseDuration = 0.4f; // seconds per mash
    private Coroutine _strugglePauseCoroutine;

    [Header("Rearm")]
    [SerializeField] private float rearmDelay = 2f; // dormant window after rescue

    [Header("Visual States")]
    [SerializeField] private Renderer trapRenderer;
    [SerializeField] private Color dormantColour = new Color(0.4f, 0.4f, 0.4f);
    [SerializeField] private Color armingColour = new Color(1f, 0.8f, 0f);
    [SerializeField] private Color activeColour = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private Color draggingColour = new Color(0.8f, 0f, 0.8f);
    [SerializeField] private Color releasedColour = new Color(0.2f, 0.8f, 0.3f);

    private Coroutine _rearmCoroutine;

    // ── IRescueTarget implementation ───────────────────────────
    public Transform GrabbedPlayerTransform => _grabbedPlayer != null
        ? _grabbedPlayer.transform : null;
    public Player GrabbedPlayer => _grabbedPlayer;
    public float NormalisedTTK => _timeToKillRemaining / timeToKill;
    public float MashFrequency => mashFrequency;
    public float MashWindowDuration => mashWindowDuration;
    public float MashCooldown => mashCooldown;
    public float PartialHealAmount => partialHealAmount;
    public bool CanGrabbedPlayerStruggle => trapTier == 1;
    public float StrugglePauseDuration => strugglePauseDuration;
    public float RescueProximityRadius => rescueProximityRadius;

    public event Action<Player> OnPlayerGrabbed;
    public event Action OnPlayerReleased;
    public event Action OnPlayerKilled;

    [Header("Possession")]
    [SerializeField] private bool isPossessable = false;

    private bool _isStunned = false;
    private bool _isPossessed = false;
    private Coroutine _stunCoroutine;

    public bool IsStunned => _isStunned;
    public bool IsPossessed => _isPossessed;

    // ── Internal state ─────────────────────────────────────────
    private TrapState _state = TrapState.Dormant;
    private Player _grabbedPlayer;
    private float _stateTimer;
    private float _timeToKillRemaining;
    private float _damageTaken = 0f;
    private bool _ttkPaused = false;

    // Hit buffer for grab detection
    private readonly Collider[] _hitBuffer = new Collider[4];

    // ── Lifecycle ─────────────────────────────────────────────
    private void OnEnable()
    {
        RescueEventController.Instance?.RegisterTrap(this);
        if (SoftResetController.Instance != null)
            SoftResetController.Instance.OnSoftReset += HandleSoftReset;
    }

    private void OnDisable()
    {
        RescueEventController.Instance?.UnregisterTrap(this);
        if (SoftResetController.Instance != null)
            SoftResetController.Instance.OnSoftReset -= HandleSoftReset;
    }

    private void HandleSoftReset() => ForceReset();

    public void ForceReset()
    {
        if (_rearmCoroutine != null) { StopCoroutine(_rearmCoroutine); _rearmCoroutine = null; }
        if (_state == TrapState.Dragging || _state == TrapState.Arming || _state == TrapState.Active)
        {
            if (_grabbedPlayer != null)
            {
                (_grabbedPlayer.Movement as IMovementFreezable)?.SetFrozen(false);
                _grabbedPlayer.SetGrabbed(false);
            }
        }
        _state = TrapState.Dormant;
        _grabbedPlayer = null;
        _damageTaken = 0f;
        _ttkPaused = false;
        SetMaterial(TrapState.Dormant);
    }

    // ── Update ─────────────────────────────────────────────────
    private void Update()
    {
        _stateTimer -= Time.deltaTime;

        switch (_state)
        {
            case TrapState.Arming:
                if (_stateTimer <= 0f)
                    TransitionTo(TrapState.Active);
                break;

            case TrapState.Active:
                TryGrabPlayer();
                if (_stateTimer <= 0f)
                    TransitionTo(TrapState.Dormant); // nobody grabbed → go back dormant
                break;

            case TrapState.Dragging:
                if (!_ttkPaused)
                {
                    _timeToKillRemaining -= Time.deltaTime;

                    // Tier 1: player can struggle — mashing slows TTK handled by
                    // RescueEventController calling PauseTTK/ResumeTTK.
                    // Tier 2: player fully frozen, only other twin can save.

                    if (_timeToKillRemaining <= 0f)
                        KillPlayer();
                }
                break;
        }
    }

    // ── Trigger detection (player steps on trap) ───────────────
    private void OnTriggerEnter(Collider other)
    {
        if (_state != TrapState.Dormant) return;

        Player p = other.GetComponent<Player>();
        if (p == null) return;

        // Player walked onto trap → start arming
        _grabbedPlayer = p;
        TransitionTo(TrapState.Arming);
    }

    // ── State machine ──────────────────────────────────────────
    private void TransitionTo(TrapState next)
    {
        _state = next;

        // Update visual state
        SetMaterial(next);

        switch (next)
        {
            case TrapState.Dormant:
                _grabbedPlayer?.SetGrabbed(false);  // safety clear
                _grabbedPlayer = null;
                _damageTaken = 0f;
                break;

            case TrapState.Arming:
                _stateTimer = armingDelay;
                _damageTaken = 0f;
                break;

            case TrapState.Active:
                _stateTimer = grabWindow;
                break;

            case TrapState.Dragging:
                _timeToKillRemaining = timeToKill;
                _ttkPaused = false;

                _grabbedPlayer?.SetGrabbed(true);   // ADD
                if (trapTier >= 2)
                    (_grabbedPlayer?.Movement as IMovementFreezable)?.SetFrozen(true);

                OnPlayerGrabbed?.Invoke(_grabbedPlayer);
                break;

            case TrapState.Released:
                _grabbedPlayer?.SetGrabbed(false);  // ADD
                UnfreezePlayer();
                OnPlayerReleased?.Invoke();
                // FIX: rearm after delay — was never transitioning to Dormant
                if (_rearmCoroutine != null) StopCoroutine(_rearmCoroutine);
                _rearmCoroutine = StartCoroutine(RearmRoutine());
                break;

            case TrapState.Killed:
                _grabbedPlayer?.SetGrabbed(false);  // ADD
                UnfreezePlayer();
                OnPlayerKilled?.Invoke();
                // FIX: same — rearm after delay
                if (_rearmCoroutine != null) StopCoroutine(_rearmCoroutine);
                _rearmCoroutine = StartCoroutine(RearmRoutine());
                break;
        }
    }

    private void TryGrabPlayer()
    {
        if (_grabbedPlayer == null) return;

        // Confirm player is still in grab range
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, 1.5f, _hitBuffer, playerLayer);

        for (int i = 0; i < count; i++)
        {
            if (_hitBuffer[i].GetComponent<Player>() == _grabbedPlayer)
            {
                TransitionTo(TrapState.Dragging);
                return;
            }
        }
    }

    private IEnumerator RearmRoutine()
    {
        yield return new WaitForSecondsRealtime(rearmDelay); // unscaled — pacing, not gameplay
        TransitionTo(TrapState.Dormant);
        _rearmCoroutine = null;
    }

    private void SetMaterial(TrapState state)
    {
        if (trapRenderer == null) return;
        Color c = state switch
        {
            TrapState.Dormant => dormantColour,
            TrapState.Arming => armingColour,
            TrapState.Active => activeColour,
            TrapState.Dragging => draggingColour,
            TrapState.Released => releasedColour,
            TrapState.Killed => dormantColour,
            _ => dormantColour
        };
        trapRenderer.material.color = c;
    }
    private void UnfreezePlayer()
    {
        if (trapTier >= 2)
            (_grabbedPlayer?.Movement as IMovementFreezable)?.SetFrozen(false);
    }

    // ── IRescueTarget: timer control ───────────────────────────
    public void PauseTTK() => _ttkPaused = true;
    public void ResumeTTK() => _ttkPaused = false;

    public void ReleasePlayer(float healAmount)
    {
        if (_state != TrapState.Dragging) return;

        if (_grabbedPlayer != null)
        {
            var health = _grabbedPlayer.Health;
            if (health != null)
            {
                health.Heal(healAmount);
                health.ResetToAlive();                   // ADD
                StartCoroutine(TrapRescueInvincibility()); // ADD
            }
        }
        TransitionTo(TrapState.Released);
    }

    private IEnumerator TrapRescueInvincibility()
    {
        _grabbedPlayer?.Health?.SetInvincible(true);
        yield return new WaitForSeconds(1.5f);
        _grabbedPlayer?.Health?.SetInvincible(false);
    }

    public void KillPlayer()
    {
        if (_state != TrapState.Dragging) return;

        // Deal lethal damage through the standard IDamageable path
        _grabbedPlayer?.Health?.TakeDamage(new DamageData(
            9999f, DamageType.Environmental));

        TransitionTo(TrapState.Killed);
    }

    // ── IDamageable: players/abilities can damage the trap ─────
    // Taking 40 damage while Active interrupts the grab → starts dragging
    // (player hit the trap before it could fully grab them — they're
    //  still dragged but have dealt partial damage, reducing TTK? or
    //  in this design the hit simply delays: taking full requiredDamage
    //  before grab completes returns trap to Dormant / frees player)
    public void TakeDamage(DamageData damageData)
    {
        if (_state == TrapState.Dormant ||
            _state == TrapState.Released ||
            _state == TrapState.Killed) return;

        // FIX: while dragging, only soul damage counts
        // Player melee can't damage a trap that's already grabbed their partner
        // Soul is identified by DamageData.source having SoulPlayer component
        if (_state == TrapState.Dragging)
        {
            bool isSoulDamage = damageData.Source?.GetComponent<SoulPlayer>() != null;
            if (!isSoulDamage)
            {
                Debug.Log($"[SkeletonTrap] Ignoring non-soul damage while dragging");
                return;
            }
            // Soul hit trap while it's dragging player — reduce TTK
            _timeToKillRemaining -= damageData.Amount * 0.1f; // 10% TTK reduction per hit
            Debug.Log($"[SkeletonTrap] Soul hit trap while dragging — TTK reduced to {_timeToKillRemaining:F1}");
            return;
        }

        _damageTaken += damageData.Amount;

        if (_state == TrapState.Arming || _state == TrapState.Active)
        {
            if (_damageTaken >= requiredDamage)
            {
                Debug.Log("[SkeletonTrap] Suppressed before grab — dormant");
                TransitionTo(TrapState.Dormant);
            }
        }
    }

    //On Struggle for trapped or grabbed players
    public void OnStruggle()
    {
        if (!CanGrabbedPlayerStruggle) return;
        if (_state != TrapState.Dragging) return;

        // Each press pauses TTK briefly — resets if pressed again before expiry
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
    // IStunnable — stun pauses TTK and freezes the trap
    public void ApplyStun(float duration)
    {
        if (_stunCoroutine != null)
            StopCoroutine(_stunCoroutine);
        _stunCoroutine = StartCoroutine(StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        _isStunned = true;
        _ttkPaused = true;                    // pause the kill timer
        // TODO: trigger stunned VFX here

        yield return new WaitForSeconds(duration);

        _isStunned = false;
        _ttkPaused = false;                   // resume kill timer
        _stunCoroutine = null;
    }
    // IPossessable
    public void ApplyPossession(float duration, float damageMultiplier)
    {
        if (!isPossessable) return;
        _isPossessed = true;
        StartCoroutine(PossessedTrapRoutine(duration));
    }

    public void OnHitByPossessed(Enemy attacker) { } // traps don't react to being hit

    private IEnumerator PossessedTrapRoutine(float duration)
    {
        // Enter possessed trap state — grab nearest enemy
        TransitionTo(TrapState.PossessedActive);
        yield return new WaitForSeconds(duration);
        _isPossessed = false;
        TransitionTo(TrapState.Dormant);      // release and go dormant
    }

    // Called from TrapState.PossessedActive Update():
    // Find nearest enemy in grab range and call GrabByTrap on it
    private void TryGrabNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, 1.5f, _hitBuffer, enemyLayer);  // reuse existing buffer

        for (int i = 0; i < count; i++)
        {
            var grabbable = _hitBuffer[i].GetComponent<IGrabbable>();
            if (grabbable != null && !grabbable.IsGrabbedByTrap)
            {
                grabbable.GrabByTrap(99f); // held until possession ends
                return;
            }
        }
    }

    // ── Debug gizmo ────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        Gizmos.color = _state == TrapState.Active ? Color.red :
                       _state == TrapState.Dragging ? Color.magenta :
                       _state == TrapState.Arming ? Color.yellow : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, rescueProximityRadius);
    }
}