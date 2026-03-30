using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeManager : MonoBehaviour,
    IPointBank,
    ISkillUnlockState,
    IAbilityDataStore,
    ISkillTreePurchaser
{
    [Header("Kai")]
    [SerializeField] private AbilityUpgradeData _stunData;

    [Header("Lyra")]
    [SerializeField] private AbilityUpgradeData _possessData;

    [Header("Shared")]
    [SerializeField] private AbilityUpgradeData _gateData;
    [SerializeField] private AbilityUpgradeData _healthRegenData;

    [Header("Special Systems")]
    [SerializeField] private AbilityUpgradeData _accordSpirits;
    [SerializeField] private AbilityUpgradeData _coalesceData;
    [SerializeField] private AbilityUpgradeData _soulConvData;
    [SerializeField] private AbilityUpgradeData _empowerData;
    [SerializeField] private AbilityUpgradeData _accordData;

    [Header("Economy")]
    [SerializeField] private int _startingPoints = 0;

    // ── IPointBank ────────────────────────────────────────────
    public int CurrentPoints => _points;
    public event Action<int> OnPointsChanged;
    private int _points;

    public void AddPoints(int amount)
    {
        if (amount <= 0) return;
        _points += amount;
        OnPointsChanged?.Invoke(_points);
    }

    public bool TrySpendPoints(int amount)
    {
        if (_points < amount) return false;
        _points -= amount;
        OnPointsChanged?.Invoke(_points);
        return true;
    }

    // ── ISkillUnlockState ─────────────────────────────────────
    public bool IsAccordSpiritsUnlocked { get; private set; }
    public bool IsCoalesceUnlocked { get; private set; }
    public bool IsSoulConvergenceUnlocked { get; private set; }
    public bool IsEmpowerUnlocked { get; private set; }
    public bool IsAccordStateUnlocked { get; private set; }

    public event Action OnAccordSpiritsUnlocked;
    public event Action OnCoalesceUnlocked;
    public event Action OnSoulConvergenceUnlocked;
    public event Action OnEmpowerUnlocked;
    public event Action OnAccordStateUnlocked;

    // ── IAbilityDataStore ─────────────────────────────────────
    public AbilityUpgradeData StunData => _stunData;
    public AbilityUpgradeData PossessData => _possessData;
    public AbilityUpgradeData GateData => _gateData;
    public AbilityUpgradeData HealthRegenData => _healthRegenData;
    public AbilityUpgradeData AccordSpiritsData => _accordSpirits;
    public AbilityUpgradeData CoalesceData => _coalesceData;
    public AbilityUpgradeData SoulConvData => _soulConvData;
    public AbilityUpgradeData EmpowerData => _empowerData;
    public AbilityUpgradeData AccordData => _accordData;

    // ── ISkillTreePurchaser ───────────────────────────────────
    public event Action<AbilityUpgradeData> OnNodePurchased;

    public bool CanAfford(AbilityUpgradeData data)
        => data != null && data.HasNextNode && _points >= data.NextNodeCost;

    public bool TryPurchaseNode(AbilityUpgradeData data)
    {
        if (data == null || !data.HasNextNode) return false;
        if (!TrySpendPoints(data.NextNodeCost)) return false;

        data.UnlockNextNode();
        RaiseUnlockFlags(data);
        OnNodePurchased?.Invoke(data);
        return true;
    }

    // ── Lifecycle ─────────────────────────────────────────────
    void Awake()
    {
        _points = _startingPoints;
        ResetAllSOs();
    }

    void ResetAllSOs()
    {
        foreach (var d in AllData())
            d?.ResetToBase();

        IsAccordSpiritsUnlocked = false;
        IsCoalesceUnlocked = false;
        IsSoulConvergenceUnlocked = false;
        IsEmpowerUnlocked = false;
        IsAccordStateUnlocked = false;
    }

    IEnumerable<AbilityUpgradeData> AllData()
    {
        yield return _stunData;
        yield return _possessData;
        yield return _gateData;
        yield return _healthRegenData;
        yield return _accordSpirits;
        yield return _coalesceData;
        yield return _soulConvData;
        yield return _empowerData;
        yield return _accordData;
    }

    public void RebuildUnlockFlags()
    {
        if (_accordSpirits != null && _accordSpirits.currentNodeIndex > 0 && !IsAccordSpiritsUnlocked)
        { IsAccordSpiritsUnlocked = true; OnAccordSpiritsUnlocked?.Invoke(); }

        if (_coalesceData != null && _coalesceData.currentNodeIndex > 0 && !IsCoalesceUnlocked)
        { IsCoalesceUnlocked = true; OnCoalesceUnlocked?.Invoke(); }

        if (_soulConvData != null && _soulConvData.currentNodeIndex > 0 && !IsSoulConvergenceUnlocked)
        { IsSoulConvergenceUnlocked = true; OnSoulConvergenceUnlocked?.Invoke(); }

        if (_empowerData != null && _empowerData.currentNodeIndex > 0 && !IsEmpowerUnlocked)
        { IsEmpowerUnlocked = true; OnEmpowerUnlocked?.Invoke(); }

        if (_accordData != null && _accordData.currentNodeIndex > 0 && !IsAccordStateUnlocked)
        { IsAccordStateUnlocked = true; OnAccordStateUnlocked?.Invoke(); }

        Debug.Log($"[SkillTreeManager] RebuildUnlockFlags — " +
                  $"AccordSpirits={IsAccordSpiritsUnlocked} Coalesce={IsCoalesceUnlocked} " +
                  $"SoulConv={IsSoulConvergenceUnlocked} Empower={IsEmpowerUnlocked} " +
                  $"Accord={IsAccordStateUnlocked}");
    }

    void RaiseUnlockFlags(AbilityUpgradeData data)
    {
        if (data == _accordSpirits && !IsAccordSpiritsUnlocked)
        { IsAccordSpiritsUnlocked = true; OnAccordSpiritsUnlocked?.Invoke(); }

        if (data == _coalesceData && !IsCoalesceUnlocked)
        { IsCoalesceUnlocked = true; OnCoalesceUnlocked?.Invoke(); }

        if (data == _soulConvData && !IsSoulConvergenceUnlocked)
        { IsSoulConvergenceUnlocked = true; OnSoulConvergenceUnlocked?.Invoke(); }

        if (data == _empowerData && !IsEmpowerUnlocked)
        { IsEmpowerUnlocked = true; OnEmpowerUnlocked?.Invoke(); }

        if (data == _accordData && !IsAccordStateUnlocked)
        { IsAccordStateUnlocked = true; OnAccordStateUnlocked?.Invoke(); }
    }

    // ── Debug ─────────────────────────────────────────────────
    [ContextMenu("DEBUG — Reset All Upgrades")]
    public void Debug_ResetAll()
    {
        ResetAllSOs();
        _points = _startingPoints;
        OnPointsChanged?.Invoke(_points);
        Debug.Log("[SkillTreeManager] All upgrades reset.");
    }

    [ContextMenu("DEBUG — Add 20 Points")]
    void Debug_Add20() => AddPoints(20);

    [ContextMenu("DEBUG — Add 50 Points")]
    void Debug_Add50() => AddPoints(50);

    [ContextMenu("DEBUG — Reset All")]
    void Debug_Reset()
    {
        foreach (var d in AllData()) d?.ResetToBase();

        IsAccordSpiritsUnlocked = IsCoalesceUnlocked =
        IsSoulConvergenceUnlocked = IsEmpowerUnlocked =
        IsAccordStateUnlocked = false;

        _points = _startingPoints;
        OnPointsChanged?.Invoke(_points);
    }
}