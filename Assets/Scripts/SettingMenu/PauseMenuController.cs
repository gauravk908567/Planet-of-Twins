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

    // Pause now opens the unified settings screen directly (Resume | tabs | Exit on one page).
    // _pauseRoot / _settingsPanel are the retired flat pause card + settings panel — kept in the
    // scene as a fallback until the old controllers are removed, but no longer the pause surface.
    public bool IsPauseOpen => SettingsScreenController.Instance != null && SettingsScreenController.Instance.IsOpen;
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

        // Item 1 (controller nav): pad-traversable + visible focus highlight for the pause menu AND the
        // settings panel (it lives under _pauseRoot), in one pass.
        UINavStyle.Apply(_pauseRoot);
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
        if (_input == null) return;

        bool esc = _input.GetPauseDown();   // Esc / pad Start
        // Pad B (<Gamepad>/buttonEast) is bound to UICancel, NOT Pause — so it never reached this arbiter and the
        // gamepad "Back" glyph did nothing on the settings screen. Accept UICancel as Back, but only while the
        // unified settings screen owns the layer (elsewhere buttonEast keeps its own meaning, e.g. skill-tree back).
        bool back = esc || (IsPauseOpen && _input.GetUICancelDown());
        if (!back) return;

        // Centralised ESC/Back arbiter — each press resolves exactly one layer (priority: highest first).
        // Non-settings layers respond to Esc/Start only; the settings screen also takes pad-B.
        if (esc && TutorialOverlayController.Instance != null && TutorialOverlayController.Instance.IsOpen)
        {
            TutorialOverlayController.Instance.TriggerContinue();
            return;
        }

        if (esc && SkillPreviewModal.Instance != null && SkillPreviewModal.Instance.IsOpen)
        {
            SkillPreviewModal.Instance.Close();
            return;
        }

        // Unified settings screen → its own Back state machine (Esc or pad-B). Today = Resume; F6 Phase 3 adds
        // the per-player edit-mode branch (a player in Controls edit mode backs out of edit only, independent of
        // the other player; Resume only once nobody is editing).
        if (IsPauseOpen)
        {
            SettingsScreenController.Instance.HandleBack();
            return;
        }

        // (The old flat settings panel is retired — its GameObject is disabled in Persistent and pause opens the
        // unified screen directly, so there's no longer an ESC fallback branch for it here.)

        if (esc && SkillTreeUI.Instance != null && SkillTreeUI.Instance.IsOpen)
        {
            SkillTreeUI.Instance.Close();
            return;
        }

        if (esc) OpenPause();
    }

    // ── Public API ────────────────────────────────────────────
    public void OpenPause()
    {
        // Open the unified pause/settings screen (the arbiter still owns timescale/audio/cursor below).
        if (SettingsScreenController.Instance == null)
        {
            Debug.LogError("[PauseMenuController] SettingsScreenController.Instance is null — the unified " +
                           "settings screen isn't in the scene. Pause cannot open.", this);
            return;
        }
        SettingsScreenController.Instance.Open();

        TimeScaleService.Instance?.Request(this, 0f);
        // F4/F7 — halt gameplay audio (owner set, sole AudioListener.pause writer) + duck to the
        // Paused mixer snapshot. UI/button sounds must use AudioManager.PlayUI to stay audible.
        AudioManager.Instance?.SetPaused(this);
        AudioManager.Instance?.RequestSnapshot(this, AudioSnapshotId.Paused, PauseSnapshotPriority);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        // Controller nav + first-focus (Resume | tabs | Exit) are handled inside SettingsScreenController.Open().
    }

    public void Resume()
    {
        SettingsScreenController.Instance?.Close();
        TimeScaleService.Instance?.Release(this);
        AudioManager.Instance?.ReleasePaused(this);
        AudioManager.Instance?.ReleaseSnapshot(this);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void OpenSettings()
    {
        _settingsPanel?.SetActive(true);
        // Focus is set by SettingsMenuController.OnEnable (it owns its own first control).
    }

    public void CloseSettings()
    {
        _settingsPanel?.SetActive(false);
        // Item 1 (controller nav): return focus to the Settings button we came from.
        UINavFocus.Focus(_settingsButton);
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