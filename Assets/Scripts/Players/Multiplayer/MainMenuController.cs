using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Couch M2 — Start Menu shell (greybox). New Game / Continue / Options / Exit. Lives in Persistent;
/// <see cref="FrontEndFlowController"/> shows/hides it and listens to its events.
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

    /// <summary>Raised when New Game is pressed.</summary>
    public event Action NewGameRequested;
    /// <summary>Raised when Continue is pressed (only reachable when a save exists).</summary>
    public event Action ContinueRequested;
    /// <summary>Raised when Options is pressed (wire to the settings UI later).</summary>
    public event Action OptionsRequested;

    private void Awake()
    {
        if (newGameButton  != null) newGameButton.onClick.AddListener(RaiseNewGame);
        if (continueButton != null) continueButton.onClick.AddListener(RaiseContinue);
        if (optionsButton  != null) optionsButton.onClick.AddListener(RaiseOptions);
        if (exitButton     != null) exitButton.onClick.AddListener(Quit);

        // Item 1 (controller nav): make the menu buttons pad-traversable + give them a visible focus highlight.
        UINavStyle.Apply(panel);
    }

    private void OnDestroy()
    {
        if (newGameButton  != null) newGameButton.onClick.RemoveListener(RaiseNewGame);
        if (continueButton != null) continueButton.onClick.RemoveListener(RaiseContinue);
        if (optionsButton  != null) optionsButton.onClick.RemoveListener(RaiseOptions);
        if (exitButton     != null) exitButton.onClick.RemoveListener(Quit);
    }

    public void Show()
    {
        // Re-evaluate Continue each time the menu shows — a save may have been written since the last visit.
        if (continueButton != null) continueButton.interactable = AnySaveExists();
        if (panel != null) panel.SetActive(true);

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
        // Feature gate: while save/Continue is dormant, Continue stays disabled even if a stale test-save file
        // lingers on disk (see SaveService.enableSaving — world-state contract deferred until post-couch).
        if (SaveService.Instance == null || !SaveService.Instance.SavingEnabled) return false;
        for (int i = 0; i < SaveSystem.SlotCount; i++) if (SaveSystem.HasSave(i)) return true;
        return false;
    }

    private void RaiseNewGame()  => NewGameRequested?.Invoke();
    private void RaiseContinue() => ContinueRequested?.Invoke();
    private void RaiseOptions()  => OptionsRequested?.Invoke();

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
