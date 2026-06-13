using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pause menu. ESC key opens/closes.
/// Priority order when ESC pressed:
///   1. SkillPreviewModal open → close modal only
///   2. Settings panel open → close settings only
///   3. Pause menu open → close pause menu
///   4. Nothing open → open pause menu
///
/// SETUP:
///   Add to a canvas (Screen Space Overlay, sort 25).
///   PauseRoot disabled by default.
///   SettingsPanel child of PauseRoot — also disabled by default.
///
/// HIERARCHY:
///   PauseMenuController (this script)
///     └── PauseRoot
///           ├── DimPanel      (full screen Image, alpha 0.6)
///           ├── MenuCard      (centred panel)
///           │     ├── ResumeButton
///           │     ├── SettingsButton
///           │     └── ExitButton
///           └── SettingsPanel (child — SettingsMenuController on this)
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject _pauseRoot;
    [SerializeField] private GameObject _settingsPanel;

    [Header("Buttons")]
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _exitButton;

    public bool IsPauseOpen => _pauseRoot.activeSelf;
    public bool IsSettingsOpen => _settingsPanel != null && _settingsPanel.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _pauseRoot.SetActive(false);
        _settingsPanel?.SetActive(false);

        _resumeButton?.onClick.AddListener(Resume);
        _settingsButton?.onClick.AddListener(OpenSettings);
        _exitButton?.onClick.AddListener(ExitGame);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // Centralised ESC arbiter — each press closes exactly one layer (priority: highest first)
        if (TutorialOverlayController.Instance != null && TutorialOverlayController.Instance.IsOpen)
        {
            TutorialOverlayController.Instance.TriggerContinue();
            return;
        }

        if (SkillPreviewModal.Instance != null && SkillPreviewModal.Instance.IsOpen)
        {
            SkillPreviewModal.Instance.Close();
            return;
        }

        if (IsSettingsOpen)
        {
            CloseSettings();
            return;
        }

        if (IsPauseOpen)
        {
            Resume();
            return;
        }

        if (SkillTreeUI.Instance != null && SkillTreeUI.Instance.IsOpen)
        {
            SkillTreeUI.Instance.Close();
            return;
        }

        OpenPause();
    }

    // ── Public API ────────────────────────────────────────────
    public void OpenPause()
    {
        _pauseRoot.SetActive(true);
        TimeScaleService.Instance?.Request(this, 0f);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        _settingsPanel?.SetActive(false);
        _pauseRoot.SetActive(false);
        TimeScaleService.Instance?.Release(this);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void OpenSettings()
    {
        _settingsPanel?.SetActive(true);
    }

    public void CloseSettings()
    {
        _settingsPanel?.SetActive(false);
    }

    public void ExitGame()
    {
        TimeScaleService.Instance?.ReleaseAll();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}