using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Couch M2 — save-slot select (greybox). Shown between the Start Menu and Character Select in BOTH front-end
/// modes: <b>New Game</b> (pick a slot to save into) and <b>Continue</b> (pick an existing save to resume).
///
/// <para><b>Mode:</b>
/// <list type="bullet">
/// <item><b>NewGame</b> — every slot pickable; an occupied slot arms a two-press overwrite guard before it
/// commits (greybox — no confirm dialog GO).</item>
/// <item><b>Continue</b> — only occupied slots pickable (empty ones greyed); picking one stages that save for
/// the boot path.</item>
/// </list></para>
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
/// Submit (A / Enter) picks, UI Cancel (B / Esc) = Back. (Replaced a P1-only stick index that drew no highlight.)</para>
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

    private Button[] _slotButtons;
    private TMP_Text[] _slotLabels;

    /// <summary>Raised with the chosen slot index once the pick has been committed to <see cref="SaveService"/>.</summary>
    public event Action<int> SlotChosen;
    /// <summary>Raised when Back is pressed (return to the Start Menu).</summary>
    public event Action BackRequested;

    private Mode _mode;
    private int _armedOverwrite = -1;   // NewGame two-press guard: the occupied slot currently armed (-1 = none)

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

        // Item 1 (controller nav): pad/keyboard-traversable cards + the shared focus glow.
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
        _armedOverwrite = -1;
        if (panel != null) panel.SetActive(true);
        if (titleText != null)
            titleText.text = mode == Mode.NewGame ? "NEW GAME — CHOOSE A SLOT" : "CONTINUE — CHOOSE A SAVE";
        Refresh();

        // Wrap AFTER Refresh settles interactability (Continue greys empty slots → skipped), then land the focus on
        // the first pickable slot (Back if none) so any device can drive the screen immediately.
        UINavStyle.WireWrap(panel);
        int first = FirstPickable();
        UINavFocus.Focus(first >= 0 && _slotButtons[first] != null ? _slotButtons[first] : backButton);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    // ── Input: navigation/submit are the EventSystem's; this only adds Back + the overwrite-guard reset ──
    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        if (UINavFocus.CancelPressedThisFrame()) { RaiseBack(); return; }   // B / Esc from any device

        // Moving the focus off the armed slot cancels the pending overwrite (the old index-nav did this too).
        if (_armedOverwrite >= 0)
        {
            var sel = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
            var armed = _slotButtons[_armedOverwrite];
            if (sel != null && armed != null && sel != armed.gameObject) { _armedOverwrite = -1; Refresh(); }
        }
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

    // ── Commit ─────────────────────────────────────────────────
    private void OnSlotClicked(int slot)
    {
        if (!IsPickable(slot)) { Status("That slot is empty."); return; }

        // Occupied + New Game: require a second press on the same slot before overwriting (greybox guard).
        if (_mode == Mode.NewGame && SaveSystem.HasSave(slot) && _armedOverwrite != slot)
        {
            _armedOverwrite = slot;
            Refresh();
            Status("Occupied slot — press again to overwrite.");
            return;
        }

        Commit(slot);
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
            statusText.text = _mode == Mode.Continue
                ? "Pick a save to resume"
                : "Pick a slot (an occupied slot needs a second press to overwrite)";
    }

    private void Status(string msg) { if (statusText != null) statusText.text = msg; }

    private string LabelFor(int slot)
    {
        var sb = new StringBuilder();
        sb.Append("SLOT ").Append(slot + 1).Append('\n');

        var data = SaveSystem.Peek(slot);
        if (data == null || data.IsEmpty)
        {
            sb.Append(_mode == Mode.NewGame ? "Empty — start here" : "Empty");
        }
        else
        {
            sb.Append(data.areaId).Append('\n').Append(FormatTime(data.savedAtUtc));
            if (_mode == Mode.NewGame && _armedOverwrite == slot) sb.Append("\nPress again to overwrite");
        }
        return sb.ToString();
    }

    private static string FormatTime(string isoUtc)
    {
        return DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : isoUtc;
    }
}
