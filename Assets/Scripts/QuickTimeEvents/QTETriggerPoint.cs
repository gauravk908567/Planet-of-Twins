using UnityEngine;
using TMPro;

/// <summary>
/// World-space F-press trigger for QTE.
///
/// FIX: removed movement lock on player lock-in. Locking movement caused two bugs:
///   1. If locked player got killed, rescue couldn't proceed (movement stayed locked).
///   2. Player couldn't dodge incoming attacks while locked to trigger.
/// The player is now visually "attached" (tracked as LockedPlayer) but free to move.
/// QTEController handles the actual constraint of the sequence.
///
/// FIX: OnTriggerEnter/Exit ignore SoulPlayer (same fix as QTEZoneTrigger).
/// </summary>
public class QTETriggerPoint : MonoBehaviour
{
    [Header("Prompt UI (World Space Canvas child)")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject promptRoot;

    [Header("Cancel hold duration (must match QTEController)")]
    [SerializeField] private float cancelHoldDuration = 0.75f;

    public Player LockedPlayer { get; private set; } = null;
    public bool IsOccupied => LockedPlayer != null;

    private Player _playerInRange = null;
    private bool _isActive = false;
    private float _cancelHoldTimer = 0f;

    public event System.Action<QTETriggerPoint, Player> OnPlayerLockedIn;
    public event System.Action<QTETriggerPoint, Player> OnPlayerReleased;

    public void SetActive(bool active)
    {
        _isActive = active;
        if (!active) ReleasePlayer(silent: true);
        RefreshPrompt();
    }

    public void ForceRelease()
    {
        ReleasePlayer(silent: true);
        RefreshPrompt();
    }

    private void Update()
    {
        if (!_isActive) return;

        if (!IsOccupied)
        {
            // Lock-in reads the Interact action (F / pad) through the DEVICE that owns the twin standing here:
            // couch → each trigger point answers only to its own occupant's controller; solo → both twins route
            // to P1. (Was raw Input.GetKeyDown(KeyCode.F), which bypassed the Input System entirely — no gamepad
            // could ever enter the QTE. Raw Input.* outside TwinInputReader is banned.)
            var input = _playerInRange != null ? PlayerInputRouter.For(_playerInRange) : null;
            if (input != null && input.GetInteractDown())
                LockPlayer(_playerInRange);
        }
        else
        {
            // Cancel (hold) reads the Cancel action (X / pad) through the LOCKED twin's own device. LockedPlayer
            // is non-null here (IsOccupied). (Was raw Input.GetKey(KeyCode.X).)
            var input = PlayerInputRouter.For(LockedPlayer);
            if (input != null && input.GetCancelHeld())
            {
                _cancelHoldTimer += Time.deltaTime;
                if (_cancelHoldTimer >= cancelHoldDuration)
                    ReleasePlayer(silent: false);
            }
            else
            {
                _cancelHoldTimer = 0f;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isActive) return;
        var p = other.GetComponent<Player>();
        // Exclude soul player
        if (p == null || p is SoulPlayer) return;
        if (_playerInRange == null)
        {
            _playerInRange = p;
            RefreshPrompt();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var p = other.GetComponent<Player>();
        if (p == null || p is SoulPlayer) return;

        if (p == _playerInRange)
        {
            _playerInRange = null;
        }

        // FIX: if the player who was locked in walks out, release them and
        // fire the event so QTEController aborts. Without this, LockedPlayer
        // stays set while the player is physically outside — QTE thinks they
        // are still committed, "Press F" never reappears, and the only exit
        // is hold-X despite the player already having left the area.
        if (p == LockedPlayer)
        {
            ReleasePlayer(silent: false);
        }

        RefreshPrompt();
    }

    private void LockPlayer(Player player)
    {
        LockedPlayer = player;
        _cancelHoldTimer = 0f;
        // NOTE: movement NOT locked — see class summary for reason.
        RefreshPrompt();
        OnPlayerLockedIn?.Invoke(this, player);
    }

    private void ReleasePlayer(bool silent)
    {
        if (LockedPlayer == null) return;
        var released = LockedPlayer;
        LockedPlayer = null;
        _cancelHoldTimer = 0f;
        // No movement unlock needed since we never locked it
        RefreshPrompt();
        if (!silent)
            OnPlayerReleased?.Invoke(this, released);
    }

    private void RefreshPrompt()
    {
        if (promptRoot != null)
            promptRoot.SetActive(_isActive && (_playerInRange != null || IsOccupied));

        if (promptText == null) return;

        if (IsOccupied)
            promptText.text = "Hold X to cancel";
        else if (_playerInRange != null)
            promptText.text = "Press F";
        else
            promptText.text = "";
    }
}