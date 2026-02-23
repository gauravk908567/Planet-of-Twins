using UnityEngine;

public class TwinManager : MonoBehaviour
{
    [Header("Twins")]
    [SerializeField] private Player leftTwinCharacter;
    [SerializeField] private Player rightTwinCharacter;

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

    private bool teleportUnlocked;

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
        SetupAbilities();
    }

    private void SetupAbilities()
    {
        var leftAbility = leftTwinCharacter.GetComponent<AbilityController>();
        var rightAbility = rightTwinCharacter.GetComponent<AbilityController>();

        // LEFT TWIN
        leftAbility.SetPrimaryAbility(
            new PossessionAbility(possessAbilityData, enemyLayer)
        );

        leftAbility.SetTeleportAbility(
            new TeleportAbility(
                teleportAbilityData,
                leftTwinCharacter.Movement,
                rightTwinCharacter.Movement
            )
        );

        // RIGHT TWIN
        rightAbility.SetPrimaryAbility(
            new StunAbility(stunAbilityData, enemyLayer, rightAbility)
        );

        rightAbility.SetTeleportAbility(
            new TeleportAbility(
                teleportAbilityData,
                rightTwinCharacter.Movement, leftTwinCharacter.Movement
            )
        );
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
        if (teleportUnlocked)
            return;

        if (damagedTwin.FinalHealth <= teleportHealthThreshold)
        {
            teleportUnlocked = true;

            Debug.Log("Emergency Teleport Available!");
            // Later: trigger UI warning
        }
        else
        {
            teleportUnlocked = false;
        }
    }

    private void HandleEmergencyTeleport()
    {
        if (!teleportUnlocked)
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