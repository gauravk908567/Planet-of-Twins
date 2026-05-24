using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
// SkillPreviewModal
//
// PREFAB SETUP:
//   SkillPreviewModal (this script on root GO — always active in scene)
//     └── Panel (_root — disabled until Show() called)
//           ├── DimPanel      Image fullscreen, alpha 0.75, Button → Close()
//           └── ModalCard     960x700 centred (_modalCard)
//                 ├── CloseButton   top-right "✕" Button → Close()
//                 ├── VideoFrame    RawImage top ~960x490 (_videoFrame)
//                 ├── TitleText     TMP (_titleText)
//                 ├── DescText      TMP (_descText)
//                 ├── SkillPointsText TMP (_skillPointsText)
//                 ├── ButtonRow     HLG
//                 │     ├── PrevButton  "◄" (_prevButton)
//                 │     ├── BuyButton   (_buyButton)
//                 │     │     └── BuyLabel TMP (_buyLabel)
//                 │     └── NextButton  "►" (_nextButton)
//                 └── PrereqText    TMP (_prereqText)
//
// FIXES:
//   - _root activates BEFORE PopulateContent so VideoPlayer.Prepare() fires
//   - OnModalVideoPrepared guards against closed/re-opened state
//   - Close() cancels pending prepare via _isPreparing flag
//   - Re-Show() while closing correctly restarts everything
// ─────────────────────────────────────────────────────────────────────────────
public class SkillPreviewModal : MonoBehaviour
{
    public static SkillPreviewModal Instance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject _root;

    [Header("Animation")]
    [SerializeField] private RectTransform _modalCard;
    [SerializeField] private float _popDuration = 0.18f;

    [Header("Video")]
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private RawImage _videoFrame;
    [SerializeField] private RenderTexture _modalRT;

    [Header("Text")]
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _descText;
    [SerializeField] private TMP_Text _skillPointsText;

    [Header("Buttons")]
    [SerializeField] private Button _buyButton;
    [SerializeField] private TMP_Text _buyLabel;
    [SerializeField] private Button _prevButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _dimPanel;
    [SerializeField] private TMP_Text _prereqText;

    // ── Internal ──────────────────────────────────────────────
    private AbilityUpgradeData _currentData;
    private int _currentNodeIndex;
    private ISkillTreePurchaser _purchaser;
    private IPointBank _pointBank;
    private Coroutine _animCoroutine;

    private List<SkillNodeButton> _tabButtons = new List<SkillNodeButton>();
    private int _cycleIndex;

    // Guards against stale prepare callbacks after close
    private bool _isOpen = false;
    public bool IsOpen => _isOpen;
    private VideoClip _pendingClip = null;

    // ── Lifecycle ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _root.SetActive(false);

        if (_modalRT != null && _videoPlayer != null)
        {
            _videoPlayer.targetTexture = _modalRT;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.isLooping = true;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            _videoPlayer.playOnAwake = false;
        }

        if (_videoFrame != null && _modalRT != null)
            _videoFrame.texture = _modalRT;

        _closeButton?.onClick.AddListener(Close);
        _dimPanel?.onClick.AddListener(Close);
        _buyButton?.onClick.AddListener(OnBuyClicked);
        _prevButton?.onClick.AddListener(OnPrev);
        _nextButton?.onClick.AddListener(OnNext);
    }

    private void Update()
    {
        if (!_root.activeSelf) return;
        if (Input.GetMouseButtonDown(1)) Close();
    }

    // ── Show ──────────────────────────────────────────────────
    public void Show(AbilityUpgradeData data, int nodeIndex,
                     ISkillTreePurchaser purchaser, IPointBank pointBank,
                     SkillNodeButton sourceButton = null)
    {
        _purchaser = purchaser;
        _pointBank = pointBank;
        _isOpen = true;

        if (sourceButton != null)
            BuildCycleList(sourceButton);

        if (_pointBank != null)
        {
            _pointBank.OnPointsChanged -= OnPointsChanged;
            _pointBank.OnPointsChanged += OnPointsChanged;
        }

        _cycleIndex = FindCycleIndex(data, nodeIndex);

        // ── CRITICAL: activate root BEFORE PopulateContent ──
        // VideoPlayer.Prepare() requires the GameObject to be active.
        // Activating here guarantees the callback fires correctly.
        _root.SetActive(true);

        PopulateContent(data, nodeIndex);

        if (_modalCard != null)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(PopOut());
        }
    }

    // ── Cycle ─────────────────────────────────────────────────
    private void BuildCycleList(SkillNodeButton source)
    {
        _tabButtons.Clear();

        Transform parent = source.transform.parent;
        while (parent != null)
        {
            var buttons = parent.GetComponentsInChildren<SkillNodeButton>(true);
            if (buttons.Length > 1)
            {
                _tabButtons.AddRange(buttons);
                break;
            }
            parent = parent.parent;
        }

        if (_tabButtons.Count == 0)
            _tabButtons.Add(source);
    }

    private int FindCycleIndex(AbilityUpgradeData data, int nodeIndex)
    {
        for (int i = 0; i < _tabButtons.Count; i++)
            if (_tabButtons[i]._data == data && _tabButtons[i].NodeIndex == nodeIndex)
                return i;
        return 0;
    }

    private void OnPrev()
    {
        if (_tabButtons.Count == 0) return;
        _cycleIndex = (_cycleIndex - 1 + _tabButtons.Count) % _tabButtons.Count;
        NavigateTo(_tabButtons[_cycleIndex]);
    }

    private void OnNext()
    {
        if (_tabButtons.Count == 0) return;
        _cycleIndex = (_cycleIndex + 1) % _tabButtons.Count;
        NavigateTo(_tabButtons[_cycleIndex]);
    }

    private void NavigateTo(SkillNodeButton btn)
    {
        if (btn == null || btn._data == null) return;
        PopulateContent(btn._data, btn.NodeIndex);
    }

    // ── Populate content ──────────────────────────────────────
    private void PopulateContent(AbilityUpgradeData data, int nodeIndex)
    {
        _currentData = data;
        _currentNodeIndex = nodeIndex;

        var node = data.nodes[nodeIndex];

        if (_titleText) _titleText.text = node.label;
        if (_descText) _descText.text = string.IsNullOrEmpty(node.description)
                                          ? "" : node.description;
        if (_skillPointsText)
            _skillPointsText.text = $"Skill Points: {_pointBank?.CurrentPoints ?? 0}";

        RefreshBuyButton(data, nodeIndex);
        PlayVideo(node.previewClip);
    }

    // ── Video ─────────────────────────────────────────────────
    private void PlayVideo(VideoClip clip)
    {
        if (_videoPlayer == null) return;

        // Stop and clear previous prepare callback
        _videoPlayer.Stop();
        _videoPlayer.prepareCompleted -= OnModalVideoPrepared;

        if (clip == null)
        {
            _videoPlayer.clip = null;
            if (_videoFrame) _videoFrame.gameObject.SetActive(false);
            return;
        }

        _pendingClip = clip;
        _videoPlayer.clip = clip;
        _videoPlayer.prepareCompleted += OnModalVideoPrepared;
        _videoPlayer.Prepare();

        if (_videoFrame) _videoFrame.gameObject.SetActive(true);
    }

    private void OnModalVideoPrepared(VideoPlayer vp)
    {
        // Guard — if modal was closed before prepare completed, don't play
        if (!_isOpen) return;

        // Guard — if a newer clip was requested, this callback is stale
        if (vp.clip != _pendingClip) return;

        vp.Play();
    }

    // ── Pop animations ────────────────────────────────────────
    private IEnumerator PopOut()
    {
        _modalCard.localScale = Vector3.zero;
        float elapsed = 0f;

        while (elapsed < _popDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _popDuration);
            float scale = 1f - Mathf.Pow(1f - t, 3f);
            _modalCard.localScale = Vector3.one * scale;
            yield return null;
        }

        _modalCard.localScale = Vector3.one;
        _animCoroutine = null;
    }

    private IEnumerator PopIn()
    {
        float elapsed = 0f;
        float duration = _popDuration * 0.6f;
        float startScale = _modalCard.localScale.x;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _modalCard.localScale = Vector3.one * Mathf.Lerp(startScale, 0f, t);
            yield return null;
        }

        _modalCard.localScale = Vector3.zero;
        _root.SetActive(false);
        _animCoroutine = null;
    }

    // ── Buy ───────────────────────────────────────────────────
    private void OnBuyClicked()
    {
        if (_currentData == null || _purchaser == null) return;

        if (!_purchaser.TryPurchaseNode(_currentData))
        {
            Debug.Log("[SkillPreviewModal] Purchase failed — not enough points.");
            return;
        }

        foreach (var btn in FindObjectsByType<SkillNodeButton>(FindObjectsSortMode.None))
            if (btn._data == _currentData) btn.Refresh();

        if (_skillPointsText)
            _skillPointsText.text = $"Skill Points: {_pointBank?.CurrentPoints ?? 0}";

        RefreshBuyButton(_currentData, _currentNodeIndex);
    }

    // ── Close ─────────────────────────────────────────────────
    public void Close()
    {
        if (!_root.activeSelf) return;

        _isOpen = false;
        _pendingClip = null;
        _currentData = null;

        if (_pointBank != null)
            _pointBank.OnPointsChanged -= OnPointsChanged;

        // Stop video and clear prepare callback so no stale play fires
        if (_videoPlayer != null)
        {
            _videoPlayer.prepareCompleted -= OnModalVideoPrepared;
            _videoPlayer.Stop();
        }

        if (_modalCard != null)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(PopIn());
        }
        else
        {
            _root.SetActive(false);
        }
    }

    private void OnPointsChanged(int points)
    {
        if (!_isOpen || _currentData == null) return;

        if (_skillPointsText)
            _skillPointsText.text = $"Skill Points: {points}";

        // Refresh buy button too — affordability may have changed
        RefreshBuyButton(_currentData, _currentNodeIndex);
    }
    // ── Buy button + prereq state ─────────────────────────────
    private void RefreshBuyButton(AbilityUpgradeData data, int nodeIndex)
    {
        if (_buyButton == null) return;

        bool isPurchased = nodeIndex < data.currentNodeIndex;
        bool isLocked = nodeIndex > data.currentNodeIndex;
        bool isMaxed = data.IsMaxed && nodeIndex >= data.TotalNodes;
        bool canAfford = _purchaser != null && _purchaser.CanAfford(data);

        if (isPurchased || isMaxed)
        {
            _buyButton.interactable = false;
            if (_buyLabel) _buyLabel.text = isPurchased ? "Already Purchased" : "Maxed";
        }
        else if (isLocked)
        {
            _buyButton.interactable = false;
            if (_buyLabel) _buyLabel.text = "Unlock Previous First";
        }
        else
        {
            _buyButton.interactable = canAfford;
            int cost = data.HasNextNode ? data.NextNodeCost : 0;
            int have = _pointBank?.CurrentPoints ?? 0;
            if (_buyLabel) _buyLabel.text = canAfford
                ? $"Unlock  —  {cost} pts"
                : $"Need {cost - have} more pts";
        }

        if (_prereqText)
        {
            _prereqText.text = isLocked ? "⚠  Unlock previous node first"
                             : isPurchased ? "✓  Unlocked"
                             : isMaxed ? "✓  Fully upgraded"
                             : !canAfford ? $"⚠  Need {data.NextNodeCost - (_pointBank?.CurrentPoints ?? 0)} more skill points"
                             : "";

            _prereqText.color = (isLocked || !canAfford)
                ? new Color(0.8f, 0.5f, 0.1f)
                : new Color(0.2f, 0.8f, 0.4f);
        }
    }
}