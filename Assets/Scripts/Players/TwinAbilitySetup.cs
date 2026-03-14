using UnityEngine;

public class TwinAbilitySetup : MonoBehaviour
{
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [SerializeField] private Player soulTwin;

    [SerializeField] private AbilityData stunAbilityData;
    [SerializeField] private AbilityData possessAbilityData;
    [SerializeField] private AbilityData teleportAbilityData;

    [SerializeField] private AbilityUpgradeData stunUpgradeData;
    [SerializeField] private AbilityUpgradeData possessUpgradeData;
    [SerializeField] private LayerMask stunTargetLayer;
    [SerializeField] private LayerMask possessTargetLayer;
    [SerializeField] private LayerMask enemyLayer;

    [SerializeField] private Transform barrierTransform;
    [SerializeField] private float minCastDistanceFromBarrier = 8f;

    [SerializeField] private MonoBehaviour timeFactorControllerObject;
    [SerializeField] private MonoBehaviour coroutineRunnerObject;
    [SerializeField] private MonoBehaviour twinSelectorObject;
    [SerializeField] private RescueEventController rescueEventController;

    public IStunEvents StunEvents { get; private set; }
    public IPossessEvents PossessEvents { get; private set; }

    private ITimeFactorController _timeFactorController;
    private ICoroutineRunner _coroutineRunner;
    private ISelectionLock _selectionLock;

    private void Awake()
    {
        _timeFactorController = timeFactorControllerObject as ITimeFactorController;
        _coroutineRunner = coroutineRunnerObject as ICoroutineRunner;
        _selectionLock = twinSelectorObject as ISelectionLock;

        if (_selectionLock == null)
            Debug.LogError("[TwinAbilitySetup] twinSelectorObject missing ISelectionLock.", this);
    }

    private void Start()
    {
        SetupLeftTwin();
        SetupRightTwin();

        var soul = soulTwin as SoulPlayer;
        soul?.ShouldSoulSleep(true);

        if (soul != null && rescueEventController != null)
            rescueEventController.RegisterSoulPlayer(soul);

        // Wire OnSoulArrived → HandleSoulArrived on RescueEventController.
        // This is required so the state machine can transition SoulDied → Triggered
        // when the player recasts the gate on a retry. Without this, subscribers=0
        // and HandleSoulArrived never fires — the belt-and-suspenders fix in Update
        // (SoulDied case calling CheckProximityForTrigger) handles it as a fallback,
        // but direct subscription is the primary path.
        if (rescueEventController != null)
        {
            var leftTA = leftTwin.GetComponent<AbilityController>()?.GetTeleportAbility();
            var rightTA = rightTwin.GetComponent<AbilityController>()?.GetTeleportAbility();

            if (leftTA != null) rescueEventController.RegisterTeleportAbility(leftTA);
            else Debug.LogError("[TwinAbilitySetup] Left TeleportAbility null — can't register.", this);

            if (rightTA != null) rescueEventController.RegisterTeleportAbility(rightTA);
            else Debug.LogError("[TwinAbilitySetup] Right TeleportAbility null — can't register.", this);
        }
    }

    private void SetupLeftTwin()
    {
        var ability = leftTwin.GetComponent<AbilityController>();
        var possess = new PossessAbility(possessAbilityData, possessTargetLayer, ability, possessUpgradeData);
        PossessEvents = possess;
        ability.SetPrimaryAbility(possess);
        ability.SetTeleportAbility(BuildTeleportAbility(leftTwin, rightTwin));
        ability.SetBarrierReference(barrierTransform, minCastDistanceFromBarrier);
    }

    private void SetupRightTwin()
    {
        var ability = rightTwin.GetComponent<AbilityController>();
        var stun = new StunAbility(stunAbilityData, stunTargetLayer, ability, stunUpgradeData);
        StunEvents = stun;
        ability.SetPrimaryAbility(stun);
        ability.SetTeleportAbility(BuildTeleportAbility(rightTwin, leftTwin));
        ability.SetBarrierReference(barrierTransform, minCastDistanceFromBarrier);
    }

    private TeleportAbility BuildTeleportAbility(Player caster, Player target)
    {
        return new TeleportAbility(
            teleportAbilityData,
            caster, target, soulTwin,
            _timeFactorController,
            _coroutineRunner,
            _selectionLock,
            rescueEventController);
    }
}