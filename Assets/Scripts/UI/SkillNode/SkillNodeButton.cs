using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(Button))]
public class SkillNodeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scene assignment — set per button in Inspector")]
    [SerializeField] public AbilityUpgradeData NodeData;
    [SerializeField] public int NodeIndex;

    [Header("UI References — assign in prefab")]
    public TMP_Text NodeLabel;
    public TMP_Text StatLine;
    public TMP_Text CostBadge;
    public Image Background;
    public Image LockIcon;

    [Header("Video — assign manually in prefab")]
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private RawImage _thumbnailStrip;
    [SerializeField] private Image _lockedOverlay;

    [Header("Hover threshold to open video zoom (seconds)")]
    [SerializeField] private float HoverThreshold = 1.2f;

    // ── Colours ───────────────────────────────────────────────
    static readonly Color BgPurchased = new Color(0.08f, 0.20f, 0.12f);
    static readonly Color BgAffordable = new Color(0.10f, 0.16f, 0.26f);
    static readonly Color BgLocked = new Color(0.09f, 0.09f, 0.11f);
    static readonly Color BgMaxed = new Color(0.18f, 0.15f, 0.04f);
    static readonly Color TextGreen = new Color(0.22f, 0.88f, 0.44f);
    static readonly Color TextGold = new Color(0.90f, 0.75f, 0.20f);
    static readonly Color TextDim = new Color(0.40f, 0.40f, 0.45f);

    // ── Internal ──────────────────────────────────────────────
    internal AbilityUpgradeData _data;
    private int _nodeIndex;
    private ISkillTreePurchaser _purchaser;
    private IPointBank _pointBank;
    private Button _btn;

    private bool _isHovered;
    private float _hoverTimer;
    private bool _clipAssigned = false;

    private enum State { Purchased, NextAffordable, NextLocked, Locked, Maxed }

    // ── Lifecycle ─────────────────────────────────────────────
    private void Awake()
    {
        _btn = GetComponent<Button>();

        if (_videoPlayer == null)
            _videoPlayer = GetComponent<VideoPlayer>();

        if (_thumbnailStrip == null)
            _thumbnailStrip = GetComponentInChildren<RawImage>();
    }

    private void OnEnable()
    {
        if (_data != null && !_clipAssigned)
        {
            AssignClip();
            _clipAssigned = true;
        }
        else if (_videoPlayer != null && _videoPlayer.clip != null)
        {
            // Tab reactivated — just play regardless of prepared state
            // VideoPlayer on an active GO resumes correctly from Stop
            if (!_videoPlayer.isPrepared)
            {
                // Lost prepared state during deactivation — re-prepare
                _videoPlayer.prepareCompleted -= OnPrepared;
                _videoPlayer.prepareCompleted += OnPrepared;
                _videoPlayer.Prepare();
            }
            else if (!_videoPlayer.isPlaying)
            {
                ApplyVideoState(GetState());
            }
        }

        //Debug.Log($"[OnEnable] {gameObject.name} | VP={_videoPlayer?.GetInstanceID()} | RT={_videoPlayer?.targetTexture?.GetInstanceID()} | Strip={_thumbnailStrip?.GetInstanceID()} | StripTex={_thumbnailStrip?.texture?.GetInstanceID()}");
    }

    private void OnDisable()
    {
        // Stop cleanly on tab hide — prevents audio/buffer issues
        if (_videoPlayer != null && _videoPlayer.isPlaying)
            _videoPlayer.Pause();
    }

    private void Update()
    {
        if (!_isHovered) return;
        _hoverTimer += Time.unscaledDeltaTime;
        if (_hoverTimer >= HoverThreshold)
        {
            _isHovered = false;
            _hoverTimer = 0f;
            OpenVideoModal();
        }
    }

    private void OnDestroy()
    {
        if (_videoPlayer != null) _videoPlayer.Stop();
        if (_pointBank != null) _pointBank.OnPointsChanged -= OnPointsChanged;
    }

    // ── Initialise ────────────────────────────────────────────
    public void Initialise(AbilityUpgradeData data, int nodeIndex,
                           ISkillTreePurchaser purchaser, IPointBank pointBank)
    {
        _data = data;
        _nodeIndex = nodeIndex;
        _purchaser = purchaser;
        _pointBank = pointBank;

        if (_btn == null) _btn = GetComponent<Button>();

        _btn.onClick.RemoveAllListeners();
        _btn.onClick.AddListener(OnCardClicked);

        if (_thumbnailStrip != null)
        {
            var videoBtn = _thumbnailStrip.GetComponent<Button>()
                        ?? _thumbnailStrip.gameObject.AddComponent<Button>();
            videoBtn.transition = Selectable.Transition.None;
            videoBtn.onClick.RemoveAllListeners();
            videoBtn.onClick.AddListener(OpenVideoModal);
        }

        _pointBank.OnPointsChanged -= OnPointsChanged;
        _pointBank.OnPointsChanged += OnPointsChanged;

        // Don't call AssignClip here — panel may be inactive
        // AssignClip fires in OnEnable when panel first activates

        Refresh();
    }

    public void InitialiseFromScene(ISkillTreePurchaser purchaser, IPointBank pointBank)
    {
        if (NodeData == null)
        {
            Debug.LogError($"[SkillNodeButton] {gameObject.name} — NodeData is NULL", this);
            return;
        }
        Initialise(NodeData, NodeIndex, purchaser, pointBank);
    }

    // ── Clip assignment ───────────────────────────────────────
    private void AssignClip()
    {
        if (_videoPlayer == null)
        {
            Debug.LogWarning($"[SkillNodeButton] {gameObject.name} — VideoPlayer not found.");
            return;
        }

        var node = _data.nodes[_nodeIndex];
        if (node.previewClip == null)
        {
            _videoPlayer.clip = null;
            _videoPlayer.Stop();
            return;
        }

        _videoPlayer.clip = node.previewClip;
        _videoPlayer.prepareCompleted -= OnPrepared;
        _videoPlayer.prepareCompleted += OnPrepared;
        _videoPlayer.Prepare();
    }

    private void OnPrepared(VideoPlayer vp)
    {
        if (GetState() != State.Locked)
            vp.Play();
    }

    private void ApplyVideoState(State state)
    {
        if (_videoPlayer == null || _videoPlayer.clip == null) return;

        if (state == State.Locked)
            _videoPlayer.Stop();
        else if (!_videoPlayer.isPlaying)
            _videoPlayer.Play();
    }

    // ── Card click → direct purchase ──────────────────────────
    private void OnCardClicked()
    {
        if (_purchaser == null || _data == null) return;

        var state = GetState();
        if (state == State.Locked || state == State.Purchased || state == State.Maxed)
            return;

        if (!_purchaser.TryPurchaseNode(_data))
        {
            Debug.Log("[SkillNodeButton] Purchase failed — not enough points.");
            return;
        }

        foreach (var btn in FindObjectsByType<SkillNodeButton>(FindObjectsSortMode.None))
            if (btn._data == _data) btn.Refresh();
    }

    // ── Hover ─────────────────────────────────────────────────
    public void OnPointerEnter(PointerEventData _)
    {
        _isHovered = true;
        _hoverTimer = 0f;
    }

    public void OnPointerExit(PointerEventData _)
    {
        _isHovered = false;
        _hoverTimer = 0f;
    }

    // ── Open video modal ──────────────────────────────────────
    private void OpenVideoModal()
    {
        var node = _data?.nodes[_nodeIndex];
        if (node == null || node.previewClip == null) return;

        if (SkillPreviewModal.Instance == null)
        {
            Debug.LogWarning("[SkillNodeButton] SkillPreviewModal.Instance is null.");
            return;
        }

        // Pass this as source so modal can build cycle list from same tab
        SkillPreviewModal.Instance.Show(_data, _nodeIndex, _purchaser, _pointBank, this);
    }

    // ── Refresh visuals ───────────────────────────────────────
    public void Refresh()
    {
        if (_data == null) return;
        var state = GetState();
        var node = _data.nodes[_nodeIndex];

        if (NodeLabel) NodeLabel.text = node.label;

        _btn.interactable = state != State.Locked
                         && state != State.Purchased
                         && state != State.Maxed;

        if (Background) Background.color = state switch
        {
            State.Purchased => BgPurchased,
            State.NextAffordable => BgAffordable,
            State.Maxed => BgMaxed,
            _ => BgLocked
        };

        if (LockIcon) LockIcon.enabled = (state == State.Locked);

        if (CostBadge)
        {
            CostBadge.text = state == State.Purchased ? "✓"
                           : state == State.Maxed ? "MAX"
                           : $"{node.pointCost} pts";

            CostBadge.color = state == State.Purchased ? TextGold
                            : state == State.NextAffordable ? TextGreen
                            : TextDim;
        }

        if (StatLine) StatLine.text = BuildStatLine(state, node);
        if (_lockedOverlay) _lockedOverlay.enabled = (state == State.Locked);

        ApplyVideoState(state);
    }

    // ── Stat line ─────────────────────────────────────────────
    string BuildStatLine(State state, AbilityUpgradeNode node)
    {
        return state switch
        {
            State.Purchased => $"<color=#3fb950>✓  {node.description}</color>",
            State.Maxed => "<color=#e3b341>FULLY UPGRADED</color>",
            State.Locked => "<color=#555>Unlock previous node first</color>",
            _ => string.IsNullOrEmpty(node.description) ? "" : node.description
        };
    }

    // ── State ─────────────────────────────────────────────────
    State GetState()
    {
        if (_data.IsMaxed && _nodeIndex >= _data.TotalNodes) return State.Maxed;
        if (_nodeIndex < _data.currentNodeIndex) return State.Purchased;
        if (_nodeIndex > _data.currentNodeIndex) return State.Locked;
        return _purchaser.CanAfford(_data) ? State.NextAffordable : State.NextLocked;
    }

    void OnPointsChanged(int _) => Refresh();
}