using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The top bar: Resume (far left) | tab buttons (middle) | Exit (far right). Navigating the bar with
/// stick/D-pad, arrow keys, or LB/RB moves the highlight; landing on a tab shows its panel (via
/// <see cref="SettingsTabButton"/>). Resume/Exit are actioned with Submit or click. Panel visibility
/// and the current index live here; the <see cref="SettingsScreenController"/> owns open/close and
/// the resume/exit actions.
/// </summary>
public sealed class SettingsTabBar : MonoBehaviour
{
    [Serializable]
    public sealed class TabEntry
    {
        public SettingTab tab;
        public Button button;
        public GameObject panel;
    }

    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _exitButton;
    [SerializeField] private List<TabEntry> _tabs = new List<TabEntry>();

    [Header("Shoulder tab-switch hints (Overwatch-style, gamepad-only)")]
    [Tooltip("LB glyph shown to the LEFT of the tab group. Hidden on keyboard/mouse (arrow keys / clicks nav there).")]
    [SerializeField] private TMP_Text _tabHintLeft;
    [Tooltip("RB glyph shown to the RIGHT of the tab group. Hidden on keyboard/mouse.")]
    [SerializeField] private TMP_Text _tabHintRight;

    private SettingsScreenController _screen;
    private int _current = -1;
    // F6 Phase 3 — while ANY player is in Controls edit mode the whole bar is frozen: no shoulder walk, no
    // arrow/stick nav (EventSystem.sendNavigationEvents is also off, set by ControlsRebindView), and Resume/Exit/
    // tabs are non-interactable so a stray mouse click can't leave Controls mid-edit. Unlocked once nobody edits.
    private bool _locked;
    private readonly List<Selectable> _navBuffer = new List<Selectable>(24);
    // The full top-bar order [Resume, tab0…tabN, Exit] — LB/RB walk this so the shoulders reach Resume and Exit,
    // not just the tabs. Built by WireBarNavigation (same order as the explicit Left/Right chain).
    private readonly List<Selectable> _barItems = new List<Selectable>(8);

    // Shared-UI provider for the shoulder glyphs (LB/RB). Menu context — reads the shared/last-used device.
    private IInputProvider _hintInput;

    public Button FirstTabButton => _tabs.Count > 0 ? _tabs[0].button : null;

    private string _resumeText;   // the authored Resume label, restored when not in menu mode

    /// <summary>Main-menu copy of the screen: Resume reads "Back" (it closes the screen) and Exit is hidden (the main
    /// menu has its own quit). Call before <see cref="Initialise"/> so the bar's navigation skips the hidden Exit.</summary>
    public void SetMenuMode(bool menu)
    {
        var label = _resumeButton != null ? _resumeButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null)
        {
            _resumeText ??= label.text;
            label.text = menu ? "Back" : _resumeText;
        }
        if (_exitButton != null) _exitButton.gameObject.SetActive(!menu);
    }

    public void Initialise(SettingsScreenController screen)
    {
        _screen = screen;
        _current = -1;

        if (_resumeButton != null)
        {
            _resumeButton.onClick.RemoveAllListeners();
            _resumeButton.onClick.AddListener(() => _screen.RequestResume());
        }
        if (_exitButton != null)
        {
            _exitButton.onClick.RemoveAllListeners();
            _exitButton.onClick.AddListener(() => _screen.RequestExit());
        }

        for (int i = 0; i < _tabs.Count; i++)
        {
            var entry = _tabs[i];
            if (entry.button == null) continue;
            var tabBtn = entry.button.GetComponent<SettingsTabButton>();
            if (tabBtn == null) tabBtn = entry.button.gameObject.AddComponent<SettingsTabButton>();
            tabBtn.Bind(this, i);
        }

        WireBarNavigation();
        RefreshTabHints();
        Select(0);
    }

    // ── Overwatch-style shoulder tab-switch hints ──────────────────────────────
    // A pad player sees [LB] flanking the tab group on the left and [RB] on the right — the same affordance the
    // skill tree shows ("LB Tabs RB"). Gamepad-gated: on keyboard/mouse the tabs are click/arrow-navigable, so the
    // shoulder glyphs are hidden. Live device-switch keeps them in step with LB/RB shoulder presses.
    private void OnEnable()
    {
        LastUsedDeviceTracker.OnLastUsedChanged += OnDeviceChanged;   // named handler, unsubbed OnDisable (R8)
        RefreshTabHints();
    }

    private void OnDisable() => LastUsedDeviceTracker.OnLastUsedChanged -= OnDeviceChanged;

    private void OnDeviceChanged(InputDeviceKind kind) => RefreshTabHints();

    private void RefreshTabHints()
    {
        if (_tabHintLeft == null && _tabHintRight == null) return;
        if (_hintInput == null) _hintInput = PlayerInputRouter.SharedInput;

        bool pad = LastUsedDeviceTracker.LastUsed == InputDeviceKind.Gamepad;
        if (_tabHintLeft != null)
        {
            _tabHintLeft.gameObject.SetActive(pad);
            if (pad && _hintInput != null) InputGlyphText.Apply(_tabHintLeft, "{TabLeft}", _hintInput);
        }
        if (_tabHintRight != null)
        {
            _tabHintRight.gameObject.SetActive(pad);
            if (pad && _hintInput != null) InputGlyphText.Apply(_tabHintRight, "{TabRight}", _hintInput);
        }
    }

    public void Select(int index)
    {
        if (index < 0 || index >= _tabs.Count || index == _current) return;
        _current = index;
        for (int i = 0; i < _tabs.Count; i++)
            if (_tabs[i].panel != null) _tabs[i].panel.SetActive(i == index);
        WireContentNavigation(index);
        SyncHighlight(index);
    }

    // Keep the highlight (EventSystem selection = the yellow tab outline) in step with the shown panel. LB/RB call
    // Select() directly, so without this the highlight lags a step behind the content (SettingsTabButton.OnSelect
    // only fires on focus-driven changes, not on a shoulder cycle). Idempotent when the tab is already selected
    // (the focus-nav path), and the re-entrant OnSelect → Select(index) returns immediately since _current==index.
    private void SyncHighlight(int index)
    {
        var btn = _tabs[index].button;
        if (btn == null) return;
        var es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != btn.gameObject)
            es.SetSelectedGameObject(btn.gameObject);
    }

    // ── Explicit controller/keyboard navigation ───────────────────────────────
    // The greybox controls default to Automatic navigation, which picks targets by screen
    // geometry — that made Up from a (right-aligned) slider jump to Exit, and left Exit unable
    // to reach the tabs. We wire it explicitly instead: a linear horizontal chain across the
    // whole bar, and a vertical chain inside each panel whose first row returns to its tab.

    // Resume ‹— tab0 ‹—› … ‹—› tabN —› Exit. Left/Right only; the ends stop (no wrap). The same order backs
    // the LB/RB shoulder walk (_barItems), so shoulders and the D-pad/arrow chain agree across the whole bar.
    private void WireBarNavigation()
    {
        _barItems.Clear();
        if (_resumeButton != null && _resumeButton.gameObject.activeSelf) _barItems.Add(_resumeButton);
        foreach (var t in _tabs) if (t.button != null) _barItems.Add(t.button);
        if (_exitButton != null && _exitButton.gameObject.activeSelf) _barItems.Add(_exitButton);   // hidden on the main menu

        for (int i = 0; i < _barItems.Count; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnLeft = i > 0 ? _barItems[i - 1] : null;
            nav.selectOnRight = i < _barItems.Count - 1 ? _barItems[i + 1] : null;
            nav.selectOnUp = null;
            nav.selectOnDown = null;   // tab buttons get Down set per-panel in WireContentNavigation
            _barItems[i].navigation = nav;
        }
    }

    // Down from a tab enters its content; Up from the first content row returns to that tab.
    // Rows chain vertically; Left/Right stay free so sliders/dropdowns consume them as value changes.
    private void WireContentNavigation(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        var entry = _tabs[index];
        if (entry.panel == null) return;

        _navBuffer.Clear();
        foreach (var s in entry.panel.GetComponentsInChildren<Selectable>(includeInactive: false))
            if (s != null && s.IsInteractable() && !(s is Scrollbar)) _navBuffer.Add(s);

        if (entry.button != null)
        {
            var tabNav = entry.button.navigation;
            tabNav.mode = Navigation.Mode.Explicit;
            tabNav.selectOnDown = _navBuffer.Count > 0 ? _navBuffer[0] : null;
            entry.button.navigation = tabNav;
        }

        for (int i = 0; i < _navBuffer.Count; i++)
        {
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = i > 0 ? _navBuffer[i - 1] : (Selectable)entry.button;
            nav.selectOnDown = i < _navBuffer.Count - 1 ? _navBuffer[i + 1] : null;
            nav.selectOnLeft = null;
            nav.selectOnRight = null;
            _navBuffer[i].navigation = nav;
        }
    }

    public void Next() => ShiftBar(+1);
    public void Prev() => ShiftBar(-1);

    // ── CONTROLS two-cursor access (a player's cursor can hop onto the shared bar) ──────────────
    public int BarCount => _barItems.Count;
    public Selectable BarItem(int i) => (i >= 0 && i < _barItems.Count) ? _barItems[i] : null;
    public bool Locked => _locked;

    /// <summary>Where the CONTROLS tab sits in the full bar order [Resume, tabs…, Exit] — the CONTROLS view lands a
    /// hopped-up cursor here, and "Down" from here drops back into that player's column.</summary>
    public int ControlsTabBarIndex
    {
        get
        {
            for (int t = 0; t < _tabs.Count; t++)
                if (_tabs[t].tab == SettingTab.Controls && _tabs[t].button != null)
                {
                    int bi = _barItems.IndexOf(_tabs[t].button);
                    if (bi >= 0) return bi;
                }
            return 0;
        }
    }

    /// <summary>True if the bar item at <paramref name="i"/> is a tab button (not Resume/Exit).</summary>
    public bool IsTabBarItem(int i)
    {
        var item = BarItem(i);
        if (item == null) return false;
        return !ReferenceEquals(item, _resumeButton) && !ReferenceEquals(item, _exitButton);
    }

    /// <summary>Activate a bar item for a player whose cursor is on the bar: Resume / Exit fire the screen actions;
    /// a tab switches to it. No-op while locked (a player is editing).</summary>
    public void ActivateBarItem(int i)
    {
        if (_locked) return;
        var item = BarItem(i);
        if (item == null || _screen == null) return;
        if (ReferenceEquals(item, _resumeButton)) { _screen.RequestResume(); return; }
        if (ReferenceEquals(item, _exitButton)) { _screen.RequestExit(); return; }
        for (int t = 0; t < _tabs.Count; t++)
            if (ReferenceEquals(_tabs[t].button, item)) { Select(t); return; }
    }

    // F6 Phase 3 — lock/unlock the whole bar while a player is rebinding on the Controls tab. Locks the shoulder
    // walk (ShiftBar guard) and makes Resume/Exit + every tab button non-interactable so nothing (pad, arrow, or
    // mouse click) can leave Controls mid-edit; the active Controls tab stays shown. Idempotent.
    public void SetLocked(bool locked)
    {
        _locked = locked;
        if (_resumeButton != null) _resumeButton.interactable = !locked;
        if (_exitButton != null) _exitButton.interactable = !locked;
        foreach (var t in _tabs)
            if (t.button != null) t.button.interactable = !locked;
    }

    // Move the highlight one step along the WHOLE bar (Resume ‹—› tabs ‹—› Exit) so LB/RB reach Resume and Exit,
    // not just the tabs. Landing on a tab shows its panel (its OnSelect fires Select); Resume/Exit just highlight.
    // Ends stop (no wrap), matching the D-pad/arrow chain.
    private void ShiftBar(int dir)
    {
        if (_locked || _barItems.Count == 0) return;
        int idx = Mathf.Clamp(CurrentBarIndex() + dir, 0, _barItems.Count - 1);
        var target = _barItems[idx];
        if (target != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target.gameObject);
    }

    // Where the highlight sits on the bar right now: the focused bar item, else the active tab's slot (when focus
    // has dropped into the panel content), else the start of the bar.
    private int CurrentBarIndex()
    {
        var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (sel != null)
            for (int i = 0; i < _barItems.Count; i++)
                if (_barItems[i] != null && _barItems[i].gameObject == sel) return i;

        if (_current >= 0 && _current < _tabs.Count && _tabs[_current].button != null)
            for (int i = 0; i < _barItems.Count; i++)
                if (ReferenceEquals(_barItems[i], _tabs[_current].button)) return i;

        return 0;
    }

    // LB/RB shoulder walk along the whole bar. This is the pause overlay (menu context), so it reads the New Input
    // System device directly; the IInputProvider/tutorial-gate rule governs GAMEPLAY input, not menus.
    private void Update()
    {
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad == null) return;
        if (pad.rightShoulder.wasPressedThisFrame) ShiftBar(+1);
        else if (pad.leftShoulder.wasPressedThisFrame) ShiftBar(-1);
    }
}
