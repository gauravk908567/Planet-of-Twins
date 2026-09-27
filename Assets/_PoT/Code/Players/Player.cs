using System;
using UnityEngine;
public class Player : MonoBehaviour, ITimeAffected
{
    private void Awake()
    {
        // Auto-find health if not wired in Inspector — prevents silent null
        // on HealthTracker which causes IndividualHealthPresenter to never subscribe
        if (health == null)
            health = GetComponent<PlayerHealthComponent>();

        if (health == null)
            Debug.LogError($"[Player] {gameObject.name}: PlayerHealthComponent not found. " +
                "Wire it in Inspector or ensure it is on the same GameObject.", this);
    }
    [SerializeField] private PlayerMovementController movement;
    [SerializeField] private PlayerHealthComponent health;          // concrete ref for Unity wiring
    [SerializeField] protected PlayerAttackController attackController; // private — not protected
    [SerializeField] private TimeFactorRegistrar timeFactorRegistrar; // injected, not found

    private bool _isGrabbed;
    public bool IsGrabbed => _isGrabbed;
    public void SetGrabbed(bool grabbed) => _isGrabbed = grabbed;

    // What holds this twin that it mashes free of with its melee button (BUG-144), or null. The holder sets it on
    // catch and clears it on every release path; TwinAttackDispatcher reads it.
    private IStruggleHold _struggleHold;
    public IStruggleHold StruggleHold
    {
        get
        {
            // A holder destroyed without releasing (scene unload) must never leave the twin unable to melee.
            if (_struggleHold is UnityEngine.Object holder && holder == null) _struggleHold = null;
            return _struggleHold;
        }
    }
    public void SetStruggleHold(IStruggleHold hold) => _struggleHold = hold;
    /// <summary>Clears the hold only if <paramref name="hold"/> is still the one holding (a newer holder stays).</summary>
    public void ClearStruggleHold(IStruggleHold hold)
    {
        if (_struggleHold == hold) _struggleHold = null;
    }
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