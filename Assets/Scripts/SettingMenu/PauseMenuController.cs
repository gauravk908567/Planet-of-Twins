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
    [Tooltip("F7 — clears all input binding overrides back to authored defaults. Safe today " +
             "(no rebinding UI yet — F6). Optional; leave unwired if the button doesn't exist.")]
    [SerializeField] private Button _restoreKeybindsButton;

    // F7 — pause snapshot priority. Nothing else requests a snapshot yet; GameOver (enum 3)
    // must outrank pause, Setsuna (enum 2) sits below. Priority here is the arbiter's own axis.
    private const int PauseSnapshotPriority = 50;

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
        _restoreKeybindsButton?.onClick.AddListener(RestoreDefaultKeybinds);
    }

    // P13: ESC comes through IInputProvider (Input System) — raw Input.* is banned. Same-scene
    // singleton resolve in Start (R4 line); the priority chain below is unchanged.
    private IInputProvider _input;

    private void Start()
    {
        _input = PlayerInputRouter.SharedInput;   // M0: shared-UI seam (falls back to TwinInputReader.Instance)
        if (_input == null)
            Debug.LogError("[PauseMenuController] PlayerInputRouter.SharedInput unresolved — ESC dead. Is Persistent loaded?", this);
    }

    private void Update()
    {
        if (_input == null || !_input.GetPauseDown()) return;

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
        // F4/F7 — halt gameplay audio (owner set, sole AudioListener.pause writer) + duck to the
        // Paused mixer snapshot. UI/button sounds must use AudioManager.PlayUI to stay audible.
        AudioManager.Instance?.SetPaused(this);
        AudioManager.Instance?.RequestSnapshot(this, AudioSnapshotId.Paused, PauseSnapshotPriority);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void Resume()
    {
        _settingsPanel?.SetActive(false);
        _pauseRoot.SetActive(false);
        TimeScaleService.Instance?.Release(this);
        AudioManager.Instance?.ReleasePaused(this);
        AudioManager.Instance?.ReleaseSnapshot(this);
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

    // F7 — Restore Default Keybinds. Clears all binding overrides on the shared action asset
    // via the input provider, then refreshes any on-screen prompt glyphs. InputPromptView is a
    // scene-scoped non-singleton, so a sweep is the sanctioned lookup here (R4).
    public void RestoreDefaultKeybinds()
    {
        _input?.ResetBindingsToDefault();

        var prompts = FindObjectsByType<InputPromptView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var p in prompts) p.Refresh();
    }

    public void ExitGame()
    {
        TimeScaleService.Instance?.ReleaseAll();
        AudioManager.Instance?.ReleasePaused(this);
        AudioManager.Instance?.ReleaseSnapshot(this);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}