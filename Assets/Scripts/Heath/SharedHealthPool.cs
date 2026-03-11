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