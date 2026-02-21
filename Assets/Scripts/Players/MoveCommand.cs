using UnityEngine;

public class MoveCommand : IPlayerCommand
{
    private Vector2 input;

    public MoveCommand(Vector2 input)
    {
        this.input = input;
    }

    public Vector2 GetMovementInput()
    {
        return input;
    }
}