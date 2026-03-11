using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RescueEventController rescueEventController;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button restartButton;

    private void Start()
    {
        gameOverPanel?.SetActive(false);

        if (rescueEventController != null)
            rescueEventController.OnRescueStateChanged += HandleRescueState;

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartScene);
    }

    private void OnDestroy()
    {
        if (rescueEventController != null)
            rescueEventController.OnRescueStateChanged -= HandleRescueState;
    }

    private void HandleRescueState(RescueState state)
    {
        if (state == RescueState.Failed)
            TriggerGameOver();
    }

    private void TriggerGameOver()
    {
        gameOverPanel?.SetActive(true);
        Time.timeScale = 0f; // pause game
    }

    private void RestartScene()
    {
        Time.timeScale = 1f; // restore before reload
        SceneManager.LoadScene("L1Park");
    }
}
