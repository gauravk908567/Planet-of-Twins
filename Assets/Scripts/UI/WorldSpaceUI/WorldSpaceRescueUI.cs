using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// WorldSpaceRescueUI — drives the world-space rescue HUD on each player.
///
/// Three rings:
///   TTK ring  (red)   — drains as killer's timer counts down. Always centred on grab.
///                        Slides left when soul arrives so the rescue ring has room.
///   F-key ring (green) — appears when soul arrives. Player mashes F to fill it.
///   Struggle ring (white/gold) — appears ONLY for tier-1 traps (CanGrabbedPlayerStruggle).
///                        Shows "Press E!" prompt. Flashes/fills for 0.4 s on each E press.
///                        Sits below the TTK ring, independent of soul proximity.
///
/// INSPECTOR SETUP:
///   Wire all three Image rings and the two TMP_Text labels.
///   Struggle ring sits on a separate RectTransform child; position it freely.
/// </summary>
public class WorldSpaceRescueUI : MonoBehaviour
{
    [Header("Owner")]
    [SerializeField] private Player ownerPlayer;

    [Header("Controller")]
    [SerializeField] private RescueEventController rescueEventController;

    [Header("Panels")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private GameObject cooldownOverlay;

    [Header("TTK Ring (always red — danger timer)")]
    [SerializeField] private Image ttkRing;
    [SerializeField] private Vector2 ttkCentrePos = new Vector2(0f, 0f);
    [SerializeField] private Vector2 ttkSplitPos = new Vector2(-50f, 0f);

    [Header("F-Key Ring (green — rescue mash)")]
    [SerializeField] private Image fKeyRing;
    [SerializeField] private Vector2 fKeySplitPos = new Vector2(50f, 0f);

    [Header("Struggle Ring (white/gold — E mash, tier-1 traps only)")]
    [Tooltip("Radial Image — fill 0→1 on each struggle press, drains back down.")]
    [SerializeField] private Image struggleRing;
    [Tooltip("Duration the ring stays filled before draining. Match GroupGrabEnemy.strugglePauseDuration.")]
    [SerializeField] private float struggleFillDuration = 0.4f;
    [SerializeField] private Color struggleColour = new Color(1f, 0.85f, 0.2f, 1f); // gold

    [Header("Text")]
    [SerializeField] private TMP_Text pressFText;
    [SerializeField] private TMP_Text pressEText;   // "Press E!" — shown next to struggle ring
    [SerializeField] private TMP_Text cooldownText;

    [Header("Chain Prompt (Tether-Breaker)")]
    [Tooltip("Separate panel shown when player is chain-grabbed. Independent of rescue system.")]
    [SerializeField] private GameObject chainPromptPanel;
    [SerializeField] private TMP_Text chainPromptText;

    [Header("Colours")]
    [SerializeField] private Color ttkColour = new Color(0.9f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color fKeyColour = new Color(0.2f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color cooldownColour = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("Animation")]
    [SerializeField] private float splitDuration = 0.3f;

    // ── Runtime ────────────────────────────────────────────────
    private RescueState _state;
    private IRescueTarget _activeTarget;

    private RectTransform _ttkRect;
    private RectTransform _fKeyRect;
    private bool _isOwnerInDanger;
    private bool _soulInRange;
    private Coroutine _splitCoroutine;
    private Coroutine _struggleCoroutine;

    // ── Lifecycle ──────────────────────────────────────────────
    private void Awake()
    {
        _ttkRect = ttkRing?.GetComponent<RectTransform>();
        _fKeyRect = fKeyRing?.GetComponent<RectTransform>();
        if (ownerPlayer == null)
            ownerPlayer = GetComponentInParent<Player>(true);
    }

    private void OnEnable()
    {
        if (rescueEventController == null) return;
        rescueEventController.OnRescueStateChanged += HandleStateChanged;
        rescueEventController.OnMashProgressUpdated += HandleMashProgress;
        rescueEventController.OnMashTimeUpdated += HandleMashTime;
        rescueEventController.OnCooldownTimeUpdated += HandleCooldownTime;
        rescueEventController.OnPlayerInDanger += HandlePlayerInDanger;
        rescueEventController.OnActiveTargetChanged += HandleActiveTargetChanged;
        rescueEventController.OnStruggleActivated += HandleStruggleActivated;
        rescueEventController.OnStruggleCapReached += HandleStruggleCapReached;
    }

    private void OnDisable()
    {
        if (rescueEventController == null) return;
        rescueEventController.OnRescueStateChanged -= HandleStateChanged;
        rescueEventController.OnMashProgressUpdated -= HandleMashProgress;
        rescueEventController.OnMashTimeUpdated -= HandleMashTime;
        rescueEventController.OnCooldownTimeUpdated -= HandleCooldownTime;
        rescueEventController.OnPlayerInDanger -= HandlePlayerInDanger;
        rescueEventController.OnActiveTargetChanged -= HandleActiveTargetChanged;
        rescueEventController.OnStruggleActivated -= HandleStruggleActivated;
        rescueEventController.OnStruggleCapReached -= HandleStruggleCapReached;
    }

    private float _chainRefreshTimer;
    private const float ChainRefreshInterval = 1f; // recheck every second for new spawns

    private void Start()
    {
        rescueEventController ??= RescueEventController.Instance;
        if (rescueEventController == null) { Debug.LogError("[WorldSpaceRescueUI] RescueEventController not found.", this); enabled = false; return; }
        rescueEventController.OnRescueStateChanged -= HandleStateChanged;
        rescueEventController.OnRescueStateChanged += HandleStateChanged;
        rescueEventController.OnMashProgressUpdated -= HandleMashProgress;
        rescueEventController.OnMashProgressUpdated += HandleMashProgress;
        rescueEventController.OnMashTimeUpdated -= HandleMashTime;
        rescueEventController.OnMashTimeUpdated += HandleMashTime;
        rescueEventController.OnCooldownTimeUpdated -= HandleCooldownTime;
        rescueEventController.OnCooldownTimeUpdated += HandleCooldownTime;
        rescueEventController.OnPlayerInDanger -= HandlePlayerInDanger;
        rescueEventController.OnPlayerInDanger += HandlePlayerInDanger;
        rescueEventController.OnActiveTargetChanged -= HandleActiveTargetChanged;
        rescueEventController.OnActiveTargetChanged += HandleActiveTargetChanged;
        rescueEventController.OnStruggleActivated -= HandleStruggleActivated;
        rescueEventController.OnStruggleActivated += HandleStruggleActivated;
        rescueEventController.OnStruggleCapReached -= HandleStruggleCapReached;
        rescueEventController.OnStruggleCapReached += HandleStruggleCapReached;
        HideAll();
        RefreshChainSubscriptions();
    }

    private void RefreshChainSubscriptions()
    {
        foreach (var tb in FindObjectsByType<TetherBreakerEnemy>(FindObjectsSortMode.None))
        {
            tb.OnChainGrabbed -= HandleChainGrabbed;
            tb.OnChainReleased -= HandleChainReleased;
            tb.OnChainGrabbed += HandleChainGrabbed;
            tb.OnChainReleased += HandleChainReleased;
        }
    }

    private void HandleChainGrabbed(Player grabbed)
    {
        if (grabbed != ownerPlayer) return;
        chainPromptPanel?.SetActive(true);
    }

    private void HandleChainReleased()
    {
        chainPromptPanel?.SetActive(false);
    }

    private void Update()
    {
        // Periodically resubscribe to handle TetherBreakers spawned after scene load
        _chainRefreshTimer -= Time.deltaTime;
        if (_chainRefreshTimer <= 0f)
        {
            _chainRefreshTimer = ChainRefreshInterval;
            RefreshChainSubscriptions();
        }

        if (!_isOwnerInDanger) return;

        // Drain TTK ring every frame
        if (_activeTarget != null && ttkRing != null)
            ttkRing.fillAmount = _activeTarget.NormalisedTTK;

        if (!rootPanel.activeSelf)
            rootPanel.SetActive(true);
    }

    // ── OnPlayerInDanger ───────────────────────────────────────
    private void HandlePlayerInDanger(Player grabbed)
    {
        if (grabbed != ownerPlayer) return;

        _isOwnerInDanger = true;
        _soulInRange = false;

        rootPanel?.SetActive(true);
        cooldownOverlay?.SetActive(false);

        SetTTKRingCentred();
        if (ttkRing) { ttkRing.fillAmount = 1f; ttkRing.color = ttkColour; }

        fKeyRing?.gameObject.SetActive(false);
        if (pressFText) pressFText.gameObject.SetActive(false);

        // Show struggle ring if this trap supports it
        // Read from controller directly — _activeTarget may not be set yet when this fires
        bool canStruggle = rescueEventController?.ActiveTarget?.CanGrabbedPlayerStruggle ?? false;
        SetStruggleRingVisible(canStruggle);
    }

    // ── RescueState changes ────────────────────────────────────
    private void HandleStateChanged(RescueState state)
    {
        _state = state;

        switch (state)
        {
            case RescueState.Idle:
                if (_isOwnerInDanger)
                {
                    _isOwnerInDanger = false;
                    _soulInRange = false;
                    if (_splitCoroutine != null) StopCoroutine(_splitCoroutine);
                    HideAll();
                }
                else if (rootPanel != null && rootPanel.activeSelf)
                {
                    if (_splitCoroutine != null) StopCoroutine(_splitCoroutine);
                    _splitCoroutine = StartCoroutine(AnimateCollapse());
                }
                break;

            case RescueState.Success:
            case RescueState.Failed:
                _isOwnerInDanger = false;
                _soulInRange = false;
                if (_splitCoroutine != null) StopCoroutine(_splitCoroutine);
                HideAll();
                break;

            case RescueState.Triggered:
                if (!_isOwnerInDanger) return;
                _soulInRange = true;
                // Teleport fired — hide struggle ring, cap reached or not
                SetStruggleRingVisible(false);
                if (_splitCoroutine != null) StopCoroutine(_splitCoroutine);
                _splitCoroutine = StartCoroutine(AnimateSplit());
                break;

            case RescueState.Mashing:
                if (!_isOwnerInDanger) return;
                cooldownOverlay?.SetActive(false);
                if (fKeyRing) fKeyRing.color = fKeyColour;
                break;

            case RescueState.Cooldown:
                if (!_isOwnerInDanger) return;
                cooldownOverlay?.SetActive(true);
                if (fKeyRing) fKeyRing.color = cooldownColour;
                break;

            case RescueState.SoulDied:
                if (!_isOwnerInDanger) return;
                _soulInRange = false;
                if (_splitCoroutine != null) StopCoroutine(_splitCoroutine);
                _splitCoroutine = StartCoroutine(AnimateCollapse());
                break;
        }
    }

    // ── Struggle ring ──────────────────────────────────────────
    private void HandleStruggleActivated()
    {
        // Only show on the grabbed player's UI
        if (!_isOwnerInDanger) return;
        if (_activeTarget?.GrabbedPlayer != ownerPlayer) return;

        if (_struggleCoroutine != null) StopCoroutine(_struggleCoroutine);
        _struggleCoroutine = StartCoroutine(AnimateStruggleRing());
    }

    private void HandleStruggleCapReached()
    {
        if (!_isOwnerInDanger) return;
        if (_activeTarget?.GrabbedPlayer != ownerPlayer) return;
        // 30% cap hit — hide E ring, player can no longer struggle
        SetStruggleRingVisible(false);
        if (_struggleCoroutine != null) { StopCoroutine(_struggleCoroutine); _struggleCoroutine = null; }
    }

    private IEnumerator AnimateStruggleRing()
    {
        if (struggleRing == null) yield break;

        // Fill instantly to full, hold for struggleFillDuration, then drain
        struggleRing.fillAmount = 1f;
        struggleRing.color = struggleColour;

        yield return new WaitForSeconds(struggleFillDuration);

        // Drain back to 0
        float elapsed = 0f;
        float drainDuration = 0.15f;
        while (elapsed < drainDuration)
        {
            struggleRing.fillAmount = Mathf.Lerp(1f, 0f, elapsed / drainDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        struggleRing.fillAmount = 0f;
        _struggleCoroutine = null;
    }

    private void SetStruggleRingVisible(bool visible)
    {
        if (struggleRing == null) return;
        struggleRing.gameObject.SetActive(visible);
        if (struggleRing.fillAmount <= 0f) struggleRing.fillAmount = 0f;
        if (pressEText != null) pressEText.gameObject.SetActive(visible);
    }

    // ── Mash progress → F-key ring fill ───────────────────────
    private void HandleMashProgress(float normalised)
    {
        if (!_isOwnerInDanger || fKeyRing == null) return;
        fKeyRing.fillAmount = normalised;
    }

    private void HandleMashTime(float secondsRemaining)
    {
        if (!_isOwnerInDanger || fKeyRing == null) return;
        if (secondsRemaining < 1f)
            fKeyRing.color = Color.Lerp(fKeyColour, ttkColour, 1f - secondsRemaining);
    }

    private void HandleCooldownTime(float secondsRemaining)
    {
        if (!_isOwnerInDanger || cooldownText == null) return;
        cooldownText.text = secondsRemaining.ToString("F1");
    }

    // ── Split / collapse animations ────────────────────────────
    private IEnumerator AnimateSplit()
    {
        fKeyRing?.gameObject.SetActive(true);
        if (_fKeyRect) _fKeyRect.anchoredPosition = Vector2.zero;
        if (fKeyRing) { fKeyRing.fillAmount = 1f; fKeyRing.color = fKeyColour; }
        if (pressFText) pressFText.gameObject.SetActive(true);

        float elapsed = 0f;
        Vector2 ttkStart = _ttkRect != null ? _ttkRect.anchoredPosition : ttkCentrePos;
        Vector2 fKeyStart = _fKeyRect != null ? _fKeyRect.anchoredPosition : Vector2.zero;

        while (elapsed < splitDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / splitDuration);
            if (_ttkRect) _ttkRect.anchoredPosition = Vector2.Lerp(ttkStart, ttkSplitPos, t);
            if (_fKeyRect) _fKeyRect.anchoredPosition = Vector2.Lerp(fKeyStart, fKeySplitPos, t);
            yield return null;
        }

        if (_ttkRect) _ttkRect.anchoredPosition = ttkSplitPos;
        if (_fKeyRect) _fKeyRect.anchoredPosition = fKeySplitPos;
        _splitCoroutine = null;
    }

    private IEnumerator AnimateCollapse()
    {
        float elapsed = 0f;
        Vector2 ttkStart = _ttkRect != null ? _ttkRect.anchoredPosition : ttkSplitPos;
        Vector2 fKeyStart = _fKeyRect != null ? _fKeyRect.anchoredPosition : fKeySplitPos;

        while (elapsed < splitDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / splitDuration);
            if (_ttkRect) _ttkRect.anchoredPosition = Vector2.Lerp(ttkStart, ttkCentrePos, t);
            if (_fKeyRect) _fKeyRect.anchoredPosition = Vector2.Lerp(fKeyStart, Vector2.zero, t);
            yield return null;
        }

        fKeyRing?.gameObject.SetActive(false);
        if (pressFText) pressFText.gameObject.SetActive(false);
        if (_ttkRect) _ttkRect.anchoredPosition = ttkCentrePos;
        _splitCoroutine = null;
    }

    // ── Helpers ────────────────────────────────────────────────
    private void SetTTKRingCentred()
    {
        if (_ttkRect) _ttkRect.anchoredPosition = ttkCentrePos;
    }

    private void HideAll()
    {
        rootPanel?.SetActive(false);
        fKeyRing?.gameObject.SetActive(false);
        if (pressFText) pressFText.gameObject.SetActive(false);
        cooldownOverlay?.SetActive(false);
        if (_ttkRect) _ttkRect.anchoredPosition = ttkCentrePos;

        // Hide struggle ring
        SetStruggleRingVisible(false);
        if (_struggleCoroutine != null) { StopCoroutine(_struggleCoroutine); _struggleCoroutine = null; }
        if (struggleRing) struggleRing.fillAmount = 0f;
        chainPromptPanel?.SetActive(false);
    }

    private void HandleActiveTargetChanged(IRescueTarget target)
    {
        _activeTarget = target;
    }
}