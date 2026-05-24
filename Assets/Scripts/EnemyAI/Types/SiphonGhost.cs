using System.Collections;
using UnityEngine;

/// <summary>
/// SiphonGhost — pursues and binds the soul during rescue events.
/// Lifetime tied to SiphonEnemy — dies when owner dies or rescue resolves.
///
/// Architecture:
///   GOAP + BT drives all decisions (GOAPBrainSiphonGhost wires goals/actions).
///   Coroutines handle bind physics and mash detection (self-contained).
///   Full component stack — perception, dark energy, mood all pluggable later.
///   No NavMeshAgent — direct transform movement (ghost floats, no pathfinding).
///
/// FLOW:
///   Spawn → kill window open (soul can attack ghost)
///   → kill window closes → ghost immune
///   → reaches soul → Bind (soul frozen, mash E to break)
///   → mash success OR timer expire → retreat → retry
///   → loop until ghost killed OR rescue resolves OR siphon dies
///
/// SETUP:
///   Prefab: root GO + SiphonGhost + EnemyHealthComponent (HP=8)
///   Wire _ghostRenderer for colour feedback.
///   No NavMeshAgent needed.
/// </summary>
[RequireComponent(typeof(EnemyHealthComponent))]
public class SiphonGhost : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Ghost pursuit speed in units/sec.")]
    [SerializeField] private float _pursuitSpeed = 8f;

    [Header("Bind Visual Feedback")]
    [SerializeField] private Renderer _ghostRenderer;
    [SerializeField] private Color _pursuitColour = new Color(0.5f, 0.5f, 1f, 1f);
    [SerializeField] private Color _bindColour = new Color(1f, 0.35f, 0f, 1f);

    // ── Runtime refs ──────────────────────────────────────────
    private SoulPlayer _soul;
    private RescueEventController _rescueController;
    private SiphonEnemyData _data;
    private EnemyHealthComponent _health;

    // ── State — public for GOAP brain/BT actions to read ─────
    public bool IsBinding { get; private set; }
    public bool IsRetreating { get; private set; }
    public bool IsImmune { get; private set; }
    public bool IsResolved { get; private set; }
    public bool KillWindowOpen { get; private set; }

    public SoulPlayer Soul => _soul;
    public SiphonEnemyData GhostData => _data;
    public EnemyHealthComponent Health => _health;
    public float PursuitSpeed => _pursuitSpeed;

    // ── Events — GOAP brain can subscribe ────────────────────
    public event System.Action OnKillWindowClosed;
    public event System.Action OnBindStarted;
    public event System.Action OnBindEnded;
    public event System.Action OnGhostResolved;

    // ── Init ──────────────────────────────────────────────────
    public void Initialise(SoulPlayer soul, RescueEventController rescueController,
                           SiphonEnemyData data)
    {
        _soul = soul;
        _rescueController = rescueController;
        _data = data;
        _health = GetComponent<EnemyHealthComponent>();
        KillWindowOpen = true;

        if (_health != null)
        {
            _health.SetMaxHealth(data?.ghostMaxHp ?? 8f);
            _health.OnDeath += OnGhostDied;
        }

        SetColour(_pursuitColour);

        if (_rescueController != null)
            _rescueController.OnRescueResolved += OnRescueResolved;

        StartCoroutine(KillWindowRoutine());
        StartCoroutine(PursuitLoop());
    }

    private void OnDestroy()
    {
        if (_rescueController != null)
            _rescueController.OnRescueResolved -= OnRescueResolved;
    }

    // ── Kill window ───────────────────────────────────────────
    private IEnumerator KillWindowRoutine()
    {
        float killWindow = _data?.ghostKillWindowDuration ?? 2.2f;
        yield return new WaitForSeconds(killWindow);

        KillWindowOpen = false;
        IsImmune = true;
        if (_health != null) _health.enabled = false;
        OnKillWindowClosed?.Invoke();
    }

    // ── Pursuit — driven by GOAP goals but movement here ──────
    private IEnumerator PursuitLoop()
    {
        while (!IsResolved)
        {
            if (_soul == null || !_soul.gameObject.activeSelf)
            { yield return null; continue; }

            if (IsRetreating)
            {
                Vector3 fleeDir = (transform.position - _soul.transform.position).normalized;
                fleeDir.y = 0f;
                transform.position += fleeDir * _pursuitSpeed * Time.deltaTime;
            }
            else if (!IsBinding)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    _soul.transform.position,
                    _pursuitSpeed * Time.deltaTime);

                float dist = Vector3.Distance(transform.position, _soul.transform.position);
                if (dist <= 0.8f)
                    StartCoroutine(BindRoutine());
            }

            yield return null;
        }
    }

    // ── Bind ──────────────────────────────────────────────────
    public void TryBind()
    {
        if (IsBinding || IsRetreating || IsResolved) return;
        StartCoroutine(BindRoutine());
    }

    private IEnumerator BindRoutine()
    {
        IsBinding = true;
        SetColour(_bindColour);
        OnBindStarted?.Invoke();

        var activeTarget = _rescueController?.ActiveTarget;
        activeTarget?.PauseTTK();

        var soulMovement = _soul?.GetComponent<IMovementFreezable>();
        soulMovement?.SetFrozen(true);
        _soul?.GetComponent<SoulFrozenVFX>()?.SetFrozen(true);

        var input = _rescueController?.InputProvider;
        int mashCount = 0;
        int mashNeeded = _data?.ghostMashThreshold ?? 8;
        float bindDuration = _data?.ghostBindDuration ?? 2f;
        float elapsed = 0f;

        while (elapsed < bindDuration && !IsResolved)
        {
            bool mashed = input != null
                ? input.GetStruggleMash()
                : Input.GetKeyDown(KeyCode.E);

            if (mashed)
            {
                mashCount++;
                if (mashCount >= mashNeeded)
                {
                    EndBind(activeTarget, soulMovement, stun: true);
                    yield break;
                }
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Timer expired
        EndBind(activeTarget, soulMovement, stun: false);
    }

    private void EndBind(IRescueTarget activeTarget, IMovementFreezable soulMovement, bool stun)
    {
        activeTarget?.ResumeTTK();
        soulMovement?.SetFrozen(false);
        _soul?.GetComponent<SoulFrozenVFX>()?.SetFrozen(false);
        IsBinding = false;
        IsRetreating = true;
        SetColour(_pursuitColour);
        OnBindEnded?.Invoke();

        float delay = stun
            ? (_data?.ghostStunOnBreakDuration ?? 1.25f)
            : (_data?.ghostRetryDelay ?? 0.75f);

        StartCoroutine(EndRetreatAfter(delay));
    }

    private IEnumerator EndRetreatAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        IsRetreating = false;
    }

    // ── Death / Resolution ────────────────────────────────────
    private void OnGhostDied()
    {
        if (IsResolved) return;
        Resolve();
    }

    private void OnRescueResolved()
    {
        if (IsResolved) return;
        Resolve();
    }

    private void Resolve()
    {
        _soul?.GetComponent<IMovementFreezable>()?.SetFrozen(false);
        _soul?.GetComponent<SoulFrozenVFX>()?.SetFrozen(false);
        _rescueController?.ActiveTarget?.ResumeTTK();
        IsBinding = false;
        IsRetreating = false;
        IsResolved = true;
        OnGhostResolved?.Invoke();
        _rescueController?.UnregisterGhost();
        Destroy(gameObject);
    }

    private void SetColour(Color c)
    {
        if (_ghostRenderer != null)
            _ghostRenderer.material.color = c;
    }

    public void TakeDamage(float amount)
    {
        _health?.TakeDamage(new DamageData(amount, DamageType.Ability));
    }

    /// <summary>Called by SiphonEnemy on its own death.</summary>
    public void KillOnSiphonDeath()
    {
        if (IsResolved) return;
        Resolve();
    }
}