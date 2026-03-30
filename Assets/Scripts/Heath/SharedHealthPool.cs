using UnityEngine;
using System;

public class SharedHealthPool : MonoBehaviour, ISharedHealthPool
{
    [SerializeField] private PlayerHealthComponent leftPlayer;
    [SerializeField] private PlayerHealthComponent rightPlayer;

    private const float BaseMaxHealth = 200f;
    public float MaxCombinedHealth => BaseMaxHealth;
    public float CombinedHealth { get; private set; }
    public float IncomingDamageMultiplier { get; set; } = 1f;

    public event Action<float> OnCombinedHealthChanged;
    public event Action OnSharedPoolEmpty;

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
        CombinedHealth = MaxCombinedHealth;

        // FIX: allocate once in Awake — references are valid by here
        _onLeftChanged = _ => RecalculateCombined();
        _onRightChanged = _ => RecalculateCombined();
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