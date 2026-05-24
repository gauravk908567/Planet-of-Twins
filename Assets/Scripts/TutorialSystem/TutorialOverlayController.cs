using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using System.Collections;

/// <summary>
/// Schedule 1 style tutorial prompt overlay.
/// Shows a card with video + title + body text + continue button.
/// Pauses time while shown. Fires callback on continue.
///
/// SETUP:
///   Add TutorialOverlayCanvas prefab to scene (Screen Space Overlay, sort 20).
///   OverlayRoot disabled by default.
///
/// HIERARCHY:
///   TutorialOverlayCanvas
///     └── OverlayRoot             (_root — disabled by default)
///           ├── DimPanel          (full screen, alpha 0.6, non-interactive)
///           └── Card              (centred ~660x440)
///                 ├── VideoFrame  (RawImage, ~660x280, top)
///                 ├── TitleText   (TMP, 24pt bold)
///                 ├── BodyText    (TMP, 16pt, wrap)
///                 └── ContinueBtn (Button)
///                       └── BtnLabel (TMP — "Continue")
/// </summary>
public class TutorialOverlayController : MonoBehaviour
{
    public static TutorialOverlayController Instance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject _root;

    [Header("Card")]
    [SerializeField] private RectTransform _card;
    [SerializeField] private float _popDuration = 0.14f;

    [Header("Video")]
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private RawImage _videoFrame;
    [SerializeField] private RenderTexture _overlayRT;   // 1280x720 RT asset

    [Header("Text")]
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;

    [Header("Button")]
    [SerializeField] private Button _continueButton;
    [SerializeField] private TMP_Text _continueBtnLabel;

    [SerializeField] private Button _dimPanelButton;
    // ── Internal ──────────────────────────────────────────────
    private System.Action _onContinue;
    private Coroutine _animCoroutine;
    private VideoClip _pendingClip;
    private bool _isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _root.SetActive(false);

        if (_overlayRT != null && _videoPlayer != null)
        {
            _videoPlayer.targetTexture = _overlayRT;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.isLooping = true;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            _videoPlayer.playOnAwake = false;
        }

        if (_videoFrame != null && _overlayRT != null)
            _videoFrame.texture = _overlayRT;

        _continueButton?.onClick.AddListener(OnContinueClicked);

        _dimPanelButton?.onClick.AddListener(OnContinueClicked);
    }

    /// <summary>
    /// Show a tutorial prompt. Time is paused until player clicks Continue.
    /// onContinue fires after time is restored.
    /// </summary>
    public void Show(string title, string body, VideoClip clip,
                     System.Action onContinue, string continueLabel = "Continue")
    {
        _onContinue = onContinue;
        _isOpen = true;

        if (_titleText) _titleText.text = title;
        if (_bodyText) _bodyText.text = body;
        if (_continueBtnLabel) _continueBtnLabel.text = continueLabel;

        // Activate before Prepare so VP fires callback
        _root.SetActive(true);
        Time.timeScale = 0f;

        PlayVideo(clip);

        if (_card != null)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(PopOut());
        }
    }
    private void Update()
    {
        if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            OnContinueClicked();
    }
    private void OnContinueClicked()
    {
        if (!_isOpen) return;
        _isOpen = false;

        _videoPlayer?.Stop();
        _videoPlayer.prepareCompleted -= OnPrepared;

        Time.timeScale = 1f;

        if (_card != null)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(PopIn(() =>
            {
                _onContinue?.Invoke();
                _onContinue = null;
            }));
        }
        else
        {
            _root.SetActive(false);
            _onContinue?.Invoke();
            _onContinue = null;
        }
    }

    private void PlayVideo(VideoClip clip)
    {
        if (_videoPlayer == null) return;
        _videoPlayer.Stop();
        _videoPlayer.prepareCompleted -= OnPrepared;

        if (clip == null)
        {
            _videoFrame?.gameObject.SetActive(false);
            return;
        }

        _pendingClip = clip;
        _videoPlayer.clip = clip;
        _videoPlayer.prepareCompleted += OnPrepared;
        _videoPlayer.Prepare();
        _videoFrame?.gameObject.SetActive(true);
    }

    private void OnPrepared(VideoPlayer vp)
    {
        if (!_isOpen || vp.clip != _pendingClip) return;
        vp.Play();
    }

    private IEnumerator PopOut()
    {
        _card.localScale = Vector3.zero;
        float elapsed = 0f;
        while (elapsed < _popDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _popDuration);
            _card.localScale = Vector3.one * (1f - Mathf.Pow(1f - t, 3f));
            yield return null;
        }
        _card.localScale = Vector3.one;
        _animCoroutine = null;
    }

    private IEnumerator PopIn(System.Action onDone)
    {
        float elapsed = 0f;
        float duration = _popDuration * 0.6f;
        float start = _card.localScale.x;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _card.localScale = Vector3.one * Mathf.Lerp(start, 0f, elapsed / duration);
            yield return null;
        }
        _card.localScale = Vector3.zero;
        _root.SetActive(false);
        _animCoroutine = null;
        onDone?.Invoke();
    }
}