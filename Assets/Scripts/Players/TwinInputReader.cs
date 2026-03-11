using UnityEngine;

public class TwinInputReader : MonoBehaviour, IInputProvider
{
    public Vector2 GetMovementInput() =>
        new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

    public bool GetAttackDown() => Input.GetKeyDown(KeyCode.E);   // was F
    public bool GetSwitchDown() => Input.GetKeyDown(KeyCode.LeftShift);
    public bool GetAbilityDown() => Input.GetKeyDown(KeyCode.Q);
    public bool GetTeleportHeld() => Input.GetKeyDown(KeyCode.C);   // was E
    public bool GetTeleportReleased() => Input.GetKeyUp(KeyCode.C);     // was E
    public bool GetRescueMash() => Input.GetKeyDown(KeyCode.F);   // NEW
    public Vector3 GetMovementDirection() =>
        new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;
}