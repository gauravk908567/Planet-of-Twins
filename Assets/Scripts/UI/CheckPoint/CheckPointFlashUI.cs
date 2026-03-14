using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows a short-lived "Checkpoint saved" notification.
///
/// SETUP:
///   - This GameObject (script owner) must ALWAYS stay active in the scene.
///   - Create a child GameObject "FlashPanel" with the visual content.
///   - Assign FlashPanel to flashPanel slot — it starts inactive.
///   - Assign the TMP_Text inside FlashPanel to messageText.
///   - Optionally assign a CanvasGroup on FlashPanel for fade.
/// </summary>
public class CheckpointFlashUI : MonoBehaviour
{
    [SerializeField] private GameObject flashPanel;   // child — toggled on/off
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private CanvasGroup canvasGroup; // optional, on flashPanel
    [SerializeField] private float displayDuration = 1.5f;
    [SerializeField] private float fadeDuration = 0.4f;

    private Coroutine _activeCoroutine;

    private void Awake()
    {
        flashPanel?.SetActive(false);
    }

    public void Flash(string message)
    {
        if (messageText != null) messageText.text = message;
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        flashPanel?.SetActive(true);

        yield return new WaitForSeconds(displayDuration);

        if (canvasGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
        }

        flashPanel?.SetActive(false);
        _activeCoroutine = null;
    }
}