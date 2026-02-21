using UnityEngine;

public class NormalMovementModifier : IMovementModifier
{
    public Vector2 Modify(Vector2 input)
    {
        return input;
    }
}