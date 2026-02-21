using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovementController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private CharacterController controller;
    private IMovementModifier movementModifier;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public void SetMovementModifier(IMovementModifier modifier)
    {
        movementModifier = modifier;
    }

    public void ExecuteCommand(IPlayerCommand command)
    {
        Vector2 input = command.GetMovementInput();

        if (movementModifier != null)
            input = movementModifier.Modify(input);

        Vector3 move =
            transform.forward * input.y +
            transform.right * input.x;

        controller.Move(move * moveSpeed * Time.deltaTime);
    }
}