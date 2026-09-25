using UnityEngine;

public class TwinAbilitySetup : MonoBehaviour
{
    [SerializeField] private Player leftTwin;
    [SerializeField] private Player rightTwin;
    [Tooltip("Lyra's (leftTwin's) rescue soul — its OWN Weaver's Gate soul object (couch two-soul model).")]
    [SerializeField] private SoulPlayer leftSoul;
    [Tooltip("Kai's (rightTwin's) rescue soul — its OWN Weaver's Gate soul object (couch two-soul model).")]
    [SerializeField] private SoulPlayer rightSoul;

    [SerializeField] private AbilityData stunAbilityData;
    [SerializeField] private AbilityData possessAbilityData;
    [SerializeField] private AbilityData teleportAbilityData;

    [SerializeField] private AbilityUpgradeData stunUpgradeData;
    [SerializeField] private AbilityUpgradeData gateUpgradeData;
    [SerializeField] private AbilityUpgradeData possessUpgradeData;
    [SerializeField] private LayerMask stunTargetLayer;
    [SerializeField] private LayerMask possessTargetLayer;
    [SerializeField] private LayerMask enemyLayer;

    [Header("VFX Library")]
    [Tooltip("Optional override — leave null to resolve VfxLibraryProvider.Instance in Start (R4). " +
             "Player ability books are pulled from PlayerVfxLibrary, not wired per-ability here.")]
    [SerializeField] private VfxLibraryProvider vfxLibraryProvider;

    [Tooltip("How close to the barrier a player must be to cast Gate. " +
             "Barrier Transform is resolved at runtime via BarrierPOI/POIManager.")]
    [SerializeField] private float minCastDistanceFromBarrier = 8f;

    [SerializeField] private MonoBehaviour timeFactorControllerObject;
    [SerializeField] private MonoBehaviour coroutineRunnerObject;
    [SerializeField] private RescueEventController rescueEventController;

    [Header("Gate Soul Travel")]
    [Tooltip("World speed during soul travel. 0.85 = 85% speed. 1.0 = no slow.")]
    [SerializeField] private float _soulTravelTimeFactor = 0.85f;

    [Header("Accord State")]
    [Tooltip("Drag AccordStateSystem GO here — subscribes to OnAccordDeactivated to restore abilities.")]
    [SerializeField] private AccordStateSystem accordStateSystem;

    public IStunEvents StunEvents { get; private set; }
    public IPossessEvents PossessEvents { get; private set; }

    private ITimeFactorController _timeFactorController;
    private ICoroutineRunner _coroutineRunner;

    // Stored so we can restore after Accord State ends
    private IAbility _kaiOriginalQ;
    private IAbility _lyraOriginalQ;

    private void Awake()
    {
        _timeFactorController = timeFactorControllerObject as ITimeFactorController;
        _coroutineRunner = coroutineRunnerObject as ICoroutineRunner;

        if (accordStateSystem == null)
            Debug.LogWarning("[TwinAbilitySetup] AccordStateSystem not assigned — abilities won't restore after Accord.", this);
    }

    private void OnEnable()
    {
        if (accordStateSystem != null)
            accordStateSystem.OnAccordDeactivated += RestoreOriginalAbilities;
    }

    private void OnDisable()
    {
        if (accordStateSystem != null)
            accordStateSystem.OnAccordDeactivated -= RestoreOriginalAbilities;
    }

    // R4: resolve the Persistent provider here (Start), fail loud if unresolved.
    private PlayerVfxLibrary PlayerVfx => vfxLibraryProvider != null ? vfxLibraryProvider.Player : null;

    private void Start()
    {
        // R4 — optional serialized slot, else the Persistent singleton; LogError if still null.
        vfxLibraryProvider = vfxLibraryProvider != null ? vfxLibraryProvider : VfxLibraryProvider.Instance;
        if (vfxLibraryProvider == null)
            Debug.LogError("[TwinAbilitySetup] No VfxLibraryProvider — ability VFX books unresolved. " +
                           "Add one to Persistent.unity and wire PlayerVfxLibrary.", this);

        SetupLeftTwin();
        SetupRightTwin();

        leftSoul?.ShouldSoulSleep(true);
        rightSoul?.ShouldSoulSleep(true);

        if (rescueEventController != null)
        {
            if (leftSoul != null)  rescueEventController.RegisterSoulPlayer(leftSoul);
            if (rightSoul != null) rescueEventController.RegisterSoulPlayer(rightSoul);
        }

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
        var possess = new PossessAbility(
            possessAbilityData, possessTargetLayer, ability, possessUpgradeData);
        PossessEvents = possess;
        _lyraOriginalQ = possess; // store for restore after Accord
        ability.SetPrimaryAbility(possess);
        ability.SetTeleportAbility(BuildTeleportAbility(leftTwin, rightTwin, leftSoul));
        ability.SetMinCastDistance(minCastDistanceFromBarrier);
    }

    private void SetupRightTwin()
    {
        var ability = rightTwin.GetComponent<AbilityController>();
        var stun = new StunAbility(
            stunAbilityData, stunTargetLayer, ability, stunUpgradeData, PlayerVfx?.Stun);
        StunEvents = stun;
        _kaiOriginalQ = stun; // store for restore after Accord
        ability.SetPrimaryAbility(stun);
        ability.SetTeleportAbility(BuildTeleportAbility(rightTwin, leftTwin, rightSoul));
        ability.SetMinCastDistance(minCastDistanceFromBarrier);
    }

    private void RestoreOriginalAbilities()
    {
        // Guard against scene teardown
        if (rightTwin == null || leftTwin == null) return;

        if (_kaiOriginalQ != null)
            rightTwin.GetComponent<AbilityController>()?.SetPrimaryAbility(_kaiOriginalQ);

        if (_lyraOriginalQ != null)
            leftTwin.GetComponent<AbilityController>()?.SetPrimaryAbility(_lyraOriginalQ);

        Debug.Log("[TwinAbilitySetup] Original Q abilities restored after Accord State.");
    }

    private TeleportAbility BuildTeleportAbility(Player caster, Player target, SoulPlayer soul)
    {
        if (soul == null)
            Debug.LogError($"[TwinAbilitySetup] No soul wired for {caster?.name}'s Weaver's Gate — rescue will " +
                           "fail. Wire leftSoul / rightSoul in the Inspector.", this);

        var ta = new TeleportAbility(
            teleportAbilityData,
            caster, target, soul,
            _timeFactorController,
            _coroutineRunner,
            rescueEventController);

        // Inject Gate upgrade data so pulse scales with tree upgrades
        if (gateUpgradeData != null)
            ta.SetGateData(gateUpgradeData);

        // Inject world slow factor
        ta.SetSoulTravelTimeFactor(_soulTravelTimeFactor);

        // Inject gate data into SoulPulseSystem so it can gate behind Node 3
        if (gateUpgradeData != null)
        {
            var pulse = soul != null ? soul.GetComponent<SoulPulseSystem>() : null;
            if (pulse != null)
                pulse.SetGateData(gateUpgradeData);
        }

        return ta;
    }
}