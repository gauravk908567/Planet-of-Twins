using UnityEngine;
using System.Collections.Generic;
using System;

public class StunAbility : AbilityBase, IStunEvents
{
    private readonly LayerMask _targetLayer;
    private readonly AbilityController _owner;
    private readonly AbilityUpgradeData _upgradeData;

    private readonly Collider[] _hitBuffer = new Collider[20];

    private int _targetsHit;
    private float _currentDuration;
    private float _currentCooldown;   // cached at Activate() — used by EffectiveCooldown
    private int _currentMaxTargets;
    private float _currentRange;

    private readonly CueBookData _cueBook;   // "OnStun_Active" = held VFX on the caster; "OnStun_Hit" = held VFX per stunned enemy
    private CueHandle _activeHandle;                                          // the caster's OnStun_Active (stopped in End)
    private readonly Dictionary<GameObject, CueHandle> _hitHandles = new Dictionary<GameObject, CueHandle>();  // per-enemy OnStun_Hit

    public event Action OnStunCast;
    public event Action<GameObject> OnStunApplied;
    public event Action<GameObject> OnStunEnded;

    // HashSet prevents re-stunning the same target within one window
    private readonly HashSet<IStunnable> _stunnedThisWindow = new HashSet<IStunnable>();

    public StunAbility(
        AbilityData data,
        LayerMask targetLayer,
        AbilityController owner,
        AbilityUpgradeData upgradeData,
        CueBookData cueBook = null)
        : base(data)
    {
        _targetLayer = targetLayer;
        _owner = owner;
        _upgradeData = upgradeData;
        _cueBook = cueBook;
    }

    // Returns the upgraded cooldown — AbilityBase.TryActivate and
    // ReduceCooldownByFraction both read this instead of data.cooldown.
    protected override float EffectiveCooldown
        => _currentCooldown > 0f ? _currentCooldown : data.cooldown;

    protected override bool Activate()
    {
        // Cache ALL upgrade values at the moment the window opens.
        // This snapshot is used for the entire active duration.
        _currentDuration = _upgradeData != null ? _upgradeData.CurrentDuration : data.duration;
        _currentCooldown = _upgradeData != null ? _upgradeData.CurrentCooldown : data.cooldown;
        _currentMaxTargets = _upgradeData != null ? _upgradeData.CurrentMaxTargets : 1;
        _currentRange = _upgradeData != null ? _upgradeData.CurrentRange : data.range;

        _targetsHit = 0;
        _stunnedThisWindow.Clear();

        _owner.ShowPrimaryPreview(_currentRange);

        // Notify DualCastSystem that a stun window opened
        OnStunCast?.Invoke();

        // Active cast VFX — held OnStun_Active on the caster for the whole window (stopped in End()).
        // Scale the VFX radius to the UPGRADED range: scale = currentRange / base range, so the cue (authored
        // to look right at base range) grows proportionally with range upgrades (CueContext.scale).
        if (_cueBook != null)
        {
            float baseRange = data.range > 0f ? data.range : _currentRange;
            float rangeScale = baseRange > 0f ? _currentRange / baseRange : 1f;
 // Tier-resolved: plays OnStun_Active_t[n] when authored in the book, else the base id.
            _activeHandle = FxManager.Instance?.PlayBook(_cueBook,
                UpgradeCueResolver.Resolve(_cueBook, _upgradeData, FxIds.Player.Stun.OnStun_Active),
                CueContext.Follow(_owner.transform, scale: rangeScale)) ?? CueHandle.None;
        }

        Debug.Log($"[StunAbility] Window opened — duration={_currentDuration}s " +
                  $"cooldown={_currentCooldown}s maxTargets={_currentMaxTargets} range={_currentRange}");

        return true;
    }

    public override void Tick()
    {
        if (!isActive) return;

        // Prune enemies destroyed while stunned
        _stunnedThisWindow.RemoveWhere(
            s => (s as Component) == null || (s as Component).gameObject == null);

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

            var stunnable = col.GetComponent<IStunnable>();
            if (stunnable == null) continue;
            if (stunnable.IsStunned) continue;
            if (_stunnedThisWindow.Contains(stunnable)) continue;

            stunnable.ApplyStun(_currentDuration);
            _stunnedThisWindow.Add(stunnable);
            _targetsHit++;

            int listenerCount = OnStunApplied == null ? 0 : OnStunApplied.GetInvocationList().Length;
            Debug.Log($"[StunAbility] Firing OnStunApplied — listeners={listenerCount} enemy={col.gameObject.name}");
            OnStunApplied?.Invoke(col.gameObject);

            // Per-enemy held OnStun_Hit VFX (the "I'm stunned" stars — 2 looping particles WithPrevious) on the
            // enemy; runs while it's immobilised, stopped in End(). The Manpu glyph reacts via OnStunApplied.
            if (_cueBook != null)
            {
                var hitHandle = FxManager.Instance?.PlayBook(_cueBook,
                    UpgradeCueResolver.Resolve(_cueBook, _upgradeData, FxIds.Player.Stun.OnStun_Hit),
                    CueContext.Follow(col.transform)) ?? CueHandle.None;
                if (!hitHandle.IsNone) _hitHandles[col.gameObject] = hitHandle;
            }

            Debug.Log($"[StunAbility] Stunned {col.gameObject.name} ({_targetsHit}/{_currentMaxTargets})");
        }
    }

    protected override void End()
    {
        FxManager.Instance?.Stop(_activeHandle);   // stop the held caster OnStun_Active VFX
        _activeHandle = CueHandle.None;

        foreach (var kv in _hitHandles) FxManager.Instance?.Stop(kv.Value);   // stop every per-enemy OnStun_Hit
        _hitHandles.Clear();

        foreach (var stunnable in _stunnedThisWindow)
        {
            var comp = stunnable as Component;
            if (comp == null || comp.gameObject == null) continue;
            OnStunEnded?.Invoke(comp.gameObject);
        }

        _targetsHit = 0;
        _stunnedThisWindow.Clear();
        _owner.HidePrimaryPreview();
        base.End();

        Debug.Log($"[StunAbility] Window closed on {_owner.gameObject.name}");
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