using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

/// <summary>
/// Persistent singleton. Owns the QTE state machine and shared UI.
/// Lives in Persistent.unity — wired in Inspector like any other persistent system.
///
/// One QTE runs at a time. Area scenes provide a QTESceneAnchor that bundles
/// scene-local data (trigger points, camera, activatables). QTEManager never
/// holds direct scene references — it drives everything through the anchor.
///
/// FLOW:
///   QTEZoneTrigger → anchor.BeginQTE() → QTEManager.Instance.BeginQTE(anchor)
///     → freeze enemies, switch camera, arm trigger points
///     → both players step on trigger points → BeginMashPhase()
///     → mash F → SucceedQTE() → anchor.FireSuccess(), OnQTESucceeded event
///                               OR TimeOut → FailQTE() → OnQTEFailed event
///
/// WIRING:
///   • Services (IEnemyFreezeService, ICameraController, CameraSwitcher) —
///     wire in Inspector; all live in Persistent.unity.
///   • UI: no Persistent UI. Each QTESceneAnchor carries World UI refs (rootPanel,
///     fillBar, timerRing, labels). QTEManager pulls them at BeginQTE time.
/// </summary>
public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance { get; private set; }

    [Header("Services — wire in Inspector (Persistent.unity)")]
    [SerializeField] private MonoBehaviour enemyFreezeServiceMono;   // IEnemyFreezeService
    [SerializeField] private MonoBehaviour cameraControllerMono;     // ICameraController
    [SerializeField] private CameraSwitcher cameraSwitcher;

    // UI refs are supplied per-QTE by the QTESceneAnchor (scene-local World UI).
    // QTEManager has no persistent UI of its own — each anchor provides its own canvas.
    private GameObject _rootPanel;
    private Image      _fillBar;
    private Image      _timerRing;
    // Optional PoT/UIRingTimer widget on the anchor's ring Image (game.md §17.5). When present
    // it owns fill AND colour/urgency (shader-side); when absent the legacy fillAmount+colour
    // path below runs unchanged — anchors migrate one at a time, fail-open.
    private UIRingTimerView _timerRingView;
    private TMP_Text   _instructionLabel;
    private TMP_Text   _countdownLabel;

    [Header("Colours")]
    [SerializeField] private Color activeColour = new Color(0.2f, 0.8f, 0.3f, 1f);
    [SerializeField] private Color dangerColour = new Color(0.9f, 0.2f, 0.2f, 1f);

    // ── Events ─────────────────────────────────────────────────────────────
    /// <summary>Fired when a QTE succeeds. Arg = eventId from QTEDefinitionSO.</summary>
    public event Action<string> OnQTESucceeded;
    /// <summary>Fired when a QTE fails or times out.</summary>
    public event Action<string> OnQTEFailed;

    // ── State machine ──────────────────────────────────────────────────────
    private enum QTEPhase { Inactive, WaitingForPlayers, Mashing, Success, Failed }
    private QTEPhase _phase = QTEPhase.Inactive;

    public bool IsQTEActive => _phase != QTEPhase.Inactive && _phase != QTEPhase.Success && _phase != QTEPhase.Failed;
    public bool IsSuccess   => _phase == QTEPhase.Success;

    // ── Active anchor ──────────────────────────────────────────────────────
    private QTESceneAnchor _activeAnchor;
    private QTEDefinitionSO ActiveDef => _activeAnchor?.Definition;

    // ── Runtime ────────────────────────────────────────────────────────────
    private IEnemyFreezeService _freezeService;
    private ICameraController   _cameraController;
    private IInputProvider      _input;   // P13: mash via Input System — raw Input.* is banned
    private int   _mashCount;
    private float _mashTimer;
    private float _windowTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _freezeService    = enemyFreezeServiceMono as IEnemyFreezeService;
        _cameraController = cameraControllerMono   as ICameraController;

        if (_freezeService == null && enemyFreezeServiceMono != null)
            Debug.LogWarning("[QTEManager] enemyFreezeServiceMono does not implement IEnemyFreezeService.", this);
        if (_cameraController == null && cameraControllerMono != null)
            Debug.LogWarning("[QTEManager] cameraControllerMono does not implement ICameraController.", this);

        SetPanelVisible(false);
    }

    private void Start()
    {
        _input = PlayerInputRouter.SharedInput;   // M0: shared-UI seam (falls back to TwinInputReader.Instance), R4 line
        if (_input == null)
            Debug.LogError("[QTEManager] PlayerInputRouter.SharedInput unresolved — QTE mash dead.", this);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Start a QTE driven by the given anchor.
    /// Safe to call from any area scene — QTEManager accesses the anchor's
    /// trigger points and camera through the anchor's public properties.
    /// </summary>
    public void BeginQTE(QTESceneAnchor anchor)
    {
        if (_phase != QTEPhase.Inactive)
        {
            Debug.LogWarning($"[QTEManager] QTE already in phase {_phase}. " +
                             $"Ignoring BeginQTE for '{anchor?.Definition?.eventId}'.", this);
            return;
        }

        _activeAnchor = anchor;
        _phase        = QTEPhase.WaitingForPlayers;
        _windowTimer  = ActiveDef?.windowDuration ?? 15f;

        // Pull World UI refs from the scene-local anchor
        _rootPanel        = anchor.RootPanel;
        _fillBar          = anchor.FillBar;
        _timerRing        = anchor.TimerRing;
        _timerRingView    = _timerRing != null ? _timerRing.GetComponent<UIRingTimerView>() : null;
        _instructionLabel = anchor.InstructionLabel;
        _countdownLabel   = anchor.CountdownLabel;

        _freezeService?.FreezeAll();

        cameraSwitcher?.SuppressAutoSwitch(true);
        if (anchor.QTECamera != null && _cameraController != null)
            _cameraController.SwitchToCamera(anchor.QTECamera);

        anchor.SetTriggerPointsActive(true);

        if (_countdownLabel != null)
            _countdownLabel.gameObject.SetActive(ActiveDef?.isTimedApproach ?? false);

        // Subscribe trigger point events
        foreach (var tp in anchor.TriggerPoints)
        {
            if (tp == null) continue;
            tp.OnPlayerLockedIn += HandlePlayerLockedIn;
            tp.OnPlayerReleased += HandlePlayerReleased;
        }

        Debug.Log($"[QTEManager] QTE started: '{ActiveDef?.eventId}' " +
                  $"mode={(ActiveDef?.isTimedApproach ?? false ? "Timed" : "Infinite")}");
    }

    /// <summary>Force-abort the active QTE (used by SoftResetController).</summary>
    public void AbortQTE()
    {
        if (_phase == QTEPhase.Inactive || _phase == QTEPhase.Success) return;
        FailQTE();
    }

    // ── Update ─────────────────────────────────────────────────────────────

    private void Update()
    {
        switch (_phase)
        {
            case QTEPhase.WaitingForPlayers when (ActiveDef?.isTimedApproach ?? false):
                _windowTimer -= Time.unscaledDeltaTime;
                if (_countdownLabel != null)
                    _countdownLabel.text = $"{Mathf.CeilToInt(_windowTimer)}";
                if (_windowTimer <= 0f) FailQTE();
                break;

            case QTEPhase.Mashing:
                _mashTimer -= Time.unscaledDeltaTime;

                // P13: mash reads the QTEMash action (F). QTEDefinitionSO.mashKey is legacy
                // display data now — per-QTE distinct mash keys were never used (all assets = F).
                if (ActiveDef != null && _input != null && _input.GetQTEMashDown())
                    _mashCount++;

                int required = ActiveDef?.mashCountRequired ?? 20;
                float mashDur = ActiveDef?.mashDuration ?? 5f;
                float progress = Mathf.Clamp01((float)_mashCount / required);

                if (_fillBar != null)
                    _fillBar.fillAmount = progress;

                float timeFrac = mashDur > 0f ? Mathf.Clamp01(_mashTimer / mashDur) : 0f;
                if (_timerRingView != null)
                {
                    // New ring widget: shader owns colour + urgency heat/pulse + closing ring.
                    _timerRingView.SetProgress(timeFrac);
                }
                else if (_timerRing != null)
                {
                    _timerRing.fillAmount = timeFrac;
                    _timerRing.color = Color.Lerp(activeColour, dangerColour, progress);
                    if (_mashTimer < 1f) _timerRing.color = dangerColour;
                }

                if (_mashCount >= required) SucceedQTE();
                else if (_mashTimer <= 0f) FailQTE();
                break;
        }
    }

    // ── State transitions ──────────────────────────────────────────────────

    private void HandlePlayerLockedIn(QTETriggerPoint tp, Player player)
    {
        if (_phase != QTEPhase.WaitingForPlayers || _activeAnchor == null) return;

        bool allLocked = true;
        foreach (var t in _activeAnchor.TriggerPoints)
        {
            if (t == null || !t.IsOccupied) { allLocked = false; break; }
        }
        if (allLocked) BeginMashPhase();
    }

    private void HandlePlayerReleased(QTETriggerPoint tp, Player player)
    {
        if (_phase == QTEPhase.WaitingForPlayers || _phase == QTEPhase.Mashing)
            FailQTE();
    }

    private void BeginMashPhase()
    {
        _phase     = QTEPhase.Mashing;
        _mashCount = 0;
        _mashTimer = ActiveDef?.mashDuration ?? 5f;

        if (_fillBar != null) _fillBar.fillAmount = 0f;
        if (_timerRingView != null) _timerRingView.SetProgress(1f);
        else if (_timerRing != null) { _timerRing.fillAmount = 1f; _timerRing.color = activeColour; }
        if (_instructionLabel != null) _instructionLabel.text = ActiveDef?.instructionText ?? "Press F!";
        if (_countdownLabel != null) _countdownLabel.gameObject.SetActive(false);

        SetPanelVisible(true);
        Debug.Log("[QTEManager] Mash phase started.");
    }

    private void SucceedQTE()
    {
        var eventId = ActiveDef?.eventId;
        _phase = QTEPhase.Success;

        SetPanelVisible(false);
        UnsubscribeTriggerPoints();
        _activeAnchor?.SetTriggerPointsActive(false);
        ReturnCamera();
        _activeAnchor?.FireSuccess();

        StartCoroutine(UnfreezeAfterDelay(1.5f));
        OnQTESucceeded?.Invoke(eventId);
        Debug.Log($"[QTEManager] QTE succeeded: '{eventId}'");

        StartCoroutine(ResetToInactive());
    }

    private void FailQTE()
    {
        if (_phase == QTEPhase.Inactive || _phase == QTEPhase.Success) return;

        var eventId = ActiveDef?.eventId;
        _phase = QTEPhase.Failed;

        SetPanelVisible(false);
        UnsubscribeTriggerPoints();
        _activeAnchor?.SetTriggerPointsActive(false);
        ReturnCamera();
        _freezeService?.UnfreezeAll();

        OnQTEFailed?.Invoke(eventId);
        Debug.Log($"[QTEManager] QTE failed: '{eventId}'");

        StartCoroutine(ResetAfterFail());
    }

    // ── Coroutines ─────────────────────────────────────────────────────────

    private IEnumerator UnfreezeAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        _freezeService?.UnfreezeAll();
    }

    private IEnumerator ResetAfterFail()
    {
        yield return null;
        _activeAnchor?.ZoneTrigger?.ResetTrigger();
        _activeAnchor = null;
        _phase = QTEPhase.Inactive;
        ClearUIRefs();
    }

    private IEnumerator ResetToInactive()
    {
        yield return new WaitForSecondsRealtime(1.6f);
        _activeAnchor = null;
        _phase = QTEPhase.Inactive;
        ClearUIRefs();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void UnsubscribeTriggerPoints()
    {
        if (_activeAnchor == null) return;
        foreach (var tp in _activeAnchor.TriggerPoints)
        {
            if (tp == null) continue;
            tp.OnPlayerLockedIn -= HandlePlayerLockedIn;
            tp.OnPlayerReleased -= HandlePlayerReleased;
        }
    }

    private void ReturnCamera()
    {
        // Demote the QTE camera first so it no longer overrides, then release the
        // auto-switch suppression. CameraSwitcher.Update() picks the correct cam
        // (tutorial or gameplay) on the next frame — don't hardcode a specific camera
        // here or it causes a one-frame incorrect blend when in tutorial mode.
        if (_cameraController is CameraManager cm)
            cm.DemoteExternalCamera();
        cameraSwitcher?.SuppressAutoSwitch(false);
    }

    private void SetPanelVisible(bool visible) => _rootPanel?.SetActive(visible);

    private void ClearUIRefs()
    {
        _rootPanel = null; _fillBar = null; _timerRing = null; _timerRingView = null;
        _instructionLabel = null; _countdownLabel = null;
    }

    private void OnDestroy() => cameraSwitcher?.SuppressAutoSwitch(false);
}
