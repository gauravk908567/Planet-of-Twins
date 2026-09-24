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

    // ── Item 4 (skill-tree controller nav) — UI-map reads, polled by SkillTreeUI / SkillPreviewModal
    //    while the tree is open (menu context, ungated). Any other IInputProvider impl must add these. ──
    /// <summary>Press LB (left shoulder) — cycle the skill-tree tab left (Kai ← Lyra ← Shared).</summary>
    bool GetUITabLeftDown();
    /// <summary>Press RB (right shoulder) — cycle the skill-tree tab right.</summary>
    bool GetUITabRightDown();
    /// <summary>Press Y/North — instant-buy the focused skill node (no preview). Also buys while the
    /// preview modal is open. Separate from Submit (A/South = open preview → 2nd press buys).</summary>
    bool GetInstantBuyDown();
    /// <summary>Press B/East — "back" in menus/modals (close the skill preview, back out a panel). Polled
    /// alongside the EventSystem's own Cancel; the menu that owns the layer decides what "back" does.</summary>
    bool GetUICancelDown();
    /// <summary>Press button 1 / North — skill-tree "open preview" over the focused node (1st press), and
    /// "buy" inside the preview modal (2nd press). Separate from InstantBuy (button 3 / South = direct buy).</summary>
    bool GetUIPreviewDown();
    /// <summary>Press X/West (keyboard Delete) — "delete the focused item" in menus (save-slot screen). The
    /// menu shows a confirmation before anything is removed.</summary>
    bool GetUIDeleteDown();

    // ── F5 (Button HUDs) — live binding display for on-screen prompts ──
    /// <summary>
    /// Human-readable key/button label for an action, read live from the Input System
    /// action asset (P13) so prompts stay true under rebinding (F6). Never hard-code key names.
    /// <paramref name="actionName"/> is the action name in the asset (e.g. "Interact",
    /// "Teleport", "Attack"). <paramref name="preferGamepad"/> picks the gamepad binding
    /// instead of keyboard/mouse. Returns "?" if the action or a matching binding is missing.
    /// </summary>
    string GetBindingDisplay(string actionName, bool preferGamepad = false);

    /// <summary>F6 — human-readable label for ONE part of a composite binding (e.g. Move's "up"/"down"/"left"/
    /// "right" 2DVector parts) of the given device family, read live so it stays true under rebinding. Returns "?"
    /// when the action has no such composite part for that family (e.g. a gamepad stick has no directional parts).</summary>
    string GetCompositePartDisplay(string actionName, string part, bool preferGamepad);

    /// <summary>F6 — the effective (override-aware) layout-relative control PATH for a row's binding ("w",
    /// "buttonNorth"), or a composite part when <paramref name="part"/> is set. Used by the CONTROLS view to detect
    /// duplicate bindings (conflicts) within a column. null when the action or a matching binding is missing.</summary>
    string GetEffectiveBindingPath(string actionName, string part, InputDeviceKind kind);

    /// <summary>F6 — has this row's binding been changed from its authored default? The CONTROLS conflict check uses
    /// it to grandfather duplicates that ship in the defaults (Interact + Convergence share F/A by design), flagging
    /// only a duplicate the player introduced.</summary>
    bool IsRowBindingChanged(string actionName, string part, InputDeviceKind kind);

    // ── Item 5 (button glyphs) — the control PATH (not display text) is the glyph key ──
    /// <summary>The resolved CONTROL PATH for an action's binding of the given device kind (e.g. "buttonSouth",
    /// "f", "leftButton") — the stable key the glyph atlas is indexed by, unlike <see cref="GetBindingDisplay"/>'s
    /// human text. False when the action or a binding for that kind is missing (caller falls back to text).</summary>
    bool TryGetBindingControlPath(string actionName, InputDeviceKind kind, out string controlPath, out string deviceLayout);

    /// <summary>This provider's paired device family (couch per-occupant): Gamepad if restricted to a pad, else
    /// KeyboardMouse; null when UNRESTRICTED (solo / shared) — the caller then uses <see cref="LastUsedDeviceTracker"/>.</summary>
    InputDeviceKind? PairedDeviceKind { get; }

    /// <summary>
    /// F7 — clear all runtime binding overrides on the action asset, restoring the authored
    /// defaults (Assets/Settings/Input/PlanetOfTwins.inputactions). Safe no-op today (no
    /// rebinding UI exists yet — that lands in F6); wired now so the pause "Restore Default
    /// Keybinds" button and future F6 rebinding can rely on it.
    /// </summary>
    void ResetBindingsToDefault();

    // ── F6 Phase 3 (CONTROLS edit mode: two independent per-column cursors + interactive rebind) ──
    /// <summary>UI-map Navigate value (Vector2) read from THIS provider's own (per-player, device-restricted)
    /// action asset — so P1's keyboard and P2's pad drive their own column cursor independently. Menu context,
    /// ungated (the game is already paused when the settings screen is open).</summary>
    Vector2 GetUINavigate();

    /// <summary>UI-map Submit pressed this frame on THIS provider's own asset — confirm/enter on the focused row
    /// (Enter / pad South). Menu context, ungated.</summary>
    bool GetUISubmitDown();

    /// <summary>UI-map Submit currently held on THIS provider's own asset. The rebind flow waits for this to go
    /// false before capturing, so the button that CONFIRMED the rebind (Enter / pad South) isn't captured as the
    /// new binding.</summary>
    bool GetUISubmitHeld();

    /// <summary>F6 — begin an interactive rebind of <paramref name="actionName"/>'s device-family binding on THIS
    /// provider's own action asset (per-player: the override is live and independent of the other player). The next
    /// control the player actuates becomes the new binding. <paramref name="onDone"/> fires on complete OR cancel
    /// (the UI refreshes either way). Returns false immediately (never starting) when the action/binding is missing
    /// or this provider can't rebind a single device (the shared aggregator). Cancels any rebind already in flight
    /// on this provider first. Live/in-session only — no persistence (project save system is deferred/inert).
    /// <paramref name="part"/> names a composite part to rebind ("up"/"down"/"left"/"right" for Move's 2DVector);
    /// null/empty rebinds the action's single device-family binding (the common case).</summary>
    bool StartInteractiveRebind(string actionName, string part, System.Action onDone);

    /// <summary>Cancel an interactive rebind in flight on this provider (player pressed Back / left edit mode /
    /// the screen closed mid-capture). Idempotent — a no-op when nothing is capturing.</summary>
    void CancelActiveRebind();

    /// <summary>Does this provider currently have a live paired input device? True when UNRESTRICTED (reads all
    /// devices) or when at least one paired device is still present in the system. False when this provider's
    /// paired device(s) have all disconnected. The settings screen uses this to grey the disconnected player's
    /// column WITHOUT reshuffling slot↔device assignments (which mid-rebind would confuse both players).</summary>
    bool HasLivePairedDevice();
}