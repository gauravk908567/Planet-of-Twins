using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Couch M2 — Character-Select V2 (SPATIAL). Three zones — <b>LEFT = Kai (Vethara)</b>,
/// <b>RIGHT = Lyra (Luminari)</b>, <b>CENTER-BOTTOM = Random</b> (default). Each player is an old-school
/// <c>1P</c>/<c>2P</c> badge that sits at the <b>top-middle of the box it occupies</b> and adopts that zone's
/// clan hue (Random = neutral). Both badges start on Random; a player moves theirs onto a twin. A single
/// explicit <b>Start</b> button launches (enabled only when the two picks resolve to distinct twins).
///
/// <para>Backed by the SAME <see cref="CharacterSelectController"/> (only the presentation changed): marker
/// zone → <see cref="CharacterSelectController.SetPick"/>, Start → <see cref="CharacterSelectController.StartFromCurrentPicks"/>,
/// enable/conflict via <see cref="CharacterSelectController.CanStartFromPicks"/>/<see cref="CharacterSelectController.HasConflictFromPicks"/>.</para>
///
/// <para><b>Input.</b> Per-device (M2.2): each player drives THEIR slot via <see cref="PlayerInputRouter.ForSlot"/> —
/// left → Kai, right → Lyra, down → Random (edge-detected); Attack = press Start. Slot Two is polled only while
/// couch is active (solo → P2 falls back to P1). Mouse: click a zone box moves P1's marker; click Start.</para>
/// </summary>
[DisallowMultipleComponent]
public class CharacterSelectScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private CharacterSelectController controller;

    [Header("Zone boxes — mouse click moves P1's marker there")]
    [SerializeField] private Button kaiBox;
    [SerializeField] private Button lyraBox;
    [SerializeField] private Button randomBox;

    [Header("Zone badge anchors — top-middle of each box (badges reparent here)")]
    [SerializeField] private RectTransform kaiAnchor;
    [SerializeField] private RectTransform lyraAnchor;
    [SerializeField] private RectTransform randomAnchor;

    [Header("Player badges — reparented to the occupied zone's anchor")]
    [SerializeField] private RectTransform p1Badge;
    [SerializeField] private Image p1BadgeBg;   // tinted to the current zone's clan hue
    [SerializeField] private RectTransform p2Badge;
    [SerializeField] private Image p2BadgeBg;

    [Header("Controls")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text statusText;

    /// <summary>Raised when Back is pressed (FrontEndFlowController returns to the Start Menu).</summary>
    public event Action BackRequested;

    private bool _subscribed;

    // Per-slot nav edge-latch: a stick must return toward centre before it moves the marker again.
    private readonly bool[] _navEngaged = { false, false };
    private const float NavEngage = 0.5f;
    private const float NavRelease = 0.3f;

    // Clan hues (ArtStyle §10 mid-ramp) — the marker adopts the hue of the zone it's on; Random = neutral.
    private static readonly Color KaiHue    = new(0.659f, 0.455f, 0.941f); // #A874F0 Vethara
    private static readonly Color LyraHue   = new(1.000f, 0.808f, 0.322f); // #FFCE52 Luminari
    private static readonly Color RandomHue = new(0.878f, 0.878f, 0.898f); // neutral pale
    private static Color HueFor(CharacterPick p) =>
        p == CharacterPick.Kai ? KaiHue : p == CharacterPick.Lyra ? LyraHue : RandomHue;

    private void Awake()
    {
        if (kaiBox    != null) kaiBox.onClick.AddListener(() => OnZoneClicked(CharacterPick.Kai));
        if (lyraBox   != null) lyraBox.onClick.AddListener(() => OnZoneClicked(CharacterPick.Lyra));
        if (randomBox != null) randomBox.onClick.AddListener(() => OnZoneClicked(CharacterPick.Random));
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (backButton  != null) backButton.onClick.AddListener(RaiseBack);
    }

    private void OnDestroy()
    {
        if (kaiBox    != null) kaiBox.onClick.RemoveAllListeners();
        if (lyraBox   != null) lyraBox.onClick.RemoveAllListeners();
        if (randomBox != null) randomBox.onClick.RemoveAllListeners();
        if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
        if (backButton  != null) backButton.onClick.RemoveListener(RaiseBack);
        Unsubscribe();
    }

    public void Show()
    {
        if (controller != null)
        {
            controller.ResetSelection();
            if (!_subscribed) { controller.OnChanged += Refresh; _subscribed = true; }
        }
        _navEngaged[0] = _navEngaged[1] = false;
        if (panel != null) panel.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        Unsubscribe();
        if (panel != null) panel.SetActive(false);
    }

    private void Unsubscribe()
    {
        if (_subscribed && controller != null) controller.OnChanged -= Refresh;
        _subscribed = false;
    }

    // ── Per-device input (M2.2) ────────────────────────────────
    private void Update()
    {
        if (controller == null || panel == null || !panel.activeSelf || controller.IsComplete) return;

        PollSlot(PlayerSlot.One);
        // Slot Two only when a distinct second device is paired (couch). Solo → P2 falls back to P1, so reading
        // it would move both markers off one stick; the mouse handles P2 in that case.
        bool couch = CouchDeviceManager.Instance != null && CouchDeviceManager.Instance.IsCouchActive;
        if (couch) PollSlot(PlayerSlot.Two);
    }

    // Reads one slot's paired device: left → Kai, right → Lyra, down → Random (edge-detected); Attack = Start.
    private void PollSlot(PlayerSlot slot)
    {
        var input = PlayerInputRouter.ForSlot(slot);
        if (input == null) return;
        int i = (int)slot;

        Vector2 m = input.GetMovementInput();
        if (!_navEngaged[i] && m.magnitude > NavEngage)
        {
            _navEngaged[i] = true;
            if (Mathf.Abs(m.x) >= Mathf.Abs(m.y))
                controller.SetPick(slot, m.x < 0f ? CharacterPick.Kai : CharacterPick.Lyra);
            else if (m.y < 0f)
                controller.SetPick(slot, CharacterPick.Random);
            // up (m.y > 0) is unused — the two twins are the left/right poles, Random is below.
        }
        else if (_navEngaged[i] && m.magnitude < NavRelease)
        {
            _navEngaged[i] = false;
        }

        if (input.GetAttackDown())
            TryStart();
    }

    // ── Mouse ──────────────────────────────────────────────────
    private void OnZoneClicked(CharacterPick pick) => controller?.SetPick(PlayerSlot.One, pick);
    private void OnStartClicked() => TryStart();
    private void RaiseBack() => BackRequested?.Invoke();

    private void TryStart()
    {
        if (controller == null) return;
        controller.StartFromCurrentPicks();   // no-ops + flashes status when the picks conflict
    }

    // ── Presentation ───────────────────────────────────────────
    private void Refresh()
    {
        if (controller == null) return;

        PlaceBadge(PlayerSlot.One, p1Badge, p1BadgeBg);
        PlaceBadge(PlayerSlot.Two, p2Badge, p2BadgeBg);

        bool conflict = controller.HasConflictFromPicks;
        if (startButton != null) startButton.interactable = !conflict && !controller.IsComplete;

        if (statusText != null)
        {
            if (controller.IsComplete)  statusText.text = "Starting…";
            else if (conflict)          statusText.text = "Same twin — pick different!";
            else                        statusText.text = "Move to a twin, then press Start";
        }
    }

    // Reparents a player's badge under the top-middle anchor of the zone it currently picks, and tints it to
    // that zone's clan hue. Two badges sharing a zone sit side by side (the anchor carries a layout group).
    private void PlaceBadge(PlayerSlot slot, RectTransform badge, Image bg)
    {
        if (badge == null) return;
        var anchor = AnchorFor(controller.PickOf(slot));
        if (anchor != null && badge.parent != anchor)
        {
            badge.SetParent(anchor, false);
            badge.anchoredPosition = Vector2.zero;   // layout group (if any) positions it
        }
        if (bg != null) bg.color = HueFor(controller.PickOf(slot));
    }

    private RectTransform AnchorFor(CharacterPick pick) =>
        pick == CharacterPick.Kai ? kaiAnchor : pick == CharacterPick.Lyra ? lyraAnchor : randomAnchor;
}
