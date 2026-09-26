using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pause entry point + central ESC/Back arbiter. ESC (or pad Start) opens the unified
/// pause/settings screen (Resume | tabs | Exit on one page — SettingsScreenController).
///
/// ESC/Back priority (each press resolves exactly ONE layer, highest first):
///   1. Tutorial overlay open        → TriggerContinue
///   2. SkillPreviewModal open        → close modal
///   3. Unified settings screen open  → its own Back state machine (HandleBack; also takes pad-B)
///   4. Skill tree open               → close skill tree
///   5. Nothing open                  → open pause (the unified screen)
///
/// SETUP: add to a Screen-Space-Overlay canvas (sort 25). This controller has NO serialized UI refs —
/// it is purely the pause entry + ESC/Back arbiter. Resume/Exit live on the unified screen's
/// SettingsTabBar, which calls Resume()/ExitGame() here as the shared timescale/audio/cursor arbiter.
/// (The old flat pause card — PauseRoot + Resume/Settings/Exit + the flat SettingsPanel — was deleted
/// from the scene once the unified screen replaced it.)
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    // F7 — pause snapshot priority. Nothing else requests a snapshot yet; GameOver (enum 3)
    // must outrank pause, Setsuna (enum 2) sits below. Priority here is the arbiter's own axis.
    private const int PauseSnapshotPriority = 50;

    // Pause opens the unified settings screen directly (Resume | tabs | Exit on one page). The retired
    // flat pause card (_pauseRoot) is disabled in the scene and is no longer the pause surface.
    public bool IsPauseOpen => SettingsScreenController.Instance != null && SettingsScreenController.Instance.IsOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
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
        PoTLog.Crumb(PoTCrumb.Pause, "paused");

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
        PoTLog.Crumb(PoTCrumb.Pause, "resumed");
        SettingsScreenController.Instance?.Close();
        TimeScaleService.Instance?.Release(this);
        AudioManager.Instance?.ReleasePaused(this);
        AudioManager.Instance?.ReleaseSnapshot(this);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ExitGame()
    {
        PoTLog.Crumb(PoTCrumb.Flow, "exit game (pause menu)");
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