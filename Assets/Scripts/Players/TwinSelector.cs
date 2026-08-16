using System;
using UnityEngine;

public class TwinSelector : MonoBehaviour, ITwinSelector, ISelectionLock, ISelectionBroadcaster
{
    public static TwinSelector Instance { get; private set; }
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private MonoBehaviour inputProviderObject;

    private IInputProvider _input;

    // COUCH M1.2: movement mirroring is GONE — this selector now only tracks the selected twin for the
    // ABILITY path (TwinAbilityDispatcher / Empower / Rescue), which stays selection-based until M3.
    // Per-player movement is owned by TwinMovementDispatcher via PlayerInputRouter.For(twin). Slated for
    // removal in the post-M3 cleanup once abilities go per-player.

    private int _lockCounter = 0;  // counter handles overlapping locks

    // ── ITwinSelector ──────────────────────────────────────────
    public Transform SelectedTransform { get; private set; }
    public event Action<Transform> OnTwinSelected;

    // Read-only access used by cross-scene components (CheckpointTrigger, TutorialCheckpoint)
    // that can't serialize a Player ref from a different scene.
    public Player LeftTwin  => leftTwin;
    public Player RightTwin => rightTwin;

    // ── ISelectionLock ─────────────────────────────────────────
    public bool IsSelectionLocked => _lockCounter > 0;

    public void LockSelection()
    {
        _lockCounter++;
    }

    public void UnlockSelection()
    {
        _lockCounter = Mathf.Max(0, _lockCounter - 1);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _input = inputProviderObject as IInputProvider;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        SelectRight(); // default — matches original TwinManager behaviour
    }

    private void Update()
    {
        // ISelectionLock: block switch during teleport AND grab simultaneously
        if (IsSelectionLocked) return;
        if (!_input.GetSwitchDown()) return;

        if (SelectedTransform == leftTwin.transform)
            SelectRight();
        else
            SelectLeft();
    }

    public void ForceSelect(Player twin)
    {
        if (twin == leftTwin) SelectLeft();
        else SelectRight();
    }

    private void SelectLeft()
    {
        SelectedTransform = leftTwin.transform;
        OnTwinSelected?.Invoke(SelectedTransform);
    }

    private void SelectRight()
    {
        SelectedTransform = rightTwin.transform;
        OnTwinSelected?.Invoke(SelectedTransform);
    }
}