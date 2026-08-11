using UnityEngine;
using System;

public class SharedHealthPool : MonoBehaviour, ISharedHealthPool
{
    public static SharedHealthPool Instance { get; private set; }
    [SerializeField] private PlayerHealthComponent leftPlayer;
    [SerializeField] private PlayerHealthComponent rightPlayer;

    private const float BaseMaxHealth = 200f;
    public float MaxCombinedHealth => BaseMaxHealth;
    public float CombinedHealth { get; private set; }
    public float IncomingDamageMultiplier { get; set; } = 1f;

    public event Action<float> OnCombinedHealthChanged;
    public event Action OnSharedPoolEmpty;

    // ── Survival channel (BUG-081) ─────────────────────────────────────────────
    // CombinedHealth above sums the two DISPLAY healths — real HP multiplied by each twin's
    // distance modifier — so it FALLS when the twins merely walk apart. That masked value still
    // legitimately drives game-over (OnSharedPoolEmpty: being stretched past the limit genuinely
    // kills), but it must NOT drive the bar FILL, or a stretched-but-alive pair reads as "dying".
    // CombinedSurvival01 is the distance-independent real pool (0..1) and drives fill instead.
    public float CombinedSurvival01
    {
        get
        {
            int n = 0; float sum = 0f;
            if (leftPlayer != null)  { sum += leftPlayer.SurvivalHealth01;  n++; }
            if (rightPlayer != null) { sum += rightPlayer.SurvivalHealth01; n++; }
            return n == 0 ? 0f : sum / n;
        }
    }

    // Pulses whenever either twin's health/distance moves — the trigger the bar FILL subscribes
    // to so it re-reads CombinedSurvival01 (never the masked CombinedHealth).
    public event Action OnSurvivalChanged;

    /// <summary>
    /// Setsuna uses this to snapshot health at cast time and restore on rewind.
    /// </summary>
    public float CurrentHealth => CombinedHealth;

    /// <summary>
    /// Force-sets both player health components to split the target value evenly.
    /// Called by SetsunaSystem after rewind to restore health snapshot.
    /// </summary>
    public void ForceSetHealth(float targetCombined)
    {
        float half = Mathf.Clamp(targetCombined * 0.5f, 0f, BaseMaxHealth * 0.5f);
        leftPlayer?.SetDisplayHealthDirectly(half);
        rightPlayer?.SetDisplayHealthDirectly(half);
    }

    // FIX: named delegates — same instance used for += and -=
    private Action<float> _onLeftChanged;
    private Action<float> _onRightChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        CombinedHealth = MaxCombinedHealth;

        // FIX: allocate once in Awake — references are valid by here
        _onLeftChanged = _ => HandleTwinChanged();
        _onRightChanged = _ => HandleTwinChanged();
    }

    private void OnEnable()
    {
        if (leftPlayer != null) leftPlayer.OnDisplayHealthChanged += _onLeftChanged;
        if (rightPlayer != null) rightPlayer.OnDisplayHealthChanged += _onRightChanged;

        RecalculateCombined(); // sync immediately on enable
    }

    private void OnDisable()
    {
        if (leftPlayer != null) leftPlayer.OnDisplayHealthChanged -= _onLeftChanged;
        if (rightPlayer != null) rightPlayer.OnDisplayHealthChanged -= _onRightChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Both twins route their change events here: keep the masked-combined + game-over path exactly
    // as before, then pulse the survival channel that drives the bar FILL (BUG-081).
    private void HandleTwinChanged()
    {
        RecalculateCombined();
        OnSurvivalChanged?.Invoke();
    }

    private void RecalculateCombined()
    {
        float newCombined = (leftPlayer != null ? leftPlayer.DisplayHealth : 0f)
                          + (rightPlayer != null ? rightPlayer.DisplayHealth : 0f);

        if (Mathf.Abs(newCombined - CombinedHealth) > 0.001f)
        {
            CombinedHealth = newCombined;
            OnCombinedHealthChanged?.Invoke(CombinedHealth);
            if (CombinedHealth <= 0f)
                OnSharedPoolEmpty?.Invoke();
        }
    }
}