/// <summary>
/// Item 5 (button glyphs) — the physical input family a prompt/glyph should represent.
///
/// Only TWO kinds by design: our universal-controller layer (SDL_GameControllerDB) normalises EVERY pad to
/// the standard Unity <c>&lt;Gamepad&gt;</c> layout, so one <see cref="Gamepad"/> kind covers all pads —
/// glyphs key on the control PATH (e.g. <c>buttonSouth</c>), never the raw HID or the brand. This mirrors
/// Overwatch 2, which shows one Xbox-style set for every controller and does not detect PS/Nintendo. Mouse
/// rides with the keyboard for prompt purposes (a keyboard-family player is "keyboard &amp; mouse").
///
/// <para>Future enhancement (parked): our DB carries each pad's VID/PID, so real PS/Nintendo glyph swapping
/// is possible later without the PC brand-detection problem OW2 cites — it would add glyph SETS, not kinds.</para>
/// </summary>
public enum InputDeviceKind
{
    KeyboardMouse,
    Gamepad,
}
