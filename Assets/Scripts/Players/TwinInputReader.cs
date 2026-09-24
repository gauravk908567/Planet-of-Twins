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
    // UI map — Item 4 (skill-tree controller nav): LB/RB tab switch + instant-buy + B/East back + open-preview
    private InputAction _uiTabLeft, _uiTabRight, _instantBuy, _uiCancel, _uiPreview;
    // UI map — F6 Phase 3 (CONTROLS edit mode): per-column cursor move + confirm
    private InputAction _uiNavigate, _uiSubmit;
    // UI map — save-slot screen: X/West (keyboard Delete) = delete the focused slot (menu confirms first)
    private InputAction _uiDelete;
    // F6 Phase 3 — the single interactive-rebind op in flight on THIS reader's asset (one at a time). Disposed on
    // complete/cancel and when the reader disables, so a captured op never dangles listening for input.
    private UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation _activeRebind;

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

        LoadBindingOverrides();   // before the asset enables (OnEnable) — the player's saved rebinds apply from frame 1

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
        _uiTabLeft   = Find("UI/TabLeft");
        _uiTabRight  = Find("UI/TabRight");
        _instantBuy  = Find("UI/InstantBuy");
        _uiCancel    = Find("UI/UICancel");
        _uiPreview   = Find("UI/UIPreview");
        _uiDelete    = Find("UI/UIDelete");
        _uiNavigate  = Find("UI/Navigate");
        _uiSubmit    = Find("UI/Submit");
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
        CancelActiveRebind();   // never leave a capture listening once the asset is disabled
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

    // "Press ANY key / button" — intro skip / tutorial-prompt dismiss. The AnySkip action only carries the
    // clean bindings (mouse + gamepad South/Start); its <Keyboard>/anyKey binding is inert through
    // WasPressedThisFrame (anyKey is a synthetic control, no clean Button "performed" edge), and a South-only
    // pad binding misses the rest of the pad. So beyond the action we poll this reader's OWN devices for any
    // key / top-level button this frame — so a keyboard-only AND a gamepad-only player can dismiss with
    // literally anything — honouring couch pairing (a device-restricted reader only skips on its own devices).
    public bool GetAnySkipDown()
    {
        if (Down(_anySkip)) return true;

        if (_actions != null && _actions.devices.HasValue)
        {
            foreach (var d in _actions.devices.Value)
                if (AnySkipFromDevice(d)) return true;
            return false;
        }
        // Unrestricted (solo / single-device): check the current keyboard + gamepad.
        return AnySkipFromDevice(Keyboard.current) || AnySkipFromDevice(Gamepad.current);
    }

    // Any key (keyboard) or any TOP-LEVEL button (pad/joystick: face/shoulder/trigger/stick-press/start/select)
    // pressed this frame. Top-level only (parent == device) so stick / dpad directional drift never auto-dismisses.
    private static bool AnySkipFromDevice(InputDevice device)
    {
        if (device == null) return false;
        if (device is Keyboard kb) return kb.anyKey.wasPressedThisFrame;

        var controls = device.allControls;
        for (int i = 0; i < controls.Count; i++)
            if (controls[i] is UnityEngine.InputSystem.Controls.ButtonControl b &&
                ReferenceEquals(b.parent, device) && b.wasPressedThisFrame)
                return true;
        return false;
    }

    // Hints panel show/hide — H (ControlHintsVisibility; ungated, works while frozen/paused)
    public bool GetHintsToggleDown() => Down(_toggleHints);

    // Item 4 — skill-tree tab switch (LB/RB) + instant-buy (Y/North). Ungated menu reads; only the
    // skill-tree UI acts on them, and only while its panel is open (game already frozen at timeScale 0).
    public bool GetUITabLeftDown() => Down(_uiTabLeft);
    public bool GetUITabRightDown() => Down(_uiTabRight);
    public bool GetInstantBuyDown() => Down(_instantBuy);
    public bool GetUICancelDown() => Down(_uiCancel);
    public bool GetUIPreviewDown() => Down(_uiPreview);
    public bool GetUIDeleteDown() => Down(_uiDelete);

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

    // F6 — human-readable label for one composite PART (Move's "up"/"down"/"left"/"right"), read live so it stays
    // true under rebinding. "?" when the family has no such composite part (e.g. a gamepad stick).
    public string GetCompositePartDisplay(string actionName, string part, bool preferGamepad)
    {
        if (_actions == null || string.IsNullOrEmpty(actionName) || string.IsNullOrEmpty(part)) return "?";
        var action = _actions.FindAction(actionName, throwIfNotFound: false);
        if (action == null) return "?";
        int idx = FindCompositePartIndex(action, preferGamepad, part);
        if (idx < 0) return "?";
        string d = action.GetBindingDisplayString(idx, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        return string.IsNullOrEmpty(d) ? "?" : d;
    }

    // F6 — the effective (override-aware) layout-relative control PATH for a row's binding (e.g. "w", "buttonNorth"),
    // or a composite part when `part` is set. The device family is fixed by the column, so this path alone is a
    // sufficient key for duplicate-binding (conflict) detection. null when the action/binding is missing.
    public string GetEffectiveBindingPath(string actionName, string part, InputDeviceKind kind)
    {
        if (_actions == null || string.IsNullOrEmpty(actionName)) return null;
        var action = _actions.FindAction(actionName, throwIfNotFound: false);
        if (action == null) return null;
        bool pad = kind == InputDeviceKind.Gamepad;
        int idx = string.IsNullOrEmpty(part) ? FindBindingIndex(action, pad) : FindCompositePartIndex(action, pad, part);
        if (idx < 0) return null;
        action.GetBindingDisplayString(idx, out _, out string controlPath, InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        return string.IsNullOrEmpty(controlPath) ? null : controlPath;
    }

    // F6 — has this row's binding been CHANGED from its authored default (effective path ≠ authored path)? The
    // conflict check uses this to grandfather duplicates that ship in the defaults (e.g. Interact + Convergence both
    // on F/A by design) — only a duplicate the player introduced is flagged. An override equal to the default is not
    // "changed".
    public bool IsRowBindingChanged(string actionName, string part, InputDeviceKind kind)
    {
        if (_actions == null || string.IsNullOrEmpty(actionName)) return false;
        var action = _actions.FindAction(actionName, throwIfNotFound: false);
        if (action == null) return false;
        bool pad = kind == InputDeviceKind.Gamepad;
        int idx = string.IsNullOrEmpty(part) ? FindBindingIndex(action, pad) : FindCompositePartIndex(action, pad, part);
        if (idx < 0) return false;
        var b = action.bindings[idx];
        return (b.effectivePath ?? string.Empty) != (b.path ?? string.Empty);
    }

    // ── Item 5 (button glyphs) — control-PATH resolution ───────────────────────────────
    // Unlike GetBindingDisplay (human TEXT: "E", "Button South"), this yields the stable CONTROL PATH the
    // glyph atlas keys on ("f", "buttonSouth", "leftShoulder"). Resolves the binding for the requested device
    // kind; false when the action or a binding for that kind is missing (caller falls back to text).
    public bool TryGetBindingControlPath(string actionName, InputDeviceKind kind,
                                         out string controlPath, out string deviceLayout)
    {
        controlPath = null;
        deviceLayout = null;
        if (_actions == null || string.IsNullOrEmpty(actionName)) return false;

        var action = _actions.FindAction(actionName, throwIfNotFound: false);
        if (action == null) return false;

        int chosen = FindBindingIndex(action, kind == InputDeviceKind.Gamepad);
        if (chosen < 0) return false;

        // The out-param overload yields the resolved layout-relative control path (no device prefix),
        // e.g. "buttonSouth" / "f" — exactly the glyph key. Rebinding-safe (reads effective bindings).
        action.GetBindingDisplayString(chosen, out deviceLayout, out controlPath,
            InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
        return !string.IsNullOrEmpty(controlPath);
    }

    // This reader's paired device family (couch per-occupant): Gamepad if it is restricted to a Gamepad
    // device (SetPairedDevices), else KeyboardMouse. Null when UNRESTRICTED (solo / single-device) — the
    // caller then resolves the family from the last-used-device tracker (P1.2) instead.
    public InputDeviceKind? PairedDeviceKind
    {
        get
        {
            if (_actions == null || !_actions.devices.HasValue) return null; // unrestricted
            foreach (var d in _actions.devices.Value)
                if (d is Gamepad) return InputDeviceKind.Gamepad;
            return InputDeviceKind.KeyboardMouse;
        }
    }

    // ── F7 — restore default keybinds ──────────────────────────────────
    // Clears every runtime binding override on the whole asset, returning to the authored defaults, and forgets
    // this player's persisted rebinds (so defaults survive a restart too).
    public void ResetBindingsToDefault()
    {
        if (_actions == null)
        {
            Debug.LogError("[TwinInputReader] ResetBindingsToDefault: InputActionAsset not assigned.", this);
            return;
        }
        _actions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(BindingPrefsKey);
        PlayerPrefs.Save();
    }

    // ── Rebind persistence (save-system §4(d)) ─────────────────────────
    // Rebinds are SETTINGS, not progress → PlayerPrefs per PLAYER (P1 = the shared reader, P2 = the non-shared
    // clone), never inside a save slot: New Game / slot choice must not reset a player's controls. Only the
    // override delta is stored (SaveBindingOverridesAsJson), keyed by binding id — authored defaults changing
    // later still apply to every binding the player never touched. The P2 clone is built from ToJson(), which
    // carries no overrides, so P1's rebinds can never leak into P2 (each loads its own key).
    private string BindingPrefsKey => _isShared ? "pot_bindings_p1" : "pot_bindings_p2";

    private void LoadBindingOverrides()
    {
        string json = PlayerPrefs.GetString(BindingPrefsKey, "");
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            _actions.LoadBindingOverridesFromJson(json, removeExisting: true);
        }
        catch (System.Exception e)
        {
            // Corrupt/incompatible prefs must never brick input — drop them and run on authored defaults.
            Debug.LogWarning($"[TwinInputReader] Saved rebinds for '{BindingPrefsKey}' unreadable ({e.Message}) — " +
                             "reverting to defaults.", this);
            _actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingPrefsKey);
        }
    }

    private void SaveBindingOverrides()
    {
        if (_actions == null) return;
        PlayerPrefs.SetString(BindingPrefsKey, _actions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();   // flush now — "changes saved live" must hold even if the game is killed
    }

    // ── F6 Phase 3 — CONTROLS edit-mode UI reads + interactive rebind ──────────────────
    // These read/rebind THIS reader's own (per-player, device-restricted) asset, so P1's keyboard and P2's pad
    // each drive their own column cursor and their own overrides — no cross-talk. Menu context (game paused),
    // so the tutorial gate doesn't apply. Never frozen: the settings screen is a menu, not gameplay.
    public Vector2 GetUINavigate() => _uiNavigate?.ReadValue<Vector2>() ?? Vector2.zero;
    public bool GetUISubmitDown() => Down(_uiSubmit);
    public bool GetUISubmitHeld() => Held(_uiSubmit);

    // Begin an interactive rebind of the chosen action's device-family binding on THIS reader's asset. The
    // override is live and independent (P2 clones its own asset). Pad-B (buttonEast) and mouse motion are excluded
    // so "cancel/back" and the pointer never get captured AS the binding; keyboard Escape cancels the capture. The
    // onDone callback fires on both complete and cancel — the view just re-reads the current binding either way.
    public bool StartInteractiveRebind(string actionName, string part, System.Action onDone)
    {
        if (_actions == null || string.IsNullOrEmpty(actionName)) return false;

        var action = _actions.FindAction(actionName, throwIfNotFound: false);
        if (action == null) return false;

        bool preferPad = PairedDeviceKind == InputDeviceKind.Gamepad;
        // part set (e.g. Move's "up") → rebind that composite part's binding; else the single device-family binding.
        int idx = string.IsNullOrEmpty(part)
            ? FindBindingIndex(action, preferPad)
            : FindCompositePartIndex(action, preferPad, part);
        if (idx < 0) return false;   // no binding for this device family (or no such composite part) → read-only

        CancelActiveRebind();        // one capture at a time on this reader

        // COUCH SAFETY (concurrent rebinds): a RebindingOperation listens to ALL input globally — it is NOT scoped to
        // the action's paired devices. Two hazards if the other player touches their device mid-capture:
        //   (1) their control could be captured INTO this binding (merge/swap), and
        //   (2) — the subtler one — a foreign press is registered as a CANDIDATE (arming the 0.05s completion timer)
        //       one line BEFORE our filter can drop it, so the op then "completes" with zero candidates: it applies no
        //       binding but ENDS the capture. To the player that looks like a mysterious cancel the instant the other
        //       player moves their pad.
        // FIX: EXCLUDE every non-owned device UP FRONT (checked before a control can become a candidate), so foreign
        // input never arms the timer at all. OnPotentialMatch is kept as a second line of defence for binding
        // correctness. When UNRESTRICTED (solo), only one column is editable, so there's no concurrency to guard.
        InputDevice[] ownDevices = null;
        if (_actions.devices.HasValue && _actions.devices.Value.Count > 0)
        {
            var devs = _actions.devices.Value;
            ownDevices = new InputDevice[devs.Count];
            for (int i = 0; i < devs.Count; i++) ownDevices[i] = devs[i];
        }

        // The action MUST be disabled BEFORE PerformInteractiveRebinding — that call's internal WithAction() THROWS
        // "Cannot rebind action while it is enabled" (gameplay actions stay enabled under the pause). Disable ONLY this
        // one action (never the whole map) so the other player and this player's other inputs are untouched; re-enable
        // on BOTH complete and cancel. The game is paused, so losing this one action for the ~1s capture is invisible.
        // The whole build+Start is wrapped so any throw fails loud + re-enables — the action can never get stuck off.
        action.Disable();
        UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation op = null;
        try
        {
            // Mid-rebind CANCEL is a dedicated control so the player's normal Back button stays fully BINDABLE (the AAA
            // pattern): keyboard Escape, gamepad SELECT (View/Share). Pad-East (B) is therefore capturable like any other
            // button — a duplicate it creates is surfaced by the view's conflict check, not swallowed here. Binding
            // capture itself is device-isolated by OnPotentialMatch.
            op = action.PerformInteractiveRebinding(idx);
            if (preferPad)
                op = op.WithCancelingThrough("<Gamepad>/select");
            else
                op = op.WithCancelingThrough("<Keyboard>/escape");
            if (ownDevices != null)
            {
                // (a) Exclude the OPPOSITE device family wholesale — reliable, layout-based; covers the common
                //     keyboard + pad couch split (P1 keyboard ignores all pads, P2 pad ignores keyboard/mouse).
                if (preferPad)
                    op = op.WithControlsExcluding("<Keyboard>").WithControlsExcluding("<Mouse>").WithControlsExcluding("<Pointer>");
                else
                    op = op.WithControlsExcluding("<Gamepad>").WithControlsExcluding("<Joystick>");
                // (b) Exclude any SAME-family FOREIGN device instance (e.g. the 2nd pad in a two-pad game) by its
                //     runtime path, so only THIS reader's own device(s) can ever become a candidate.
                foreach (var dev in UnityEngine.InputSystem.InputSystem.devices)
                {
                    bool mine = false;
                    for (int d = 0; d < ownDevices.Length; d++)
                        if (ReferenceEquals(ownDevices[d], dev)) { mine = true; break; }
                    if (!mine && !string.IsNullOrEmpty(dev.path)) op = op.WithControlsExcluding(dev.path);
                }
                // (c) Second line of defence: drop any foreign candidate that still slips through before it can bind.
                op = op.OnPotentialMatch(o =>
                {
                    for (int i = o.candidates.Count - 1; i >= 0; i--)
                    {
                        var dev = o.candidates[i].device;
                        bool mine = false;
                        for (int d = 0; d < ownDevices.Length; d++)
                            if (ReferenceEquals(ownDevices[d], dev)) { mine = true; break; }
                        if (!mine) o.RemoveCandidate(o.candidates[i]);
                    }
                });
            }
            op = op.OnComplete(o => { _activeRebind = null; o.Dispose(); action.Enable(); SaveBindingOverrides(); onDone?.Invoke(); })
                   .OnCancel(o => { _activeRebind = null; o.Dispose(); action.Enable(); onDone?.Invoke(); });

            _activeRebind = op;
            op.Start();
        }
        catch (System.Exception e)
        {
            // Fail loud, restore state (R4) — never leave the action stuck disabled.
            Debug.LogError($"[TwinInputReader] interactive rebind failed to start for '{actionName}': {e.Message}", this);
            _activeRebind = null;
            op?.Dispose();
            action.Enable();
            return false;
        }
        return true;
    }

    public void CancelActiveRebind()
    {
        if (_activeRebind == null) return;
        var op = _activeRebind;
        _activeRebind = null;        // null first so the OnCancel handler is a no-op re-entry
        op.Cancel();
        op.Dispose();
    }

    // Live paired-device check (couch settings screen). Unrestricted (solo / single-device) always reads as live.
    // Otherwise at least one paired device must still be present in the system (InputDevice.added flips false when
    // a device is removed/disconnected). Lets the settings screen grey the DISCONNECTED slot without CouchDeviceManager
    // reshuffling device↔slot assignments mid-rebind.
    public bool HasLivePairedDevice()
    {
        if (_actions == null) return false;
        if (!_actions.devices.HasValue || _actions.devices.Value.Count == 0) return true;   // unrestricted
        foreach (var d in _actions.devices.Value)
            if (d != null && d.added) return true;
        return false;
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

    // Resolve the binding index of a named composite PART (e.g. Move's "up") for the requested device family. Walks
    // composites, picks the first whose parts belong to that family (peeking the first part's path), then matches the
    // part by name. Returns -1 when there's no such family composite (e.g. gamepad Move is a stick/dpad single, not a
    // 2DVector composite) — the caller then treats the row as read-only.
    private static int FindCompositePartIndex(InputAction action, bool preferGamepad, string partName)
    {
        var bindings = action.bindings;
        int i = 0;
        while (i < bindings.Count)
        {
            if (bindings[i].isComposite)
            {
                int firstPart = i + 1;
                string partPath = firstPart < bindings.Count ? (bindings[firstPart].effectivePath ?? string.Empty) : string.Empty;
                bool isPad = partPath.StartsWith("<Gamepad>");
                bool isKbm = partPath.StartsWith("<Keyboard>") || partPath.StartsWith("<Mouse>");
                if (preferGamepad ? isPad : isKbm)
                {
                    for (int j = firstPart; j < bindings.Count && bindings[j].isPartOfComposite; j++)
                        if (string.Equals(bindings[j].name, partName, System.StringComparison.OrdinalIgnoreCase))
                            return j;
                }
                // skip past this composite's parts and keep looking
                i = firstPart;
                while (i < bindings.Count && bindings[i].isPartOfComposite) i++;
                continue;
            }
            i++;
        }
        return -1;
    }
}
