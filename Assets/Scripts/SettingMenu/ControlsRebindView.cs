using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// F6 — the CONTROLS tab's two-column keybinding view (P1 | P2) with two INDEPENDENT, zone-aware player cursors.
/// Each player owns exactly one tagged cursor (P1 / P2) driven only by their own device. A cursor lives in that
/// player's column and can hop up to the shared top bar and back:
/// <list type="bullet">
/// <item>In a column: Up/Down move between the Edit/Restore buttons and the action rows; Left/Right toggle
/// Edit↔Restore. Submit → Edit enters that column's edit mode, Restore restores, a row rebinds (while editing).</item>
/// <item>Up from the Edit/Restore row hops the cursor to the shared bar (landing on the CONTROLS tab). On the bar,
/// Left/Right move Resume↔tabs↔Exit and Submit activates; Down from the CONTROLS tab drops back into the column.</item>
/// </list>
/// Unity's EventSystem is single-selection, so both cursors are drawn MANUALLY (per-player coloured highlight
/// overlays with a P1/P2 tag) and EventSystem keyboard/pad navigation is suspended while this tab is shown (mouse
/// clicks still work). While ANY player is editing, tab-switch / Resume / Exit are locked. Per-player device
/// isolation for rebinds + mid-edit disconnect handling live in <see cref="TwinInputReader"/> and here. Rebinds
/// apply live, in-session only (persistence deferred).
/// </summary>
public sealed class ControlsRebindView : MonoBehaviour
{
    // One rebindable row. Simple actions map a single device-family binding; the keyboard Move (a 2DVector composite)
    // is EXPANDED into four directional part-rows (up/down/left/right → WASD parts) so movement can be rebound
    // key-by-key. The pad's Move (leftStick) has no directional parts → it stays one read-only row. "Switch Twin" is
    // intentionally NOT listed (couch has no twin-switch). Rows are built PER COLUMN from that column's device kind.
    private struct RowSpec
    {
        public string action;    // asset action name (Gameplay map)
        public string label;     // display label
        public string part;      // composite part ("up"/"down"/"left"/"right"); null = the single binding
        public bool rebindable;  // false → read-only (e.g. the gamepad Move stick)
        public string display;   // fixed binding text shown verbatim (read-only rows); null → resolve glyph/part live
    }

    private static void BuildRowSpecs(List<RowSpec> into, InputDeviceKind kind)
    {
        into.Clear();
        if (kind == InputDeviceKind.Gamepad)
            // Analog stick can't be rebound per-direction (user call: keep one row); show it plainly.
            into.Add(new RowSpec { action = "Move", label = "Move", part = null, rebindable = false, display = "Left Stick / D-Pad" });
        else
        {
            into.Add(new RowSpec { action = "Move", label = "Move Up",    part = "up",    rebindable = true });
            into.Add(new RowSpec { action = "Move", label = "Move Down",  part = "down",  rebindable = true });
            into.Add(new RowSpec { action = "Move", label = "Move Left",  part = "left",  rebindable = true });
            into.Add(new RowSpec { action = "Move", label = "Move Right", part = "right", rebindable = true });
        }
        into.Add(new RowSpec { action = "Attack",      label = "Attack",            part = null, rebindable = true });
        into.Add(new RowSpec { action = "Ability",     label = "Ability",           part = null, rebindable = true });
        into.Add(new RowSpec { action = "Teleport",    label = "Teleport",          part = null, rebindable = true });
        into.Add(new RowSpec { action = "Interact",    label = "Interact / Rescue", part = null, rebindable = true });
        into.Add(new RowSpec { action = "Empower",     label = "Empower",           part = null, rebindable = true });
        into.Add(new RowSpec { action = "Convergence", label = "Soul Convergence",  part = null, rebindable = true });
        into.Add(new RowSpec { action = "Overview",    label = "Overview Cam",      part = null, rebindable = true });
    }

    // A column's device family. Couch default: P1 keyboard, P2 gamepad (used when the slot's provider is unrestricted
    // or unpaired). Assignment is frozen while the settings screen is open, so a column's kind is stable per session.
    private static InputDeviceKind ColumnKind(Cursor c)
    {
        var p = PlayerInputRouter.ForSlot(c.slot);
        return p?.PairedDeviceKind ?? (c.slot == PlayerSlot.One ? InputDeviceKind.KeyboardMouse : InputDeviceKind.Gamepad);
    }

    private static bool IsRebindable(Cursor c, int row) => row >= 0 && row < c.rows.Count && c.rows[row].rebindable;

    private static int FirstRebindableRow(Cursor c)
    {
        for (int i = 0; i < c.rows.Count; i++) if (c.rows[i].rebindable) return i;
        return 0;
    }

    // Column item indices: 0 = Edit, 1 = Restore, 2.. = rows (row = colIndex - RowBase).
    private const int ColEdit = 0;
    private const int ColRestore = 1;
    private const int RowBase = 2;

    [Header("Columns")]
    [SerializeField] private CanvasGroup _columnP1Group;
    [SerializeField] private CanvasGroup _columnP2Group;
    [SerializeField] private Transform _rowsP1;
    [SerializeField] private Transform _rowsP2;
    [SerializeField] private TMP_Text _deviceP1;
    [SerializeField] private TMP_Text _deviceP2;

    [Header("Per-column buttons")]
    [SerializeField] private Button _restoreP1;
    [SerializeField] private Button _restoreP2;
    [SerializeField] private Button _editP1;
    [SerializeField] private Button _editP2;

    [Header("Live-save legend (shown while any column is editing)")]
    [SerializeField] private TMP_Text _savedLiveLegend;

    [Header("Greybox colours")]
    [SerializeField] private Color _textColor    = new Color(0.92f, 0.92f, 0.95f, 1f);
    [SerializeField] private Color _bindingColor = new Color(1f, 0.86f, 0.45f, 1f);
    [SerializeField] private Color _subColor     = new Color(0.65f, 0.68f, 0.78f, 1f);
    [SerializeField] private Color _disconnectedColor = new Color(0.95f, 0.5f, 0.45f, 1f);
    [SerializeField] private Color _p1CursorColor = new Color(0.35f, 0.8f, 1f, 1f);   // P1 = cyan
    [SerializeField] private Color _p2CursorColor = new Color(1f, 0.72f, 0.28f, 1f);  // P2 = amber
    [SerializeField] private Color _captureColor  = new Color(0.55f, 1f, 0.6f, 1f);   // rebind-capture highlight

    private enum Zone { Column, Bar }

    private sealed class Cursor
    {
        public PlayerSlot slot;
        public string tag;                 // "P1" / "P2"
        public Color color;
        public CanvasGroup group;
        public Transform rowsParent;
        public TMP_Text device;
        public Button edit;
        public Button restore;
        public readonly List<TMP_Text> binds = new List<TMP_Text>();
        public readonly List<TMP_Text> rowNames = new List<TMP_Text>();
        public readonly List<RectTransform> rowRects = new List<RectTransform>();
        public readonly List<RowSpec> rows = new List<RowSpec>();
        public readonly HashSet<int> conflictRows = new HashSet<int>();   // rows whose binding duplicates another row's
        public InputDeviceKind builtKind;   // device family the current rows were built for (rebuild on change)

        public bool connected;             // distinct provider AND its device is live
        public bool wasLive;
        public bool everJoined;

        public Zone zone = Zone.Column;
        public int colIndex = ColEdit;
        public int barIndex;

        public bool editing;
        public bool capturing;
        public int captureRow = -1;
        public bool ignoreSubmitUntilRelease;
        public bool submitPending;
        public int submitPendingRow = -1;

        public bool navPrimed = true;
        public float lastStepTime;

        public RectTransform overlay;      // per-player highlight rect (tracks the focused element)
        public Image overlayImg;
    }

    private Cursor _c1, _c2;
    private bool _overlaysBuilt;
    private string _legendDefaultText;                                   // the authored "changes saved live" legend text
    private Color _legendDefaultColor = new Color(0.65f, 0.68f, 0.78f, 1f);
    private static readonly Color ConflictColor = new Color(1f, 0.42f, 0.38f, 1f);   // duplicate-binding highlight (red)

    // Two-column bottom device legend (CONTROLS-only). Built at runtime under this panel (same pattern as the rows/
    // overlays). Because CONTROLS shows two device columns, the bottom legend splits per player — P1's hints in its
    // column's device language, P2's in its own — driven by each column's PairedDeviceKind (NOT the shared last-used
    // flip). While this tab is up the shared single legend is hidden and this one takes its place.
    private RectTransform _legendRoot;
    private CanvasGroup _legendP1Group, _legendP2Group;
    private bool _legendBuilt;
    private InputDeviceKind _legendKindP1, _legendKindP2;
    private UILegendBar _sharedLegend;

    private SettingsTabBar TabBar => SettingsScreenController.Instance != null ? SettingsScreenController.Instance.TabBar : null;

    private void Awake()
    {
        _c1 = new Cursor { slot = PlayerSlot.One, tag = "P1", color = _p1CursorColor,
                           group = _columnP1Group, rowsParent = _rowsP1, device = _deviceP1, edit = _editP1, restore = _restoreP1 };
        _c2 = new Cursor { slot = PlayerSlot.Two, tag = "P2", color = _p2CursorColor,
                           group = _columnP2Group, rowsParent = _rowsP2, device = _deviceP2, edit = _editP2, restore = _restoreP2 };

        if (_restoreP1 != null) { _restoreP1.onClick.RemoveAllListeners(); _restoreP1.onClick.AddListener(() => RestoreSlot(PlayerSlot.One)); }
        if (_restoreP2 != null) { _restoreP2.onClick.RemoveAllListeners(); _restoreP2.onClick.AddListener(() => RestoreSlot(PlayerSlot.Two)); }
        if (_editP1 != null) { _editP1.onClick.RemoveAllListeners(); _editP1.onClick.AddListener(() => EnterEdit(PlayerSlot.One)); _editP1.interactable = true; }
        if (_editP2 != null) { _editP2.onClick.RemoveAllListeners(); _editP2.onClick.AddListener(() => EnterEdit(PlayerSlot.Two)); _editP2.interactable = true; }

        if (_savedLiveLegend != null)
        {
            _legendDefaultText = _savedLiveLegend.text;      // remember authored text so the conflict warning can restore it
            _legendDefaultColor = _savedLiveLegend.color;
            _savedLiveLegend.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        LastUsedDeviceTracker.OnLastUsedChanged += OnDeviceChanged;   // named handler, unsubbed OnDisable (R8)
        EnsureRows();
        // This tab drives navigation manually for two cursors → suspend the single-selection EventSystem nav so it
        // can't fight the per-player cursors (mouse clicks still work). Restored in OnDisable.
        var es = EventSystem.current;
        if (es != null) es.sendNavigationEvents = false;

        // CONTROLS carries its own two-column, per-device legend bar; hide the shared single legend while it's shown,
        // and (since our bar lives under ScreenRoot, not this panel) raise + show ours.
        var shared = SharedLegend;
        if (shared != null) shared.gameObject.SetActive(false);
        EnsureDeviceLegend();
        if (_legendRoot != null) { _legendRoot.gameObject.SetActive(true); _legendRoot.SetAsLastSibling(); }

        ResetCursor(_c1);
        ResetCursor(_c2);
        Refresh();
    }

    private void OnDisable()
    {
        LastUsedDeviceTracker.OnLastUsedChanged -= OnDeviceChanged;
        // Tear down any live edit + unlock, WITHOUT touching EventSystem selection (doing so during a tab-switch
        // selection callback throws "select … while already selecting"). Just restore nav + hide overlays.
        ForceExitAll();
        var es = EventSystem.current;
        if (es != null) es.sendNavigationEvents = true;
        if (_c1 != null && _c1.overlay != null) _c1.overlay.gameObject.SetActive(false);
        if (_c2 != null && _c2.overlay != null) _c2.overlay.gameObject.SetActive(false);

        // Leaving CONTROLS (tab switch or screen close): hide our bar (it lives under ScreenRoot, so it won't hide
        // with this panel) and restore the shared single legend for the other tabs.
        if (_legendRoot != null) _legendRoot.gameObject.SetActive(false);
        var shared = SharedLegend;
        if (shared != null) shared.gameObject.SetActive(true);
    }

    private void OnDeviceChanged(InputDeviceKind kind)
    {
        if (!AnyEditing) Refresh();   // don't stomp a live capture's row text with a full repaint
    }

    private void ResetCursor(Cursor c)
    {
        c.zone = Zone.Column;
        c.colIndex = ColEdit;
        c.editing = false;
        c.capturing = false;
        c.submitPending = false;
        c.ignoreSubmitUntilRelease = false;
        c.navPrimed = true;
    }

    // ── Display refresh ──────────────────────────────────────────────────────
    public void Refresh()
    {
        EnsureRows();
        RefreshColumn(_c1);
        RefreshColumn(_c2);
    }

    // Paint ONE column from live state (bindings, device label, connected/grey, edit-enable). "connected" = distinct
    // provider AND its device is live. A slot that HAD a device but lost it reads "Disconnected"; never-joined reads
    // "Not connected". No reshuffle here (CouchDeviceManager is suspended while the screen is open).
    private void RefreshColumn(Cursor c)
    {
        EnsureColumnRows(c);   // rebuild this column's rows if its device kind changed since last build
        var p1 = PlayerInputRouter.ForSlot(PlayerSlot.One);
        var provider = PlayerInputRouter.ForSlot(c.slot);
        bool distinct = c.slot == PlayerSlot.One ? provider != null
                                                 : (provider != null && !ReferenceEquals(provider, p1));
        bool live = distinct && provider.HasLivePairedDevice();
        c.connected = live;
        c.wasLive = live;
        if (live) c.everJoined = true;

        if (live)
        {
            InputDeviceKind kind = provider.PairedDeviceKind ?? (c.slot == PlayerSlot.One ? InputDeviceKind.KeyboardMouse : InputDeviceKind.Gamepad);
            if (c.device != null) { c.device.text = DeviceLabel(kind); c.device.color = _subColor; }
            FillColumn(c, provider, kind, active: true);
            DetectConflicts(c, provider, kind);   // duplicate bindings within this column → conflictRows
            SetColumnActive(c.group, true);
            if (c.edit != null) c.edit.interactable = true;
        }
        else
        {
            bool disconnected = c.slot == PlayerSlot.One || c.everJoined || distinct;
            if (c.device != null)
            {
                c.device.text = disconnected ? "Disconnected" : "Not connected";
                c.device.color = disconnected ? _disconnectedColor : _subColor;
            }
            FillColumn(c, null, InputDeviceKind.Gamepad, active: false);
            c.conflictRows.Clear();
            SetColumnActive(c.group, false);
            if (c.edit != null) c.edit.interactable = false;
        }

        ApplyConflictStyling(c);
        RefreshLegend();
        RefreshDeviceLegend();   // grey/ungrey the bottom per-player legend to match this column's connected state
    }

    // ── Duplicate-binding (conflict) detection ───────────────────────────────
    // A conflict = two ROWS in the SAME column (same device family) resolving to the same control path (e.g. Attack
    // and Ability both on pad-B, or Move Up and Move Down both on "s"). Both rows get flagged; the player resolves it
    // by rebinding one to a free control. Cross-column never conflicts (different device / different asset).
    private void DetectConflicts(Cursor c, IInputProvider provider, InputDeviceKind kind)
    {
        c.conflictRows.Clear();
        if (provider == null) return;

        // Group rows by their effective control path.
        var groups = new Dictionary<string, List<int>>();
        for (int i = 0; i < c.rows.Count; i++)
        {
            string path = provider.GetEffectiveBindingPath(c.rows[i].action, c.rows[i].part, kind);
            if (string.IsNullOrEmpty(path)) continue;
            if (!groups.TryGetValue(path, out var list)) { list = new List<int>(4); groups[path] = list; }
            list.Add(i);
        }

        // A group of ≥2 rows is a duplicate. GRANDFATHER duplicates that ship in the defaults (Interact + Soul
        // Convergence both on F/A by design): flag only when at least one row in the group was CHANGED by the player —
        // that's a user-introduced conflict to resolve.
        foreach (var kv in groups)
        {
            if (kv.Value.Count < 2) continue;
            bool anyChanged = false;
            for (int j = 0; j < kv.Value.Count; j++)
                if (provider.IsRowBindingChanged(c.rows[kv.Value[j]].action, c.rows[kv.Value[j]].part, kind)) { anyChanged = true; break; }
            if (!anyChanged) continue;
            for (int j = 0; j < kv.Value.Count; j++) c.conflictRows.Add(kv.Value[j]);
        }
    }

    // Paint conflicting rows red (name + binding); everything else back to its normal colour. Persistent — a flagged
    // conflict stays red even after the player leaves edit mode, until they fix it.
    private void ApplyConflictStyling(Cursor c)
    {
        for (int i = 0; i < c.binds.Count; i++)
        {
            bool conf = c.conflictRows.Contains(i);
            if (c.binds[i] != null) c.binds[i].color = conf ? ConflictColor : _bindingColor;
            if (i < c.rowNames.Count && c.rowNames[i] != null) c.rowNames[i].color = conf ? ConflictColor : _textColor;
        }
    }

    // The shared legend doubles as the conflict banner: a red "resolve the conflict" line while EITHER column has a
    // duplicate, otherwise the authored "changes saved live" text.
    private void RefreshLegend()
    {
        if (_savedLiveLegend == null) return;
        bool conflict = (_c1 != null && _c1.conflictRows.Count > 0) || (_c2 != null && _c2.conflictRows.Count > 0);
        bool show = AnyEditing || conflict;   // visible while editing OR while any conflict remains unresolved
        _savedLiveLegend.gameObject.SetActive(show);
        if (!show) return;
        if (conflict)
        {
            _savedLiveLegend.text = "⚠ Binding conflict — rebind one of the highlighted rows to a free control.";
            _savedLiveLegend.color = ConflictColor;
        }
        else
        {
            _savedLiveLegend.text = _legendDefaultText;
            _savedLiveLegend.color = _legendDefaultColor;
        }
    }

    // ── Per-frame two-cursor input ───────────────────────────────────────────
    private void Update()
    {
        if (!Application.isPlaying) return;
        TickCursor(_c1);
        if (!isActiveAndEnabled) return;   // a bar activation may have switched tabs / closed the screen
        TickCursor(_c2);
        if (!isActiveAndEnabled) return;
        UpdateOverlay(_c1);
        UpdateOverlay(_c2);
    }

    private void TickCursor(Cursor c)
    {
        if (c == null) return;
        var provider = PlayerInputRouter.ForSlot(c.slot);
        if (c.slot == PlayerSlot.Two && ReferenceEquals(provider, PlayerInputRouter.ForSlot(PlayerSlot.One))) provider = null;
        bool live = provider != null && provider.HasLivePairedDevice();

        if (live != c.wasLive)
        {
            if (!live && c.editing) ExitEdit(c.slot);
            else RefreshColumn(c);
            if (live) ResetCursor(c);            // reconnected → back to a sane spot
        }
        if (!live) return;

        // ── capture in flight: the rebind op owns input; it ends via callback (or Back → HandleBack) ──
        if (c.capturing) return;

        // ── swallow the Submit that entered edit until released (can't self-capture / can't insta-rebind row 0) ──
        if (c.ignoreSubmitUntilRelease)
        {
            if (!provider.GetUISubmitHeld()) c.ignoreSubmitUntilRelease = false;
            return;
        }

        // ── pending capture: wait for Submit release, then start listening ──
        if (c.submitPending)
        {
            if (!provider.GetUISubmitHeld()) BeginCapture(c, provider, c.submitPendingRow);
            return;
        }

        if (TryStep(c, provider, out int dx, out int dy)) MoveCursor(c, dx, dy);

        if (provider.GetUISubmitDown()) Submit(c);
    }

    // ── Navigation ───────────────────────────────────────────────────────────
    private bool TryStep(Cursor c, IInputProvider p, out int dx, out int dy)
    {
        dx = 0; dy = 0;
        Vector2 v = p.GetUINavigate();
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y);
        float mag = Mathf.Max(ax, ay);
        float now = Time.unscaledTime;

        if (mag < 0.35f) { c.navPrimed = true; return false; }
        if (mag < 0.5f) return false;
        if (!c.navPrimed && now - c.lastStepTime < 0.18f) return false;

        c.navPrimed = false;
        c.lastStepTime = now;
        if (ax >= ay) dx = v.x > 0f ? 1 : -1;
        else          dy = v.y > 0f ? 1 : -1;
        return true;
    }

    private void MoveCursor(Cursor c, int dx, int dy)
    {
        int lastCol = RowBase + c.binds.Count - 1;

        if (c.zone == Zone.Bar)
        {
            var bar = TabBar;
            int barMax = bar != null ? bar.BarCount - 1 : 0;
            if (dx > 0) c.barIndex = Mathf.Min(barMax, c.barIndex + 1);
            else if (dx < 0) c.barIndex = Mathf.Max(0, c.barIndex - 1);
            else if (dy < 0)   // down: drop into the column only from the CONTROLS tab
            {
                if (bar != null && c.barIndex == bar.ControlsTabBarIndex) { c.zone = Zone.Column; c.colIndex = ColEdit; }
            }
            return;
        }

        // Zone.Column
        if (c.editing)
        {
            // Confined to rows while editing (can't leave mid-edit).
            if (dy > 0) c.colIndex = Mathf.Max(RowBase, c.colIndex - 1);
            else if (dy < 0) c.colIndex = Mathf.Min(lastCol, c.colIndex + 1);
            return;
        }

        if (dy > 0)   // up
        {
            if (c.colIndex >= RowBase + 1) c.colIndex--;
            else if (c.colIndex == RowBase) c.colIndex = ColEdit;
            else                            HopToBar(c);   // from Edit/Restore
        }
        else if (dy < 0)  // down
        {
            if (c.colIndex <= ColRestore) c.colIndex = RowBase;
            else c.colIndex = Mathf.Min(lastCol, c.colIndex + 1);
        }
        else if (dx > 0)  { if (c.colIndex == ColEdit) c.colIndex = ColRestore; }
        else if (dx < 0)  { if (c.colIndex == ColRestore) c.colIndex = ColEdit; }
    }

    private void HopToBar(Cursor c)
    {
        var bar = TabBar;
        c.zone = Zone.Bar;
        c.barIndex = bar != null ? bar.ControlsTabBarIndex : 0;
    }

    // ── Submit ───────────────────────────────────────────────────────────────
    private void Submit(Cursor c)
    {
        if (c.zone == Zone.Bar)
        {
            TabBar?.ActivateBarItem(c.barIndex);   // no-op while locked (someone editing); may switch tab / close screen
            return;
        }

        if (c.editing)
        {
            if (c.colIndex >= RowBase && IsRebindable(c, c.colIndex - RowBase))
            {
                c.submitPending = true;
                c.submitPendingRow = c.colIndex - RowBase;
            }
            return;
        }

        if (c.colIndex == ColEdit) EnterEdit(c.slot);
        else if (c.colIndex == ColRestore) RestoreSlot(c.slot);
        // Submit on a row while not editing: no-op (press Edit first).
    }

    // ── Interactive rebind (per-player, device-isolated in TwinInputReader) ──
    private void BeginCapture(Cursor c, IInputProvider provider, int row)
    {
        c.submitPending = false;
        c.submitPendingRow = -1;
        if (row < 0 || row >= c.rows.Count || !c.rows[row].rebindable) return;

        var spec = c.rows[row];
        bool started = provider.StartInteractiveRebind(spec.action, spec.part, () => OnRebindDone(c));
        if (!started) return;

        c.capturing = true;
        c.captureRow = row;
        if (row < c.binds.Count && c.binds[row] != null)
            c.binds[row].text = provider.PairedDeviceKind == InputDeviceKind.Gamepad ? "Press a button…" : "Press a key…";
    }

    private void OnRebindDone(Cursor c)
    {
        c.capturing = false;
        c.captureRow = -1;
        RefreshColumn(c);
        RefreshExternalPrompts();
    }

    // ── Enter / exit edit (per-player, independent) ──────────────────────────
    public bool AnyEditing => (_c1 != null && _c1.editing) || (_c2 != null && _c2.editing);

    private Cursor Col(PlayerSlot slot) => slot == PlayerSlot.One ? _c1 : _c2;

    private void EnterEdit(PlayerSlot slot)
    {
        var c = Col(slot);
        if (c == null || c.editing || !c.connected) return;

        bool wasAnyEditing = AnyEditing;
        c.editing = true;
        c.capturing = false;
        c.submitPending = false;
        c.ignoreSubmitUntilRelease = true;    // don't let the entering Submit also fire a rebind
        c.zone = Zone.Column;
        c.colIndex = RowBase + FirstRebindableRow(c);
        c.navPrimed = true;
        c.lastStepTime = 0f;

        if (!wasAnyEditing) EnterEditLock();
    }

    /// <summary>Leave THIS player's edit mode (his column returns to the idle read state). Cancels any capture in
    /// flight. Independent — never touches the other column. Once nobody is editing, the bar unlocks.</summary>
    public void ExitEdit(PlayerSlot slot)
    {
        var c = Col(slot);
        if (c == null || !c.editing) return;

        // Cancel THIS player's capture only. On a mid-edit disconnect ForSlot(Two) falls back to P1 — guard so we
        // don't cancel P1's op (the disconnected reader already disposed its own).
        var provider = PlayerInputRouter.ForSlot(slot);
        if (slot == PlayerSlot.Two && ReferenceEquals(provider, PlayerInputRouter.ForSlot(PlayerSlot.One))) provider = null;
        provider?.CancelActiveRebind();

        c.editing = false;
        c.capturing = false;
        c.captureRow = -1;
        c.submitPending = false;
        c.colIndex = ColEdit;

        RefreshColumn(c);
        if (!AnyEditing) ExitEditLock();
    }

    private void ForceExitAll()
    {
        if (_c1 != null && _c1.editing) ExitEdit(PlayerSlot.One);
        if (_c2 != null && _c2.editing) ExitEdit(PlayerSlot.Two);
        if (!AnyEditing) ExitEditLock();
    }

    // First editor locks the bar + shows the legend. (EventSystem nav is already off for the whole tab.)
    private void EnterEditLock()
    {
        SettingsScreenController.Instance?.SetEditLock(true);
        RefreshLegend();   // legend authority: shows the saved-live text (or a live conflict warning)
    }

    private void ExitEditLock()
    {
        SettingsScreenController.Instance?.SetEditLock(false);
        RefreshLegend();   // hides the legend unless a conflict is still unresolved
    }

    /// <summary>Back arbiter while editing (called by <see cref="SettingsScreenController.HandleBack"/>). Re-polls
    /// each editing column for a Back press THIS frame (its own device: pad-B via UICancel, or Esc/Start via Pause)
    /// and exits only that player's edit — per-player, independent. Never resumes while anyone still edits.
    /// A column that is CAPTURING is skipped: its Back is owned by the RebindingOperation's own-device cancel (kb Esc /
    /// pad Select), so the shared Back arbiter (which polls SharedInput and hears BOTH devices) must not force-exit it —
    /// otherwise a player pressing pad-B (also UICancel) mid-capture would CANCEL the capture instead of binding B, and
    /// one player's Back could nuke the other's live capture.</summary>
    public void HandleBackWhileEditing()
    {
        if (_c1 != null && _c1.editing && !_c1.capturing && SlotBackPressed(PlayerSlot.One)) ExitEdit(PlayerSlot.One);
        if (_c2 != null && _c2.editing && !_c2.capturing && SlotBackPressed(PlayerSlot.Two)) ExitEdit(PlayerSlot.Two);
    }

    private static bool SlotBackPressed(PlayerSlot slot)
    {
        var p = PlayerInputRouter.ForSlot(slot);
        if (p == null) return false;
        return p.GetUICancelDown() || p.GetPauseDown();
    }

    // ── Highlight overlays (the two tagged cursors) ──────────────────────────
    private void UpdateOverlay(Cursor c)
    {
        if (c == null || c.overlay == null) return;
        if (!c.connected) { c.overlay.gameObject.SetActive(false); return; }

        RectTransform target = FocusRect(c);
        if (target == null) { c.overlay.gameObject.SetActive(false); return; }

        c.overlay.gameObject.SetActive(true);
        if (c.overlay.parent != null)
            c.overlay.SetAsLastSibling();   // keep it drawn above the content it highlights
        var r = target.rect;
        c.overlay.position = target.TransformPoint(new Vector3(r.center.x, r.center.y, 0f));
        c.overlay.sizeDelta = new Vector2(r.width + 10f, r.height + 8f);
        if (c.overlayImg != null)
        {
            var col = (c.capturing) ? _captureColor : c.color;
            col.a = 0.22f;
            c.overlayImg.color = col;
        }
    }

    private RectTransform FocusRect(Cursor c)
    {
        if (c.zone == Zone.Bar)
        {
            var bar = TabBar;
            var item = bar != null ? bar.BarItem(c.barIndex) : null;
            return item != null ? item.transform as RectTransform : null;
        }
        if (c.colIndex == ColEdit) return c.edit != null ? c.edit.transform as RectTransform : null;
        if (c.colIndex == ColRestore) return c.restore != null ? c.restore.transform as RectTransform : null;
        int row = c.colIndex - RowBase;
        return (row >= 0 && row < c.rowRects.Count) ? c.rowRects[row] : null;
    }

    // ── Row + overlay building (once, at runtime) ────────────────────────────
    private void EnsureRows()
    {
        if (!Application.isPlaying) return;
        EnsureColumnRows(_c1);
        EnsureColumnRows(_c2);
        if (!_overlaysBuilt)
        {
            _overlaysBuilt = true;
            BuildOverlay(_c1);
            BuildOverlay(_c2);
        }
    }

    // Build (or rebuild) a column's rows when they're missing or its device kind changed. Kind is stable per session
    // (assignment frozen while the screen is open), so this fires at most once per column per open.
    private void EnsureColumnRows(Cursor c)
    {
        if (!Application.isPlaying || c.rowsParent == null) return;
        var kind = ColumnKind(c);
        if (c.rowRects.Count > 0 && c.builtKind == kind) return;
        c.builtKind = kind;
        BuildRows(c);
    }

    private void BuildRows(Cursor c)
    {
        c.binds.Clear();
        c.rowNames.Clear();
        c.rowRects.Clear();
        c.conflictRows.Clear();
        if (c.rowsParent == null) return;

        // Clear any previously-built rows (a rebuild on device-kind change).
        for (int i = c.rowsParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(c.rowsParent.GetChild(i).gameObject);

        BuildRowSpecs(c.rows, c.builtKind);

        foreach (var spec in c.rows)
        {
            var rowGO = new GameObject("Row_" + spec.label, typeof(RectTransform));
            var rowRT = (RectTransform)rowGO.transform;
            rowGO.transform.SetParent(c.rowsParent, false);
            c.rowRects.Add(rowRT);

            var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.spacing = 10; hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.padding = new RectOffset(8, 8, 0, 0);
            var rowLE = rowGO.AddComponent<LayoutElement>(); rowLE.minHeight = 40f; rowLE.preferredHeight = 40f;

            var name = NewText(rowGO.transform, "Name", spec.label, 20f, TextAlignmentOptions.Left, _textColor);
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            c.rowNames.Add(name);

            var binding = NewText(rowGO.transform, "Binding", "—", 20f, TextAlignmentOptions.Right, _bindingColor);
            binding.enableWordWrapping = false;
            var bLE = binding.gameObject.AddComponent<LayoutElement>(); bLE.minWidth = 150f; bLE.flexibleWidth = 0f;
            c.binds.Add(binding);
        }
    }

    // A single per-player highlight overlay: a translucent fill + coloured outline + a P1/P2 tag, parented under the
    // panel (ignores layout) and moved over the focused element each frame.
    private void BuildOverlay(Cursor c)
    {
        var go = new GameObject("CursorHi_" + c.tag, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        go.AddComponent<LayoutElement>().ignoreLayout = true;

        var img = go.AddComponent<Image>();
        var fill = c.color; fill.a = 0.22f; img.color = fill;
        img.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = c.color;
        outline.effectDistance = new Vector2(3f, 3f);

        // A solid P1/P2 badge pill anchored to the highlight's TOP-LEFT corner (sits as a small tab just above the bar,
        // exactly where the player marked it). Bright cursor-colour fill + dark bold label so it reads as a distinct
        // player badge rather than floating text. It rides the overlay, so it tracks whatever the cursor highlights.
        var badgeGO = new GameObject("Badge", typeof(RectTransform));
        var badgeRT = (RectTransform)badgeGO.transform;
        badgeRT.SetParent(go.transform, false);
        badgeRT.anchorMin = badgeRT.anchorMax = new Vector2(0f, 1f);   // top-left of the overlay
        badgeRT.pivot = new Vector2(0f, 0f);                            // grow up-right from that corner (tab above bar)
        badgeRT.anchoredPosition = new Vector2(2f, -2f);
        badgeRT.sizeDelta = new Vector2(38f, 22f);
        var badgeImg = badgeGO.AddComponent<Image>();
        var badgeFill = c.color; badgeFill.a = 0.97f; badgeImg.color = badgeFill;
        badgeImg.raycastTarget = false;

        var tag = NewText(badgeGO.transform, "Tag", c.tag, 14f, TextAlignmentOptions.Center, new Color(0.05f, 0.07f, 0.10f, 1f));
        tag.fontStyle = FontStyles.Bold;
        var tagRT = (RectTransform)tag.transform;
        tagRT.anchorMin = Vector2.zero; tagRT.anchorMax = Vector2.one;   // fill the badge
        tagRT.offsetMin = Vector2.zero; tagRT.offsetMax = Vector2.zero;

        c.overlay = rt;
        c.overlayImg = img;
        go.SetActive(false);
    }

    // ── Two-column bottom device legend (CONTROLS only) ──────────────────────
    // The shared single legend (UILegendBar) flips keyboard↔pad by last-used device — right for the other tabs
    // where one shared cursor drives the menu. CONTROLS has two independent per-player cursors on two device columns,
    // so its legend splits to match: P1's hints in its column's device language, P2's in its own, each FIXED by that
    // column's PairedDeviceKind. Resolve the shared legend once (a sibling UI widget, not a manager — a find is the
    // right tool, same as the InputPromptView sweep); it's hidden while CONTROLS is up and restored on leave.
    private UILegendBar SharedLegend
    {
        get
        {
            if (_sharedLegend == null)
                _sharedLegend = FindFirstObjectByType<UILegendBar>(FindObjectsInactive.Include);
            return _sharedLegend;
        }
    }

    // Build once, then rebuild only if a column's device kind changed (kind is frozen while the screen is open, so this
    // is at most one build per open). RefreshDeviceLegend keeps the greying in step with connect/disconnect.
    private void EnsureDeviceLegend()
    {
        if (!Application.isPlaying) return;
        var k1 = ColumnKind(_c1);
        var k2 = ColumnKind(_c2);
        if (!_legendBuilt || k1 != _legendKindP1 || k2 != _legendKindP2)
        {
            BuildDeviceLegend(k1, k2);
            _legendBuilt = true;
            _legendKindP1 = k1;
            _legendKindP2 = k2;
        }
        RefreshDeviceLegend();
    }

    // Grey a disconnected player's legend group, mirroring its greyed column (connected = distinct provider + live device).
    private void RefreshDeviceLegend()
    {
        if (_legendP1Group != null) _legendP1Group.alpha = (_c1 != null && _c1.connected) ? 1f : 0.4f;
        if (_legendP2Group != null) _legendP2Group.alpha = (_c2 != null && _c2.connected) ? 1f : 0.4f;
    }

    private void BuildDeviceLegend(InputDeviceKind k1, InputDeviceKind k2)
    {
        if (_legendRoot != null) DestroyImmediate(_legendRoot.gameObject);

        // Put the bar in the SAME container as the shared legend (under ScreenRoot) so it spans the full SCREEN width
        // and reaches the very bottom EDGE — covering the bottom HUD strip the player marked, exactly where the earlier
        // shared legend sat. ~45% thicker than that bar per request. Falls back to the CONTROLS panel bottom if the
        // shared legend isn't wired. Because it now lives OUTSIDE this panel, OnEnable/OnDisable toggle its visibility.
        var shared = SharedLegend;
        var sharedRT = shared != null ? shared.transform as RectTransform : null;
        Transform parentT = sharedRT != null ? sharedRT.parent : transform;
        float baseH = sharedRT != null ? Mathf.Max(40f, sharedRT.rect.height) : 52f;
        float barH = baseH * 1.45f;   // ~45% thicker than the shared bar

        var rootGO = new GameObject("DeviceLegend2Col", typeof(RectTransform));
        _legendRoot = (RectTransform)rootGO.transform;
        _legendRoot.SetParent(parentT, false);
        _legendRoot.anchorMin = new Vector2(0f, 0f);
        _legendRoot.anchorMax = new Vector2(1f, 0f);
        _legendRoot.pivot = new Vector2(0.5f, 0f);
        _legendRoot.offsetMin = new Vector2(0f, 0f);
        _legendRoot.offsetMax = new Vector2(0f, barH);   // full width, hugging the very bottom edge
        rootGO.AddComponent<LayoutElement>().ignoreLayout = true;

        var bar = rootGO.AddComponent<Image>();
        bar.color = new Color(0.09f, 0.10f, 0.13f, 0.98f);   // solid bar → reads as fixed + hides the HUD behind it
        bar.raycastTarget = false;

        // Thin separator along the bar's top edge for definition against the rows above.
        var sepGO = new GameObject("Sep", typeof(RectTransform));
        var sepRT = (RectTransform)sepGO.transform;
        sepRT.SetParent(_legendRoot, false);
        sepRT.anchorMin = new Vector2(0f, 1f);
        sepRT.anchorMax = new Vector2(1f, 1f);
        sepRT.pivot = new Vector2(0.5f, 1f);
        sepRT.offsetMin = new Vector2(0f, -2f);
        sepRT.offsetMax = new Vector2(0f, 0f);
        var sepImg = sepGO.AddComponent<Image>();
        sepImg.color = new Color(1f, 1f, 1f, 0.08f);
        sepImg.raycastTarget = false;

        _legendP1Group = BuildLegendGroup(_c1, k1, anchorLeft: true);
        _legendP2Group = BuildLegendGroup(_c2, k2, anchorLeft: false);

        _legendRoot.SetAsLastSibling();   // draw above the sibling tab panels + the (hidden) shared legend
    }

    // One player's hints INSIDE the shared bar (the bar is the block, so the group itself is transparent): a coloured
    // P1/P2 pill then Move / Select / Back chips in THAT column's device language, hugging its own edge of the bar.
    private CanvasGroup BuildLegendGroup(Cursor c, InputDeviceKind kind, bool anchorLeft)
    {
        var go = new GameObject("LegendGroup_" + c.tag, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_legendRoot, false);
        rt.anchorMin = new Vector2(anchorLeft ? 0f : 1f, 0.5f);
        rt.anchorMax = new Vector2(anchorLeft ? 0f : 1f, 0.5f);
        rt.pivot = new Vector2(anchorLeft ? 0f : 1f, 0.5f);
        rt.anchoredPosition = new Vector2(anchorLeft ? 24f : -24f, 0f);   // padded in from its edge

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = false; hlg.childControlHeight = false;   // children self-size via their own fitters
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.spacing = 16f;
        hlg.childAlignment = anchorLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var group = go.AddComponent<CanvasGroup>();

        AddTagPill(go.transform, c);
        bool pad = kind == InputDeviceKind.Gamepad;
        AddLegendChip(go.transform, pad ? "L-Stick / D-Pad" : "WASD / Arrows", "Move");
        AddLegendChip(go.transform, pad ? "A" : "Enter", "Select");
        AddLegendChip(go.transform, pad ? "B" : "Esc", "Back");
        return group;
    }

    // A coloured P1/P2 pill (matches the cursor badge): cursor-colour box + dark bold label. Self-hugging.
    private void AddTagPill(Transform parent, Cursor c)
    {
        var pill = MakeHugBox(parent, "TagPill", c.color, 0.97f);
        var t = NewText(pill, "T", c.tag, 15f, TextAlignmentOptions.Center, new Color(0.05f, 0.07f, 0.10f, 1f));
        t.fontStyle = FontStyles.Bold; t.enableWordWrapping = false;
    }

    // A greybox hint chip: a keycap-BOX (bright text in a dark block, like the row keycaps) + a sub-coloured caption.
    private void AddLegendChip(Transform parent, string keycap, string caption)
    {
        var chipGO = new GameObject("Chip_" + caption, typeof(RectTransform));
        chipGO.transform.SetParent(parent, false);
        var hlg = chipGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.spacing = 7f; hlg.childAlignment = TextAnchor.MiddleLeft;
        var chipFit = chipGO.AddComponent<ContentSizeFitter>();
        chipFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        chipFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var box = MakeHugBox(chipGO.transform, "Key", new Color(0.20f, 0.21f, 0.26f, 1f), 1f);
        var key = NewText(box, "K", keycap, 15f, TextAlignmentOptions.Center, _bindingColor);
        key.fontStyle = FontStyles.Bold; key.enableWordWrapping = false;

        var cap = NewText(chipGO.transform, "Cap", caption, 15f, TextAlignmentOptions.Left, _subColor);
        cap.enableWordWrapping = false;
        var capFit = cap.gameObject.AddComponent<ContentSizeFitter>();
        capFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        capFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // A self-hugging coloured box: Image + LayoutGroup(controls the single text child) + ContentSizeFitter(sizes the
    // box to that child + padding). The sanctioned LayoutGroup+ContentSizeFitter-on-one-object pattern (a chip/keycap).
    private Transform MakeHugBox(Transform parent, string name, Color color, float alpha)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        var col = color; col.a = alpha; img.color = col; img.raycastTarget = false;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.padding = new RectOffset(9, 9, 4, 4);
        var fit = go.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return go.transform;
    }

    // ── Fill / helpers ───────────────────────────────────────────────────────
    private void FillColumn(Cursor c, IInputProvider provider, InputDeviceKind kind, bool active)
    {
        var asset = InputGlyphText.SpriteAsset;
        for (int i = 0; i < c.binds.Count && i < c.rows.Count; i++)
        {
            var label = c.binds[i];
            if (label == null) continue;
            if (asset != null && label.spriteAsset != asset) label.spriteAsset = asset;

            if (!active || provider == null) { label.text = "—"; continue; }

            var spec = c.rows[i];
            if (!string.IsNullOrEmpty(spec.display)) { label.text = spec.display; continue; }  // fixed read-only label

            if (!string.IsNullOrEmpty(spec.part))
            {
                // Composite direction (Move Up/Down/Left/Right) → plain key text; the glyph atlas keys whole actions,
                // not individual 2DVector parts, so a readable "W"/"A"/"↑" is clearer here than a sprite.
                string d = provider.GetCompositePartDisplay(spec.action, spec.part, kind == InputDeviceKind.Gamepad);
                label.text = (!string.IsNullOrEmpty(d) && d != "?") ? d : "—";
                continue;
            }

            string stem = InputGlyphResolver.ResolveTmpSpriteNameForKind(provider, spec.action, kind, out string fallback);
            label.text = !string.IsNullOrEmpty(stem) ? $"<sprite name=\"{stem}\" tint=1>"
                       : (!string.IsNullOrEmpty(fallback) ? fallback : "—");
        }
    }

    private static void SetColumnActive(CanvasGroup group, bool active)
    {
        if (group == null) return;
        group.alpha = active ? 1f : 0.4f;
        group.interactable = active;
        group.blocksRaycasts = active;
    }

    private static string DeviceLabel(InputDeviceKind kind)
        => kind == InputDeviceKind.Gamepad ? "Gamepad" : "Keyboard & Mouse";

    private TMP_Text NewText(Transform parent, string name, string text, float size,
                             TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
        return t;
    }

    // ── Per-column restore (per-player asset → clean) ────────────────────────
    private void RestoreSlot(PlayerSlot slot)
    {
        var p1 = PlayerInputRouter.ForSlot(PlayerSlot.One);
        var provider = PlayerInputRouter.ForSlot(slot);
        if (slot == PlayerSlot.Two && ReferenceEquals(provider, p1)) return;   // P2 unpaired → no-op (don't hit P1)

        provider?.ResetBindingsToDefault();
        RefreshColumn(Col(slot));
        RefreshExternalPrompts();
    }

    private static void RefreshExternalPrompts()
    {
        var prompts = Object.FindObjectsByType<InputPromptView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p in prompts) p.Refresh();
    }
}
