using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Text")]
    [SerializeField] private TMP_Text pressFText;
    [SerializeField] private TMP_Text cooldownText;

    [Header("Colours")]
    [SerializeField] private Color ttkColour = new Color(0.9f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color fKeyColour = new Color(0.2f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color cooldownColour = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("Animation")]
    [SerializeField] private float splitDuration = 0.3f;

    private RescueState _state;
    private IRescueTarget _activeTarget;

    private RectTransform _ttkRect;
    private RectTransform _fKeyRect;
    private bool _isOwnerInDanger;
    private bool _soulInRange;
    private Coroutine _splitCoroutine;

    // ── Lifecycle ──────────────────────────────────────────────
    private void Awake()
    {
        _ttkRect = ttkRing?.GetComponent<RectTransform>();
        _fKeyRect = fKeyRing?.GetComponent<RectTransform>();
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
    }

    private void Start() => HideAll();

    private void Update()
    {
        if (!_isOwnerInDanger) return;

        // Drain TTK ring every frame
        if (_activeTarget != null && ttkRing != null)
            ttkRing.fillAmount = _activeTarget.NormalisedTTK;

        // Keep panel visible while in danger
        if (!rootPanel.activeSelf)
            rootPanel.SetActive(true);
    }

    // ── OnPlayerInDanger — fires at moment of grab, before soul arrives ──
    private void HandlePlayerInDanger(Player grabbed)
    {
        if (grabbed != ownerPlayer) return;

        _isOwnerInDanger = true;
        _soulInRange = false;

        rootPanel?.SetActive(true);
        cooldownOverlay?.SetActive(false);

        // TTK ring centred, full, red — soul not here yet
        SetTTKRingCentred();
        if (ttkRing) { ttkRing.fillAmount = 1f; ttkRing.color = ttkColour; }

        // F-key ring hidden until soul arrives
        fKeyRing?.gameObject.SetActive(false);
        if (pressFText) pressFText.gameObject.SetActive(false);
    }

    // ── RescueState changes ────────────────────────────────────
    private void HandleStateChanged(RescueState state)
    {
        _state = state;

        switch (state)
        {
            case RescueState.Idle:
                // Three paths reach here:
                // 1. Killer died before soul arrived (_isOwnerInDanger=true) — hide UI
                // 2. CleanupRescueEvent after Failed/Success (_isOwnerInDanger=false,
                //    panel already hidden) — do nothing, skip spurious collapse
                // 3. Genuinely idle with panel visible — run collapse animation
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
                // Soul has arrived in radius — animate split
                if (!_isOwnerInDanger) return;
                _soulInRange = true;
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
                if (pressFText) pressFText.text = "Wait...";
                break;

            case RescueState.SoulDied:
                if (!_isOwnerInDanger) return;
                // Soul died — collapse back to single TTK ring
                _soulInRange = false;
                if (_splitCoroutine != null) StopCoroutine(_splitCoroutine);
                _splitCoroutine = StartCoroutine(AnimateCollapse());
                break;
        }
    }

    // ── Mash progress → F-key ring fill ───────────────────────
    private void HandleMashProgress(float normalised)
    {
        if (!_isOwnerInDanger || fKeyRing == null) return;
        fKeyRing.fillAmount = normalised;
    }

    // ── Mash time remaining → F-key ring color pulse ──────────
    private void HandleMashTime(float secondsRemaining)
    {
        if (!_isOwnerInDanger || fKeyRing == null) return;
        if (secondsRemaining < 1f)
            fKeyRing.color = Color.Lerp(fKeyColour, ttkColour,
                1f - secondsRemaining); // pulses toward red as time runs out
    }

    private void HandleCooldownTime(float secondsRemaining)
    {
        if (!_isOwnerInDanger || cooldownText == null) return;
        cooldownText.text = secondsRemaining.ToString("F1");
    }

    // ── Split animation ────────────────────────────────────────
    // TTK ring slides left, F-key ring fades + slides in from right
    private IEnumerator AnimateSplit()
    {
        // Prepare F-key ring
        fKeyRing?.gameObject.SetActive(true);
        if (_fKeyRect) _fKeyRect.anchoredPosition = Vector2.zero; // start at centre
        if (fKeyRing) { fKeyRing.fillAmount = 1f; fKeyRing.color = fKeyColour; }
        if (pressFText) pressFText.gameObject.SetActive(true);
        if (pressFText) pressFText.text = "Press F!";

        // Animate both rings sliding apart
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

    // Collapse back to single centred TTK ring (soul died)
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
    }

    private void HandleActiveTargetChanged(IRescueTarget target)
    {
        _activeTarget = target;
    }
}