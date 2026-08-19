using UnityEngine;
using System;

/// <summary>
/// Tutorial checkpoint trigger zone.
/// Fires OnCorrectTwinReached when the required twin enters.
/// Fires OnWrongTwinReached when the wrong twin enters — Director handles reset.
///
/// RequiredTwin.Either — any twin counts (CP1).
/// RequiredTwin.Left / Right — specific twin required (CP2, CP3).
///
/// SETUP:
///   Add to an empty GO with a trigger collider.
///   Wire leftTwin and rightTwin.
///   Set requiredTwin.
///   Create two empty child GOs: LeftResetPoint / RightResetPoint.
///   Place them where twins should reappear after a wrong-twin reset.
///   Wire those Transforms to leftResetPoint and rightResetPoint.
///   Assign markerVisual to child GO with particle system (disabled by default in scene).
/// </summary>
public class TutorialCheckpoint : MonoBehaviour
{
    public enum RequiredTwin { Either, Left, Right }

    [Header("Setup")]
    [Tooltip("Leave null — resolved at Start from PlayerRoster (Persistent).")]
    [SerializeField] private Player leftTwin;
    [Tooltip("Leave null — resolved at Start from PlayerRoster (Persistent).")]
    [SerializeField] private Player rightTwin;
    [SerializeField] private RequiredTwin requiredTwin = RequiredTwin.Either;

    [Tooltip("Child GO with particle system — disabled by default in scene.")]
    [SerializeField] private GameObject markerVisual;

    [Header("Reset positions — drag empty GOs placed in scene")]
    [SerializeField] private Transform leftResetPoint;
    [SerializeField] private Transform rightResetPoint;

    public bool IsCompleted { get; private set; }

    public event Action<TutorialCheckpoint> OnCorrectTwinReached;
    public event Action<TutorialCheckpoint, Player> OnWrongTwinReached;

    // ── Public accessors — used by TutorialCheckpointStepSO for swap logic ──
    public Player LeftTwin => leftTwin;
    public Player RightTwin => rightTwin;

    public Vector3 LeftResetPosition => leftResetPoint != null
        ? leftResetPoint.position : transform.position;
    public Vector3 RightResetPosition => rightResetPoint != null
        ? rightResetPoint.position : transform.position;

    private Collider _triggerCollider;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        SetMarkerVisible(false);
    }

    /// <summary>Disables the trigger collider only — preserves marker and particle state.</summary>
    public void Suspend() { if (_triggerCollider != null) _triggerCollider.enabled = false; }
    /// <summary>Re-enables the trigger collider.</summary>
    public void Resume()  { if (_triggerCollider != null) _triggerCollider.enabled = true;  }

    private void Start()
    {
        if (leftTwin != null && rightTwin != null) return;
        if (PlayerRoster.Instance == null) { Debug.LogWarning("[TutorialCheckpoint] PlayerRoster not found.", this); return; }
        if (leftTwin == null)  leftTwin  = PlayerRoster.Instance.TwinA;
        if (rightTwin == null) rightTwin = PlayerRoster.Instance.TwinB;
    }

    /// <summary>
    /// Activates checkpoint — shows marker.
    /// Ignored if already completed.
    /// </summary>
    public void Activate()
    {
        if (IsCompleted) return;
        gameObject.SetActive(true);
        SetMarkerVisible(true);
        // R11 / Phase 7.6b: SetActive is a no-op when any ancestor is inactive.
        // Walk the chain and log the first culprit so it's immediately visible in console.
        if (!gameObject.activeInHierarchy)
        {
            var t = transform.parent;
            while (t != null)
            {
                if (!t.gameObject.activeSelf)
                {
                    Debug.LogError($"[TutorialCheckpoint] Activate() no-op — ancestor '{t.name}' is inactive. " +
                                   $"Move this GO out of any Activation Track hierarchy (R11).", this);
                    break;
                }
                t = t.parent;
            }
        }
    }

    /// <summary>
    /// Marks complete and hides marker.
    /// </summary>
    public void Complete()
    {
        IsCompleted = true;
        SetMarkerVisible(false);
    }

    /// <summary>
    /// Fully resets checkpoint for retry after failure reset.
    /// </summary>
    public void FullReset()
    {
        IsCompleted = false;
        SetMarkerVisible(true);
        gameObject.SetActive(true);
        Resume(); // re-arm trigger if it was Suspended before the reset sequence
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsCompleted) return;

        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;

        bool isLeft = player == leftTwin;
        bool isRight = player == rightTwin;
        if (!isLeft && !isRight) return;

        bool isCorrect = requiredTwin switch
        {
            RequiredTwin.Either => true,
            RequiredTwin.Left => isLeft,
            RequiredTwin.Right => isRight,
            _ => false
        };

        if (isCorrect) { Complete(); OnCorrectTwinReached?.Invoke(this); }
        else { OnWrongTwinReached?.Invoke(this, player); }
    }

    public void SetMarkerVisible(bool visible)
    {
        if (markerVisual == null) return;

        if (!visible)
        {
            foreach (var ps in markerVisual.GetComponentsInChildren<ParticleSystem>(true))
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        markerVisual.SetActive(visible);

        if (visible)
        {
            foreach (var ps in markerVisual.GetComponentsInChildren<ParticleSystem>(true))
                ps.Play(true);
        }
    }
}