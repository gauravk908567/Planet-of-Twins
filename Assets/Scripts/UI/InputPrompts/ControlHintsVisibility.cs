using UnityEngine;

/// <summary>
/// Show/hide toggle for the ControlHints panel (H by default — the "UI/ToggleHints" action).
/// Lives on the ControlHints GO in Persistent (R9: screen-space HUD).
///
/// Hides every child hint row EXCEPT the toggle row itself (that row must stay visible or the
/// player can never find the way back), and flips the toggle row's label between
/// "Hide controls" / "Show controls" via <see cref="InputPromptView.SetAction"/>.
///
/// Input: through IInputProvider ONLY (raw Input.* is banned outside TwinInputReader).
/// R4: resolves TwinInputReader.Instance in Start, LogError + disable when unresolved.
/// Ungated + works while gameplay is frozen — hiding UI chrome is never gameplay.
/// </summary>
[DisallowMultipleComponent]
public class ControlHintsVisibility : MonoBehaviour
{
    [Tooltip("The hint row that stays visible while the rest are hidden (the 'press H' row). " +
             "Its label flips between the two texts below.")]
    [SerializeField] private InputPromptView _toggleRow;

    [SerializeField] private string _labelWhenShown = "Hide controls";
    [SerializeField] private string _labelWhenHidden = "Show controls";

    [Tooltip("Start with the hints visible.")]
    [SerializeField] private bool _visibleOnStart = true;

    private IInputProvider _input;
    private bool _visible;

    private void Start()
    {
        _input = TwinInputReader.Instance;
        if (_input == null)
        {
            Debug.LogError("[ControlHintsVisibility] No IInputProvider (TwinInputReader.Instance " +
                           "null) — H toggle dead. Is Persistent loaded? Disabling.", this);
            enabled = false;
            return;
        }

        if (_toggleRow == null)
            Debug.LogWarning("[ControlHintsVisibility] _toggleRow not assigned — all rows will " +
                             "hide together and the label cannot flip.", this);

        _visible = _visibleOnStart;
        Apply();
    }

    private void Update()
    {
        if (_input.GetHintsToggleDown())
        {
            _visible = !_visible;
            Apply();
        }
    }

    private void Apply()
    {
        Transform toggleT = _toggleRow != null ? _toggleRow.transform : null;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == toggleT) continue;

            // Rows the designer deactivated on purpose (retired hints) must stay off — only
            // rows that were visible in the shown state are driven by the toggle. We mark
            // driven rows by remembering nothing: a hidden panel turns rows OFF, a shown panel
            // turns rows ON only if they are not deliberately-retired. Deliberate retirement is
            // expressed by deactivating the row while the panel is SHOWN in the editor — those
            // rows are captured once below.
            if (_visible && _retired.Contains(child)) continue;
            child.gameObject.SetActive(_visible);
        }

        if (_toggleRow != null)
            _toggleRow.SetAction("ToggleHints", _visible ? _labelWhenShown : _labelWhenHidden);
    }

    // Rows that were inactive when the panel initialised = deliberately retired in the editor
    // (e.g. Hint_Ability / Hint_Teleport, removed 2026-07-21). The toggle never resurrects them.
    private readonly System.Collections.Generic.HashSet<Transform> _retired =
        new System.Collections.Generic.HashSet<Transform>();

    private void Awake()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.gameObject.activeSelf) _retired.Add(child);
        }
    }
}
