using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RescueEventController rescueEventController;
    [SerializeField] private SharedHealthPool sharedHealthPool;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button loadCheckpointButton;

    [Header("Checkpoint")]
    [SerializeField] private CheckpointManager checkpointManager;

    [Header("Scenes")]
    [SerializeField] private SceneReference bootstrapScene;

    // The HUD panels sharing the canvas sit in front of GameOverPanel and would eat its clicks (UIModalPanel).
    private UIModalPanel _modal;

    private void Awake()
    {
        gameOverPanel?.SetActive(false);
        _modal = new UIModalPanel(gameOverPanel);

        // Button listeners are pure UI -- safe in Awake
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartScene);
        else
            Debug.LogWarning("[GameOverController] restartButton not assigned.", this);

        if (loadCheckpointButton != null)
            loadCheckpointButton.onClick.AddListener(LoadCheckpoint);

        // Item 1 (controller nav, BUG-116): pad/keyboard-traversable buttons + the shared focus glow.
        UINavStyle.Apply(gameOverPanel);
    }

    private void Start()
    {
        // R4: resolve Persistent managers in Start(); prefer serialized slot,
        // fall back to Instance. FindAnyObjectByType is banned for managers (R4).
        if (rescueEventController == null)
            rescueEventController = RescueEventController.Instance;
        if (sharedHealthPool == null)
            sharedHealthPool = FindAnyObjectByType<SharedHealthPool>();   // no Instance yet
        if (checkpointManager == null)
            checkpointManager = FindAnyObjectByType<CheckpointManager>(); // no Instance yet

        if (rescueEventController != null)
            // OnRescueFailed (not OnRescueStateChanged) — the state event never delivers the terminal
            // Failed value; the dedicated failure signal is the only reliable game-over trigger.
            rescueEventController.OnRescueFailed += TriggerGameOver;
        else
            Debug.LogWarning("[GameOverController] rescueEventController not found.", this);

        if (sharedHealthPool != null)
            sharedHealthPool.OnSharedPoolEmpty += TriggerGameOver;
        else
            Debug.LogWarning("[GameOverController] sharedHealthPool not found -- tether death won't trigger game over.", this);

        RefreshCheckpointButton();
    }

    private void OnDestroy()
    {
        if (rescueEventController != null)
            rescueEventController.OnRescueFailed -= TriggerGameOver;
        if (sharedHealthPool != null)
            sharedHealthPool.OnSharedPoolEmpty -= TriggerGameOver;
    }

    private void TriggerGameOver()
    {
        PoTLog.Crumb(PoTCrumb.Flow, "game over");
        PoTLog.Twins?.Info($"TriggerGameOver — timeScale={Time.timeScale}");

        RefreshCheckpointButton();
        _modal.Open();

        // Controller focus (BUG-116): any device can pick. Land on Load Checkpoint when one exists (the respawn
        // path), else Restart. Wrap wired after interactability is settled so a disabled button is skipped.
        UINavStyle.WireWrap(gameOverPanel);
        bool canLoad = loadCheckpointButton != null && loadCheckpointButton.interactable;
        UINavFocus.Focus(canLoad ? loadCheckpointButton : restartButton);

        TimeScaleService.Instance?.Request(this, 0f);
    }

    private void RestartScene()
    {
        TimeScaleService.Instance?.ReleaseAll();
        if (bootstrapScene.IsValid)
            SceneManager.LoadScene(bootstrapScene.Name);
        else
            Debug.LogError("[GameOverController] bootstrapScene not assigned — cannot restart.", this);
    }

    private void LoadCheckpoint()
    {
        if (checkpointManager == null)
        {
            Debug.LogWarning("[GameOverController] No CheckpointManager assigned.", this);
            return;
        }
        bool success = checkpointManager.TryRespawnAtCheckpoint();
        if (success)
        {
            TimeScaleService.Instance?.Release(this);
            _modal.Close();
        }
    }

    private void RefreshCheckpointButton()
    {
        if (loadCheckpointButton == null) return;
        loadCheckpointButton.interactable =
            checkpointManager != null && checkpointManager.HasCheckpoint;
    }
}