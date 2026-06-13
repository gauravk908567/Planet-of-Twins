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

    private void Awake()
    {
        gameOverPanel?.SetActive(false);

        // Button listeners are pure UI -- safe in Awake
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartScene);
        else
            Debug.LogWarning("[GameOverController] restartButton not assigned.", this);

        if (loadCheckpointButton != null)
            loadCheckpointButton.onClick.AddListener(LoadCheckpoint);
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
            rescueEventController.OnRescueStateChanged += HandleRescueState;
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
            rescueEventController.OnRescueStateChanged -= HandleRescueState;
        if (sharedHealthPool != null)
            sharedHealthPool.OnSharedPoolEmpty -= TriggerGameOver;
    }

    private void HandleRescueState(RescueState state)
    {
        if (state == RescueState.Failed)
            TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        Debug.Log($"[GameOverController] TriggerGameOver � timeScale={Time.timeScale}");

        RefreshCheckpointButton();
        gameOverPanel?.SetActive(true);

        // FIX: disable raycast blocking on all sibling panels in the same canvas.
        // Other HUD panels (RescuePanel, SoulTimerPanel, AbilitiesHUD etc.) sit in
        // front of GameOverPanel and eat pointer events even when game over is on top.
        // This programmatically removes their blocking so buttons are always clickable.
        DisableSiblingRaycasts();

        TimeScaleService.Instance?.Request(this, 0f);
    }

    private void DisableSiblingRaycasts()
    {
        if (gameOverPanel == null) return;
        Transform parent = gameOverPanel.transform.parent;
        if (parent == null) return;

        foreach (Transform sibling in parent)
        {
            if (sibling.gameObject == gameOverPanel) continue;
            var cg = sibling.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                // Add one if missing so we can control it
                cg = sibling.gameObject.AddComponent<CanvasGroup>();
            }
            cg.blocksRaycasts = false;
        }
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
            EnableSiblingRaycasts();
            gameOverPanel?.SetActive(false);
        }
    }

    private void EnableSiblingRaycasts()
    {
        if (gameOverPanel == null) return;
        Transform parent = gameOverPanel.transform.parent;
        if (parent == null) return;

        foreach (Transform sibling in parent)
        {
            if (sibling.gameObject == gameOverPanel) continue;
            var cg = sibling.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;
        }
    }

    private void RefreshCheckpointButton()
    {
        if (loadCheckpointButton == null) return;
        loadCheckpointButton.interactable =
            checkpointManager != null && checkpointManager.HasCheckpoint;
    }
}