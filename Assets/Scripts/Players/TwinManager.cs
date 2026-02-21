using UnityEngine;

public class TwinManager : MonoBehaviour
{
    [Header("Twins")]
    [SerializeField] private PlayerMovementController leftTwin;
    [SerializeField] private PlayerMovementController rightTwin;

    private IMovementModifier normal = new NormalMovementModifier();
    private IMovementModifier mirrored = new MirroredMovementModifier();

    private PlayerMovementController selectedTwin;

    private void Start()
    {
        SelectLeft(); 
    }

    private void Update()
    {
        HandleSwitch();

        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        IPlayerCommand moveCommand = new MoveCommand(input);

        leftTwin.ExecuteCommand(moveCommand);
        rightTwin.ExecuteCommand(moveCommand);
    }

    private void HandleSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (selectedTwin == leftTwin)
                SelectRight();
            else
                SelectLeft();
        }
    }

    private void SelectLeft()
    {
        selectedTwin = leftTwin;

        leftTwin.SetMovementModifier(normal);
        rightTwin.SetMovementModifier(mirrored);
    }

    private void SelectRight()
    {
        selectedTwin = rightTwin;

        rightTwin.SetMovementModifier(normal);
        leftTwin.SetMovementModifier(mirrored);
    }
}