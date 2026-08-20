using System;
using UnityEngine;

public class SoulPlayer : Player
{
    public event Action OnSoulDied;

    private Player _caster;
    private Player _target;

    /// <summary>The twin whose player deployed and CONTROLS this soul (couch: soul input follows the caster).
    /// Fixed at setup via SetLinkedPlayers — Lyra's soul → Lyra, Kai's soul → Kai.</summary>
    public Player Caster => _caster;

    public override void OnEffectStarted() { } // soul is immune to time freeze
    public override void OnEffectEnded() { }

    // Named delegate — prevents the lambda unsubscription bug
    private Action _onDeathDelegate;

    public void SetLinkedPlayers(Player caster, Player target)
    {
        _caster = caster;
        _target = target;
    }

    private void Awake()
    {
        _onDeathDelegate = HandleDeath;
    }

    private void OnEnable()
    {
        if (Health != null)
            Health.OnDeath += _onDeathDelegate;
    }

    private void OnDisable()
    {
        if (Health != null)
            Health.OnDeath -= _onDeathDelegate;
    }

    public void ShouldSoulSleep(bool sleep)
    {
        gameObject.SetActive(!sleep);
        attackController?.SetSoulMode(sleep);
        _caster?.Movement.SetSoulMode(!sleep);
        _target?.Movement.SetSoulMode(!sleep);

        // Reset soul HP to full when activating for a new rescue attempt
        if (!sleep)
            Health?.ResetToFull();
    }

    private void HandleDeath()
    {
        OnSoulDied?.Invoke();
        // TeleportAbility will call End() which calls ShouldSoulSleep(true)
        // So we don't SetActive here — let TeleportAbility own the lifecycle
    }
}
