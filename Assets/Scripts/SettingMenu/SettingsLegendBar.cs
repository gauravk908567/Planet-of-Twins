using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Phase 1 of the F6 rebinding/legend work — the bottom legend bar of the unified settings screen. Shows the
/// four navigation affordances (Move / Switch Tab / Select / Back) as device-aware hints that flip
/// keyboard↔gamepad LIVE (Overwatch-2 style) via <see cref="LastUsedDeviceTracker"/>, plus an always-on
/// "who last moved" tag (P1/P2 + device family) so couch players can see which of them is driving the shared
/// menu cursor.
///
/// <para>Greybox by intent: each hint renders as a bright keycap TEXT that swaps by device family, with an
/// optional resolved glyph SPRITE layered in for single-control actions (Select) when the
/// <see cref="InputGlyphMap"/> has one — same icon-or-text fallback as <see cref="InputPromptView"/>. Directional
/// / paired actions (Move = stick/WASD, Switch Tab = shoulders/◄►) have no single control path, so they stay
/// text. A theme pass swaps the text chips for the full keycap atlas later; nothing here bakes visual polish.</para>
///
/// Lives under ScreenRoot (toggled with the screen), so <see cref="OnEnable"/> refreshes it every time the
/// screen opens; the device-switch event keeps it live while open. No cross-scene refs (R2), no new singleton.
/// </summary>
public sealed class SettingsLegendBar : MonoBehaviour
{
    public enum LegendAction { Move, SwitchTab, Select, Back, Delete }

    [Serializable]
    public sealed class Item
    {
        public LegendAction action;
        [Tooltip("Optional glyph slot — shown (text hidden) only when a sprite resolves for a single-control action.")]
        public Image icon;
        [Tooltip("Keycap text fallback ('Esc', 'A', 'WASD / Arrows'…) — swapped by device family each refresh.")]
        public TMP_Text keyText;
        [Tooltip("Caption ('Move', 'Select'…).")]
        public TMP_Text label;
    }

    [SerializeField] private List<Item> _items = new List<Item>();
    [Tooltip("Always-on 'who last moved' tag: P1/P2 (when resolvable) + device family.")]
    [SerializeField] private TMP_Text _deviceTag;

    // Interface-typed (R4/SOLID); resolved lazily like InputPromptView (SharedInput may not exist in Awake).
    private IInputProvider _input;

    private void OnEnable()
    {
        LastUsedDeviceTracker.OnLastUsedChanged += OnDeviceSwitched;   // named handler, unsubbed OnDisable (R8)
        Refresh();
    }

    private void OnDisable() => LastUsedDeviceTracker.OnLastUsedChanged -= OnDeviceSwitched;

    private void OnDeviceSwitched(InputDeviceKind kind) => Refresh();

    /// <summary>Re-resolve every hint + the tag for the current device family. Public so the screen controller
    /// can force a refresh on open; also called on the live device-switch event.</summary>
    public void Refresh()
    {
        if (_input == null) _input = PlayerInputRouter.SharedInput;

        InputDeviceKind kind = LastUsedDeviceTracker.LastUsed;
        bool pad = kind == InputDeviceKind.Gamepad;

        foreach (var it in _items)
        {
            if (it == null) continue;
            GetHint(it.action, out string glyphAction, out string kbText, out string padText, out string caption);

            // Try a real glyph only for single-control actions (glyphAction != null); Move/Switch-tab are paired.
            Sprite sprite = null;
            if (glyphAction != null && _input != null)
                sprite = InputGlyphResolver.ResolveSprite(_input, glyphAction, out _);

            bool useSprite = sprite != null && it.icon != null;
            if (it.icon != null)
            {
                it.icon.gameObject.SetActive(useSprite);
                if (useSprite) it.icon.sprite = sprite;
            }
            if (it.keyText != null)
            {
                it.keyText.gameObject.SetActive(!useSprite);
                if (!useSprite) it.keyText.text = pad ? padText : kbText;
            }
            if (it.label != null) it.label.text = caption;
        }

        if (_deviceTag != null) _deviceTag.text = BuildTag(kind);
    }

    // Per-action hint table: the glyph action name (null = paired/directional → text only), the keyboard text,
    // the gamepad text, and the caption. Menu nav is fixed (not rebindable), so these representatives are stable.
    private static void GetHint(LegendAction a, out string glyphAction, out string kb, out string pad, out string caption)
    {
        switch (a)
        {
            case LegendAction.Move:      glyphAction = null;     kb = "WASD / Arrows"; pad = "L-Stick";  caption = "Move"; break;
            case LegendAction.SwitchTab: glyphAction = null;     kb = "◄  ►"; pad = "LB / RB";  caption = "Switch Tab"; break;
            case LegendAction.Select:    glyphAction = "Submit"; kb = "Enter";         pad = "A";        caption = "Select"; break;
            case LegendAction.Back:      glyphAction = null;     kb = "Esc";           pad = "B";        caption = "Back"; break;
            case LegendAction.Delete:    glyphAction = "UIDelete"; kb = "Del";         pad = "X";        caption = "Delete"; break;
            default:                     glyphAction = null;     kb = pad = "?";       caption = string.Empty; break;
        }
    }

    // "P1  ·  Keyboard & Mouse". P1/P2 is resolved by matching the last-used family to a slot's paired device
    // — correct for the common couch config (keyboard + one pad). Ambiguous (both pads) or solo → family only.
    private static string BuildTag(InputDeviceKind kind)
    {
        string family = kind == InputDeviceKind.Gamepad ? "Gamepad" : "Keyboard & Mouse";
        string slot = ResolveSlotLabel(kind);
        return slot != null ? slot + "  ·  " + family : family;
    }

    private static string ResolveSlotLabel(InputDeviceKind kind)
    {
        var router = PlayerInputRouter.Instance;
        if (router == null) return null;

        var p1 = router.ProviderForSlot(PlayerSlot.One);
        var p2 = router.ProviderForSlot(PlayerSlot.Two);

        // Solo / single-device: P2 falls back to P1's instance → only one player is present.
        if (ReferenceEquals(p1, p2)) return "P1";

        bool p1Match = p1 != null && p1.PairedDeviceKind == kind;
        bool p2Match = p2 != null && p2.PairedDeviceKind == kind;
        if (p1Match && !p2Match) return "P1";
        if (p2Match && !p1Match) return "P2";
        return null;   // both on the same family (e.g. two pads) — can't disambiguate, show family alone
    }
}
