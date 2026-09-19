using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SkillTreeUI : MonoBehaviour
{
    public static SkillTreeUI Instance { get; private set; }

    [Header("Panel — starts inactive")]
    [SerializeField] private GameObject SkillTreePanel;

    [Header("Inject — drag SkillTreeManager into all three")]
    [SerializeField] private MonoBehaviour _dataStoreMono;
    [SerializeField] private MonoBehaviour _purchaserMono;
    [SerializeField] private MonoBehaviour _pointBankMono;

    [Header("Points display")]
    [SerializeField] private TMP_Text PointsText;

    [Header("Tab root objects — parent of hand-placed buttons")]
    [SerializeField] private GameObject KaiTabContent;
    [SerializeField] private GameObject LyraTabContent;
    [SerializeField] private GameObject SharedTabContent;

    [Header("Tab buttons")]
    [SerializeField] private Button KaiTab;
    [SerializeField] private Button LyraTab;
    [SerializeField] private Button SharedTab;

    [Header("Tab colours")]
    [SerializeField] private Color ActiveTabCol = new Color(0.35f, 0.67f, 1.00f);
    [SerializeField] private Color InactiveTabCol = new Color(0.35f, 0.35f, 0.38f);

    private IAbilityDataStore _dataStore;
    private ISkillTreePurchaser _purchaser;
    private IPointBank _pointBank;

    // Item 4 (controller nav): which tab is showing (0 Kai / 1 Lyra / 2 Shared) — LB/RB cycle it.
    private int _activeTab;

    // P-B — a single badge pinned to the selected node showing which player LAST drove the cursor (1=P1, 2=P2).
    private int _lastMover = 1;
    private TMP_Text _moverBadge;
    private static readonly Color P1Colour = new Color(1f, 0.82f, 0.30f);     // gold
    private static readonly Color P2Colour = new Color(0.66f, 0.45f, 0.94f);  // violet

    // Item 6 (controller nav legend): runtime-built glyph legends so a pad player sees the affordances the
    // (already-wired) nav uses. Built like the badge above — zero scene wiring. Gamepad-gated: the UI-nav
    // actions are pad-only, so on keyboard the tabs are mouse-clickable and arrow-keys navigate (legend hidden).
    private TMP_Text _tabHint;    // top, by the tabs: "LB  Tabs  RB"
    private TMP_Text _navLegend;  // bottom bar: "Y Preview   A Buy   B Back"
    private const string TabHintTemplate   = "{TabLeft}  Tabs  {TabRight}";
    private const string NavLegendTemplate = "{UIPreview} Preview     {InstantBuy} Buy     {UICancel} Back";

    public bool IsOpen => SkillTreePanel != null && SkillTreePanel.activeSelf;

    public void Close()
    {
        if (!IsOpen) return;
        SkillTreePanel.SetActive(false);
        TimeScaleService.Instance?.Release(this);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // P13: Tab comes through IInputProvider (Input System) — raw Input.* is banned.
    private IInputProvider _input;

    void Start()
    {
        _input = PlayerInputRouter.SharedInput;   // M0: shared-UI seam (falls back to TwinInputReader.Instance)
        if (_input == null)
            Debug.LogError("[SkillTreeUI] PlayerInputRouter.SharedInput unresolved — Tab toggle dead.", this);

        _dataStore = _dataStoreMono as IAbilityDataStore;
        _purchaser = _purchaserMono as ISkillTreePurchaser;
        _pointBank = _pointBankMono as IPointBank;

        _dataStore ??= SkillTreeManager.Instance;
        _purchaser ??= SkillTreeManager.Instance;
        _pointBank ??= SkillTreeManager.Instance;

        if (_pointBank == null)
            Debug.LogError("[SkillTreeUI] IPointBank unresolved — is Persistent loaded?", this);
        if (_purchaser == null)
            Debug.LogError("[SkillTreeUI] ISkillTreePurchaser unresolved — is Persistent loaded?", this);

        // Re-subscribe in case OnEnable fired before _pointBank was resolved.
        if (_pointBank != null)
        {
            _pointBank.OnPointsChanged -= RefreshPoints;
            _pointBank.OnPointsChanged += RefreshPoints;
        }

        InitialiseTab(KaiTabContent, GetKaiData());
        InitialiseTab(LyraTabContent, GetLyraData());
        InitialiseTab(SharedTabContent, GetSharedData());

        // P-B — a ScrollRect only scrolls on mouse by itself, so controller/keyboard nav walks the cursor off
        // screen. Attach the ensure-selected-visible driver to every ScrollRect under the panel (once), so the
        // list scrolls to follow the cursor whatever the exact tab hierarchy is. No scene wiring required.
        if (SkillTreePanel != null)
            foreach (var sr in SkillTreePanel.GetComponentsInChildren<ScrollRect>(true))
                if (sr.GetComponent<ScrollRectEnsureSelectedVisible>() == null)
                    sr.gameObject.AddComponent<ScrollRectEnsureSelectedVisible>();

        ShowTab(0);
        SkillTreePanel?.SetActive(false);
    }

    void OnEnable()
    {
        if (_pointBank != null) _pointBank.OnPointsChanged += RefreshPoints;
        KaiTab?.onClick.AddListener(() => ShowTab(0));
        LyraTab?.onClick.AddListener(() => ShowTab(1));
        SharedTab?.onClick.AddListener(() => ShowTab(2));
        // Item 6: re-resolve the nav legend when the active device flips keyboard↔pad (named handler, R8).
        LastUsedDeviceTracker.OnLastUsedChanged += OnDeviceSwitched;
    }

    void OnDisable()
    {
        if (_pointBank != null) _pointBank.OnPointsChanged -= RefreshPoints;
        KaiTab?.onClick.RemoveAllListeners();
        LyraTab?.onClick.RemoveAllListeners();
        SharedTab?.onClick.RemoveAllListeners();
        LastUsedDeviceTracker.OnLastUsedChanged -= OnDeviceSwitched;
    }

    void OnDeviceSwitched(InputDeviceKind kind) => RefreshNavLegend();

    void Update()
    {
        // BUG (couch, pad) — while the unified pause/settings screen owns the layer, the skill tree must be
        // INERT. Otherwise pressing the skill-tree toggle here opens the tree ON TOP of the pause menu, and
        // ShowTab(0) steals the EventSystem selection — stranding a pad player behind the pause menu with no
        // way back (the pad can't re-focus a menu it never sees). Pause is the higher-priority layer; its
        // ESC/Start arbiter owns close order. So suspend ALL skill-tree input (toggle + nav) while it's open.
        // (IsPauseOpen covers Settings too now — the unified screen IS the settings screen.)
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPauseOpen)
            return;

        // Tab key toggles the skill tree open/closed. ESC is handled by PauseMenuController (central arbiter).
        if (_input != null && _input.GetSkillTreeToggleDown())
        {
            bool opening = !SkillTreePanel.activeSelf;
            SkillTreePanel.SetActive(opening);

            if (opening)
            {
                TimeScaleService.Instance?.Request(this, 0f);
                RefreshPoints(_pointBank?.CurrentPoints ?? 0);
                ShowTab(0);          // ShowTab focuses the first node for the controller
            }
            else
            {
                TimeScaleService.Instance?.Release(this);
            }
            return;
        }

        // ── Item 4: controller nav while the tree is open ──
        if (!SkillTreePanel.activeSelf) return;

        // When the preview modal is up it owns its own input (North buys, B closes) — don't double-handle here,
        // and hide the last-mover badge (the cursor is on the modal, not a node).
        if (SkillPreviewModal.Instance != null && SkillPreviewModal.Instance.IsOpen)
        {
            if (_moverBadge != null) _moverBadge.gameObject.SetActive(false);
            return;
        }
        if (_input == null) return;

        // Back / Cancel (B / East — the UICancel action) closes the skill tree: "Back" everywhere. The preview
        // modal, when open, already consumed its own B in the block above, so this only closes the panel itself.
        // This is the same button the EventSystem's default UI "Cancel" uses (buttonEast), so it works on any pad.
        if (_input.GetUICancelDown()) { Close(); return; }

        UpdateLastMover();   // who's driving the shared cursor right now
        UpdateBadge();       // pin the P1/P2 badge to the selected node

        // LB / RB cycle the tab (3 tabs, wrap). ShowTab re-focuses the first node of the new tab.
        if (_input.GetUITabLeftDown())       ShowTab((_activeTab + 2) % 3);
        else if (_input.GetUITabRightDown()) ShowTab((_activeTab + 1) % 3);

        var selGO   = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var selNode = selGO != null ? selGO.GetComponent<SkillNodeButton>() : null;

        // Button 1 / North → open the focused node's preview (North then buys inside the modal).
        if (selNode != null && _input.GetUIPreviewDown())
            selNode.OpenPreview();

        // Button 3 / South → direct/instant buy the focused node (no preview), then re-focus the new frontier.
        if (selNode != null && _input.GetInstantBuyDown())
        {
            selNode.RequestPurchase();
            FocusFirstNode();
        }
    }

    /// <summary>Item 4 — land the controller's focus on the active tab's first buyable node (so arrow/stick
    /// nav works and Y/North has a target); if none is buyable, focus the first node anyway so its preview is
    /// still reachable; if the tab is empty, clear selection so a stale focus from another tab can't be bought.</summary>
    void FocusFirstNode()
    {
        if (SkillTreePanel == null || !SkillTreePanel.activeInHierarchy) return;

        GameObject content = _activeTab == 0 ? KaiTabContent
                           : _activeTab == 1 ? LyraTabContent
                           : SharedTabContent;
        if (content == null) return;

        SkillNodeButton first = null, firstBuyable = null;
        foreach (var node in content.GetComponentsInChildren<SkillNodeButton>(false))
        {
            if (first == null) first = node;
            if (firstBuyable == null && node.IsBuyable) firstBuyable = node;
        }

        var target = firstBuyable != null ? firstBuyable : first;
        if (target != null) UINavFocus.Focus(target.gameObject);
        else if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // ── P-B: last-mover badge ─────────────────────────────────
    // Track which player last drove the ONE shared cursor (movement or a UI button). P1 wins a same-frame tie
    // (checked first) — the user's "exact same time → pick one" rule. In solo, P2 falls back to P1 so it stays P1.
    void UpdateLastMover()
    {
        var p1 = PlayerInputRouter.ForSlot(PlayerSlot.One);
        var p2 = PlayerInputRouter.ForSlot(PlayerSlot.Two);
        if (ProviderActive(p1)) _lastMover = 1;
        else if (p2 != null && !ReferenceEquals(p2, p1) && ProviderActive(p2)) _lastMover = 2;
        // else: no input this frame — keep whoever moved it last.
    }

    static bool ProviderActive(IInputProvider p)
    {
        if (p == null) return false;
        if (p.GetMovementInput().sqrMagnitude > 0.04f) return true;   // stick / dpad / WASD past a deadzone
        return p.GetUITabLeftDown() || p.GetUITabRightDown()
            || p.GetInstantBuyDown() || p.GetUICancelDown();
    }

    // Pin a single "P1"/"P2" badge to the corner of the selected node so it reads as part of the cursor.
    void UpdateBadge()
    {
        var sel  = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var node = sel != null ? sel.GetComponent<SkillNodeButton>() : null;
        if (node == null)
        {
            if (_moverBadge != null) _moverBadge.gameObject.SetActive(false);
            return;
        }

        EnsureBadge();
        if (_moverBadge == null) return;

        var rt = _moverBadge.rectTransform;
        if (rt.parent != node.transform)
        {
            rt.SetParent(node.transform, false);              // rides the node (incl. scroll)
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); // top-right corner of the card
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-4f, -4f);
        }
        _moverBadge.gameObject.SetActive(true);
        _moverBadge.text  = _lastMover == 2 ? "P2" : "P1";
        _moverBadge.color = _lastMover == 2 ? P2Colour : P1Colour;
    }

    void EnsureBadge()
    {
        if (_moverBadge != null) return;
        var go = new GameObject("LastMoverBadge", typeof(RectTransform));
        _moverBadge = go.AddComponent<TextMeshProUGUI>();
        if (PointsText != null) _moverBadge.font = PointsText.font;   // reuse the panel's TMP font asset
        _moverBadge.fontSize = 18f;
        _moverBadge.fontStyle = FontStyles.Bold;
        _moverBadge.alignment = TextAlignmentOptions.Center;
        _moverBadge.raycastTarget = false;
        _moverBadge.rectTransform.sizeDelta = new Vector2(40f, 22f);
    }

    // ── Item 6: pad nav legend (runtime-built) ─────────────────────────────
    /// <summary>Build the two legend labels under the panel once (top tab-hint + bottom nav bar). Mirrors
    /// <see cref="EnsureBadge"/> — no scene wiring; the labels ride the panel's active state (hidden when closed).</summary>
    void EnsureNavLegend()
    {
        if (SkillTreePanel == null) return;
        if (_tabHint == null)   _tabHint   = BuildLegendLabel("TabHint (auto)",   anchorTop: true,  size: 22f);
        if (_navLegend == null) _navLegend = BuildLegendLabel("NavLegend (auto)", anchorTop: false, size: 22f);
    }

    TMP_Text BuildLegendLabel(string name, bool anchorTop, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var t  = go.AddComponent<TextMeshProUGUI>();
        var rt = t.rectTransform;
        rt.SetParent(SkillTreePanel.transform, false);
        float y = anchorTop ? 1f : 0f;
        rt.anchorMin = new Vector2(0f, y);
        rt.anchorMax = new Vector2(1f, y);
        rt.pivot     = new Vector2(0.5f, y);
        // A ~34px band inset 20px on the sides, 8px off the near edge (top hint hangs down, bottom bar sits up).
        if (anchorTop) { rt.offsetMin = new Vector2(20f, -42f); rt.offsetMax = new Vector2(-20f, -8f); }
        else           { rt.offsetMin = new Vector2(20f,   8f); rt.offsetMax = new Vector2(-20f, 42f); }
        if (PointsText != null) t.font = PointsText.font;   // reuse the panel's TMP font asset
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        t.enableWordWrapping = false;
        return t;
    }

    /// <summary>Gamepad-gate + populate the nav legends with live device glyphs. Hidden on keyboard/mouse (the
    /// UI-nav actions are pad-only; tabs are clickable and arrow-keys navigate there). Cheap; called on open,
    /// tab change, and device switch — never per-frame.</summary>
    void RefreshNavLegend()
    {
        EnsureNavLegend();
        bool pad = LastUsedDeviceTracker.LastUsed == InputDeviceKind.Gamepad;

        if (_tabHint != null)
        {
            _tabHint.gameObject.SetActive(pad);
            if (pad && _input != null) InputGlyphText.Apply(_tabHint, TabHintTemplate, _input);
        }
        if (_navLegend != null)
        {
            _navLegend.gameObject.SetActive(pad);
            if (pad && _input != null) InputGlyphText.Apply(_navLegend, NavLegendTemplate, _input);
        }
    }

    void InitialiseTab(GameObject root, AbilityUpgradeData[] _)
    {
        if (root == null) return;
        foreach (var btn in root.GetComponentsInChildren<SkillNodeButton>(true))
            btn.InitialiseFromScene(_purchaser, _pointBank);
    }

    void ShowTab(int index)
    {
        _activeTab = index;

        if (KaiTabContent) KaiTabContent.SetActive(index == 0);
        if (LyraTabContent) LyraTabContent.SetActive(index == 1);
        if (SharedTabContent) SharedTabContent.SetActive(index == 2);

        SetTabColour(KaiTab, index == 0);
        SetTabColour(LyraTab, index == 1);
        SetTabColour(SharedTab, index == 2);

        // Item 4 — put the controller cursor on the newly-shown tab (no-op while the panel is closed).
        FocusFirstNode();

        // Item 6 — refresh the pad nav legend (device gate + live glyphs) each time a tab shows / the tree opens.
        RefreshNavLegend();
    }

    void SetTabColour(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img) img.color = active ? ActiveTabCol : InactiveTabCol;
    }

    void RefreshPoints(int pts)
    {
        if (PointsText) PointsText.text = $"Points: <b>{pts}</b>";
    }

    AbilityUpgradeData[] GetKaiData() => new[] { _dataStore?.StunData };
    AbilityUpgradeData[] GetLyraData() => new[] { _dataStore?.PossessData };
    AbilityUpgradeData[] GetSharedData() => new[]
    {
        _dataStore?.GateData,
        _dataStore?.HealthRegenData,
        _dataStore?.AccordSpiritsData,
        _dataStore?.CoalesceData,
        _dataStore?.SoulConvData,
        _dataStore?.EmpowerData,
        _dataStore?.AccordData
    };
}