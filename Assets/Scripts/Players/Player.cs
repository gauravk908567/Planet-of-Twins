using System;
using UnityEngine;
public class Player : MonoBehaviour, ITimeAffected
{
    [SerializeField] private PlayerMovementController movement;
    [SerializeField] private PlayerHealthComponent health;          // concrete ref for Unity wiring
    [SerializeField] protected PlayerAttackController attackController; // private — not protected
    [SerializeField] private TimeFactorRegistrar timeFactorRegistrar; // injected, not found

    private bool _isGrabbed;
    public bool IsGrabbed => _isGrabbed;
    public void SetGrabbed(bool grabbed) => _isGrabbed = grabbed;
    public PlayerMovementController Movement => movement;

    // Expose abstraction so consumers (TwinManager, UI) don't need concrete type
    public IHealthTracker HealthTracker => health;

    // Keep concrete accessor ONLY for systems that genuinely need to
    // call mutation methods (TwinBondManager, UpgradeManager).
    // All read-only consumers MUST use HealthTracker.
    public PlayerHealthComponent Health => health;

    // ── ITimeAffected ──────────────────────────────────────────
    public virtual void OnEffectStarted()
    {
        movement?.SetSoulMode(true);
        attackController?.SetSoulMode(true);
    }

    public virtual void OnEffectEnded()
    {
        movement?.SetSoulMode(false);
        attackController?.SetSoulMode(false);
    }
}

// ── TimeFactorRegistrar ────────────────────────────────────────────
// FILE: Player/TimeFactorRegistrar.cs
//
// SRP: Owns only the "register this ITimeAffected with the registry" task.
// DIP FIX: Gets ITimeFactorRegistry via GetComponent on the same GameObject
//          (GameSystem object) — no FindAnyObjectByType, no concrete type.
//          Drag the GameSystem object into the [SerializeField] slot.
public class TimeFactorRegistrar : MonoBehaviour
{
    [SerializeField] private MonoBehaviour timeFactorRegistryObject;
    private ITimeFactorRegistry _registry;

    // The ITimeAffected that this registrar manages — assigned by Player or SoulPlayer
    [SerializeField] private MonoBehaviour timeAffectedObject;
    private ITimeAffected _timeAffected;

    private void Awake()
    {
        _registry = timeFactorRegistryObject as ITimeFactorRegistry;
        _timeAffected = timeAffectedObject as ITimeAffected;

        if (_registry == null)
            Debug.LogError($"[TimeFactorRegistrar] {timeFactorRegistryObject?.name} does not implement ITimeFactorRegistry.", this);
        if (_timeAffected == null)
            Debug.LogError($"[TimeFactorRegistrar] {timeAffectedObject?.name} does not implement ITimeAffected.", this);
    }

    private void OnEnable() => _registry?.Register(_timeAffected);
    private void OnDisable() => _registry?.Unregister(_timeAffected);
}

