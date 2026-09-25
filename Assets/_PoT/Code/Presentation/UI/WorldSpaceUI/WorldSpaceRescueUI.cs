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

    // ── Button-glyph prompts (item 5 / P2b) ────────────────────
    // The F prompt reflects the PARTNER's device (they mash Interact); the E prompt reflects the GRABBED twin's
    // (they mash Struggle). Providers come from the controller so the glyph matches exactly what the mash reads.
    private const string RescueMashTemplate = "{Interact}";
    private const string StruggleTemplate = "{Struggle}";

    // ── Runtime ────────────────────────────────────────────────
    private RescueState _state;
    private IRescueTarget _activeTarget;

    private RectTransform _ttkRect;
    private RectTransform _fKeyRect;
    private UIRingTimerView _ttkView;       // §17.5 "one ring language" — null until the prefab ring is migrated
    private UIRingTimerView _fKeyView;
    private UIRingTimerView _struggleView;
    private ButtonPromptView _fKeyPrompt;    // press-feel (punch / ripple / glow) on the F-key mash ring
    private ButtonPromptView _strugglePrompt; // press-feel on the E struggle ring
    private float _lastMashProgress;         // pulse the F-key prompt only when the mash advances
    private bool _isOwnerInDanger;
    private bool _soulInRange;
    private Coroutine _splitCoroutine;
    private Coroutine _struggleCoroutine;

    // ── Lifecycle ──────────────────────────────────────────────
    private void Awake()
    {
        _ttkRect = ttkRing?.GetComponent<RectTransform>();
        _fKeyRect = fKeyRing?.GetComponent<RectTransform>();
        _ttkView = ttkRing != null ? ttkRing.GetComponent<UIRingTimerView>() : null;
        _fKeyView = fKeyRing != null ? fKeyRing.GetComponent<UIRingTimerView>() : null;
        _struggleView = struggleRing != null ? struggleRing.GetComponent<UIRingTimerView>() : null;
        _fKeyPrompt = fKeyRing != null ? fKeyRing.GetComponent<ButtonPromptView>() : null;
        _strugglePrompt = struggleRing != null ? struggleRing.GetComponent<ButtonPromptView>() : null;
        if (ownerPlayer == null)
            ownerPlayer = GetComponentInParent<Player>(true);
    }

    // Ring draw — prefer the PoT/UIRingTimer widget (game.md §17.5 "one ring language"); fall back to the
    // legacy Image.fillAmount/colour on any ring not yet wired with a UIRingTimerView, so rescue keeps
    // working on prefabs that haven't been migrated. Semantic colours (danger red / mash green /
    // struggle gold) are preserved — the widget upgrades the LOOK, not the meaning.
    private static void SetRingProgress(Image img, UIRingTimerView view, float p)
    {
        if (view != null) view.SetProgress(p);
        else if (img != null) img.fillAmount = p;
    }
    private static void SetRingColour(Image img, UIRingTimerView view, Color c)
    {
        if (view != null) view.SetFillColor(c);
        else if (img != null) img.color = c;
    }

    private void OnEnable()
    {
        // Item 5/P2b: re-resolve the F/E button glyphs when the active device flips (keyboard↔pad). Independent of
        // the controller subscription below, so it arms even before the controller resolves.
        LastUsedDeviceTracker.OnLastUsedChanged += OnDeviceSwitched;

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
        LastUsedDeviceTracker.OnLastUsedChanged -= OnDeviceSwitched;

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
            SetRingProgress(ttkRing, _ttkView, _activeTarget.NormalisedTTK);

        if (!rootPanel.activeSelf)
            rootPanel.SetActive(true);
    }

    // ── OnPlayerInDanger ───────────────────────────────────────
    private void HandlePlayerInDanger(Player grabbed)
    {
        if (grabbed != ownerPlayer) return;

        _isOwnerInDanger = true;
        _soulInRange = false;
        _lastMashProgress = 0f;

        rootPanel?.SetActive(true);
        cooldownOverlay?.SetActive(false);

        SetTTKRingCentred();
        // TTK ring reuses the QTE timer material (M_UIRingTimer_QTE) — let its near-white/gold look with
        // built-in urgency heat show, so only tint the legacy (non-widget) fallback red.
        if (ttkRing) { SetRingProgress(ttkRing, _ttkView, 1f); if (_ttkView == null) SetRingColour(ttkRing, _ttkView, ttkColour); }

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
                if (fKeyRing) SetRingColour(fKeyRing, _fKeyView, fKeyColour);
                break;

            case RescueState.Cooldown:
                if (!_isOwnerInDanger) return;
                cooldownOverlay?.SetActive(true);
                if (fKeyRing) SetRingColour(fKeyRing, _fKeyView, cooldownColour);
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

        _strugglePrompt?.Pulse();   // one punch/ripple/glow per E press
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
        SetRingProgress(struggleRing, _struggleView, 1f);
        SetRingColour(struggleRing, _struggleView, struggleColour);

        yield return new WaitForSeconds(struggleFillDuration);

        // Drain back to 0
        float elapsed = 0f;
        float drainDuration = 0.15f;
        while (elapsed < drainDuration)
        {
            SetRingProgress(struggleRing, _struggleView, Mathf.Lerp(1f, 0f, elapsed / drainDuration));
            elapsed += Time.deltaTime;
            yield return null;
        }
        SetRingProgress(struggleRing, _struggleView, 0f);
        _struggleCoroutine = null;
    }

    private void SetStruggleRingVisible(bool visible)
    {
        if (struggleRing == null) return;
        struggleRing.gameObject.SetActive(visible);
        if (struggleRing.fillAmount <= 0f) struggleRing.fillAmount = 0f;
        if (pressEText != null)
        {
            pressEText.gameObject.SetActive(visible);
            if (visible) ApplyStrugglePrompt();
        }
    }

    // ── Mash progress → F-key ring fill ───────────────────────
    private void HandleMashProgress(float normalised)
    {
        if (!_isOwnerInDanger || fKeyRing == null) return;
        SetRingProgress(fKeyRing, _fKeyView, normalised);
        // Pulse the button-ring feel only when the mash actually advances (one punch per press).
        if (normalised > _lastMashProgress + 0.001f) _fKeyPrompt?.Pulse();
        _lastMashProgress = normalised;
    }

    private void HandleMashTime(float secondsRemaining)
    {
        if (!_isOwnerInDanger || fKeyRing == null) return;
        if (secondsRemaining < 1f)
            SetRingColour(fKeyRing, _fKeyView, Color.Lerp(fKeyColour, ttkColour, 1f - secondsRemaining));
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
        if (fKeyRing) { SetRingProgress(fKeyRing, _fKeyView, 1f); SetRingColour(fKeyRing, _fKeyView, fKeyColour); }
        if (pressFText) { pressFText.gameObject.SetActive(true); ApplyRescuePrompt(); }

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
        if (struggleRing) SetRingProgress(struggleRing, _struggleView, 0f);
        chainPromptPanel?.SetActive(false);
    }

    private void HandleActiveTargetChanged(IRescueTarget target)
    {
        _activeTarget = target;
    }

    // ── Button glyphs (item 5 / P2b) ───────────────────────────
    private void OnDeviceSwitched(InputDeviceKind kind)
    {
        ApplyRescuePrompt();
        ApplyStrugglePrompt();
    }

    /// <summary>Set the F prompt to the PARTNER's device glyph for Interact (only while it's shown).</summary>
    private void ApplyRescuePrompt()
    {
        if (pressFText == null || rescueEventController == null || !pressFText.gameObject.activeSelf) return;
        InputGlyphText.Apply(pressFText, RescueMashTemplate, rescueEventController.RescueMashInput);
    }

    /// <summary>Set the E prompt to the GRABBED twin's device glyph for Struggle (only while it's shown).</summary>
    private void ApplyStrugglePrompt()
    {
        if (pressEText == null || rescueEventController == null || !pressEText.gameObject.activeSelf) return;
        InputGlyphText.Apply(pressEText, StruggleTemplate, rescueEventController.StruggleInput);
    }
}