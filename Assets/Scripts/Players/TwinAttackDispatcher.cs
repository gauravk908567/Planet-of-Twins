using UnityEngine;

/// <summary>
/// Sole dispatcher for twin E (melee) input.
/// Soul active: E → the deployed soul, driven by its CASTER's input (couch two-soul model).
/// During Accord State: E → the shared AccordStateSystem.ExecuteAccordMelee() (either player, once).
/// During rescue: the grabbed twin's E struggles (tier-1), the free twin's E attacks.
/// Normal (couch S1): each twin attacks on its OWNING player's E (PlayerInputRouter.For) — per-twin,
/// independent. Single-device (P2→P1) → both reads identical → one E still swings both (unchanged).
/// </summary>
public class TwinAttackDispatcher : MonoBehaviour
{
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private MonoBehaviour inputProviderObject;
    [SerializeField] private RescueEventController rescueEventController;

    [Tooltip("Drag AccordStateSystem here — implements IAccordModeProvider")]
    [SerializeField] private MonoBehaviour accordModeProviderObject;

    private IInputProvider _input;
    private IAccordModeProvider _accordMode;

    private void Awake()
    {
        _input = inputProviderObject as IInputProvider;
        _accordMode = accordModeProviderObject as IAccordModeProvider;

        if (_input == null)
            Debug.LogError("[TwinAttackDispatcher] inputProviderObject missing IInputProvider.", this);
        if (_accordMode == null)
            Debug.LogWarning("[TwinAttackDispatcher] accordModeProviderObject not assigned " +
                             "or missing IAccordModeProvider — accord melee won't fire.", this);
    }

    private void Update()
    {
        // ── Soul active — E goes to the soul ONLY, driven by the CASTER who deployed it (couch two-soul model) ──
        var soul = rescueEventController?.ActiveSoul;
        if (soul != null && soul.gameObject.activeSelf)
        {
            if (soul.Caster != null && (PlayerInputRouter.For(soul.Caster)?.GetAttackDown() ?? false))
                soul.GetComponent<PlayerAttackController>()?.PerformAttack();
            return;
        }

        // ── Accord State — E triggers the shared accord melee (either player, once) ──
        if (_accordMode != null && _accordMode.IsAccordActive)
        {
            if (AnyAttackDown())
                _accordMode.ExecuteAccordMelee();
            return;
        }

        // ── Rescue + Normal — per-twin (couch S1): each player attacks their OWN twin ──
        AttackTwin(leftTwin);
        AttackTwin(rightTwin);
    }

    // Either player's E is down (shared-effect triggers: soul / accord melee).
    private bool AnyAttackDown() =>
        (PlayerInputRouter.For(leftTwin)?.GetAttackDown() ?? false) ||
        (PlayerInputRouter.For(rightTwin)?.GetAttackDown() ?? false);

    // Per-twin melee on the twin's OWNING player's E. During rescue, the grabbed twin can't melee —
    // its E struggles (tier-1 traps) instead; the free twin attacks. (RescueEventController also owns a
    // GetStruggleMash path per M3.1 — the struggle here mirrors the pre-existing attack-dispatcher path.)
    private void AttackTwin(Player twin)
    {
        if (twin == null) return;
        if (!(PlayerInputRouter.For(twin)?.GetAttackDown() ?? false)) return;

        Player grabbed = rescueEventController?.ActiveGrabbedPlayer;
        IRescueTarget target = rescueEventController?.ActiveTarget;
        if (grabbed != null && target != null && twin == grabbed)
        {
            if (target.CanGrabbedPlayerStruggle) target.OnStruggle();
            return;   // held — no melee
        }

        twin.GetComponent<PlayerAttackController>()?.PerformAttack();
    }
}