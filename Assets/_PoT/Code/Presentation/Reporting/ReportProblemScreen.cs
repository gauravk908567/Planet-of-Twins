using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using PoT.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The "Report a Problem" screen (game.md §27.2–27.3). One prefab, placed twice: in FrontEnd.unity (Main Menu copy,
/// Menu Context ON) and in Persistent.unity (in-game copy, opened from the pause screen's Support row). The two scenes
/// never coexist, so each scene's copy is <see cref="Instance"/>.
///
/// <para>Consent first: nothing is built until the player presses Send, and the screen lists what a report holds. One
/// input is required: a description OR a category chip (pad players have no keyboard; the user chose "text or chip"
/// over the spec's keyboard rule, 2026-09-26). The contact email is optional. A cancelled draft is kept until it's
/// sent.</para>
///
/// <para>Send collects the report on the main thread (<see cref="ReportCollector.Collect"/>), writes the zip on a
/// worker thread, then shows the report id. Phase 3 keeps the zip on this PC; phase 4 adds the upload.</para>
///
/// <para>Back (Esc, pad B or Start) arrives through <see cref="HandleBack"/> from the scene's arbiter
/// (PauseMenuController in-game, FrontEndFlowController on the Main Menu). The screen runs while the game is paused
/// (timeScale 0), so nothing here uses scaled time.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class ReportProblemScreen : MonoBehaviour
{
    [Serializable]
    public sealed class CategoryChip
    {
        public string category;
        public Toggle toggle;
    }

    /// <summary>The quick categories (game.md §27.8), in chip order.</summary>
    public static readonly string[] Categories = { "Crash", "Stuck", "Visual", "Controls", "Other" };
    /// <summary>Picked for the player when the screen opens from the Main Menu's crash notice.</summary>
    public const string CrashCategory = "Crash";

    private const int DescriptionLimit = 2000;
    private const int ContactLimit = 120;
    private const string NeedInputHint = "Describe the problem or pick a type to send.";

    public static ReportProblemScreen Instance { get; private set; }

    [Tooltip("ON for the Main Menu copy (FrontEnd): no screenshot (there's no gameplay frame) and every save on disk " +
             "is attached. OFF for the in-game copy (Persistent).")]
    [SerializeField] private bool _menuContext;

    [Header("Screen")]
    [Tooltip("The toggled visual root. Inactive by default; Open() shows it.")]
    [SerializeField] private GameObject _screenRoot;
    [SerializeField] private GameObject _formRoot;
    [SerializeField] private GameObject _resultRoot;

    [Header("Form")]
    [SerializeField] private TMP_InputField _description;
    [SerializeField] private List<CategoryChip> _chips = new List<CategoryChip>();
    [SerializeField] private TMP_InputField _contact;
    [SerializeField] private TMP_Text _includedText;
    [SerializeField] private Button _privacyButton;
    [Tooltip("The privacy notice page (game.md §27.5). Empty = the link is hidden.")]
    [SerializeField] private string _privacyNoticeUrl = "";
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private Button _sendButton;

    [Header("Result")]
    [SerializeField] private TMP_Text _resultTitle;
    [SerializeField] private TMP_Text _resultText;
    [SerializeField] private Button _openFolderButton;
    [SerializeField] private Button _doneButton;

    /// <summary>Raised after the screen closes.</summary>
    public event Action Closed;

    public bool IsOpen => _screenRoot != null && _screenRoot.activeSelf;

    private enum State { Form, Busy, Result }

    private State _state;
    private bool _valid;
    private bool _includeCrash;
    private bool _includeScreenshot;
    private bool _editingLastFrame;   // an input field was being typed in last frame (Esc may have just ended it)
    private string _statusOverride;   // an error to show instead of the hint, until the form changes
    private string _savedFolder;
    private GameObject _returnFocus;
    private readonly List<Selectable> _chipBuffer = new List<Selectable>(8);

    // ── Lifetime ──────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Same rule as SettingsScreenController: if both scenes are open by hand in the Editor, the in-game copy
            // wins; the menu copy never makes the in-game one destroy itself.
            if (!(Instance._menuContext && !_menuContext)) { Destroy(gameObject); return; }
        }
        Instance = this;

        _valid = _screenRoot != null && _formRoot != null && _resultRoot != null && _description != null &&
                 _contact != null && _cancelButton != null && _sendButton != null && _doneButton != null;
        if (!_valid)
        {
            Debug.LogError("[ReportProblemScreen] UI references are missing; the report screen is disabled. " +
                           "Rebuild the prefab (Planet of Twins Tools ▸ Diagnostics ▸ Build Report Screen Prefab).", this);
            enabled = false;
            return;
        }

        ConfigureField(_description, DescriptionLimit);
        ConfigureField(_contact, ContactLimit);
        _description.onValueChanged.AddListener(OnFormChanged);
        foreach (var chip in _chips)
            if (chip.toggle != null) chip.toggle.onValueChanged.AddListener(OnChipChanged);
        _cancelButton.onClick.AddListener(Close);
        _sendButton.onClick.AddListener(OnSendClicked);
        _doneButton.onClick.AddListener(Close);
        if (_privacyButton != null) _privacyButton.onClick.AddListener(OnPrivacyClicked);
        if (_openFolderButton != null) _openFolderButton.onClick.AddListener(OnOpenFolderClicked);

        UINavStyle.Apply(_screenRoot);   // visible focus tint; navigation itself is wired explicitly below
        _screenRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (!_valid) return;
        _description.onValueChanged.RemoveListener(OnFormChanged);
        foreach (var chip in _chips)
            if (chip.toggle != null) chip.toggle.onValueChanged.RemoveListener(OnChipChanged);
        _cancelButton.onClick.RemoveListener(Close);
        _sendButton.onClick.RemoveListener(OnSendClicked);
        _doneButton.onClick.RemoveListener(Close);
        if (_privacyButton != null) _privacyButton.onClick.RemoveListener(OnPrivacyClicked);
        if (_openFolderButton != null) _openFolderButton.onClick.RemoveListener(OnOpenFolderClicked);
    }

    private void LateUpdate()
    {
        _editingLastFrame = IsOpen && (_description.isFocused || _contact.isFocused);
    }

    // ── Open / close ──────────────────────────────────────────────────
    /// <summary>Shows the form. <paramref name="fromCrashNotice"/> (the Main Menu's crash line) picks "Crash" for the
    /// player, so Send is one press away. Returns false if the screen can't open (broken prefab: logged in Awake).</summary>
    public bool Open(bool fromCrashNotice = false)
    {
        if (!_valid) return false;
        if (IsOpen) return true;

        _returnFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        _includeCrash = CrashMarker.HasPendingCrash;
        _includeScreenshot = !_menuContext && ScreenshotCapture.HasCapture;
        if (fromCrashNotice && _includeCrash && SelectedCategory() == null) SelectCategory(CrashCategory);

        _screenRoot.SetActive(true);
        _statusOverride = null;
        ShowForm();
        FocusFirst();
        PoTLog.Crumb(PoTCrumb.Report, fromCrashNotice ? "report screen opened (crash notice)" : "report screen opened");
        return true;
    }

    /// <summary>Closes the screen, keeping an unsent draft. Ignored while the zip is being written.</summary>
    public void Close()
    {
        if (!IsOpen || _state == State.Busy) return;
        _description.DeactivateInputField();
        _contact.DeactivateInputField();
        _screenRoot.SetActive(false);
        if (_returnFocus != null && _returnFocus.activeInHierarchy) UINavFocus.Focus(_returnFocus);
        _returnFocus = null;
        Closed?.Invoke();
    }

    /// <summary>Back from the scene's arbiter: first ends typing in a field, then closes.</summary>
    public void HandleBack()
    {
        if (!IsOpen || _state == State.Busy) return;
        if (_state == State.Form && (_editingLastFrame || _description.isFocused || _contact.isFocused))
        {
            _description.DeactivateInputField();
            _contact.DeactivateInputField();
            return;
        }
        Close();
    }

    // ── Form ──────────────────────────────────────────────────────────
    private void ShowForm()
    {
        _state = State.Form;
        _formRoot.SetActive(true);
        _resultRoot.SetActive(false);
        SetFormInteractable(true);
        if (_includedText != null) _includedText.text = BuildIncludedText();
        if (_privacyButton != null) _privacyButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(_privacyNoticeUrl));
        RefreshSend();
    }

    // A keyboard player can type straight away; a pad player lands on the chips (a pad can't type on plain Windows).
    private void FocusFirst()
    {
        bool pad = LastUsedDeviceTracker.LastUsed == InputDeviceKind.Gamepad;
        var firstChip = FirstChip();
        if (pad && firstChip != null)
        {
            UINavFocus.Focus(firstChip);
            return;
        }
        UINavFocus.Focus(_description);
        _description.ActivateInputField();
    }

    private string BuildIncludedText()
    {
        var sb = new StringBuilder(512);
        sb.Append("<b>What's included</b>\n");
        Bullet(sb, "Logs from this session and the previous one");
        Bullet(sb, "Your PC's specs: system, processor, graphics card, memory, display");
        Bullet(sb, "Your game settings, controllers and button bindings");
        Bullet(sb, _menuContext ? "Your save files" : "Your save file and where you are in the game");
        if (_includeScreenshot) Bullet(sb, "A screenshot from when you paused");
        if (_includeCrash) Bullet(sb, "Crash data from last time (the crash file itself can't have your user name removed)");
        sb.Append("<size=50%>\n</size><size=85%>We remove your Windows user name, your PC's name and email addresses " +
                  "from the logs.</size>");
        return sb.ToString();
    }

    private static void Bullet(StringBuilder sb, string line) => sb.Append("•  ").Append(line).Append('\n');

    private bool CanSend() => !string.IsNullOrWhiteSpace(_description.text) || SelectedCategory() != null;

    private void OnFormChanged(string _)
    {
        _statusOverride = null;
        RefreshSend();
    }

    private void OnChipChanged(bool _)
    {
        _statusOverride = null;
        RefreshSend();
    }

    private void RefreshSend()
    {
        if (_state != State.Form) return;
        bool canSend = CanSend();
        _sendButton.interactable = canSend;
        if (_statusText != null) _statusText.text = _statusOverride ?? (canSend ? string.Empty : NeedInputHint);
        WireFormNavigation();
    }

    private void SetFormInteractable(bool on)
    {
        _description.interactable = on;
        _contact.interactable = on;
        foreach (var chip in _chips)
            if (chip.toggle != null) chip.toggle.interactable = on;
        _cancelButton.interactable = on;
        _sendButton.interactable = on && CanSend();
        if (_privacyButton != null) _privacyButton.interactable = on;
    }

    private string SelectedCategory()
    {
        foreach (var chip in _chips)
            if (chip.toggle != null && chip.toggle.isOn) return chip.category;
        return null;
    }

    private void SelectCategory(string category)
    {
        foreach (var chip in _chips)
            if (chip.toggle != null && chip.category == category) chip.toggle.isOn = true;
    }

    private Selectable FirstChip()
    {
        foreach (var chip in _chips)
            if (chip.toggle != null && chip.toggle.gameObject.activeInHierarchy) return chip.toggle;
        return null;
    }

    // ── Controller navigation ─────────────────────────────────────────
    // Explicit, so a pad can't wander onto the settings screen hidden under this one:
    //   What happened? ↕ chips (← →) ↕ email ↕ [privacy link] ↕ Cancel ← → Send
    private void WireFormNavigation()
    {
        _chipBuffer.Clear();
        foreach (var chip in _chips)
            if (chip.toggle != null && chip.toggle.gameObject.activeInHierarchy) _chipBuffer.Add(chip.toggle);
        Selectable firstChip = _chipBuffer.Count > 0 ? _chipBuffer[0] : null;
        Selectable privacy = _privacyButton != null && _privacyButton.gameObject.activeSelf ? _privacyButton : null;
        Selectable bottom = _sendButton.interactable ? _sendButton : _cancelButton;
        Selectable aboveBottom = privacy != null ? privacy : _contact;

        SetNav(_description, up: null, down: firstChip != null ? firstChip : _contact, left: null, right: null);
        for (int i = 0; i < _chipBuffer.Count; i++)
            SetNav(_chipBuffer[i], up: _description, down: _contact,
                   left: i > 0 ? _chipBuffer[i - 1] : null,
                   right: i < _chipBuffer.Count - 1 ? _chipBuffer[i + 1] : null);
        SetNav(_contact, up: firstChip != null ? firstChip : _description, down: privacy != null ? privacy : bottom,
               left: null, right: null);
        if (privacy != null) SetNav(privacy, up: _contact, down: bottom, left: null, right: null);
        SetNav(_cancelButton, up: aboveBottom, down: null, left: null, right: _sendButton.interactable ? _sendButton : null);
        SetNav(_sendButton, up: aboveBottom, down: null, left: _cancelButton, right: null);
    }

    private void WireResultNavigation()
    {
        bool folder = _openFolderButton != null && _openFolderButton.gameObject.activeSelf;
        SetNav(_doneButton, up: null, down: null, left: folder ? _openFolderButton : null, right: null);
        if (folder) SetNav(_openFolderButton, up: null, down: null, left: null, right: _doneButton);
    }

    private static void SetNav(Selectable s, Selectable up, Selectable down, Selectable left, Selectable right)
    {
        if (s == null) return;
        s.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right,
        };
    }

    // ── Send ──────────────────────────────────────────────────────────
    private void OnSendClicked()
    {
        if (_state != State.Form || !CanSend()) return;
        StartCoroutine(SendRoutine());
    }

    private IEnumerator SendRoutine()
    {
        _state = State.Busy;
        _description.DeactivateInputField();
        _contact.DeactivateInputField();
        SetFormInteractable(false);
        if (_statusText != null) _statusText.text = "Building your report…";
        yield return null;   // let that line draw before the main-thread collect

        ReportPackage package;
        Task<string> write;
        try
        {
            var request = new ReportRequest
            {
                Description = _description.text.Trim(),
                Category = SelectedCategory() ?? string.Empty,
                Contact = _contact.text.Trim(),
                Screenshot = _includeScreenshot ? ScreenshotCapture.EncodeJpg() : null,
                Sections = PoTReportSections.All,
                IncludePendingCrash = _includeCrash,
            };
            package = ReportCollector.Collect(request);
            write = ReportCollector.WriteZipAsync(package);
        }
        catch (Exception e)
        {
            Fail(e);
            yield break;
        }

        while (!write.IsCompleted) yield return null;   // frame-driven, so it runs while paused
        if (write.IsFaulted || write.IsCanceled)
        {
            Fail(write.Exception != null ? write.Exception.GetBaseException() : new OperationCanceledException());
            yield break;
        }
        Succeed(package, write.Result);
    }

    private void Succeed(ReportPackage package, string zipPath)
    {
        if (_includeCrash) CrashMarker.ClearPendingCrash();   // it's in this report now
        _savedFolder = Path.GetDirectoryName(zipPath);
        _description.text = string.Empty;   // the contact stays, for the next report this session
        foreach (var chip in _chips)
            if (chip.toggle != null) chip.toggle.SetIsOnWithoutNotify(false);
        PoTLog.Crumb(PoTCrumb.Report, $"report {package.ReportId} saved");

        _state = State.Result;
        _formRoot.SetActive(false);
        _resultRoot.SetActive(true);
        if (_resultTitle != null) _resultTitle.text = "Report saved";
        if (_resultText != null)
            _resultText.text = $"Thank you! Your report ID is <b>{package.ReportId}</b>.\n\n" +
                               "It's saved on this PC for now. Sending it to us straight from the game " +
                               "comes in a later update.";
        if (_openFolderButton != null) _openFolderButton.gameObject.SetActive(!string.IsNullOrEmpty(_savedFolder));
        WireResultNavigation();
        UINavFocus.Focus(_doneButton);
    }

    private void Fail(Exception e)
    {
        Debug.LogError($"[ReportProblemScreen] The report could not be saved.\n{e}", this);
        _statusOverride = $"The report couldn't be saved ({e.Message}). Please try again.";
        ShowForm();
        UINavFocus.Focus(_sendButton.interactable ? _sendButton : (Selectable)_cancelButton);
    }

    // ── Links ─────────────────────────────────────────────────────────
    private void OnPrivacyClicked()
    {
        if (!string.IsNullOrWhiteSpace(_privacyNoticeUrl)) Application.OpenURL(_privacyNoticeUrl);
    }

    private void OnOpenFolderClicked()
    {
        if (!string.IsNullOrEmpty(_savedFolder) && Directory.Exists(_savedFolder)) Application.OpenURL(_savedFolder);
    }

    // ── Helpers ───────────────────────────────────────────────────────
    private static void ConfigureField(TMP_InputField field, int limit)
    {
        field.characterLimit = limit;
        field.restoreOriginalTextOnEscape = false;   // Esc ends typing; it must never wipe what was written
        field.shouldActivateOnSelect = false;        // a pad moving over the field doesn't trap it in typing mode
    }
}
