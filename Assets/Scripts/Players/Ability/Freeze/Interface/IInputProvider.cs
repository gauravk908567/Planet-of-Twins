using UnityEngine;
public interface IInputProvider
{
    Vector2 GetMovementInput();
    bool GetAttackDown();
    bool GetSwitchDown();
    bool GetAbilityDown();
    bool GetTeleportHeld();
    bool GetTeleportReleased();
    bool GetRescueMash();
}
