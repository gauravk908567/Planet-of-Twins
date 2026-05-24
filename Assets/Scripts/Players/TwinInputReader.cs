using UnityEngine;

/// <summary>
/// Raw input reader. All dispatchers wire to this permanently.
///
/// Optional tutorialGateMono — wire to TutorialInputGate in tutorial scene.
/// If null (all other scenes), every input is allowed unconditionally.
/// This means multi-scene works without any tutorial manager present.
/// </summary>
public class TwinInputReader : MonoBehaviour, IInputProvider
{
    [Tooltip("Optional — wire to TutorialInputGate in tutorial scene only. " +
             "Leave null in all other scenes.")]
    [SerializeField] private MonoBehaviour tutorialGateMono;

    private ITutorialGate _gate;

    private void Awake()
    {
        _gate = tutorialGateMono as ITutorialGate;
    }

    // ── Helpers ───────────────────────────────────────────────
    private bool AttackAllowed => _gate == null || _gate.IsAttackAllowed;
    private bool AbilityAllowed => _gate == null || _gate.IsAbilityAllowed;
    private bool TeleportAllowed => _gate == null || _gate.IsTeleportAllowed;
    private bool RescueAllowed => _gate == null || _gate.IsRescueAllowed;
    private bool SwitchAllowed => _gate == null || _gate.IsSwitchAllowed;
    private bool InteractAllowed => _gate == null || _gate.IsInteractAllowed;

    // ── IInputProvider ────────────────────────────────────────
    public Vector2 GetMovementInput() =>
        new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

    public Vector3 GetMovementDirection() =>
        new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")).normalized;

    // Attack — E or LMB
    public bool GetAttackDown() =>
        AttackAllowed &&
        (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0));

    // Switch — Shift
    public bool GetSwitchDown() =>
        SwitchAllowed && Input.GetKeyDown(KeyCode.LeftShift);

    // Ability — Q or RMB
    public bool GetAbilityDown() =>
        AbilityAllowed &&
        (Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(1));

    // Teleport — hold/release C
    public bool GetTeleportHeld() => TeleportAllowed && Input.GetKey(KeyCode.C);
    public bool GetTeleportReleased() => TeleportAllowed && Input.GetKeyUp(KeyCode.C);

    // Rescue mash — F
    public bool GetRescueMash() => RescueAllowed && Input.GetKeyDown(KeyCode.F);
    public bool GetInteractDown() => InteractAllowed && Input.GetKeyDown(KeyCode.F);

    // Cancel — X (always allowed — needed to cancel QTE, soul chain etc)
    public bool GetCancelHeld() => Input.GetKey(KeyCode.X);

    // Empower — hold R
    public bool GetEmpowerHeld() => AbilityAllowed && Input.GetKey(KeyCode.R);

    // Struggle — E while grabbed (always allowed — attack lock shouldn't block escape)
    public bool GetStruggleMash() => Input.GetKeyDown(KeyCode.E);

    // Soul break — C mash while chain-bound
    public bool GetSoulBreakMash() => Input.GetKeyDown(KeyCode.C);
}