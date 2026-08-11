using System.Collections;
using UnityEngine;

/// <summary>
/// Central manpu ability driver. Subscribes to the twins' stun/possess ability
/// events from <c>TwinAbilitySetup.StunEvents/PossessEvents</c> (the same source the abilities' own VFX
/// fire from) — so the glyph can't desync from the effect. On apply it claims the affected enemy's
/// <c>ManpuSlot</c> for a held→closing arc (R1); on end it releases it. Place on an always-active GO
/// (e.g. the GameSystem beside <c>ImmobiliseAuraVFX</c>).
/// </summary>
public class ManpuAbilityListener : MonoBehaviour
{
    [SerializeField] private TwinAbilitySetup _abilitySetup;

    private IStunEvents _stunEvents;
    private IPossessEvents _possessEvents;
    private bool _subscribed;

    private void Start() => StartCoroutine(LateStart());

    private IEnumerator LateStart()
    {
        yield return null; // wait for TwinAbilitySetup.Start()
        _stunEvents = _abilitySetup?.StunEvents;
        _possessEvents = _abilitySetup?.PossessEvents;

        if (_stunEvents == null) Debug.LogError("[ManpuAbilityListener] StunEvents null — check TwinAbilitySetup.", this);
        if (_possessEvents == null) Debug.LogError("[ManpuAbilityListener] PossessEvents null — check TwinAbilitySetup.", this);
        Subscribe();
    }

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed) return;
        if (_stunEvents != null) { _stunEvents.OnStunApplied += HandleStunApplied; _stunEvents.OnStunEnded += HandleStunEnded; }
        if (_possessEvents != null) { _possessEvents.OnPossessApplied += HandlePossessApplied; _possessEvents.OnPossessEnded += HandlePossessEnded; }
        _subscribed = _stunEvents != null || _possessEvents != null;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (_stunEvents != null) { _stunEvents.OnStunApplied -= HandleStunApplied; _stunEvents.OnStunEnded -= HandleStunEnded; }
        if (_possessEvents != null) { _possessEvents.OnPossessApplied -= HandlePossessApplied; _possessEvents.OnPossessEnded -= HandlePossessEnded; }
        _subscribed = false;
    }

    private void HandleStunApplied(GameObject enemy) => Slot(enemy)?.ClaimAbility(ManpuAbility.Stun);
    private void HandleStunEnded(GameObject enemy) => Slot(enemy)?.ReleaseAbility(ManpuAbility.Stun);
    private void HandlePossessApplied(GameObject enemy) => Slot(enemy)?.ClaimAbility(ManpuAbility.Possess);
    private void HandlePossessEnded(GameObject enemy) => Slot(enemy)?.ReleaseAbility(ManpuAbility.Possess);

    private static ManpuSlot Slot(GameObject enemy)
        => enemy == null ? null : enemy.GetComponentInChildren<ManpuSlot>(true);
}
