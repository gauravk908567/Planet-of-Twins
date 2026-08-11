using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Tutorial-only trap. Grabs whichever twin enters first (or a specific one
/// if onlyGrabSpecificPlayer is set). Freezes grabbed twin's movement.
/// Other twin must rescue via Weaver's Gate.
///
/// Implements IRescueTarget so RescueEventController handles it identically
/// to SkeletonTrap — no changes to rescue system needed.
///
/// isTutorial = true:
///   On TTK timeout — does NOT kill the player. Releases them, resets
///   after resetDelay seconds, and re-grabs so the player can retry.
///
/// SETUP:
///   Add to a GO with a trigger collider.
///   Wire rescueControllerMono → GameSystem (RescueEventController).
///   Wire leftTwin and rightTwin.
///   Optionally wire specificTarget to force-grab one twin only.
///   Set isTutorial = true for tutorial use.
///   Set timeToKill high (30-40s) for tutorial.
///   Keep GO disabled — enable via Timeline Signal.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialTrap : MonoBehaviour, IRescueTarget
{
    [Header("Twins")]
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;

    [Header("Specific Target (optional)")]
    [Tooltip("If set, only this twin can be grabbed. Leave null to grab either twin.")]
    [SerializeField] private Player specificTarget;

    [Header("Trap Config")]
    [SerializeField] private float timeToKill = 35f;

    [Header("Rescue Config")]
    [SerializeField] private float mashFrequency = 2f;
    [SerializeField] private float mashWindowDuration = 4f;
    [SerializeField] private float mashCooldown = 0.75f;
    [SerializeField] private float partialHealAmount = 50f;
    [SerializeField] private float rescueProximityRadius = 2f;

    [Header("Tutorial Behaviour")]
    [Tooltip("If true, on TTK timeout the trap resets instead of killing.")]
    [SerializeField] private bool isTutorial = true;
    [Tooltip("Seconds after timeout before trap rearms and grabs again.")]
    [SerializeField] private float resetDelay = 2f;

    [Header("Registration")]
    [Tooltip("Wire to GameSystem (RescueEventController).")]
    [SerializeField] private MonoBehaviour rescueControllerMono;

    // ── IRescueTarget ─────────────────────────────────────────────────────
    public Transform GrabbedPlayerTransform => _grabbed != null
        ? _grabbed.transform : null;
    public Player GrabbedPlayer => _grabbed;
    public float NormalisedTTK => _ttkRemaining / timeToKill;
    public float MashFrequency => mashFrequency;
    public float MashWindowDuration => mashWindowDuration;
    public float MashCooldown => mashCooldown;
    public float PartialHealAmount => partialHealAmount;
    public bool CanGrabbedPlayerStruggle => false;
    public float StrugglePauseDuration => 0f;

    public event Action<Player> OnPlayerGrabbed;
    public event Action OnPlayerReleased;
    public event Action OnPlayerKilled;

    // ── Internal ──────────────────────────────────────────────────────────
    private Player _grabbed;
    private float _ttkRemaining;
    private bool _ttkPaused;
    private bool _isGrabbing;
    private bool _isResolved;

    private IRescueTrapRegistry _registry;

    // ── Unity ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        // Optional same-scene override; cross-scene slot is R2 violation — null is expected in builds.
        _registry = rescueControllerMono as IRescueTrapRegistry;
    }

    private void Start()
    {
        // Resolve via Instance (R4) — covers the case where OnEnable fired before Start with null registry.
        if (_registry == null) _registry = RescueEventController.Instance;
        if (_registry == null)
            Debug.LogError("[TutorialTrap] RescueEventController.Instance is null.", this);
        else { _registry.UnregisterTrap(this); _registry.RegisterTrap(this); }

        if (leftTwin == null)  leftTwin  = TwinSelector.Instance?.LeftTwin;
        if (rightTwin == null) rightTwin = TwinSelector.Instance?.RightTwin;
        if (leftTwin == null || rightTwin == null)
            Debug.LogError("[TutorialTrap] Twin refs null — wire in Inspector or TwinSelector must be loaded.", this);
    }

    private void OnEnable()
    {
        _registry?.RegisterTrap(this);
        ResetState();
        Debug.Log("[TutorialTrap] Enabled and registered.");
    }

    private void OnDisable()
    {
        _registry?.UnregisterTrap(this);
    }

    private void Update()
    {
        if (!_isGrabbing || _ttkPaused || _isResolved) return;

        _ttkRemaining -= Time.deltaTime;
        if (_ttkRemaining <= 0f)
            KillPlayer();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isGrabbing) return;

        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;

        // Only accept left or right twin
        bool isLeft = player == leftTwin;
        bool isRight = player == rightTwin;
        if (!isLeft && !isRight) return;

        // If specific target is set, only grab that twin
        if (specificTarget != null && player != specificTarget) return;

        GrabPlayer(player);
    }

    // ── Grab ──────────────────────────────────────────────────────────────
    private void GrabPlayer(Player player)
    {
        _grabbed = player;
        _ttkRemaining = timeToKill;
        _ttkPaused = false;
        _isGrabbing = true;
        _isResolved = false;

        // Freeze grabbed twin's movement — other twin must rescue
        (_grabbed.Movement as IMovementFreezable)?.SetFrozen(true);
        _grabbed.SetGrabbed(true);

        OnPlayerGrabbed?.Invoke(_grabbed);
        Debug.Log($"[TutorialTrap] Grabbed {player.name}. TTK={timeToKill}s");
    }

    // ── IRescueTarget timer control ───────────────────────────────────────
    public void PauseTTK() => _ttkPaused = true;
    public void ResumeTTK() => _ttkPaused = false;

    public void ReleasePlayer(float healAmount)
    {
        if (!_isGrabbing || _isResolved) return;
        _isResolved = true;
        _isGrabbing = false;

        (_grabbed?.Movement as IMovementFreezable)?.SetFrozen(false);
        _grabbed?.SetGrabbed(false);
        _grabbed?.Health?.Heal(healAmount);

        _grabbed = null;
        OnPlayerReleased?.Invoke();

        Debug.Log("[TutorialTrap] Player rescued successfully.");
        // Disable after successful rescue — tutorial is done with this trap
        gameObject.SetActive(false);
    }

    public void KillPlayer()
    {
        if (!_isGrabbing || _isResolved) return;
        _isResolved = true;
        _isGrabbing = false;

        (_grabbed?.Movement as IMovementFreezable)?.SetFrozen(false);
        _grabbed?.SetGrabbed(false);

        var wasGrabbed = _grabbed;
        _grabbed = null;

        OnPlayerKilled?.Invoke();
        Debug.Log("[TutorialTrap] TTK ran out.");

        if (isTutorial)
        {
            // Tutorial — don't kill the player, just reset and try again
            // Health stays as is, rescue system resets via Failed state
            StartCoroutine(ResetAfterDelay());
        }
        else
        {
            wasGrabbed?.Health?.TakeDamage(
                new DamageData(9999f, DamageType.Environmental));
        }
    }

    public void OnStruggle() { } // no struggle on tutorial trap

    // ── Reset ─────────────────────────────────────────────────────────────
    private IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSecondsRealtime(resetDelay); // unscaled — pacing under rescue-tutorial slow
        ResetState();
        Debug.Log("[TutorialTrap] Reset complete — ready to grab again.");
    }

    private void ResetState()
    {
        if (_grabbed != null)
        {
            (_grabbed.Movement as IMovementFreezable)?.SetFrozen(false);
            _grabbed.SetGrabbed(false);
        }

        _grabbed = null;
        _ttkRemaining = timeToKill;
        _ttkPaused = false;
        _isGrabbing = false;
        _isResolved = false;
    }
}