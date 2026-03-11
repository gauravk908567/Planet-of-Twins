//using UnityEngine;
//using System;
//public class TwinManager : MonoBehaviour, ISelectionBroadcaster, IInputProvider
//{
//    [Header("Twins")]
//    [SerializeField] private Player leftTwinCharacter;
//    [SerializeField] private Player rightTwinCharacter;
//    [SerializeField] private Player soulTwinCharacter;

//    [SerializeField] private AbilityData stunAbilityData;
//    [SerializeField] private AbilityData possessAbilityData;
//    [SerializeField] private AbilityData teleportAbilityData;
//    [SerializeField] private LayerMask enemyLayer;
//    [SerializeField] private GameObject timeManagerObject;

//    [SerializeField] private MonoBehaviour twinSelectorObject;
//    private ISelectionLock _selectionLock;
//    private IRescueTarget _activeRescueTarget;

//    [SerializeField] private int teleportHealthThreshold = 20;

//    private IMovementModifier normal = new NormalMovementModifier();
//    private IMovementModifier mirrored = new MirroredMovementModifier();

//    private PlayerMovementController selectedTwin;
//    private AbilityController abilityController;
//    private AttackController leftAttackController;
//    private AttackController rightAttackController;
//    private AttackController soulAttackController;

//    [SerializeField] private bool isTeleportUnlocked;

//    private ITimeFactorController timeFactorController;
//    private ICoroutineRunner coroutineRunner;

//    public event Action<Transform> OnTwinSelected; // satisfies ISelectionBroadcaster ✓

//    // ── FIX 3: IInputProvider implementation ────────────────────────
//    // These methods read Unity's Input system and fulfil the interface contract.
//    // When you are ready to split into TwinInputReader, move these there
//    // and remove IInputProvider from TwinManager's class declaration.
//    public Vector2 GetMovementInput()
//        => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

//    public bool GetAttackDown() => Input.GetKeyDown(KeyCode.F);
//    public bool GetSwitchDown() => Input.GetKeyDown(KeyCode.LeftShift);
//    public bool GetAbilityUp() => Input.GetKeyUp(KeyCode.Q);
//    public bool GetTeleportHeld() => Input.GetKeyDown(KeyCode.E);
//    public bool GetTeleportReleased() => Input.GetKeyUp(KeyCode.E);

//    // Also still used externally — keep it:
//    public Vector3 GetMovementDirection()
//        => new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;
//    // ── End IInputProvider implementation ───────────────────────────

//    private void Awake()
//    {
//        if (timeManagerObject != null)
//        {
//            timeFactorController = timeManagerObject.GetComponent<ITimeFactorController>();
//            coroutineRunner = timeManagerObject.GetComponent<ICoroutineRunner>();
//        }
//        else
//        {
//            Debug.LogError("Time Manager Object is missing! Drag the Game System GameObject into the TwinManager's inspector slot.");
//        }

//        _selectionLock = twinSelectorObject as ISelectionLock;
//    }

//    private void OnEnable()
//    {
//        leftTwinCharacter.Health.OnDamageTaken += OnTwinDamaged;
//        rightTwinCharacter.Health.OnDamageTaken += OnTwinDamaged;
//        OnTwinSelected += OnTwinSelectedCallback;
//    }

//    private void OnDisable()
//    {
//        leftTwinCharacter.Health.OnDamageTaken -= OnTwinDamaged;
//        rightTwinCharacter.Health.OnDamageTaken -= OnTwinDamaged;
//        OnTwinSelected -= OnTwinSelectedCallback;
//    }

//    private void Start()
//    {
//        SelectRight();
//        SetupAttackContoller();
//        SetupAbilities();
//    }

//    private void SetupAttackContoller()
//    {
//        leftAttackController = leftTwinCharacter.GetComponent<AttackController>();
//        rightAttackController = rightTwinCharacter.GetComponent<AttackController>();
//        soulAttackController = soulTwinCharacter.GetComponent<AttackController>();
//    }

//    private void SetupAbilities()
//    {
//        var leftAbility = leftTwinCharacter.GetComponent<AbilityController>();
//        var rightAbility = rightTwinCharacter.GetComponent<AbilityController>();

//        leftAbility.SetPrimaryAbility(
//            new PossessionAbility(possessAbilityData, enemyLayer, leftAbility));

//        leftAbility.SetTeleportAbility(
//            new TeleportAbility(
//                teleportAbilityData,
//                leftTwinCharacter,
//                rightTwinCharacter,
//                soulTwinCharacter,
//                timeFactorController,
//                coroutineRunner));

//        rightAbility.SetPrimaryAbility(
//            new StunAbility(stunAbilityData, enemyLayer, rightAbility));

//        rightAbility.SetTeleportAbility(
//            new TeleportAbility(
//                teleportAbilityData,
//                rightTwinCharacter,
//                leftTwinCharacter,
//                soulTwinCharacter,
//                timeFactorController,
//                coroutineRunner));

//        (soulTwinCharacter as SoulPlayer)?.ShouldSoulSleep(true);
//    }

//    private void Update()
//    {
//        HandleSwitch();
//        HandleMovement();
//        HandleAttack();
//        HandleAbilities();
//        HandleEmergencyTeleport();
//    }

//    private void HandleMovement()
//    {
//        Vector2 input = GetMovementInput();           // uses interface method now
//        IPlayerCommand cmd = new MoveCommand(input);
//        leftTwinCharacter.Movement.ExecuteCommand(cmd);
//        rightTwinCharacter.Movement.ExecuteCommand(cmd);
//    }

//    private void HandleAttack()
//    {
//        if (!GetAttackDown()) return;                      // uses interface method now
//        leftAttackController?.PerformAttack();
//        rightAttackController?.PerformAttack();
//        soulAttackController?.PerformAttack();
//    }

//    private void HandleSwitch()
//    {
//        if (!GetSwitchDown()) return;                      // uses interface method now
//        if (selectedTwin == leftTwinCharacter.Movement)
//            SelectRight();
//        else
//            SelectLeft();
//    }

//    private void OnTwinSelectedCallback(Transform t)
//    {
//        abilityController = selectedTwin.GetComponent<AbilityController>();
//    }

//    private void HandleAbilities()
//    {
//        if (GetAbilityUp())                                // uses interface method now
//            abilityController?.ActivatePrimary();
//    }

//    private void OnTwinDamaged(PlayerHealthComponent damagedTwin, float damage)
//    {
//        isTeleportUnlocked = damagedTwin.DisplayHealth <= teleportHealthThreshold;

//        if (isTeleportUnlocked)
//            Debug.Log("Emergency Teleport Available!");
//    }

//    private void HandleEmergencyTeleport()
//    {
//        if (!isTeleportUnlocked) return;
//        OnCriticalCondition();
//    }

//    private void OnCriticalCondition()
//    {
//        if (GetTeleportHeld())                             // uses interface method now
//            abilityController?.ShowTeleportPreview();

//        if (GetTeleportReleased())                         // uses interface method now
//        {
//            abilityController?.HideTeleportPreview();
//            abilityController?.ActivateTeleport();
//        }
//    }

//    private void SelectLeft()
//    {
//        selectedTwin = leftTwinCharacter.Movement;
//        leftTwinCharacter.Movement.SetMovementModifier(normal);
//        rightTwinCharacter.Movement.SetMovementModifier(mirrored);
//        OnTwinSelected?.Invoke(leftTwinCharacter.transform);
//    }

//    private void SelectRight()
//    {
//        selectedTwin = rightTwinCharacter.Movement;
//        rightTwinCharacter.Movement.SetMovementModifier(normal);
//        leftTwinCharacter.Movement.SetMovementModifier(mirrored);
//        OnTwinSelected?.Invoke(rightTwinCharacter.transform);
//    }

//    // Call this from wherever traps are registered (scene init or trap Awake)
//    public void RegisterRescueTarget(IRescueTarget target)
//    {
//        target.OnPlayerGrabbed += HandlePlayerGrabbed;
//        target.OnPlayerReleased += HandlePlayerReleased;
//        target.OnPlayerKilled += HandlePlayerReleased; // same cleanup on kill
//    }

//    public void UnregisterRescueTarget(IRescueTarget target)
//    {
//        target.OnPlayerGrabbed -= HandlePlayerGrabbed;
//        target.OnPlayerReleased -= HandlePlayerReleased;
//        target.OnPlayerKilled -= HandlePlayerReleased;
//    }

//    private void HandlePlayerGrabbed(Player grabbedPlayer)
//    {
//        _activeRescueTarget = ???; // stored when registered — see RescueEventController

//        // Auto-select the OTHER twin
//        Player otherTwin = (grabbedPlayer == leftTwinCharacter)
//            ? rightTwinCharacter
//            : leftTwinCharacter;

//        // Force selection to non-grabbed twin
//        var selector = twinSelectorObject as TwinSelector;
//        selector?.ForceSelect(otherTwin);

//        // Lock selection — can't switch back to grabbed twin
//        _selectionLock?.LockSelection();
//    }

//    private void HandlePlayerReleased()
//    {
//        // Unlock selection — rescue complete or player died
//        _selectionLock?.UnlockSelection();
//        _activeRescueTarget = null;
//    }
//}