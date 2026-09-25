using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Couch M2 — save-slot select (greybox). Shown between the Start Menu and Character Select in BOTH front-end
/// modes: <b>New Game</b> (pick a slot to save into) and <b>Continue</b> (pick an existing save to resume).
///
/// <para><b>Mode:</b>
/// <list type="bullet">
/// <item><b>NewGame</b> — every slot pickable; picking an occupied slot asks for confirmation before overwriting.</item>
/// <item><b>Continue</b> — only occupied slots pickable (empty ones greyed); picking one stages that save for
/// the boot path.</item>
/// </list>
/// In both modes an occupied slot can be <b>deleted</b> (pad X / keyboard Delete on the focused slot), also after a
/// confirmation. The shared <see cref="UIConfirmDialog"/> asks both questions; focus starts on its safe choice.</para>
///
/// <para>On pick: the mode + slot are RECORDED in <see cref="SessionSetup.SetMode"/> — this screen lives in the
/// FrontEnd scene, which runs BEFORE Persistent loads, so SaveService doesn't exist yet (calling it here was a
/// silent no-op → no slot was ever chosen → nothing was written to disk). Persistent's SaveService applies the
/// choice in Awake. Continue validates the slot is loadable first (a stale/corrupt file keeps the screen up).
/// Then <see cref="SlotChosen"/> fires and <see cref="FrontEndFlowController"/> advances to Character Select.
/// Back → <see cref="BackRequested"/> (returns to the Start Menu).</para>
///
/// <para><b>Input</b> (BUG-116): the shared EventSystem navigation, same as the Main Menu — ANY device (both pads,
/// keyboard, mouse) moves the focus glow among pickable slots + Back (<see cref="UINavStyle"/> / <see cref="UINavFocus"/>),
/// Submit (A / Enter) picks, UI Cancel (B / Esc) = Back (or closes the dialog). Delete = <c>UIDelete</c> on either
/// player's device (<see cref="PlayerInputRouter.SharedInput"/>). The on-screen <see cref="UILegendBar"/> shows
/// the buttons; its Delete hint appears only while the focused slot holds a save.</para>
/// </summary>
[DisallowMultipleComponent]
public class SaveSlotScreen : MonoBehaviour
{
    public enum Mode { NewGame, Continue }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;

    // Slot cards — one explicit field per slot (SaveSystem.SlotCount == 3). Explicit scalars rather than a
    // serialized array so the Inspector/tooling wires each ref reliably; the two arrays below are built from
    // them in Awake and everything downstream iterates those.
    [Header("Slot cards — slot 0 / 1 / 2")]
    [SerializeField] private Button slot0Button;
    [SerializeField] private Button slot1Button;
    [SerializeField] private Button slot2Button;
    [SerializeField] private TMP_Text slot0Label;
    [SerializeField] private TMP_Text slot1Label;
    [SerializeField] private TMP_Text slot2Label;

    [Header("Controls")]
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Confirm + legend")]
    [Tooltip("Yes/No dialog for overwrite + delete (same component the settings screen uses).")]
    [SerializeField] private UIConfirmDialog confirmDialog;
    [Tooltip("Button legend (Select / Delete / Back) — refreshed on show; flips keyboard↔pad live by itself.")]
    [SerializeField] private UILegendBar legend;
    [Tooltip("The legend's Delete chip — shown only while the focused slot holds a save.")]
    [SerializeField] private GameObject deleteHint;

    private Button[] _slotButtons;
    private TMP_Text[] _slotLabels;

    /// <summary>Raised with the chosen slot index once the pick has been committed to <see cref="SaveService"/>.</summary>
    public event Action<int> SlotChosen;
    /// <summary>Raised when Back is pressed (return to the Start Menu).</summary>
    public event Action BackRequested;

    private Mode _mode;
    private int _pendingSlot = -1;   // the slot the open dialog is about (-1 = none)

    private void Awake()
    {
        _slotButtons = new[] { slot0Button, slot1Button, slot2Button };
        _slotLabels  = new[] { slot0Label,  slot1Label,  slot2Label  };

        for (int i = 0; i < _slotButtons.Length; i++)
        {
            int slot = i;   // capture per-iteration
            if (_slotButtons[i] != null) _slotButtons[i].onClick.AddListener(() => OnSlotClicked(slot));
        }
        if (backButton != null) backButton.onClick.AddListener(RaiseBack);

        // Item 1 (controller nav): pad/keyboard-traversable cards + the shared focus glow (dialog buttons included).
        UINavStyle.Apply(panel);
    }

    private void OnDestroy()
    {
        if (_slotButtons != null)
            foreach (var b in _slotButtons) if (b != null) b.onClick.RemoveAllListeners();
        if (backButton != null) backButton.onClick.RemoveListener(RaiseBack);
    }

    public void Show(Mode mode)
    {
        _mode = mode;
        _pendingSlot = -1;
        if (panel != null) panel.SetActive(true);
        if (confirmDialog != null) confirmDialog.HideImmediate();
        if (titleText != null)
            titleText.text = mode == Mode.NewGame ? "NEW GAME — CHOOSE A SLOT" : "CONTINUE — CHOOSE A SAVE";
        Refresh();
        if (legend != null) legend.Refresh();
        FocusFirst();
    }

    public void Hide()
    {
        if (confirmDialog != null) confirmDialog.HideImmediate();
        if (panel != null) panel.SetActive(false);
    }

    // Wrap AFTER Refresh settles interactability (Continue greys empty slots → skipped), then land the focus on
    // the first pickable slot (Back if none) so any device can drive the screen immediately.
    private void FocusFirst()
    {
        UINavStyle.WireWrap(panel);
        int first = FirstPickable();
        UINavFocus.Focus(first >= 0 && _slotButtons[first] != null ? _slotButtons[first] : backButton);
    }

    // ── Input: navigation/submit are the EventSystem's; this adds Back, Delete and the dialog's Back ──
    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        if (confirmDialog != null && confirmDialog.IsOpen)
        {
            if (UINavFocus.CancelPressedThisFrame()) confirmDialog.Dismiss();   // B / Esc = the safe choice
            return;
        }

        if (UINavFocus.CancelPressedThisFrame()) { RaiseBack(); return; }   // B / Esc from any device

        int focused = FocusedSlot();
        bool canDelete = focused >= 0 && SaveSystem.HasSave(focused);
        if (deleteHint != null && deleteHint.activeSelf != canDelete) deleteHint.SetActive(canDelete);

        var input = PlayerInputRouter.SharedInput;   // either player's device
        if (canDelete && input != null && input.GetUIDeleteDown()) RequestDelete(focused);
    }

    // The slot whose card has the EventSystem focus, or -1 (Back / nothing focused).
    private int FocusedSlot()
    {
        var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (sel == null || _slotButtons == null) return -1;
        for (int i = 0; i < _slotButtons.Length; i++)
            if (_slotButtons[i] != null && _slotButtons[i].gameObject == sel) return i;
        return -1;
    }

    // NewGame: every valid slot is pickable. Continue: only slots holding a LOADABLE save (stale v1 / corrupt = greyed).
    private bool IsPickable(int slot) =>
        SaveSystem.IsValidSlot(slot) && (_mode == Mode.NewGame || SaveSystem.HasLoadableSave(slot));

    private int FirstPickable()
    {
        int n = SlotCount;
        for (int i = 0; i < n; i++) if (IsPickable(i)) return i;
        return -1;
    }

    private int SlotCount => _slotButtons != null ? _slotButtons.Length : 0;

    // ── Pick ───────────────────────────────────────────────────
    private void OnSlotClicked(int slot)
    {
        if (confirmDialog != null && confirmDialog.IsOpen) return;
        if (!IsPickable(slot)) { Status("That slot is empty."); return; }

        // New Game into an occupied slot: confirm the overwrite first.
        if (_mode == Mode.NewGame && SaveSystem.HasSave(slot))
        {
            Confirm(slot, $"Overwrite Slot {slot + 1}?",
                    $"{Describe(slot)}\nwill be replaced by a new game. This can't be undone.",
                    "Overwrite", ConfirmOverwrite);
            return;
        }

        Commit(slot);
    }

    private void ConfirmOverwrite()
    {
        int slot = _pendingSlot;
        _pendingSlot = -1;
        if (slot >= 0) Commit(slot);
    }

    private void Commit(int slot)
    {
        // Record only — SaveService (Persistent) isn't loaded yet; it applies this in Awake (SessionSetup handoff).
        if (_mode == Mode.Continue)
        {
            if (!SaveSystem.HasLoadableSave(slot)) { Status("Couldn't read that save."); return; }
            SessionSetup.SetMode(SessionSetup.BootMode.Continue, slot);
        }
        else
        {
            SessionSetup.SetMode(SessionSetup.BootMode.NewGame, slot);
        }
        SlotChosen?.Invoke(slot);
    }

    // ── Delete ─────────────────────────────────────────────────
    private void RequestDelete(int slot) =>
        Confirm(slot, $"Delete Slot {slot + 1}?",
                $"{Describe(slot)}\nwill be deleted. This can't be undone.",
                "Delete", ConfirmDelete);

    private void ConfirmDelete()
    {
        int slot = _pendingSlot;
        _pendingSlot = -1;
        if (slot < 0) return;

        SaveSystem.Delete(slot);
        Refresh();
        Status($"Slot {slot + 1} deleted.");

        // Interactability may have changed (Continue greys the now-empty slot) → re-wire, and keep focus sensible.
        UINavStyle.WireWrap(panel);
        if (IsPickable(slot) && _slotButtons[slot] != null) UINavFocus.Focus(_slotButtons[slot]);
        else
        {
            int first = FirstPickable();
            UINavFocus.Focus(first >= 0 && _slotButtons[first] != null ? _slotButtons[first] : backButton);
            if (first < 0 && _mode == Mode.Continue) Status("No saves left.");
        }
    }

    // ── Dialog ─────────────────────────────────────────────────
    private void Confirm(int slot, string title, string message, string confirmLabel, Action onConfirm)
    {
        if (confirmDialog == null)
        {
            // Fail loud but stay usable: without the dialog the action simply happens (logged).
            Debug.LogError("[SaveSlotScreen] confirmDialog unwired — acting without confirmation.", this);
            _pendingSlot = slot;
            onConfirm();
            return;
        }
        _pendingSlot = slot;
        confirmDialog.Show(title, message, confirmLabel, "Cancel", onConfirm, OnConfirmCancelled);
        UINavStyle.WireWrap(confirmDialog.ButtonRow, UINavStyle.WrapAxis.Horizontal);   // pad stays on Yes/No
    }

    private void OnConfirmCancelled()
    {
        int slot = _pendingSlot;
        _pendingSlot = -1;
        if (slot >= 0 && slot < SlotCount && _slotButtons[slot] != null) UINavFocus.Focus(_slotButtons[slot]);
    }

    private void RaiseBack() => BackRequested?.Invoke();

    // ── Presentation ───────────────────────────────────────────
    private void Refresh()
    {
        int n = SlotCount;
        for (int i = 0; i < n; i++)
        {
            if (_slotButtons[i] != null) _slotButtons[i].interactable = IsPickable(i);
            if (_slotLabels != null && i < _slotLabels.Length && _slotLabels[i] != null)
                _slotLabels[i].text = LabelFor(i);
        }
        if (statusText != null)
            statusText.text = _mode == Mode.Continue ? "Pick a save to resume" : "Pick a slot to start in";
    }

    private void Status(string msg) { if (statusText != null) statusText.text = msg; }

    private string LabelFor(int slot)
    {
        var sb = new StringBuilder();
        sb.Append("SLOT ").Append(slot + 1).Append('\n');

        var data = SaveSystem.Peek(slot);
        if (data == null || data.IsEmpty)
            sb.Append(_mode == Mode.NewGame ? "Empty — start here" : "Empty");
        else
            sb.Append(AreaNameOf(data)).Append('\n').Append(FormatTime(data.savedAtUtc));
        return sb.ToString();
    }

    // "Slot 2 (Park, 2026-09-24 18:03)" for the dialog message.
    private string Describe(int slot)
    {
        var data = SaveSystem.Peek(slot);
        if (data == null || data.IsEmpty) return $"Slot {slot + 1}";
        return $"Slot {slot + 1} ({AreaNameOf(data)}, {FormatTime(data.savedAtUtc)})";
    }

    // The saved display name; saves written before it existed fall back to a name derived from the area id.
    private static string AreaNameOf(GameSaveData data) =>
        !string.IsNullOrWhiteSpace(data.areaName) ? data.areaName : WorldLocationSO.PrettifyId(data.areaId);

    private static string FormatTime(string isoUtc)
    {
        return DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : isoUtc;
    }
}
