using System;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// SkillTreeManager
//
// Implements IPointBank, ISkillUnlockState, IAbilityDataStore, ISkillTreePurchaser.
// This is a coordinator — it connects the economy to the data to the unlock flags.
// Nothing calls SkillTreeManager.Instance. Everything receives the interface it
// needs via [SerializeField] in the Inspector.
//
// UNITY SETUP:
//   1. Create empty GameObject → "SkillTreeManager"
//   2. Attach this script
//   3. Drag the 7 AbilityUpgradeData SOs into the Inspector slots
//   4. On each system (DualCastSystem, CoalesceSystem, etc.), drag THIS
//      GameObject into its interface slot — Unity will resolve the interface
//      automatically because the component implements it.
//
// DEPENDENCY MAP — each system only gets the slice it needs:
//   SkillPointSource    → IPointBank
//   SkillTreeUI         → IAbilityDataStore + ISkillTreePurchaser + IPointBank
//   DualCastSystem      → ISkillUnlockState
//   CoalesceSystem      → ISkillUnlockState + IAbilityDataStore
//   SoulConvergenceSystem → ISkillUnlockState + IAbilityDataStore + IPointBank
// ─────────────────────────────────────────────────────────────────────────────
public class SkillTreeManager : MonoBehaviour,
    IPointBank,
    ISkillUnlockState,
    IAbilityDataStore,
    ISkillTreePurchaser
{
    // ── Inspector slots ───────────────────────────────────────────────────────
    [Header("Kai")]
    [SerializeField] private AbilityUpgradeData _stunData;

    [Header("Lyra")]
    [SerializeField] private AbilityUpgradeData _possessData;

    [Header("Shared")]
    [SerializeField] private AbilityUpgradeData _gateData;
    [SerializeField] private AbilityUpgradeData _healthRegenData;

    [Header("Special Systems")]
    [SerializeField] private AbilityUpgradeData _dualCastData;
    [SerializeField] private AbilityUpgradeData _coalesceData;
    [SerializeField] private AbilityUpgradeData _soulConvData;

    [Header("Economy")]
    [SerializeField] private int _startingPoints = 0;

    // ── IPointBank ────────────────────────────────────────────────────────────
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

    // ── ISkillUnlockState ─────────────────────────────────────────────────────
    public bool IsDualCastUnlocked { get; private set; }
    public bool IsCoalesceUnlocked { get; private set; }
    public bool IsSoulConvergenceUnlocked { get; private set; }

    public event Action OnDualCastUnlocked;
    public event Action OnCoalesceUnlocked;
    public event Action OnSoulConvergenceUnlocked;

    // ── IAbilityDataStore ─────────────────────────────────────────────────────
    public AbilityUpgradeData StunData => _stunData;
    public AbilityUpgradeData PossessData => _possessData;
    public AbilityUpgradeData GateData => _gateData;
    public AbilityUpgradeData HealthRegenData => _healthRegenData;
    public AbilityUpgradeData DualCastData => _dualCastData;
    public AbilityUpgradeData CoalesceData => _coalesceData;
    public AbilityUpgradeData SoulConvData => _soulConvData;

    // ── ISkillTreePurchaser ───────────────────────────────────────────────────
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

    // ── Internal ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _points = _startingPoints;
        ResetAllSOs();  // SOs persist in editor between play sessions — always reset on start
    }

    void ResetAllSOs()
    {
        foreach (var d in AllData())
            d?.ResetToBase();

        IsDualCastUnlocked = false;
        IsCoalesceUnlocked = false;
        IsSoulConvergenceUnlocked = false;
    }

    IEnumerable<AbilityUpgradeData> AllData()
    {
        yield return _stunData;
        yield return _possessData;
        yield return _gateData;
        yield return _healthRegenData;
        yield return _dualCastData;
        yield return _coalesceData;
        yield return _soulConvData;
    }

    void RaiseUnlockFlags(AbilityUpgradeData data)
    {
        if (data == _dualCastData && !IsDualCastUnlocked)
        { IsDualCastUnlocked = true; OnDualCastUnlocked?.Invoke(); }

        if (data == _coalesceData && !IsCoalesceUnlocked)
        { IsCoalesceUnlocked = true; OnCoalesceUnlocked?.Invoke(); }

        if (data == _soulConvData && !IsSoulConvergenceUnlocked)
        { IsSoulConvergenceUnlocked = true; OnSoulConvergenceUnlocked?.Invoke(); }
    }

    // ── Debug helpers
    [ContextMenu("DEBUG — Reset All Upgrades")]
    public void Debug_ResetAll()
    {
        ResetAllSOs();
        _points = _startingPoints;
        Debug.Log("[SkillTreeManager] All upgrades reset, points reset to " + _startingPoints);
    }

    // ── ─────────────────────────────────────────────────────────
    [ContextMenu("DEBUG — Add 20 Points")]
    void Debug_Add20() => AddPoints(20);

    [ContextMenu("DEBUG — Add 50 Points")]
    void Debug_Add50() => AddPoints(50);

    [ContextMenu("DEBUG — Reset All")]
    void Debug_Reset()
    {
        foreach (var d in new[]{ _stunData, _possessData, _gateData, _healthRegenData,
                                  _dualCastData, _coalesceData, _soulConvData })
            d?.ResetToBase();

        IsDualCastUnlocked = IsCoalesceUnlocked = IsSoulConvergenceUnlocked = false;
        _points = _startingPoints;
        OnPointsChanged?.Invoke(_points);
    }
}