using UnityEngine;

public interface IMovementModifier
{
    Vector2 Modify(Vector2 input);
}