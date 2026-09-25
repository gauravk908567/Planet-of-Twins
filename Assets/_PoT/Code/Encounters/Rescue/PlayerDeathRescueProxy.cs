using System;
using System.Collections;
using UnityEngine;

public class PlayerDeathRescueProxy : MonoBehaviour, IRescueTarget, IDyingPlayer
{
    private float _timeToKill = 8f;
    private float _mashFrequency = 4f;
    private float _mashWindowDuration = 3f;
    private float _mashCooldown = 0.75f;
    private float _partialHealAmount = 20f;

    private float _rescueCooldown;
    private const float RescueCooldownDuration = 2f;

    [Header("Enemy attack slowdown during dying state")]
    [SerializeField] private float attackSlowdownMultiplier = 2f;

    // IRescueTarget — reuses RescueEventController's existing flow
    public event Action<Player> OnPlayerGrabbed;  // fires when dying starts
    public event Action OnPlayerReleased; // fires when saved
    public event Action OnPlayerKilled;   // fires when TTK expires

    private Enemy _killingEnemy;
    private Action _onKillerDiedDelegate;
    public bool CanGrabbedPlayerStruggle => false;
    public float StrugglePauseDuration => 0f; // dying player cannot struggle

    public Transform GrabbedPlayerTransform => transform;
    public Player GrabbedPlayer => _player;

    public float MashFrequency => _mashFrequency;
    public float MashWindowDuration => _mashWindowDuration;
    public float MashCooldown => _mashCooldown;
    public float PartialHealAmount => _partialHealAmount;
    public float NormalisedTTK => _ttkRemaining / _timeToKill;

    // IDyingPlayer
    public float AttackSlowdownMultiplier => attackSlowdownMultiplier;

    private Player _player;
    private Coroutine _ttkCoroutine;
    private bool _isActive;
    public bool IsActive => _isActive;
    private bool _ttkPaused;
    private float _ttkRemaining;

    private void Awake()
    {
        _player = GetComponent<Player>();
    }

    private void Start()
    {

    }

    private void Update()
    {
        if (_rescueCooldown > 0f)
            _rescueCooldown -= Time.deltaTime;
    }

    // ── Called when this player's HP hits zero ─────────────────
    public void Activate(Enemy killingEnemy)
    {
        Debug.Log($"[DeathProxy] Activate called on {gameObject.name}, " +
                  $"_isActive={_isActive}, " +
                  $"killedBy={killingEnemy?.gameObject.name ?? "NULL"}, " +
                  $"HP={_player?.Health?.DisplayHealth}");
        if (_isActive) return;
        if (_rescueCooldown > 0f) return; // FIX: prevent immediate re-activation
        _isActive = true;

        // Track who killed us — soul killing them stops TTK immediately
        _killingEnemy = killingEnemy;
        if (_killingEnemy != null)
        {
            _onKillerDiedDelegate = HandleKillerDied;
            _killingEnemy.Health.OnDeath += _onKillerDiedDelegate;
        }

        var data = killingEnemy?.Data;
        _timeToKill = data?.timeToKill ?? 8f;
        _mashFrequency = data?.mashFrequency ?? 4f;
        _mashWindowDuration = data?.mashWindowDuration ?? 3f;
        _mashCooldown = data?.mashCooldown ?? 0.75f;
        _partialHealAmount = data?.partialHealAmount ?? 20f;
        _ttkRemaining = _timeToKill;

        _player.Movement.SetFrozen(true);
        ApplySlowdownToTargetingEnemies();
        // FIX: clear target on all enemies that were targeting this player.
        // Without this, ranged enemies keep chasing and shooting at a dying
        // player regardless of TTK state. Melee enemies are already effectively
        // stopped by the attack slowdown, but ranged enemies continue firing
        // from distance. Clearing their target sends them to IdleState.
        ClearTargetOnEnemies();
        _ttkCoroutine = StartCoroutine(TTKCountdown());
        OnPlayerGrabbed?.Invoke(_player);
    }

    private void HandleKillerDied()
    {
        if (!_isActive) return; // already resolved

        Debug.Log($"[DeathProxy] Killing enemy died — free rescue for {gameObject.name}");
        UnsubscribeFromKiller();
        ReleasePlayer(_partialHealAmount); // heal and free
    }

    private void UnsubscribeFromKiller()
    {
        if (_killingEnemy != null && _onKillerDiedDelegate != null)
        {
            _killingEnemy.Health.OnDeath -= _onKillerDiedDelegate;
            _killingEnemy = null;
            _onKillerDiedDelegate = null;
        }
    }
    // ── IRescueTarget TTK control (called by RescueEventController) ──
    public void PauseTTK()
    {
        _ttkPaused = true;
        Debug.Log($"[DeathProxy] '{_player?.name}' TTK PAUSED at {_ttkRemaining:F2}s", this);
    }

    public void ResumeTTK()
    {
        _ttkPaused = false;
        Debug.Log($"[DeathProxy] '{_player?.name}' TTK RESUMED at {_ttkRemaining:F2}s", this);
    }

    // ── Called on successful rescue ────────────────────────────
    public void ReleasePlayer(float healAmount)
    {
        // Regen investigation (2026-08-10): log HP BEFORE the heal + which twin, so we can
        // distinguish "the heal never applied" from "the heal applied but the bar masked it".
        // If this line is the LAST one before a freeze, the crash is in the release/cue cascade.
        float hpBefore = _player != null ? _player.Health.CombatHealth : -1f;
        Debug.Log($"[DeathProxy] ReleasePlayer '{_player?.name}' — _isActive={_isActive}, " +
                  $"healAmount={healAmount}, HP(before)={hpBefore:F1}", this);
        if (!_isActive) return;
        _isActive = false;
        _rescueCooldown = RescueCooldownDuration;

        UnsubscribeFromKiller(); // ADD
        StopTTK();
        _player.Movement.SetFrozen(false);
        _player.Health.Heal(healAmount);
        _player.Health.ResetToAlive();
        // Regen investigation: HP AFTER heal + isDead cleared. Real regen should now resume after
        // the post-hit delay; watch for the [HealthRegen] lines on this same twin next.
        Debug.Log($"[DeathProxy] ReleasePlayer '{_player?.name}' DONE — HP(after)={_player.Health.CombatHealth:F1}, " +
                  $"isDead={_player.Health.IsDead}", this);
        ClearSlowdownFromTargetingEnemies();
        StartCoroutine(InvincibilityFrames(2f));
        OnPlayerReleased?.Invoke();
    }

    private IEnumerator InvincibilityFrames(float duration)
    {
        // Real-time grace: a freshly-rescued player gets `duration` of WALL-CLOCK invincibility.
        // Must be unscaled — if started while timeScale is low (Setsuna 0.15 / a transition at 0)
        // a scaled WaitForSeconds stretches the 2s grace into many real seconds, leaving the player
        // un-damageable far longer than intended (the "enemies do no damage" bug). The finally clears
        // _invincible even if this coroutine is stopped (StopCoroutine / external reset). R10. BUG-044.
        _player.Health.SetInvincible(true);
        try { yield return new WaitForSecondsRealtime(duration); }
        finally { _player.Health.SetInvincible(false); }
    }

    private IEnumerator TTKCountdown()
    {
        while (_ttkRemaining > 0f)
        {
            if (!_ttkPaused)
                _ttkRemaining -= Time.deltaTime;
            yield return null;
        }

        // Rescue may have completed while countdown was running
        if (!_isActive) yield break;

        _isActive = false;
        Debug.Log($"[DeathProxy] '{_player?.name}' TTK EXPIRED → rescue FAILED (player killed)", this);
        // FIX: unfreeze movement — was only unfrozen in ReleasePlayer (success path).
        // Without this, player stays permanently frozen after TTK expires.
        _player.Movement.SetFrozen(false);
        ClearSlowdownFromTargetingEnemies();
        UnsubscribeFromKiller();
        OnPlayerKilled?.Invoke();
    }

    private void StopTTK()
    {
        if (_ttkCoroutine != null)
        {
            StopCoroutine(_ttkCoroutine);
            _ttkCoroutine = null;
        }
    }

    public void KillPlayer()
    {
        if (!_isActive) return;
        _isActive = false;

        UnsubscribeFromKiller(); // ADD
        StopTTK();
        ClearSlowdownFromTargetingEnemies();
        OnPlayerKilled?.Invoke();
    }

    // ── Enemy slowdown ─────────────────────────────────────────
    private void ApplySlowdownToTargetingEnemies()
    {
        var allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var enemy in allEnemies)
        {
            if (enemy.Target == transform)
                enemy.AttackController.SetAttackSlowdown(attackSlowdownMultiplier);
        }
    }

    /// <summary>
    /// Clears target on enemies that were chasing/attacking this dying player.
    /// Sends them back to IdleState so ranged enemies stop shooting during TTK.
    /// Called on TTK start. Enemies will re-detect naturally when player is rescued.
    /// </summary>
    private void ClearTargetOnEnemies()
    {
        var allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var enemy in allEnemies)
        {
            if (enemy.Target == transform)
                enemy.ClearTarget();
        }
    }

    private void ClearSlowdownFromTargetingEnemies()
    {
        var allEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (var enemy in allEnemies)
            enemy.AttackController.ClearAttackSlowdown();
    }

    public void OnStruggle() { }
    private void OnDestroy()
    {

    }
}