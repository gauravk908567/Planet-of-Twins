using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Penitent Commander — Vethara
/// Commands: 1 Witness + 1 Penitent + 2 GroupGrab
///
/// Signature — Dark Shield (STUB):
///   When any governed soldier takes 25%+ max HP in one hit,
///   all governed soldiers become invulnerable for 2.5s. 15s cooldown.
///
/// Wiring: GOAPBrainPenitentCommander calls WireSoldierDamage()
///         for each soldier so NotifyDamageTaken() fires correctly.
///
/// Brain: GOAPBrainPenitentCommander
/// </summary>
public class PenitentCommander : Enemy, ICommander
{
    [Header("Dark Shield")]
    [SerializeField] private float _damageThreshold = 0.25f;
    [SerializeField] private float _shieldDuration = 2.5f;
    [SerializeField] private float _shieldCooldown = 15f;

    [Header("Commander")]
    [SerializeField] private float _commandRadius = 15f;
    [SerializeField] private float _deathRageDuration = 4.5f;

    private readonly List<Enemy> _soldiers = new();
    private float _lastShield = -99f;
    private bool _shieldActive = false;
    private bool _dead = false;

    // ── ICommander ─────────────────────────────────────────────
    public bool IsAlive => !_dead && !Health.IsDead;
    public float CommandRadius => _commandRadius;

    public Vector3 GetSlotWorldPosition(Vector3 localOffset)
        => transform.position + transform.TransformDirection(localOffset);

    public void RegisterSoldier(Enemy soldier)
    {
        if (soldier == null || _soldiers.Contains(soldier)) return;
        _soldiers.Add(soldier);
        soldier.Health.OnDeath += () => _soldiers.Remove(soldier);
        soldier.GetComponent<GOAPGoalHoldFormation>()?.SetCommander(this);
    }

    public IReadOnlyList<Enemy> Soldiers => _soldiers;

    // ── Init ───────────────────────────────────────────────────
    public void InitialiseCommander(float commandRadius, float deathRageDuration)
    {
        _commandRadius = commandRadius;
        _deathRageDuration = deathRageDuration;
    }

    protected override void Awake()
    {
        base.Awake();
        Health.OnDeath += OnCommanderDied;
    }

    // ── Dark Shield ────────────────────────────────────────────
    public void NotifyDamageTaken(float amount, float maxHP)
    {
        if (_shieldActive) return;
        if (Time.time - _lastShield < _shieldCooldown) return;
        if (amount / maxHP >= _damageThreshold)
            StartCoroutine(ActivateDarkShield());
    }

    private IEnumerator ActivateDarkShield()
    {
        _shieldActive = true;
        _lastShield = Time.time;
        Debug.Log($"[PenitentCommander] STUB DarkShield — {_shieldDuration}s");
 // TODO: DarkShield commander corruption VFX retired with EnemyVFXController; re-express via the
        // dark-energy corruption-state cue (EnemyDarkEnergy owns it — a held STATE aura, not a mood) when the
        // commander archetypes are finished.

        foreach (var s in _soldiers)
        {
            if (s == null || s.Health.IsDead) continue;
 // TODO: soldier shield corruption VFX retired — same corruption-state cue as above.
            Debug.Log($"[PenitentCommander] STUB Shield → {s.name}");
            // TODO: s.Health.SetInvulnerable(true)
        }

        yield return new WaitForSeconds(_shieldDuration);

        foreach (var s in _soldiers)
        {
            if (s == null || s.Health.IsDead) continue;
            // TODO: s.Health.SetInvulnerable(false)
        }
        _shieldActive = false;
    }

    // ── Death cascade ──────────────────────────────────────────
    private void OnCommanderDied()
    {
        if (_dead) return;
        _dead = true;
        StartCoroutine(DeathCascade());
        MoodEventBus.AllyDied(gameObject);
    }

    private IEnumerator DeathCascade()
    {
        foreach (var s in _soldiers)
        {
            if (s == null || s.Health.IsDead) continue;
 // Enraged mood transition drives the soldier's Manpu rage aura autonomously — the parallel
            // EnemyVFXController.PlayRage is retired.
            s.GetComponent<EnemyMoodSystem>()
             ?.TransitionTo(EnemyMood.Enraged, _deathRageDuration, EnemyMood.Aggressive);
        }
        yield return new WaitForSeconds(_deathRageDuration);
        foreach (var s in _soldiers)
        {
            if (s == null || s.Health.IsDead) continue;
            s.GetComponent<GOAPGoalHoldFormation>()?.ClearCommander();
        }
        _soldiers.Clear();
    }
}