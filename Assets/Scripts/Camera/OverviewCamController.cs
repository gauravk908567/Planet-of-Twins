using System;
using UnityEngine;

/// <summary>
/// Toggle overview camera with B key.
/// Press B → enter overview (strategic bird's eye view)
/// Press B again → exit overview
/// Auto-exits after maxHoldDuration seconds.
/// Cooldown prevents abuse after exit.
/// Time freezes during overview — player and enemies pause.
/// </summary>
public class OverviewCamController : MonoBehaviour, IOverviewBroadcaster
{
    public static OverviewCamController Instance { get; private set; }

    [SerializeField] private KeyCode overviewKey = KeyCode.B;
    [SerializeField] private float maxDuration = 5f;
    [SerializeField] private float cooldownDuration = 4f;

    public event Action<bool> OnOverviewToggled;
    public bool IsOverviewActive { get; private set; }
    public float CooldownDuration => cooldownDuration;

    private float _activeTimer = 0f;
    private float _cooldownTimer = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Tick cooldown
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.unscaledDeltaTime;
            return;
        }

        if (!Input.GetKeyDown(overviewKey)) return;

        if (!IsOverviewActive)
            StartOverview();
        else
            EndOverview();
    }

    private void LateUpdate()
    {
        if (!IsOverviewActive) return;

        // Auto-exit after max duration using unscaled time
        _activeTimer += Time.unscaledDeltaTime;
        if (_activeTimer >= maxDuration)
            EndOverview();
    }

    private void StartOverview()
    {
        IsOverviewActive = true;
        _activeTimer = 0f;
        TimeScaleService.Instance?.Request(this, 0f);
        OnOverviewToggled?.Invoke(true);
    }

    private void EndOverview()
    {
        IsOverviewActive = false;
        _cooldownTimer = cooldownDuration;
        TimeScaleService.Instance?.Release(this);
        OnOverviewToggled?.Invoke(false);
    }
}