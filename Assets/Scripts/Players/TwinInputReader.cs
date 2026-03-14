using UnityEngine;

public class TwinInputReader : MonoBehaviour, IInputProvider
{
    public Vector2 GetMovementInput() =>
        new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

    public Vector3 GetMovementDirection() =>
        new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")).normalized;

    public bool GetAttackDown() => Input.GetKeyDown(KeyCode.E);
    public bool GetSwitchDown() => Input.GetKeyDown(KeyCode.LeftShift);
    public bool GetAbilityDown() => Input.GetKeyDown(KeyCode.Q);

    // FIX: was GetKeyDown — only fired on first frame so marker preview
    // showed for one frame then disappeared. GetKey fires every frame while
    // held so the marker stays visible until the player releases.
    public bool GetTeleportHeld() => Input.GetKey(KeyCode.C);
    public bool GetTeleportReleased() => Input.GetKeyUp(KeyCode.C);

    public bool GetRescueMash() => Input.GetKeyDown(KeyCode.F);
    public bool GetInteractDown() => Input.GetKeyDown(KeyCode.F);
    public bool GetCancelHeld() => Input.GetKey(KeyCode.X);
}