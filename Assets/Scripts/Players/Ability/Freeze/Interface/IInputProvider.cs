using UnityEngine;
/// <summary>
/// Input abstraction. Implement in TwinInputReader (real) or a mock for tests.
/// NOTE: GetInteractDown and GetCancelHeld were added this session.
/// Any other IInputProvider implementations must also add these two methods.
/// </summary>
public interface IInputProvider
{
    Vector2 GetMovementInput();
    Vector3 GetMovementDirection();
    bool GetAttackDown();
    bool GetSwitchDown();
    bool GetAbilityDown();
    bool GetTeleportHeld();
    bool GetTeleportReleased();
    bool GetRescueMash();
    /// <summary>Press F in world context (QTE trigger attach). Same key as RescueMash � different context.</summary>
    bool GetInteractDown();
    /// <summary>Hold X � cancel teleport window or detach from QTE trigger.</summary>
    bool GetCancelHeld();
    bool GetEmpowerHeld();
    /// <summary>Press C while soul is chain-bound by SiphonGhost — mash to break free.</summary>
    bool GetSoulBreakMash();
    /// <summary>Press E while grabbed � mash to trigger struggle pause (tier-1 traps only).</summary>
    bool GetStruggleMash();
}