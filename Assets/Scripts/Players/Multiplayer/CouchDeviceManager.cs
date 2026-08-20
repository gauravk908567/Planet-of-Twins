using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Couch M1.6 — assigns physical input devices to the two player slots and toggles solo↔couch routing.
/// Lives on the PlayerManager hub in Persistent (R3): duplicate-destroy Awake guard + null on OnDestroy, no DDOL.
///
/// <para><b>Model.</b> P1 is the shared <see cref="TwinInputReader"/> (the <c>Instance</c> singleton — also UI/QTE/
/// intro + the tutorial gate). P2 is a second, non-shared <see cref="TwinInputReader"/> bound to its own device.
/// This manager restricts each reader to its device(s) via <see cref="TwinInputReader.SetPairedDevices"/> and routes
/// slot Two to the P2 reader through <see cref="PlayerInputRouter.SetP2Provider"/>. Clearing the route (solo) reuses
/// the router's existing P2→P1 fallback so one device drives both twins — exactly the pre-M1.6 behaviour.</para>
///
/// <para><b>Assignment.</b> <see cref="AssignAuto"/> picks a sensible default and re-runs on device connect/disconnect:
/// two gamepads → P1/P2; one gamepad + keyboard → keyboard(+mouse)=P1, gamepad=P2; otherwise solo. Character Select
/// (front-end M2) can call <see cref="AssignCouch"/> / <see cref="SetSolo"/> explicitly instead. Editor-only F8
/// (Trainer) toggles solo↔couch for quick two-device testing.</para>
///
/// <para><b>Couch M4:</b> the tutorial gate + overview-cam freeze are now SHARED across both readers (static
/// policy on <see cref="TwinInputReader"/>) — a P1 lock/freeze applies to the P2 twin too. Remaining follow-up:
/// the both-keyboard split needs a P2 arrow-key binding set in the asset (M1.6b).</para>
/// </summary>
[DisallowMultipleComponent]
public class CouchDeviceManager : MonoBehaviour
{
    public static CouchDeviceManager Instance { get; private set; }

    [Header("Wiring (same-scene, R1)")]
    [Tooltip("P2 gameplay-only reader (TwinInputReader with 'Is Shared' unchecked). REQUIRED — it is not a singleton, " +
             "so it must be serialized. P1 reader + router resolve from their singletons in Start if left blank.")]
    [SerializeField] private TwinInputReader _p2Reader;
    [Tooltip("Optional — the shared P1 reader. Blank = TwinInputReader.Instance.")]
    [SerializeField] private TwinInputReader _p1Reader;
    [Tooltip("Optional — the router. Blank = PlayerInputRouter.Instance.")]
    [SerializeField] private PlayerInputRouter _router;

    [Header("Behaviour")]
    [Tooltip("Auto-assign devices on Start and whenever a device connects/disconnects. Uncheck to drive assignment " +
             "only through Character Select / the explicit API.")]
    [SerializeField] private bool _autoAssign = true;

    /// <summary>True while two devices are separately routed to the two twins.</summary>
    public bool IsCouchActive { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        InputSystem.onDeviceChange -= OnDeviceChange;   // named handler (R8)
    }

    private void Start()
    {
        // R4 — resolve the singletons here (never Awake; Instance order not guaranteed), fail loud.
        if (_p1Reader == null) _p1Reader = TwinInputReader.Instance;
        if (_router == null) _router = PlayerInputRouter.Instance;

        if (_p1Reader == null || _router == null)
        {
            Debug.LogError("[CouchDeviceManager] Missing P1 reader or router — device assignment disabled. " +
                           "Ensure the Persistent TwinInputReader + PlayerInputRouter exist.", this);
            enabled = false;
            return;
        }
        if (_p2Reader == null)
            Debug.LogWarning("[CouchDeviceManager] No P2 reader wired — couch play unavailable, solo only. " +
                             "Add a non-shared TwinInputReader and serialize it here.", this);

        InputSystem.onDeviceChange += OnDeviceChange;   // named handler (R8)
        if (_autoAssign) AssignAuto();
        else SetSolo();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!_autoAssign) return;
        switch (change)
        {
            case InputDeviceChange.Added:
            case InputDeviceChange.Removed:
            case InputDeviceChange.Reconnected:
            case InputDeviceChange.Disconnected:
                AssignAuto();
                break;
        }
    }

    // ── Assignment API ────────────────────────────────────────
    /// <summary>Default assignment: two gamepads → P1/P2; one gamepad + keyboard → keyboard(+mouse)=P1, gamepad=P2;
    /// otherwise solo (one device drives both twins).</summary>
    public void AssignAuto()
    {
        LogConnectedDevices();   // diagnostic — shows exactly what the Input System recognizes each device AS

        var controllers = GetControllerDevices();   // Gamepads preferred; generic Joysticks as fallback
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (_p2Reader != null && controllers.Count >= 2)
        {
            AssignCouch(new[] { controllers[0] }, new[] { controllers[1] });
        }
        else if (_p2Reader != null && controllers.Count >= 1 && kb != null)
        {
            var p1 = mouse != null ? new InputDevice[] { kb, mouse } : new InputDevice[] { kb };
            AssignCouch(p1, new[] { controllers[0] });
        }
        else
        {
            SetSolo();
        }
    }

    // Player controllers: proper Gamepads first (match the asset's <Gamepad> bindings), else generic Joysticks
    // (DirectInput pads) — those need the <Joystick> bindings applied in AssignCouch to actually drive input.
    private static List<InputDevice> GetControllerDevices()
    {
        var list = new List<InputDevice>();
        foreach (var g in Gamepad.all) list.Add(g);
        if (list.Count == 0)
            foreach (var j in Joystick.all) list.Add(j);
        return list;
    }

    /// <summary>Explicit assignment (e.g. from Character Select): restrict P1 + P2 readers to their devices and
    /// route slot Two to the P2 reader. Null/empty device array = that reader reads all devices.</summary>
    public void AssignCouch(InputDevice[] p1Devices, InputDevice[] p2Devices)
    {
        if (_p2Reader == null)
        {
            Debug.LogError("[CouchDeviceManager] AssignCouch called with no P2 reader — staying solo.", this);
            SetSolo();
            return;
        }

        _p1Reader.SetPairedDevices(p1Devices);
        _p2Reader.SetPairedDevices(p2Devices);
        // A generic Joystick has no <Gamepad> bindings — add <Joystick> ones (while the reader is still disabled,
        // so AddBinding is legal) so it can actually drive P2. Dump its controls so we can map the buttons.
        if (p2Devices != null && p2Devices.Length > 0 && p2Devices[0] is Joystick js)
        {
            DumpDeviceControls(js);
            _p2Reader.ApplyJoystickBindings();
        }
        _p2Reader.enabled = true;               // OnEnable enables the P2 action asset
        _router.SetP2Provider(_p2Reader);
        IsCouchActive = true;

        Debug.Log($"[CouchDeviceManager] Couch ON — P1={Describe(p1Devices)}  P2={Describe(p2Devices)}");
    }

    /// <summary>One device drives both twins: P1 reads all devices, P2 route cleared (falls back to P1), P2 reader off.</summary>
    public void SetSolo()
    {
        _p1Reader.SetPairedDevices(null);       // no restriction
        _router.ClearP2Provider();
        if (_p2Reader != null) _p2Reader.enabled = false;
        IsCouchActive = false;

        Debug.Log("[CouchDeviceManager] Solo — one device drives both twins.");
    }

    private static string Describe(InputDevice[] d) =>
        d == null || d.Length == 0 ? "all" : string.Join("+", d.Select(x => x.displayName));

    // Dumps every connected device with the TYPE the Input System classifies it as (Gamepad / Joystick /
    // Keyboard / …). A controller that shows as 'Joystick' won't match the asset's <Gamepad> bindings — switch
    // it to XInput/gamepad mode so it registers as 'Gamepad', or it needs dedicated <Joystick> bindings.
    private static void LogConnectedDevices()
    {
        var names = InputSystem.devices.Select(d => $"{d.displayName}·{d.GetType().Name}");
        Debug.Log($"[CouchDeviceManager] Connected devices → {string.Join(",  ", names)}");
    }

    // Dumps a device's full control paths — used to discover a generic Joystick's real button/stick control names
    // so we can bind the actions to them (they vary per pad).
    private static void DumpDeviceControls(InputDevice d)
    {
        var paths = d.allControls.Select(c => c.path);
        Debug.Log($"[CouchDeviceManager] '{d.displayName}' controls → {string.Join("  |  ", paths)}");
    }

#if UNITY_EDITOR
    private void Update()
    {
        // Editor + Trainer-only dev toggle: F8 flips solo↔couch (re-runs AssignAuto) for quick two-device testing.
        // New Input System read (Keyboard.current) — not legacy Input.*; still gated behind DevConfig.Trainer.
        if (!DevConfig.Trainer) return;
        var kb = Keyboard.current;
        if (kb != null && kb[Key.F8].wasPressedThisFrame)
        {
            if (IsCouchActive) SetSolo();
            else AssignAuto();
        }

        // Joystick REMAP PROBE — press a physical button and its Unity control name prints, so we can map
        // 'the button you call 1' → 'button2' etc. with zero guessing. Editor + Trainer only.
        foreach (var js in Joystick.all)
            foreach (var ctrl in js.allControls)
                if (ctrl.parent == js && ctrl is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame)
                    Debug.Log($"[JoystickProbe] pressed → {b.name}");
    }
#endif
}
