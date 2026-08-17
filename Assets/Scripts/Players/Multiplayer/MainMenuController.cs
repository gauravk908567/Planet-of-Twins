using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Couch M2 — Start Menu shell (greybox). New Game / Continue / Options / Exit. Lives in Persistent;
/// <see cref="FrontEndFlowController"/> shows/hides it and listens to its events.
///
/// <para><b>Continue is a STUB</b> — disabled until a save-persistence milestone exists (no disk save
/// system today). This is cheap flow-test UI, not final art.</para>
/// </summary>
[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;   // STUB — disabled until saves land
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button exitButton;

    /// <summary>Raised when New Game is pressed.</summary>
    public event Action NewGameRequested;
    /// <summary>Raised when Options is pressed (wire to the settings UI later).</summary>
    public event Action OptionsRequested;

    private void Awake()
    {
        if (newGameButton != null) newGameButton.onClick.AddListener(RaiseNewGame);
        if (optionsButton != null) optionsButton.onClick.AddListener(RaiseOptions);
        if (exitButton    != null) exitButton.onClick.AddListener(Quit);
        if (continueButton != null) continueButton.interactable = false; // save persistence = later milestone
    }

    private void OnDestroy()
    {
        if (newGameButton != null) newGameButton.onClick.RemoveListener(RaiseNewGame);
        if (optionsButton != null) optionsButton.onClick.RemoveListener(RaiseOptions);
        if (exitButton    != null) exitButton.onClick.RemoveListener(Quit);
    }

    public void Show() { if (panel != null) panel.SetActive(true); }
    public void Hide() { if (panel != null) panel.SetActive(false); }

    private void RaiseNewGame() => NewGameRequested?.Invoke();
    private void RaiseOptions() => OptionsRequested?.Invoke();

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
