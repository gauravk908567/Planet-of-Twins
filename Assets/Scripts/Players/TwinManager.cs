using UnityEngine;

public class TwinManager : MonoBehaviour
{
    [Header("Twins")]
    [SerializeField] private Player leftTwinCharacter;
    [SerializeField] private Player rightTwinCharacter;
    [SerializeField] private Player soulTwinCharacter;

    [SerializeField] private AbilityData stunAbilityData;
    [SerializeField] private AbilityData possessAbilityData;
    [SerializeField] private AbilityData teleportAbilityData;
    [SerializeField] private LayerMask enemyLayer;

    [SerializeField] private int teleportHealthThreshold = 20;

    private IMovementModifier normal = new NormalMovementModifier();
    private IMovementModifier mirrored = new MirroredMovementModifier();

    private PlayerMovementController selectedTwin;
    private AbilityController abilityController;
    private AttackController leftAttackController;
    private AttackController rightAttackController;
    private AttackController soulAttackController;

    [SerializeField] private bool isTeleportUnlocked;

    public static System.Action<Transform> OnTwinSelected;

    private void OnEnable()
    {

        leftTwinCharacter.Health.OnDamageTaken += OnTwinDamaged;
        rightTwinCharacter.Health.OnDamageTaken += OnTwinDamaged;
        OnTwinSelected += OnTwinSelectedCallback;
    }

    private void OnDisable()
    {
        leftTwinCharacter.Health.OnDamageTaken -= OnTwinDamaged;
        rightTwinCharacter.Health.OnDamageTaken -= OnTwinDamaged;
        OnTwinSelected -= OnTwinSelectedCallback;
    }
    private void Start()
    {
        SelectLeft(); // default
        SetupAttackContoller();
        SetupAbilities();
    }

    private void SetupAttackContoller()
    {
        leftAttackController = leftTwinCharacter.GetComponent<AttackController>();
        rightAttackController = rightTwinCharacter.GetComponent<AttackController>();
        soulAttackController = soulTwinCharacter.GetComponent<AttackController>();
    }

    private void SetupAbilities()
    {
        var leftAbility = leftTwinCharacter.GetComponent<AbilityController>();
        var rightAbility = rightTwinCharacter.GetComponent<AbilityController>();

        // LEFT TWIN
        leftAbility.SetPrimaryAbility(
            new PossessionAbility(possessAbilityData, enemyLayer, leftAbility)
        );

        leftAbility.SetTeleportAbility(
            new TeleportAbility(
                teleportAbilityData,
                leftTwinCharacter,
                rightTwinCharacter,
                soulTwinCharacter
            )
        );

        // RIGHT TWIN
        rightAbility.SetPrimaryAbility(
            new StunAbility(stunAbilityData, enemyLayer, rightAbility)
        );

        rightAbility.SetTeleportAbility(
            new TeleportAbility(
                teleportAbilityData,
                rightTwinCharacter, leftTwinCharacter, soulTwinCharacter
            )
        );

        (soulTwinCharacter as SoulPlayer).ShouldSoulSleep(true);
    }

    private void Update()
    {
        HandleSwitch();
        HandleMovement();
        HandleAttack();
        HandleAbilities();
        HandleEmergencyTeleport();
    }



    private void HandleMovement()
    {
        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        IPlayerCommand moveCommand = new MoveCommand(input);

        leftTwinCharacter.Movement.ExecuteCommand(moveCommand);
        rightTwinCharacter.Movement.ExecuteCommand(moveCommand);
    }

    private void HandleAttack()
    {
        if (Input.GetKeyDown(KeyCode.F))   // Attack key
        {
            leftAttackController?.PerformAttack();
            rightAttackController?.PerformAttack();
            soulAttackController?.PerformAttack();
        }
    }

    private void HandleSwitch()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            if (selectedTwin == leftTwinCharacter.Movement)
                SelectRight();
            else
                SelectLeft();
        }
    }

    private void OnTwinSelectedCallback(Transform t)
    {
        abilityController = selectedTwin.GetComponent<AbilityController>();
    }

    private void HandleAbilities()
    {
        if (Input.GetKeyUp(KeyCode.Q))
        {
            abilityController.ActivatePrimary();
        }

    }

    private void OnTwinDamaged(PlayerHealthComponent damagedTwin, float damage)
    {
        if (isTeleportUnlocked)
            return;

        if (damagedTwin.FinalHealth <= teleportHealthThreshold)
        {
            isTeleportUnlocked = true;

            Debug.Log("Emergency Teleport Available!");
            // Later: trigger UI warning
        }
        else
        {
            isTeleportUnlocked = false;
        }
    }

    private void HandleEmergencyTeleport()
    {
        if (!isTeleportUnlocked)
            return;

        OnCriticalCondition();

    }

    private void OnCriticalCondition()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            abilityController.ShowTeleportPreview();
        }

        if (Input.GetKeyUp(KeyCode.E))
        {
            abilityController.HideTeleportPreview();
            abilityController.ActivateTeleport();
        }
    }

    private void SelectLeft()
    {
        selectedTwin = leftTwinCharacter.Movement;

        leftTwinCharacter.Movement.SetMovementModifier(normal);
        rightTwinCharacter.Movement.SetMovementModifier(mirrored);

        OnTwinSelected?.Invoke(leftTwinCharacter.transform);
    }

    private void SelectRight()
    {
        selectedTwin = rightTwinCharacter.Movement;

        rightTwinCharacter.Movement.SetMovementModifier(normal);
        leftTwinCharacter.Movement.SetMovementModifier(mirrored);

        OnTwinSelected?.Invoke(rightTwinCharacter.transform);
    }
}