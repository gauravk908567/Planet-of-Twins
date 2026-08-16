using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;

/// <summary>
/// DEPRECATED. Replaced by QTEManager (persistent) + QTESceneAnchor (per-area).
/// Keep this file only while existing Prefab/Scene Inspector slots still reference it.
/// Once all anchors are wired to QTEManager, delete this class.
/// </summary>
[System.Obsolete("Use QTEManager + QTESceneAnchor. See QTEManager.cs.")]
public class QTEController : MonoBehaviour
{
    [Header("Trigger points (exactly 2)")]
    [SerializeField] private QTETriggerPoint[] triggerPoints;
    [SerializeField] private QTEZoneTrigger qteZoneTrigger;

    [Header("Activatables — fired on success")]
    [SerializeField] private MonoBehaviour[] activatableMono;

    [Header("Services — auto-found at runtime from Persistent scene")]
    // These live in Persistent.unity — do NOT wire in Inspector across scenes.
    // QTEController finds them at Start() once Persistent is loaded.
    private MonoBehaviour enemyFreezeServiceMono;
    private MonoBehaviour cameraControllerMono;
    private CameraSwitcher cameraSwitcher;

    [Header("QTE Camera")]
    [SerializeField] private CinemachineCamera qteCamera;

    [Header("Mash phase")]
    [SerializeField] private float mashDuration = 5f;
    [SerializeField] private int mashCountRequired = 20;
    // P13: mash reads IInputProvider.GetQTEMashDown (F) — the old per-instance mashKey
    // KeyCode was removed (raw Input.* is banned; every instance used F).

    [Header("UI — matches RescueButtonUI layout")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Image fillBar;
    [SerializeField] private Image timerRing;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text countdownText;

    [Header("Colours — match RescueButtonUI")]
    [SerializeField] private Color activeColour = new Color(0.2f, 0.8f, 0.3f, 1f);
    [SerializeField] private Color dangerColour = new Color(0.9f, 0.2f, 0.2f, 1f);

    [Header("Mode")]
    [SerializeField] private bool isTimedApproach = false;
    [SerializeField] private float windowDuration = 15f;

    private IEnemyFreezeService _freezeService;
    private ICameraController _cameraController;
    private IActivatable[] _activatables;

    private enum QTEPhase { Inactive, WaitingForPlayers, Mashing, Success, Failed }
    private QTEPhase _phase = QTEPhase.Inactive;

    /// <summary>Polled by QTESuccessWatcher and TutorialQTEStepSO.</summary>
    public bool IsSuccess => _phase == QTEPhase.Success;

    private IInputProvider _input;   // P13: resolved in Start (R4 — area→Persistent)
    private int _mashCount;
    private float _mashTimer;
    private float _windowTimer;

    private void Awake()
    {
        _activatables = new IActivatable[activatableMono?.Length ?? 0];
        for (int i = 0; i < _activatables.Length; i++)
            _activatables[i] = activatableMono[i] as IActivatable;

        SetPanelVisible(false);
    }

    private void Start()
    {
        // Persistent services load before area scenes — safe to find here.
        enemyFreezeServiceMono = FindService<IEnemyFreezeService>();
        cameraControllerMono   = FindService<ICameraController>();
        cameraSwitcher         = FindAnyObjectByType<CameraSwitcher>(FindObjectsInactive.Include);

        _freezeService   = enemyFreezeServiceMono as IEnemyFreezeService;
        _cameraController = cameraControllerMono as ICameraController;

        if (_freezeService == null)
            Debug.LogWarning("[QTEController] IEnemyFreezeService not found.", this);
        if (_cameraController == null)
            Debug.LogWarning("[QTEController] ICameraController not found.", this);
        if (cameraSwitcher == null)
            Debug.LogWarning("[QTEController] CameraSwitcher not found.", this);

        _input = PlayerInputRouter.SharedInput;   // M0: shared-UI seam (falls back to TwinInputReader.Instance); R4 — Persistent loads before area scenes
        if (_input == null)
            Debug.LogError("[QTEController] PlayerInputRouter.SharedInput unresolved — QTE mash dead.", this);
    }

    // Returns the first MonoBehaviour in any loaded scene that implements T.
    private static MonoBehaviour FindService<T>() where T : class
    {
        foreach (var mb in FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mb is T) return mb;
        return null;
    }

    private void OnEnable()
    {
        foreach (var tp in triggerPoints)
        {
            if (tp == null) continue;
            tp.OnPlayerLockedIn += HandlePlayerLockedIn;
            tp.OnPlayerReleased += HandlePlayerReleased;
        }
    }

    private void OnDisable()
    {
        foreach (var tp in triggerPoints)
        {
            if (tp == null) continue;
            tp.OnPlayerLockedIn -= HandlePlayerLockedIn;
            tp.OnPlayerReleased -= HandlePlayerReleased;
        }
    }

    private void Update()
    {
        switch (_phase)
        {
            case QTEPhase.WaitingForPlayers when isTimedApproach:
                _windowTimer -= Time.deltaTime;
                if (countdownText != null)
                    countdownText.text = $"{Mathf.CeilToInt(_windowTimer)}";
                if (_windowTimer <= 0f)
                    FailQTE();
                break;

            case QTEPhase.Mashing:
                _mashTimer -= Time.deltaTime;

                if (_input != null && _input.GetQTEMashDown())
                    _mashCount++;

                float progress = Mathf.Clamp01((float)_mashCount / mashCountRequired);

                if (fillBar != null)
                    fillBar.fillAmount = progress;

                if (timerRing != null)
                {
                    timerRing.fillAmount = mashDuration > 0f
                        ? Mathf.Clamp01(_mashTimer / mashDuration) : 0f;
                    timerRing.color = Color.Lerp(activeColour, dangerColour, progress);
                    if (_mashTimer < 1f) timerRing.color = dangerColour;
                }

                if (_mashCount >= mashCountRequired) SucceedQTE();
                else if (_mashTimer <= 0f) FailQTE();
                break;
        }
    }

    /// <summary>
    /// Starts the QTE. Switches camera immediately so the player sees the
    /// gate before the trigger points activate.
    /// Called by QTEZoneTrigger (gameplay) or TutorialQTEStepSO (tutorial).
    /// </summary>
    public void BeginQTE()
    {
        if (_phase != QTEPhase.Inactive) return;

        _phase = QTEPhase.WaitingForPlayers;
        _windowTimer = windowDuration;

        _freezeService?.FreezeAll();

        // ── Camera switches HERE — before trigger points activate ──
        cameraSwitcher?.SuppressAutoSwitch(true);
        if (qteCamera != null && _cameraController != null)
            _cameraController.SwitchToCamera(qteCamera);

        foreach (var tp in triggerPoints)
            tp?.SetActive(true);

        if (countdownText != null)
            countdownText.gameObject.SetActive(isTimedApproach);

        Debug.Log($"[QTEController] QTE started. Mode={(isTimedApproach ? "Timed" : "Infinite")}");
    }

    private void HandlePlayerLockedIn(QTETriggerPoint tp, Player player)
    {
        if (_phase != QTEPhase.WaitingForPlayers) return;

        // Camera already switched in BeginQTE — just check if all locked
        bool allLocked = true;
        foreach (var t in triggerPoints)
        {
            if (t == null || !t.IsOccupied) { allLocked = false; break; }
        }

        if (allLocked)
            BeginMashPhase();
    }

    private void HandlePlayerReleased(QTETriggerPoint tp, Player player)
    {
        if (_phase == QTEPhase.WaitingForPlayers || _phase == QTEPhase.Mashing)
            FailQTE();
    }

    private void BeginMashPhase()
    {
        _phase = QTEPhase.Mashing;
        _mashCount = 0;
        _mashTimer = mashDuration;

        if (fillBar != null) { fillBar.fillAmount = 0f; }
        if (timerRing != null) { timerRing.fillAmount = 1f; timerRing.color = activeColour; }
        if (instructionText != null) instructionText.text = "Press F!";
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        SetPanelVisible(true);
        Debug.Log("[QTEController] Mash phase started.");
    }

    private void SucceedQTE()
    {
        _phase = QTEPhase.Success;
        SetPanelVisible(false);
        ReleaseTriggers();
        ReturnCamera();

        foreach (var a in _activatables)
            a?.Activate();

        StartCoroutine(UnfreezeAfterDelay(1.5f));
        Debug.Log("[QTEController] QTE succeeded.");
    }

    private void FailQTE()
    {
        if (_phase == QTEPhase.Inactive || _phase == QTEPhase.Success) return;
        _phase = QTEPhase.Failed;

        SetPanelVisible(false);
        ReleaseTriggers();
        ReturnCamera();
        _freezeService?.UnfreezeAll();
        StartCoroutine(ResetAfterFail());
        Debug.Log("[QTEController] QTE failed — enemies resumed.");
    }

    private IEnumerator UnfreezeAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay); // unscaled — must not stretch during Setsuna
        _freezeService?.UnfreezeAll();
    }

    private IEnumerator ResetAfterFail()
    {
        yield return null;
        _phase = QTEPhase.Inactive;
        qteZoneTrigger?.ResetTrigger();
    }

    private void ReleaseTriggers()
    {
        foreach (var tp in triggerPoints)
        {
            tp?.ForceRelease();
            tp?.SetActive(false);
        }
    }

    private void ReturnCamera()
    {
        cameraSwitcher?.SuppressAutoSwitch(false);
        if (_cameraController is CameraManager cm)
            _cameraController.SwitchToCamera(cm.CinemachineCloseCam);
    }

    private void SetPanelVisible(bool visible)
    {
        rootPanel?.SetActive(visible);
    }

    private void OnDestroy() => cameraSwitcher?.SuppressAutoSwitch(false);

    public void AbortQTE()
    {
        if (_phase == QTEPhase.Inactive || _phase == QTEPhase.Success) return;
        FailQTE();
    }
}