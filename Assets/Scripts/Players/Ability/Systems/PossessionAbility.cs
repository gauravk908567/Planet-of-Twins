using UnityEngine;
using System.Collections.Generic;
using System;

public class PossessAbility : AbilityBase, IPossessEvents
{
    private readonly LayerMask _targetLayer;
    private readonly AbilityController _owner;
    private readonly AbilityUpgradeData _upgradeData;

    private readonly Collider[] _hitBuffer = new Collider[20];
    private readonly HashSet<IPossessable> _possessedThisWindow = new HashSet<IPossessable>();

    private int _targetsHit;
    private float _currentDuration;
    private float _currentCooldown;   // cached at Activate() — used by EffectiveCooldown
    private int _currentMaxTargets;
    private float _currentRange;

    public event Action OnPossessCast;
    public event Action<GameObject> OnPossessApplied;
    public event Action<GameObject> OnPossessEnded;

    private const float DamageMultiplier = 1.2f;

    public PossessAbility(
        AbilityData data,
        LayerMask targetLayer,
        AbilityController owner,
        AbilityUpgradeData upgradeData)
        : base(data)
    {
        _targetLayer = targetLayer;
        _owner = owner;
        _upgradeData = upgradeData;
    }

    // Returns the upgraded cooldown — AbilityBase.TryActivate and
    // ReduceCooldownByFraction both read this instead of data.cooldown.
    protected override float EffectiveCooldown
        => _currentCooldown > 0f ? _currentCooldown : data.cooldown;

    protected override bool Activate()
    {
        // Cache ALL upgrade values at the moment the window opens.
        _currentDuration = _upgradeData != null ? _upgradeData.CurrentDuration : data.duration;
        _currentCooldown = _upgradeData != null ? _upgradeData.CurrentCooldown : data.cooldown;
        _currentMaxTargets = _upgradeData != null ? _upgradeData.CurrentMaxTargets : 1;
        _currentRange = _upgradeData != null ? _upgradeData.CurrentRange : data.range;

        _targetsHit = 0;
        _possessedThisWindow.Clear();

        _owner.ShowPrimaryPreview(_currentRange);

        // Notify DualCastSystem that a possess window opened
        OnPossessCast?.Invoke();

        Debug.Log($"[PossessAbility] Window opened — duration={_currentDuration}s " +
                  $"cooldown={_currentCooldown}s maxTargets={_currentMaxTargets} range={_currentRange}");

        return true;
    }

    public override void Tick()
    {
        if (!isActive) return;

        // Prune enemies destroyed while possessed
        _possessedThisWindow.RemoveWhere(
            p => (p as Component) == null || (p as Component).gameObject == null);

        if (_targetsHit < _currentMaxTargets)
            ScanForTargets();

        activeTimer += Time.deltaTime;
        if (activeTimer >= _currentDuration)
            End();
    }

    private void ScanForTargets()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            _owner.transform.position, _currentRange, _hitBuffer, _targetLayer);

        if (hitCount == 0) return;

        var sorted = SortByDistance(_owner.transform.position, hitCount);

        foreach (var col in sorted)
        {
            if (_targetsHit >= _currentMaxTargets) break;
            if (col == null || col.gameObject == null) continue;

            var possessable = col.GetComponent<IPossessable>();
            if (possessable == null) continue;
            if (possessable.IsPossessed) continue;
            if (_possessedThisWindow.Contains(possessable)) continue;

            possessable.ApplyPossession(_currentDuration, DamageMultiplier);
            _possessedThisWindow.Add(possessable);
            _targetsHit++;

            int listenerCount = OnPossessApplied == null ? 0 : OnPossessApplied.GetInvocationList().Length;
            Debug.Log($"[PossessAbility] Firing OnPossessApplied — listeners={listenerCount} enemy={col.gameObject.name}");
            OnPossessApplied?.Invoke(col.gameObject);
            Debug.Log($"[PossessAbility] Possessed {col.gameObject.name} ({_targetsHit}/{_currentMaxTargets})");
        }
    }

    protected override void End()
    {
        foreach (var possessable in _possessedThisWindow)
        {
            var comp = possessable as Component;
            if (comp == null || comp.gameObject == null) continue;
            OnPossessEnded?.Invoke(comp.gameObject);
        }

        _targetsHit = 0;
        _possessedThisWindow.Clear();
        _owner.HidePrimaryPreview();
        base.End();

        Debug.Log($"[PossessAbility] Window closed on {_owner.gameObject.name}");
    }

    // Called by DualCastSystem — delegates to AbilityBase which uses EffectiveCooldown
    public void ReduceCurrentCooldown(float fraction)
        => ReduceCooldownByFraction(fraction);

    private List<Collider> SortByDistance(Vector3 origin, int count)
    {
        var list = new List<Collider>(count);
        for (int i = 0; i < count; i++) list.Add(_hitBuffer[i]);
        list.Sort((a, b) =>
            Vector3.Distance(origin, a.transform.position)
                .CompareTo(Vector3.Distance(origin, b.transform.position)));
        return list;
    }
}