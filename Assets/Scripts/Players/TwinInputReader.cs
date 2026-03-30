using UnityEngine;

public class TwinInputReader : MonoBehaviour, IInputProvider
{
    public Vector2 GetMovementInput() =>
        new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

    public Vector3 GetMovementDirection() =>
        new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")).normalized;

    // E key OR Left Mouse Button
    public bool GetAttackDown() =>
        Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);

    public bool GetSwitchDown() => Input.GetKeyDown(KeyCode.LeftShift);

    // Q key OR Right Mouse Button
    public bool GetAbilityDown() =>
        Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(1);

    // Hold C to show gate preview; release to launch soul
    public bool GetTeleportHeld() => Input.GetKey(KeyCode.C);
    public bool GetTeleportReleased() => Input.GetKeyUp(KeyCode.C);

    public bool GetRescueMash() => Input.GetKeyDown(KeyCode.F);
    public bool GetInteractDown() => Input.GetKeyDown(KeyCode.F);
    public bool GetCancelHeld() => Input.GetKey(KeyCode.X);

    // Vigil (Anchor): hold R to enter meditative form.
    // VigilSystem reads this every frame — active while R is held, ends on release.
    public bool GetEmpowerHeld() => Input.GetKey(KeyCode.R);
}