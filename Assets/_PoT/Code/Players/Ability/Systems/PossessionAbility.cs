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

    // "Possess_Active" = held VFX on the caster for the window; "Possess_Hit" = held VFX per possessed enemy.
    // Book resolved lazily from PlayerVfxLibrary (R4) — mirrors the Stun ability's Active/Hit handle pattern.
    private CueBookData _cueBook;
    private CueHandle _activeHandle;
    private readonly Dictionary<GameObject, CueHandle> _hitHandles = new Dictionary<GameObject, CueHandle>();

    private int _targetsHit;
    private float _currentDuration;
    private float _currentCooldown;   // cached at Activate() � used by EffectiveCooldown
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

    // Returns the upgraded cooldown � AbilityBase.TryActivate and
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

        // Active cast VFX — held Possess_Active on the caster for the whole window (stopped in End()), scaled to range.
        _cueBook ??= VfxLibraryProvider.Instance?.Player?.Possess;   // R4
        if (_cueBook != null)
        {
            float baseRange = data.range > 0f ? data.range : _currentRange;
            float rangeScale = baseRange > 0f ? _currentRange / baseRange : 1f;
 // Tier-resolved: plays Possess_Active_t[n] when authored in the book, else the base id.
            _activeHandle = FxManager.Instance?.PlayBook(_cueBook,
                UpgradeCueResolver.Resolve(_cueBook, _upgradeData, FxIds.Player.Possess.Possess_Active),
                CueContext.Follow(_owner.transform, scale: rangeScale)) ?? CueHandle.None;
        }

        Debug.Log($"[PossessAbility] Window opened � duration={_currentDuration}s " +
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
            Debug.Log($"[PossessAbility] Firing OnPossessApplied � listeners={listenerCount} enemy={col.gameObject.name}");
            OnPossessApplied?.Invoke(col.gameObject);

            // Per-enemy held Possess_Hit VFX on the possessed enemy; stopped in End().
            if (_cueBook != null)
            {
                var hitHandle = FxManager.Instance?.PlayBook(_cueBook,
                    UpgradeCueResolver.Resolve(_cueBook, _upgradeData, FxIds.Player.Possess.Possess_Hit),
                    CueContext.Follow(col.transform)) ?? CueHandle.None;
                if (!hitHandle.IsNone) _hitHandles[col.gameObject] = hitHandle;
            }

            Debug.Log($"[PossessAbility] Possessed {col.gameObject.name} ({_targetsHit}/{_currentMaxTargets})");
        }
    }

    protected override void End()
    {
        FxManager.Instance?.Stop(_activeHandle);   // stop the held caster Possess_Active VFX
        _activeHandle = CueHandle.None;
        foreach (var kv in _hitHandles) FxManager.Instance?.Stop(kv.Value);   // stop every per-enemy Possess_Hit
        _hitHandles.Clear();

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

    // Called by DualCastSystem � delegates to AbilityBase which uses EffectiveCooldown
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