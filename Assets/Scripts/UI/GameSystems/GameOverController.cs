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

    // FIX: moved listener registration to Awake so it's ready before
    // any Start() on other MonoBehaviours fires RescueState.Failed.
    // Previously in Start(), TriggerGameOver fired before listeners
    // were registered � buttons had no onClick handlers attached yet.
    private void Awake()
    {
        gameOverPanel?.SetActive(false);

        if (rescueEventController != null)
            rescueEventController.OnRescueStateChanged += HandleRescueState;
        else
            Debug.LogWarning("[GameOverController] rescueEventController not assigned.", this);

        if (sharedHealthPool != null)
            sharedHealthPool.OnSharedPoolEmpty += TriggerGameOver;
        else
            Debug.LogWarning("[GameOverController] sharedHealthPool not assigned — tether death won't trigger game over.", this);

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartScene);
        else
            Debug.LogWarning("[GameOverController] restartButton not assigned.", this);

        if (loadCheckpointButton != null)
            loadCheckpointButton.onClick.AddListener(LoadCheckpoint);
    }

    private void Start()
    {
        // Refresh interactable state once checkpoint manager is ready
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

        Time.timeScale = 0f;
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
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
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
            // Re-enable sibling raycasts before hiding panel
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