using UnityEngine;

/// <summary>
/// Sole dispatcher for twin E (melee) input.
/// During Accord State: routes E to AccordStateSystem.ExecuteAccordMelee().
/// During rescue: handles struggle + free twin attack.
/// Normal: both twins attack.
/// </summary>
public class TwinAttackDispatcher : MonoBehaviour
{
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private SoulPlayer soulTwin;
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
        if (_input == null) return;
        if (!_input.GetAttackDown()) return;

        // ── Soul active — E goes to soul ONLY ─────────────────
        if (soulTwin != null && soulTwin.gameObject.activeSelf)
        {
            soulTwin.GetComponent<PlayerAttackController>()?.PerformAttack();
            return;
        }

        // ── Accord State — E goes to accord melee ─────────────
        if (_accordMode != null && _accordMode.IsAccordActive)
        {
            _accordMode.ExecuteAccordMelee();
            return;
        }

        // ── Struggle check ────────────────────────────────────
        Player grabbedPlayer = rescueEventController?.ActiveGrabbedPlayer;
        IRescueTarget target = rescueEventController?.ActiveTarget;

        if (grabbedPlayer != null &&
            target != null &&
            target.CanGrabbedPlayerStruggle)
        {
            target.OnStruggle();
            Player freeTwin = (grabbedPlayer == leftTwin) ? rightTwin : leftTwin;
            freeTwin?.GetComponent<PlayerAttackController>()?.PerformAttack();
            return;
        }

        // ── Normal — both twins attack ─────────────────────────
        leftTwin?.GetComponent<PlayerAttackController>()?.PerformAttack();
        rightTwin?.GetComponent<PlayerAttackController>()?.PerformAttack();
    }
}