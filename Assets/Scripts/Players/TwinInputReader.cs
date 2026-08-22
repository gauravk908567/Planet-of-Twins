using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Raw input reader — the ONLY class that touches input devices (P13: New Input System).
/// All dispatchers wire to this permanently; UI/QTE/intro consumers resolve
/// TwinInputReader.Instance and read through IInputProvider.
///
/// P13 migration contract: the tutorial-gate seam is UNCHANGED —
/// the gate check lives INSIDE the IInputProvider getters (read action → apply gate →
/// return), never by enabling/disabling InputActions (that would break per-category
/// fail-open semantics and area-scene gate registration).
///
/// The tutorial gate is registered at runtime by TutorialInputGate.OnEnable()
/// (cross-scene — cannot be serialized in Inspector). Leave it blank in Persistent.
/// When null, every input is allowed unconditionally (fail-open).
///
/// SETUP: assign _actions = Assets/Settings/Input/PlanetOfTwins.inputactions
/// (Gameplay + UI maps). Missing asset/actions ⇒ LogError + that input is dead (R4 fail-loud).
/// </summary>
public class TwinInputReader : MonoBehaviour, IInputProvider, ISingletonInstanceGuard
{
    public static TwinInputReader Instance { get; private set; }

    // Couch M1.6: only the shared P1 reader is the singleton. The non-shared P2 reader opts out of the
    // duplicate-singleton integrity check (SceneIntegrityChecker) — a second reader is deliberate, not an R3 bug.
    public bool IsSingletonInstance => _isShared;

    [Header("Input System (P13)")]
    [Tooltip("PlanetOfTwins.inputactions — needs a 'Gameplay' and a 'UI' map. Fail-loud when missing.")]
    [SerializeField] private InputActionAsset _actions;

    [Tooltip("Couch M1.6: the SHARED / P1 reader is the Instance singleton (UI/QTE/intro + tutorial gate). " +
             "Uncheck for a P2 gameplay-only reader bound to a second device by CouchDeviceManager.")]
    [SerializeField] private bool _isShared = true;

    // No serialized gate field — resolved at runtime by TutorialInputGate.
    // STATIC (couch M4): the tutorial gate is GLOBAL progression policy, not player-scoped state — a mechanic
    // unlocks for BOTH twins at once (shared-progression tutorial). TutorialInputGate registers it on the P1
    // Instance, but every reader (P1 + the non-shared P2) must honor the same gate, so it lives on the TYPE,
    // not the instance. Per-device *reading* stays per-instance (each reader owns its actions); only the
    // allow/deny policy is shared. Cleared to fail-open in Awake by the shared reader (R3 — see below).
    private static ITutorialGate _gate;

    // Overview-cam world freeze (2026-07-16): gameplay getters read as silence while true.
    // Same seam philosophy as the tutorial gate — checked INSIDE the getters, actions stay enabled.
    // STATIC (couch M4): the overview freeze is a SHARED-SCREEN world stop (one camera, one world) — when P1
    // holds B the P2 twin must freeze too. Same rationale as _gate: global policy, per-instance reads.
    private static bool _gameplayFrozen;

    // Gameplay map (cached in Awake — FindAction per frame allocates)
    private InputAction _move, _attack, _switch, _ability, _teleport, _interact,
                        _cancel, _empower, _struggle, _soulBreak, _convergence,
                        _overview, _qteMash;
    // UI map
    private InputAction _pause, _skillTree, _anySkip, _toggleHints;

    private void Awake()
    {
        // Couch M1.6: only the SHARED reader (P1) is the Instance singleton, serves UI/QTE/intro, and hosts the
        // tutorial gate. A non-shared reader (P2) is a second gameplay-only source bound to its own device.
        if (_isShared)
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // R3: the static gate/freeze survive a Restart (Bootstrap reload — builds skip the domain reload).
            // The shared/P1 reader is the one-per-boot anchor, so clear both to their fail-open defaults here —
            // a fresh run never inherits a stale tutorial lock or overview freeze. Re-established at runtime by
            // TutorialInputGate.Start (gate) and the overview B-hold (freeze).
            _gate = null;
            _gameplayFrozen = false;
        }

        if (_actions == null)
        {
            Debug.LogError("[TwinInputReader] InputActionAsset not assigned — ALL input is dead. " +
                           "Wire Assets/Settings/Input/PlanetOfTwins.inputactions.", this);
            enabled = false;   // OnEnable never runs → actions never enable
            return;
        }

        // P2 needs its OWN asset instance so its device pairing (.devices) is independent of P1's.
        // JSON round-trip is the Input System's stable deep-copy for a second player's action asset.
        if (!_isShared)
        {
            var clone = InputActionAsset.FromJson(_actions.ToJson());
            clone.name = _actions.name + " (P2)";
            _actions = clone;
        }

        _move        = Find("Gameplay/Move");
        _attack      = Find("Gameplay/Attack");
        _switch      = Find("Gameplay/Switch");
        _ability     = Find("Gameplay/Ability");
        _teleport    = Find("Gameplay/Teleport");
        _interact    = Find("Gameplay/Interact");
        _cancel      = Find("Gameplay/Cancel");
        _empower     = Find("Gameplay/Empower");
        _struggle    = Find("Gameplay/Struggle");
        _soulBreak   = Find("Gameplay/SoulBreak");
        _convergence = Find("Gameplay/Convergence");
        _overview    = Find("Gameplay/Overview");
        _qteMash     = Find("Gameplay/QTEMash");
        _pause       = Find("UI/Pause");
        _skillTree   = Find("UI/SkillTree");
        _anySkip     = Find("UI/AnySkip");
        _toggleHints = Find("UI/ToggleHints");
    }

    private InputAction Find(string path)
    {
        var action = _actions.FindAction(path);
        if (action == null)
            Debug.LogError($"[TwinInputReader] Action '{path}' missing from '{_actions.name}' — that input is dead.", this);
        return action;
    }

    private void OnEnable()
    {
        // Whole-asset enable — per-category gating happens in the getters, NEVER here (P13 contract).
        _actions?.Enable();
    }

    private void OnDisable()
    {
        _actions?.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Called by TutorialInputGate.OnEnable() when the tutorial scene loads,
    /// and with null by TutorialInputGate.OnDisable() when it unloads.
    /// </summary>
    public void SetGate(ITutorialGate gate) => _gate = gate;

    /// <summary>Overview cam holds the world still — gameplay inputs freeze; Pause/Overview/AnySkip stay live.</summary>
    public void SetGameplayFrozen(bool frozen) => _gameplayFrozen = frozen;

    /// <summary>Couch M1.6 — restrict this reader to specific input devices (per-player pairing). Null/empty =
    /// read from ALL devices (solo / single-device). Driven by CouchDeviceManager when a second device joins.</summary>
    public void SetPairedDevices(params InputDevice[] devices)
    {
        if (_actions == null) return;

        // Toggle the asset around the change so bindings RE-RESOLVE against the new device set. Assigning
        // `.devices` on a live (enabled) asset does not reliably re-pick the active controls, which left BOTH
        // readers still bound to every gamepad — the couch symptom where both players follow one pad.
        // Disable → set → enable forces a clean re-resolution. (A reader that's currently disabled — e.g. P2
        // before CouchDeviceManager enables it — is left disabled; it resolves correctly when enabled.)
        bool wasEnabled = _actions.FindActionMap("Gameplay")?.enabled ?? false;
        if (wasEnabled) _actions.Disable();

        if (devices == null || devices.Length == 0)
            _actions.devices = null;          // no restriction — read every device
        else
            _actions.devices = devices;       // restrict to this player's device(s)

        if (wasEnabled) _actions.Enable();
    }

    private bool _joystickBindingsApplied;

    /// <summary>Couch M1.6b — add &lt;Joystick&gt; bindings so a generic DirectInput pad (which has NO &lt;Gamepad&gt;
    /// bindings) can drive this reader. Call while the reader is DISABLED (AddBinding requires disabled actions),
    /// before enabling; applied once (guarded) so re-assignment never stacks duplicates. stick→Move and
    /// trigger→Attack are reliable; button2..8 are a sensible default — physical layout varies per pad, so the
    /// player identifies which is which and we remap here. Bindings stay inert unless a Joystick is the paired device.</summary>
    public void ApplyJoystickBindings()
    {
        if (_joystickBindingsApplied) return;
        _joystickBindingsApplied = true;

        // Reliable across pads
        _move?.AddBinding("<Joystick>/stick");
        _attack?.AddBinding("<Joystick>/trigger");     // button1
        _struggle?.AddBinding("<Joystick>/trigger");   // E-share: struggle == attack key

        // Default face/shoulder mapping (button2..button8) — tune once the player says which physical button is which.
        _ability?.AddBinding("<Joystick>/button2");      // primary (Q)
        _cancel?.AddBinding("<Joystick>/button3");       // cancel / Accord entry (X, hold)
        _empower?.AddBinding("<Joystick>/button4");      // empower (R, hold)
        _teleport?.AddBinding("<Joystick>/button5");     // emergency teleport (C, hold)
        _soulBreak?.AddBinding("<Joystick>/button5");    // C-share: soul-break == teleport key
        _interact?.AddBinding("<Joystick>/button6");     // rescue / interact (F)
        _convergence?.AddBinding("<Joystick>/button6");  // F-share: convergence hold
        _qteMash?.AddBinding("<Joystick>/button6");      // F-share: QTE mash
        _switch?.AddBinding("<Joystick>/button7");       // partner-dash during Empower (was Shift)
        _overview?.AddBinding("<Joystick>/button8");     // overview cam (B)
    }

    // ── Gate helpers (fail-open: null gate = everything allowed) ──
    private bool AttackAllowed => _gate == null || _gate.IsAttackAllowed;
    private bool AbilityAllowed => _gate == null || _gate.IsAbilityAllowed;
    private bool TeleportAllowed => _gate == null || _gate.IsTeleportAllowed;
    private bool RescueAllowed => _gate == null || _gate.IsRescueAllowed;
    private bool SwitchAllowed => _gate == null || _gate.IsSwitchAllowed;
    private bool InteractAllowed => _gate == null || _gate.IsInteractAllowed;

    // ── Action-read helpers (null-safe: a missing action reads as silence, LogError'd once in Awake) ──
    private static bool Down(InputAction a) => a != null && a.WasPressedThisFrame();
    private static bool Held(InputAction a) => a != null && a.IsPressed();
    private static bool Released(InputAction a) => a != null && a.WasReleasedThisFrame();

    // ── IInputProvider ────────────────────────────────────────
    // Move composites use 2DVector mode=1 (Digital, NOT normalized) to match legacy
    // GetAxisRaw exactly — diagonals read (±1, ±1); GetMovementDirection normalizes.
    public Vector2 GetMovementInput() => _gameplayFrozen ? Vector2.zero : _move?.ReadValue<Vector2>() ?? Vector2.zero;

    public Vector3 GetMovementDirection()
    {
        Vector2 v = GetMovementInput();
        return new Vector3(v.x, 0f, v.y).normalized;
    }

    // Attack — E or LMB (or gamepad West)
    public bool GetAttackDown() => !_gameplayFrozen && AttackAllowed && Down(_attack);

    // Switch — Shift
    public bool GetSwitchDown() => !_gameplayFrozen && SwitchAllowed && Down(_switch);

    // Ability — Q or RMB
    public bool GetAbilityDown() => !_gameplayFrozen && AbilityAllowed && Down(_ability);

    // Teleport — hold/release C
    public bool GetTeleportHeld() => !_gameplayFrozen && TeleportAllowed && Held(_teleport);
    public bool GetTeleportReleased() => !_gameplayFrozen && TeleportAllowed && Released(_teleport);

    // Rescue mash / interact — F (two gate categories over the same action)
    public bool GetRescueMash() => !_gameplayFrozen && RescueAllowed && Down(_interact);
    public bool GetInteractDown() => !_gameplayFrozen && InteractAllowed && Down(_interact);

    // Cancel — X (always allowed — needed to cancel QTE, soul chain etc)
    public bool GetCancelHeld() => !_gameplayFrozen && Held(_cancel);

    // Empower — hold R
    public bool GetEmpowerHeld() => !_gameplayFrozen && AbilityAllowed && Held(_empower);

    // Struggle — E while grabbed (always allowed — attack lock shouldn't block escape;
    // deliberately a SEPARATE action from Attack: struggle is keyboard-E only, not LMB)
    public bool GetStruggleMash() => !_gameplayFrozen && Down(_struggle);

    // Soul break — C mash while chain-bound (always allowed)
    public bool GetSoulBreakMash() => !_gameplayFrozen && Down(_soulBreak);

    // Convergence hold — F (Soul Convergence charge / Setsuna charge)
    public bool GetConvergenceHeld() => !_gameplayFrozen && AbilityAllowed && Held(_convergence);

    // ── P13 additions — former raw-Input consumers route here (all UNGATED, exactly as
    //    their raw reads were; the 6 gate categories are unchanged — the seam is locked) ──

    // Overview camera — B, hold-to-view (OverviewCamController). Never frozen — release must register.
    public bool GetOverviewDown() => Down(_overview);
    public bool GetOverviewHeld() => Held(_overview);

    // Pause / back — ESC (PauseMenuController's priority chain stays the sole consumer)
    public bool GetPauseDown() => Down(_pause);

    // Skill tree toggle — Tab (SkillTreeUI; ESC-close stays in PauseMenuController's chain)
    public bool GetSkillTreeToggleDown() => Down(_skillTree);

    // QTE mash — F (QTEManager + world-space QTEController; ungated — a QTE is already scripted)
    public bool GetQTEMashDown() => !_gameplayFrozen && Down(_qteMash);

    // "Press any key" — intro skip (keyboard anyKey + mouse buttons + gamepad South/Start)
    public bool GetAnySkipDown() => Down(_anySkip);

    // Hints panel show/hide — H (ControlHintsVisibility; ungated, works while frozen/paused)
    public bool GetHintsToggleDown() => Down(_toggleHints);

    // ── F5 (Button HUDs) — live binding display ────────────────────────
    // Reads the actual bound control from the action asset via GetBindingDisplayString,
    // so prompts stay correct after rebinding (F6). No control schemes are defined on the
    // asset, so we pick the first binding matching the requested device family.
    public string GetBindingDisplay(string actionName, bool preferGamepad = false)
    {
        if (_actions == null || string.IsNullOrEmpty(actionName)) return "?";

        var action = _actions.FindAction(actionName, throwIfNotFound: false);
        if (action == null)
        {
            Debug.LogWarning($"[TwinInputReader] GetBindingDisplay: action '{actionName}' not found.", this);
            return "?";
        }

        int chosen = FindBindingIndex(action, preferGamepad);
        if (chosen < 0)
        {
            // Fall back to the whole-action display string (may span composites).
            string all = action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
            return string.IsNullOrEmpty(all) ? "?" : all;
        }

        string display = action.GetBindingDisplayString(chosen, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        return string.IsNullOrEmpty(display) ? "?" : display;
    }

    // ── F7 — restore default keybinds ──────────────────────────────────
    // Clears every runtime binding override on the whole asset, returning to the authored
    // defaults. No-op today (no rebinding UI exists yet — F6); the pause "Restore Default
    // Keybinds" button calls this so F6 can rely on it existing.
    public void ResetBindingsToDefault()
    {
        if (_actions == null)
        {
            Debug.LogError("[TwinInputReader] ResetBindingsToDefault: InputActionAsset not assigned.", this);
            return;
        }
        _actions.RemoveAllBindingOverrides();
    }

    // Returns the index of the first non-composite binding whose control path targets the
    // requested device family (Gamepad, or Keyboard/Mouse otherwise). -1 if none match.
    private static int FindBindingIndex(InputAction action, bool preferGamepad)
    {
        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            var b = bindings[i];
            if (b.isComposite || b.isPartOfComposite) continue;
            string path = b.effectivePath ?? string.Empty;
            bool isPad = path.StartsWith("<Gamepad>");
            bool isKbm = path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>");
            if (preferGamepad ? isPad : isKbm) return i;
        }
        return -1;
    }
}
