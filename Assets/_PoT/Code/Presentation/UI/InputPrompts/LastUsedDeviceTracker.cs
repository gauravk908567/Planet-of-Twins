using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// P1.2 (button glyphs) — app-global tracker of which INPUT FAMILY the player last actually used, so shared-UI
/// prompts (one cursor, two possible devices) show the correct glyph and flip LIVE on a device switch — the
/// Overwatch-2 behaviour. Per-occupant gameplay prompts don't need this: their reader's
/// <see cref="TwinInputReader.PairedDeviceKind"/> is fixed in couch. This is the fallback for unrestricted/solo
/// readers and for the single shared cursor.
///
/// <para>A static <see cref="RuntimeInitializeOnLoadMethod"/> service (mirrors the project's
/// <c>UniversalGamepadRegistrar</c>) — NOT a scene MonoBehaviour, so it needs no Persistent-scene wiring and
/// correctly spans the whole app lifetime. <see cref="InputSystem.onEvent"/> is a global static; we subscribe once
/// (idempotent across editor domain reloads). Only <b>Keyboard</b> and <b>Gamepad</b> flip it, and only on a real
/// actuation past a threshold — mouse-move noise and sub-threshold stick drift never cause a spurious switch.</para>
/// </summary>
public static class LastUsedDeviceTracker
{
    // A changed control must actuate past this to count as "used" (ignores stick drift / analog jitter).
    private const float ActuationThreshold = 0.5f;

    public static InputDeviceKind LastUsed { get; private set; } = InputDeviceKind.KeyboardMouse;

    /// <summary>Fires when the last-used family flips (KeyboardMouse ↔ Gamepad). Prompt views subscribe with a
    /// NAMED handler and unsubscribe on destroy (R8) — the static event spans scene loads.</summary>
    public static event Action<InputDeviceKind> OnLastUsedChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        InputSystem.onEvent -= OnEvent;   // idempotent across editor domain reloads (no double-subscribe)
        InputSystem.onEvent += OnEvent;
    }

    private static void OnEvent(InputEventPtr eventPtr, InputDevice device)
    {
        // Only state/delta events carry control actuation.
        if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

        // Deliberate: only Keyboard + Gamepad flip the tracker. Mouse is ignored so passive mouse-move noise
        // can't yank a pad player onto keyboard glyphs (a KBM player presses keys constantly anyway).
        InputDeviceKind kind;
        if (device is Gamepad) kind = InputDeviceKind.Gamepad;
        else if (device is Keyboard) kind = InputDeviceKind.KeyboardMouse;
        else return;

        if (kind == LastUsed) return;

        // Require a genuine actuation past the threshold (ignores drift / idle re-reports / sub-threshold jitter).
        bool actuated = false;
        foreach (var _ in eventPtr.EnumerateChangedControls(device, ActuationThreshold)) { actuated = true; break; }
        if (!actuated) return;

        LastUsed = kind;
        OnLastUsedChanged?.Invoke(kind);
    }
}
