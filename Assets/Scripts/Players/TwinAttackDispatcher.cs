using UnityEngine;

public class TwinAttackDispatcher : MonoBehaviour
{
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private SoulPlayer soulTwin;   // ADD
    [SerializeField] private MonoBehaviour inputProviderObject;
    [SerializeField] private RescueEventController rescueEventController;

    private IInputProvider _input;

    private void Awake()
    {
        _input = inputProviderObject as IInputProvider;
        if (_input == null)
            Debug.LogError("[TwinAttackDispatcher] inputProviderObject missing IInputProvider.", this);
    }

    private void Update()
    {
        if (_input == null) return;
        if (!_input.GetAttackDown()) return;

        // ── Soul is active — E goes to soul ONLY ──────────────
        // Free twins do NOT attack when soul is traveling/active
        if (soulTwin != null && soulTwin.gameObject.activeSelf)
        {
            soulTwin.GetComponent<PlayerAttackController>()?.PerformAttack();
            return;
        }

        // ── Struggle check — grabbed player mashes E ──────────
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
