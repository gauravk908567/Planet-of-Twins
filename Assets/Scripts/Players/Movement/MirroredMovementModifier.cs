using UnityEngine;

public class MirroredMovementModifier : IMovementModifier
{
    public Vector2 Modify(Vector2 input)
    {
        return new Vector2(-input.x, input.y);
    }
}