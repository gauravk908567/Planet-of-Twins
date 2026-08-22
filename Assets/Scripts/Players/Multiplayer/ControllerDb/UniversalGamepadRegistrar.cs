using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using HID = UnityEngine.InputSystem.HID.HID;

/// <summary>
/// Couch — Option B, B-2 ("any controller"). Watches for input devices that Unity could only classify as a
/// generic <see cref="Joystick"/> (its HID descriptor is too ambiguous to know which button is "South"), looks
/// them up in <see cref="SdlControllerDb"/>, and — if found — registers a generated <see cref="Gamepad"/>
/// layout (<see cref="HidGamepadLayoutBuilder"/>); the layout's VID/PID matcher makes the Input System recreate
/// the connected device as that Gamepad.
///
/// <para><b>Zero-regression rule:</b> it acts <i>only</i> on devices that resolved to <see cref="Joystick"/>.
/// Xbox (XInput) / PS4 / PS5 pads already come up as <see cref="Gamepad"/> natively and are never touched. The
/// DragonRise pads (VID 0079 / PID 0006) ride this same DB path — the earlier hand-authored <c>DragonRiseGamepadHID</c>
/// override was retired once <c>ControllerDbSelfTest</c> proved this path regenerates its exact offsets, so they
/// are the live proof of the generic system rather than a special case. Once a pad is a Gamepad it flows through
/// the existing couch pipeline unchanged (<c>CouchDeviceManager</c> → <c>Gamepad.all</c>), no per-pad wiring.</para>
///
/// <para><b>Deferred registration (important):</b> <see cref="InputSystem.RegisterLayout(string,string,System.Nullable{InputDeviceMatcher})"/>
/// recreates matching devices, which re-enters <c>onDeviceChange</c>. Registering from inside that callback
/// recurses (a stack overflow). So <c>onDeviceChange</c> only <i>queues</i> the device; the actual registration
/// runs on the next <see cref="InputSystem.onBeforeUpdate"/>, outside any device-change notification.</para>
///
/// <para>Static bootstrap (no scene object, so it needs no serialized refs and survives the Restart→Bootstrap
/// reload): runs before the first scene in both the editor Play mode and player builds.</para>
/// </summary>
public static class UniversalGamepadRegistrar
{
    // (vid<<16)|pid we have already registered a layout for — registration is once per pad model.
    private static readonly HashSet<int> _promoted = new HashSet<int>();
    // Generic Joysticks queued by onDeviceChange, drained on the next onBeforeUpdate (never mutated in the callback).
    private static readonly List<InputDevice> _pending = new List<InputDevice>();
    private static bool _init;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (_init) return;
        _init = true;

        InputSystem.onDeviceChange -= OnDeviceChange;     // named handlers (R8); idempotent across domain reloads
        InputSystem.onDeviceChange += OnDeviceChange;
        InputSystem.onBeforeUpdate -= ProcessPending;
        InputSystem.onBeforeUpdate += ProcessPending;

        // Queue anything already connected — init order vs the Input System's own device enumeration isn't
        // guaranteed, so we both queue now and listen for later Added events.
        foreach (var d in InputSystem.devices) Enqueue(d);
    }

    private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added) Enqueue(device);   // queue only — see the class remarks
    }

    private static void Enqueue(InputDevice device)
    {
        if (device is Gamepad) return;                  // native gamepad, or already promoted — nothing to do
        if (!(device is Joystick)) return;              // keyboard / mouse / other — not a controller
        if (!_pending.Contains(device)) _pending.Add(device);
    }

    // Runs on onBeforeUpdate (outside any device-change notification), so RegisterLayout's device recreation
    // can't re-enter our registration path.
    private static void ProcessPending()
    {
        if (_pending.Count == 0) return;
        var batch = new List<InputDevice>(_pending);   // snapshot + clear: recreation may re-queue via onDeviceChange
        _pending.Clear();
        foreach (var device in batch) TryPromote(device);
    }

    private static void TryPromote(InputDevice device)
    {
        if (!(device is Joystick) || !device.added) return;         // already recreated / gone
        if (!TryVidPid(device, out int vid, out int pid)) return;

        int key = (vid << 16) | (pid & 0xFFFF);
        if (_promoted.Contains(key)) return;                        // layout already registered; matcher handles the rest
        if (!SdlControllerDb.TryGetMapping(vid, pid, out var map)) return;   // unknown pad — leave it a Joystick

        string layoutName = $"DBGamepad_{vid:X4}_{pid:X4}";
        if (!HidGamepadLayoutBuilder.TryBuildJson(device, map, layoutName, out string json))
        {
            Debug.LogWarning($"[UniversalGamepad] '{device.displayName}' (VID {vid:X4} PID {pid:X4}) is in the DB " +
                             "but its HID descriptor could not be mapped — left as a generic Joystick.");
            return;
        }

        _promoted.Add(key);   // guard before RegisterLayout — its recreation re-enters onDeviceChange (queue-only)
        try
        {
            // Registering the layout with a VID/PID matcher recreates every currently-connected matching device
            // (both couch pads) as this Gamepad. No manual RemoveDevice/AddDevice — that was the recursion source.
            InputSystem.RegisterLayout(json, layoutName,
                matches: new InputDeviceMatcher()
                    .WithInterface("HID")
                    .WithCapability("vendorId", vid)
                    .WithCapability("productId", pid));
            Debug.Log($"[UniversalGamepad] Registered a Gamepad layout for VID {vid:X4} PID {pid:X4} " +
                      $"('{device.displayName}') via SDL_GameControllerDB — matching pads are now Gamepads.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UniversalGamepad] Failed to register a Gamepad layout for '{device.displayName}': " +
                           $"{e.Message}\n--- generated layout ---\n{json}");
        }
    }

    private static bool TryVidPid(InputDevice device, out int vid, out int pid)
    {
        vid = pid = 0;
        var caps = device.description.capabilities;
        if (string.IsNullOrEmpty(caps)) return false;
        try
        {
            var hd = HID.HIDDeviceDescriptor.FromJson(caps);
            vid = hd.vendorId;
            pid = hd.productId;
            return vid != 0 && pid != 0;
        }
        catch { return false; }
    }
}
