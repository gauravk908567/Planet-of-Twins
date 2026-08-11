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
    /// <summary>Hold F to charge Soul Convergence or Setsuna. Gated by IsAbilityAllowed.</summary>
    bool GetConvergenceHeld();

    // ── P13 additions (former raw-Input consumers; all ungated — the 6 tutorial-gate
    //    categories are unchanged). Any other IInputProvider implementation must add these.
    /// <summary>Press B — toggle the overview camera (OverviewCamController).</summary>
    bool GetOverviewDown();
    /// <summary>B still held — the overview cam is hold-to-view (OverviewCamController, 2026-07-16).</summary>
    bool GetOverviewHeld();
    /// <summary>Freeze/unfreeze all GAMEPLAY inputs (movement/attack/ability/holds/mash) while a
    /// system owns the world (overview cam). Pause/Overview/AnySkip stay live. Idempotent.</summary>
    void SetGameplayFrozen(bool frozen);
    /// <summary>Press ESC — pause/back. Sole consumer: PauseMenuController's priority chain.</summary>
    bool GetPauseDown();
    /// <summary>Press Tab — toggle the skill tree (SkillTreeUI).</summary>
    bool GetSkillTreeToggleDown();
    /// <summary>Press F during a QTE mash phase (QTEManager / QTEController).</summary>
    bool GetQTEMashDown();
    /// <summary>Press anything — intro cutscene skip (IntroController).</summary>
    bool GetAnySkipDown();
    /// <summary>Press H — show/hide the control-hints panel (ControlHintsVisibility). Ungated.</summary>
    bool GetHintsToggleDown();

    // ── F5 (Button HUDs) — live binding display for on-screen prompts ──
    /// <summary>
    /// Human-readable key/button label for an action, read live from the Input System
    /// action asset (P13) so prompts stay true under rebinding (F6). Never hard-code key names.
    /// <paramref name="actionName"/> is the action name in the asset (e.g. "Interact",
    /// "Teleport", "Attack"). <paramref name="preferGamepad"/> picks the gamepad binding
    /// instead of keyboard/mouse. Returns "?" if the action or a matching binding is missing.
    /// </summary>
    string GetBindingDisplay(string actionName, bool preferGamepad = false);

    /// <summary>
    /// F7 — clear all runtime binding overrides on the action asset, restoring the authored
    /// defaults (Assets/Settings/Input/PlanetOfTwins.inputactions). Safe no-op today (no
    /// rebinding UI exists yet — that lands in F6); wired now so the pause "Restore Default
    /// Keybinds" button and future F6 rebinding can rely on it.
    /// </summary>
    void ResetBindingsToDefault();
}