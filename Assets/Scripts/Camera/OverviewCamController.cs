using System;
using UnityEngine;

/// <summary>
/// Overview camera — HOLD B (reworked 2026-07-16, user spec):
///   • Hold B → enter overview and stay as long as the button is held (no max duration).
///   • Release B → camera returns and the game resumes. Optional cooldown after release.
///   • While held, the WORLD stops but ongoing VFX keep playing: enemies (brains/agents/
///     abilities) freeze through <see cref="TimeFactorManager.TriggerEffect"/> (the same
///     entity-freeze the soul cast uses — Time.timeScale is untouched, so particles/cues
///     continue) and the player's gameplay inputs freeze via
///     <see cref="IInputProvider.SetGameplayFrozen"/> (movement, attacks, abilities, holds).
///     Pause (ESC) stays live.
/// KNOWN GAP: already-flying projectiles run on scaled time and keep travelling — stopping
/// them needs the ITimeAffected contract extended to pooled projectiles (backlog).
/// Guard: if another system (soul cast) already owns the entity freeze, overview neither
/// re-triggers nor resolves it — it only borrows the view.
/// R10: no Time.timeScale writes here anymore (the old toggle used TimeScaleService @ 0).
/// </summary>
public class OverviewCamController : MonoBehaviour, IOverviewBroadcaster
{
    public static OverviewCamController Instance { get; private set; }

    [Tooltip("Seconds (unscaled) before B works again after release — anti-spam (user call 2026-07-16).")]
    [SerializeField] private float cooldownDuration = 4f;

    public event Action<bool> OnOverviewToggled;
    public bool IsOverviewActive { get; private set; }
    public float CooldownDuration => cooldownDuration;

    private float _cooldownTimer = 0f;   // unscaled — must tick while the world is stopped
    private bool _ownsWorldFreeze;       // false when soul cast etc. already held the freeze

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (IsOverviewActive) EndOverview();   // never leak a frozen world across Restart
            Instance = null;
        }
    }

    // P13: B comes through IInputProvider (Input System) — raw Input.* is banned.
    private IInputProvider _input;

    private void Start()
    {
        _input = TwinInputReader.Instance;
        if (_input == null)
            Debug.LogError("[OverviewCamController] TwinInputReader.Instance unresolved — overview key dead.", this);
    }

    private void Update()
    {
        if (_input == null) return;

        if (IsOverviewActive)
        {
            if (!_input.GetOverviewHeld()) EndOverview();   // release = resume, instantly
            return;
        }

        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.unscaledDeltaTime;
            return;
        }

        if (_input.GetOverviewDown()) StartOverview();
    }

    private void StartOverview()
    {
        IsOverviewActive = true;

        // Freeze the world, not time: enemies stop, VFX keep playing. Don't stomp an
        // effect another system already owns (soul cast) — just ride along.
        var tfm = TimeFactorManager.Instance;
        _ownsWorldFreeze = tfm != null && !tfm.IsEffectActive;
        if (_ownsWorldFreeze) tfm.TriggerEffect();

        _input.SetGameplayFrozen(true);
        OnOverviewToggled?.Invoke(true);
    }

    private void EndOverview()
    {
        IsOverviewActive = false;
        _cooldownTimer = cooldownDuration;

        if (_ownsWorldFreeze) TimeFactorManager.Instance?.ResolveEffect();
        _ownsWorldFreeze = false;

        _input?.SetGameplayFrozen(false);
        OnOverviewToggled?.Invoke(false);
    }
}
