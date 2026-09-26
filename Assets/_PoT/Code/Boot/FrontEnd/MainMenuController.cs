using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Couch M2 — Start Menu shell (greybox). New Game / Continue / Options / Report a Problem / Exit. Lives in
/// FrontEnd; <see cref="FrontEndFlowController"/> shows/hides it and listens to its events.
///
/// <para><b>Continue</b> is enabled only when at least one save slot exists on disk (re-checked every
/// <see cref="Show"/>, since a save may have appeared since the last visit). Pressing it raises
/// <see cref="ContinueRequested"/>; the flow then opens the save-slot screen in Continue mode. This is cheap
/// flow-test UI, not final art.</para>
/// </summary>
[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;   // enabled iff any save slot exists (see Show)
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button exitButton;

    [Header("Bug reports (game.md §27.2)")]
    [SerializeField] private Button reportButton;
    [Tooltip("The line beside the Report button after a crash (text + a Not Now button). Shown only while the last " +
             "crash's report is pending; no popup at launch.")]
    [SerializeField] private GameObject crashNotice;
    [SerializeField] private Button dismissCrashButton;
    [Tooltip("The Report button's colour while the crash notice shows.")]
    [SerializeField] private Color crashHighlightColor = new Color(0.72f, 0.36f, 0.16f, 1f);   // deep amber: white label stays readable

    /// <summary>Raised when New Game is pressed.</summary>
    public event Action NewGameRequested;
    /// <summary>Raised when Continue is pressed (only reachable when a save exists).</summary>
    public event Action ContinueRequested;
    /// <summary>Raised when Options is pressed (wire to the settings UI later).</summary>
    public event Action OptionsRequested;
    /// <summary>Raised when Report a Problem is pressed. True when the crash notice was showing.</summary>
    public event Action<bool> ReportRequested;

    private Image _reportImage;
    private Color _reportNormalColor;
    private bool _crashNoticeShown;

    private void Awake()
    {
        if (newGameButton  != null) newGameButton.onClick.AddListener(RaiseNewGame);
        if (continueButton != null) continueButton.onClick.AddListener(RaiseContinue);
        if (optionsButton  != null) optionsButton.onClick.AddListener(RaiseOptions);
        if (exitButton     != null) exitButton.onClick.AddListener(Quit);
        if (reportButton   != null) reportButton.onClick.AddListener(RaiseReport);
        if (dismissCrashButton != null) dismissCrashButton.onClick.AddListener(DismissCrashNotice);

        if (reportButton != null)
        {
            _reportImage = reportButton.targetGraphic as Image;
            if (_reportImage != null) _reportNormalColor = _reportImage.color;
        }

        // Item 1 (controller nav): make the menu buttons pad-traversable + give them a visible focus highlight.
        UINavStyle.Apply(panel);
    }

    private void OnDestroy()
    {
        if (newGameButton  != null) newGameButton.onClick.RemoveListener(RaiseNewGame);
        if (continueButton != null) continueButton.onClick.RemoveListener(RaiseContinue);
        if (optionsButton  != null) optionsButton.onClick.RemoveListener(RaiseOptions);
        if (exitButton     != null) exitButton.onClick.RemoveListener(Quit);
        if (reportButton   != null) reportButton.onClick.RemoveListener(RaiseReport);
        if (dismissCrashButton != null) dismissCrashButton.onClick.RemoveListener(DismissCrashNotice);
    }

    public void Show()
    {
        // Re-evaluate Continue each time the menu shows — a save may have been written since the last visit.
        // BUG-118: the art is authored in its normal (lit) state; "no save" dims the WHOLE button (bg + label) —
        // the Button's ColorTint alone only tints the background, leaving a bright label that reads as clickable.
        if (continueButton != null)
        {
            bool canContinue = AnySaveExists();
            continueButton.interactable = canContinue;
            var cg = continueButton.GetComponent<CanvasGroup>();
            if (cg == null) cg = continueButton.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = canContinue ? 1f : 0.35f;
        }
        if (panel != null) panel.SetActive(true);
        RefreshCrashNotice();   // before the wrap wiring: Not Now joins the cycle only while it shows

        // P-C (controller nav): wire wrap-around AFTER Continue's interactable is set this showing, so a
        // disabled Continue is skipped and the cycle is New Game↕Options↕Exit (Down past Exit → New Game).
        UINavStyle.WireWrap(panel);

        // Item 1 (controller nav): land the controller focus on New Game so either pad can drive the menu
        // immediately. Mouse is unaffected (null-safe no-op if no EventSystem). See UINavFocus.
        UINavFocus.Focus(newGameButton);
    }

    public void Hide() { if (panel != null) panel.SetActive(false); }

    private static bool AnySaveExists()
    {
        // FrontEnd runs BEFORE Persistent loads, so SaveService doesn't exist here (the old SaveService gate was
        // always null → Continue was permanently greyed). Gate on the disk instead: Continue lights up only when a
        // slot holds a save that would actually LOAD (current version, parseable) — a stale v1 / corrupt file never
        // enables a Continue that then fails.
        for (int i = 0; i < SaveSystem.SlotCount; i++) if (SaveSystem.HasLoadableSave(i)) return true;
        return false;
    }

    // After a crash (or forced close) the Report button is highlighted with one line beside it, until the report is
    // sent or the player dismisses it. No popup at launch: it would read as "crashed again" (game.md §27.2).
    private void RefreshCrashNotice()
    {
        _crashNoticeShown = PoT.Diagnostics.CrashMarker.HasPendingCrash && reportButton != null;
        if (crashNotice != null) crashNotice.SetActive(_crashNoticeShown);
        if (_reportImage != null) _reportImage.color = _crashNoticeShown ? crashHighlightColor : _reportNormalColor;
    }

    private void DismissCrashNotice()
    {
        PoT.Diagnostics.CrashMarker.ClearPendingCrash();
        PoTLog.Crumb(PoTCrumb.Report, "crash notice dismissed");
        RefreshCrashNotice();
        UINavStyle.WireWrap(panel);
        UINavFocus.Focus(reportButton);
    }

    private void RaiseNewGame()  => NewGameRequested?.Invoke();
    private void RaiseContinue() => ContinueRequested?.Invoke();
    private void RaiseOptions()  => OptionsRequested?.Invoke();
    private void RaiseReport()   => ReportRequested?.Invoke(_crashNoticeShown);

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
