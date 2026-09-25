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
public class SiphonGhost : MonoBehaviour, ISpawnPoolable
{
    // ── ISpawnPoolable (P16 — pooled via GameplayPool.Summons) ─────────────────
    public void OnSpawned(GameplayPool pool)
    {
        // Refcounted warm pool for the ghost's binding chain — held while this ghost is out.
        if (_chainPrefab != null) GameplayPool.AddUser(_chainPrefab, PoolCategory.Projectiles, 1);
    }
    public void OnDespawned()
    {
        if (_chainPrefab != null) GameplayPool.RemoveUser(_chainPrefab);
        // Unsubscribe named handlers (previously OnDestroy-only — pooled objects never destroy).
        if (_rescueController != null) _rescueController.OnRescueResolved -= OnRescueResolved;
        if (_health != null) { _health.OnDeath -= OnGhostDied; _health.enabled = true; }
        if (_activeChain != null) { _activeChain.ForceDisconnect(); _activeChain = null; }
        // Full state reset — a reused ghost starts a fresh rescue with a fresh kill window.
        _soul = null;
        _rescueController = null;
        _data = null;
        IsBinding = false; IsThrowing = false; IsRetreating = false;
        IsImmune = false; IsResolved = false; KillWindowOpen = false;
        DebugPauseBindTimer = false;   // bench flag must not survive pool reuse
        OnKillWindowClosed = null; OnBindStarted = null; OnBindEnded = null; OnGhostResolved = null;
        SetColour(_pursuitColour);
        // Coroutines are stopped by the pool; held cues are swept by StopAllOn.
    }

    /// <summary>Dev bench (GameDebuggerV2): true = the bind countdown holds (mash-escape still works)
    /// so the bind chain/VFX can be observed. Trainer-only; reset on pool despawn.</summary>
    public bool DebugPauseBindTimer { get; set; }

    [Header("Movement")]
    [Tooltip("Ghost pursuit speed in units/sec.")]
    [SerializeField] private float _pursuitSpeed = 8f;

    [Header("Bind Visual Feedback")]
    [SerializeField] private Renderer _ghostRenderer;
    [SerializeField] private Color _pursuitColour = new Color(0.5f, 0.5f, 1f, 1f);
    [SerializeField] private Color _bindColour = new Color(1f, 0.35f, 0f, 1f);

    [Header("Chain throw (Mizuki-style bind)")]
    [Tooltip("The ChainProjectile prefab this ghost throws to bind the soul — the same generic chain TetherBreaker uses.")]
    [SerializeField] private GameObject _chainPrefab;

    private ChainProjectile _activeChain;

    // ── Runtime refs ──────────────────────────────────────────
    private SoulPlayer _soul;
    private RescueEventController _rescueController;
    private SiphonEnemyData _data;
    private EnemyHealthComponent _health;

    // ── State — public for GOAP brain/BT actions to read ─────
    public bool IsBinding { get; private set; }
    public bool IsThrowing { get; private set; }   // winding up / chain in flight, before catch or miss
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

        if (_chainPrefab == null)
            Debug.LogError("[SiphonGhost] _chainPrefab is unassigned — the ghost cannot throw its bind-chain, " +
                           "so it will never bind the soul. Assign the ChainProjectile prefab on the ghost.", this);

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
        // Ghost becomes untouchable — its own book's immune tell (rides the ghost).
        PlayGhostCue(FxIds.Enemy.SiphonGhost.onsiphonGhostImmune, CueContext.Follow(transform));
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
            else if (!IsBinding && !IsThrowing)
            {
                // Close to throw range, then wind up + throw the chain (it no longer binds on contact).
                float throwRange = _data?.ghostThrowRange ?? 2.5f;
                float dist = Vector3.Distance(transform.position, _soul.transform.position);
                if (dist > throwRange)
                    transform.position = Vector3.MoveTowards(
                        transform.position, _soul.transform.position, _pursuitSpeed * Time.deltaTime);
                else
                    TryThrow();
            }

            yield return null;
        }
    }

    // ── Throw → catch → bind ──────────────────────────────────
    // GOAP/BT + PursuitLoop entry. Winds up and throws the chain at the soul; the soul can dodge during the
    // marker windup. On catch → the EXISTING BindRoutine runs unchanged (rescue is identical once bound); on
    // miss → a brief retry. Guarded so PursuitLoop and the BT node can both call it safely.
    public void TryThrow()
    {
        if (IsThrowing || IsBinding || IsRetreating || IsResolved) return;
        if (_soul == null || _chainPrefab == null || _activeChain != null) return;
        StartCoroutine(ThrowRoutine());
    }

    private IEnumerator ThrowRoutine()
    {
        IsThrowing = true;

        // Face the soul, then throw at its CURRENT position (captured now — it can still dodge during the windup).
        Vector3 soulPos = _soul.transform.position;
        Vector3 dir = soulPos - transform.position; dir.y = 0f;
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);

        var go = GameplayPool.Spawn(_chainPrefab, PoolCategory.Projectiles, transform.position, Quaternion.identity);
        var chain = go != null ? go.GetComponent<ChainProjectile>() : null;
        if (chain == null) { GameplayPool.Despawn(go); IsThrowing = false; yield break; }

        _activeChain = chain;
        chain.OnChainConnected += OnSoulCaught;
        chain.OnChainMissed += OnChainThrowMissed;
        chain.Launch(
            soulPos, transform, 0,                          // layer mask unused — specificTarget path catches only the soul
            _data?.ghostChainTravelTime ?? 0.4f,
            _data?.ghostChainHitRadius ?? 0.6f,
            int.MaxValue,                                   // the ghost drives its own mash via BindRoutine — chain's is inert
            _data?.ghostRetryDelay ?? 0.75f,
            BuildGhostCueSet(),
            _data?.ghostMarkerWindup ?? 0.4f,
            _soul);                                         // catch ONLY the soul (exact target, nothing else)
        // The chain owns the windup + travel and fires OnChainConnected / OnChainMissed.
    }

    private void OnSoulCaught(Player caught)
    {
        IsThrowing = false;
        // Rescue-critical bind is UNCHANGED — freeze the soul, pause the captor TTK, mash to break.
        StartCoroutine(BindRoutine());
    }

    private void OnChainThrowMissed()
    {
        IsThrowing = false;
        _activeChain = null;                    // the chain reels in + self-destroys
        IsRetreating = true;                    // brief retry cadence before it can throw again (GOAP re-plans meanwhile)
        StartCoroutine(EndRetreatAfter(_data?.ghostRetryDelay ?? 0.75f));
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

            if (!DebugPauseBindTimer)                  // dev bench: hold the countdown to observe the bind
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

        // Break the chain: a mash-escape SNAPS it (on_chainbreak + recoil); a timer expiry disconnects quietly.
        if (_activeChain != null)
        {
            if (stun) _activeChain.Snap();
            else _activeChain.ForceDisconnect();
            _activeChain = null;
        }

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
        if (_activeChain != null) { _activeChain.ForceDisconnect(); _activeChain = null; }
        _soul?.GetComponent<IMovementFreezable>()?.SetFrozen(false);
        _soul?.GetComponent<SoulFrozenVFX>()?.SetFrozen(false);
        _rescueController?.ActiveTarget?.ResumeTTK();
        IsBinding = false;
        IsThrowing = false;
        IsRetreating = false;
        IsResolved = true;
        OnGhostResolved?.Invoke();
        _rescueController?.UnregisterGhost();
        GameplayPool.Despawn(gameObject);
    }

    private void SetColour(Color c)
    {
        if (_ghostRenderer != null)
            MaterialTint.SetColor(_ghostRenderer.material, c);
    }

    // ── Cue helpers (this ghost's own EnemyVfxLibrary book) ────
    private CueBookData GhostBook => VfxLibraryProvider.Instance?.Enemy?.SiphonGhost;

    private CueHandle PlayGhostCue(string id, in CueContext ctx)
    {
        var book = GhostBook;
        if (book == null || string.IsNullOrEmpty(id)) return CueHandle.None;
        return FxManager.Instance?.PlayBook(book, id, ctx) ?? CueHandle.None;
    }

    // The chain's cue ids, all from the ghost's book — marker (0→1 telegraph) / miss / grab / snap. No clank/drag.
    private ChainProjectile.ChainCueSet BuildGhostCueSet() => new ChainProjectile.ChainCueSet
    {
        book   = GhostBook,
        marker = FxIds.Enemy.SiphonGhost.on_chainMarker,
        miss   = FxIds.Enemy.SiphonGhost.on_chainMiss,
        grab   = FxIds.Enemy.SiphonGhost.on_chainGrab,
        snap   = FxIds.Enemy.SiphonGhost.on_chainbreak,
    };

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