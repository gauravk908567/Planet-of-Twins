using UnityEngine;

/// <summary>
/// Drives a looping particle system on the player while health regen is active.
/// Event-driven — no polling, no flickering.
///
/// SETUP:
///   - Add this component to the same GameObject as PlayerHealthComponent.
///   - Create a child GameObject, add your healing particle system to it.
///   - Particle System settings: Play On Awake = OFF, Loop = ON.
///   - Drag that particle system into the regenParticles slot.
/// </summary>
public class HealthRegenVFX : MonoBehaviour
{
    [SerializeField] private ParticleSystem regenParticles;

    private PlayerHealthComponent _health;

    private void Awake()
    {
        _health = GetComponent<PlayerHealthComponent>();
        if (_health == null)
            _health = GetComponentInParent<PlayerHealthComponent>();

        if (_health == null)
            Debug.LogWarning("[HealthRegenVFX] No PlayerHealthComponent found.", this);

        if (regenParticles == null)
            Debug.LogWarning("[HealthRegenVFX] regenParticles not assigned.", this);

        regenParticles?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnEnable()
    {
        if (_health == null) return;
        _health.OnRegenStarted += StartVFX;
        _health.OnRegenStopped += StopVFX;
    }

    private void OnDisable()
    {
        if (_health == null) return;
        _health.OnRegenStarted -= StartVFX;
        _health.OnRegenStopped -= StopVFX;
    }

    private void StartVFX()
    {
        if (regenParticles == null) return;
        regenParticles.Play(true);
    }

    private void StopVFX()
    {
        if (regenParticles == null) return;
        // StopEmitting lets existing particles finish their natural fade
        regenParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}