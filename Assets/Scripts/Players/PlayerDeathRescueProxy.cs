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
    }

    public void ResumeTTK()
    {
        _ttkPaused = false;
    }

    // ── Called on successful rescue ────────────────────────────
    public void ReleasePlayer(float healAmount)
    {
        Debug.Log($"[DeathProxy] ReleasePlayer — _isActive={_isActive}");
        if (!_isActive) return;
        _isActive = false;
        _rescueCooldown = RescueCooldownDuration;

        UnsubscribeFromKiller(); // ADD
        StopTTK();
        _player.Movement.SetFrozen(false);
        _player.Health.Heal(healAmount);
        _player.Health.ResetToAlive();
        ClearSlowdownFromTargetingEnemies();
        StartCoroutine(InvincibilityFrames(2f));
        OnPlayerReleased?.Invoke();
    }

    private IEnumerator InvincibilityFrames(float duration)
    {
        _player.Health.SetInvincible(true);
        yield return new WaitForSeconds(duration);
        _player.Health.SetInvincible(false);
    }

    private IEnumerator TTKCountdown()
    {
        while (_ttkRemaining > 0f)
        {
            if (!_ttkPaused)
                _ttkRemaining -= Time.deltaTime;
            yield return null;
        }

        // ADD THIS LINE — rescue may have completed while countdown was running
        if (!_isActive) yield break;

        _isActive = false;
        ClearSlowdownFromTargetingEnemies();
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