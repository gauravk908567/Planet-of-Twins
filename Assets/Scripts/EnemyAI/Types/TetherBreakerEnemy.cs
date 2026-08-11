using System.Collections;
using UnityEngine;

/// <summary>
/// Tether-Breaker — throws chain to drag twins apart.
/// GOAP+BT drives all decisions. Chain coroutine handles physics.
/// </summary>
public class TetherBreakerEnemy : Enemy
{
    // ── VFX cue (EnemyVfxLibrary, R4) ──
    public override CueBookData VfxBook => VfxLibraryProvider.Instance?.Enemy?.TetherBreaker;
    protected override string MeleeAttackCueId => FxIds.Enemy.TetherBreaker.On_TetherMelee;

    [Header("Tether-Breaker — project assets")]
    [SerializeField] private TetherBreakerEnemyData _tbData;
    [SerializeField] private GameObject _chainPrefab;
    [SerializeField] private LayerMask _playerLayer;

    private Player _leftPlayer;
    private Player _rightPlayer;

    private bool _isFrozen;
    private bool _chainOnCooldown;
    private bool _throwing;
    private bool _sprinting;
    private bool _inRage;
    private ChainProjectile _activeChain;
    private Player _draggedPlayer;

    // ── Public accessors for GOAP goals and BT actions ─────────
    public bool IsInRage => _inRage;
    public bool IsThrowing => _throwing;
    public bool IsSprinting => _sprinting;
    public bool ChainOnCooldown => _chainOnCooldown;
    public bool ChainActive => _activeChain != null;
    public float MeleeRange => Data?.attackRange ?? 2f;
    public float ChainRange => _tbData?.chainAttackRange ?? 8f;
    public TetherBreakerEnemyData TBData => _tbData;

    public event System.Action<Player> OnChainGrabbed;
    public event System.Action OnChainReleased;

    private static readonly Color RageColor = new Color(1f, 0.2f, 0f);

    protected override void Awake()
    {
        base.Awake();
    }

    private void FindTwinRefs()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p is SoulPlayer) continue;
            if (_leftPlayer == null) _leftPlayer = p;
            else if (_rightPlayer == null) { _rightPlayer = p; break; }
        }
    }

    public void Release()
    {
        _activeChain?.ForceDisconnect();
        _activeChain = null;
        _leftPlayer = null;
        _rightPlayer = null;
        _inRage = false;
        _throwing = false;
        _sprinting = false;
        _chainOnCooldown = false;
    }

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);
        if (data is TetherBreakerEnemyData td)
        {
            _tbData = td;
            SetAttackRange(td.chainAttackRange);
            FindTwinRefs();
        }
    }

    public override void OnEffectStarted() { base.OnEffectStarted(); _isFrozen = true; }
    public override void OnEffectEnded() { base.OnEffectEnded(); _isFrozen = false; }

    private void Update()
    {
        if (_isFrozen) return;
        if (_activeChain != null && Input.GetKeyDown(KeyCode.E))
            _activeChain.NotifyMash();
    }

    // ── Called by BTActionChainAttack ──────────────────────────
    public void TryChainAttack()
    {
        if (_chainOnCooldown || _throwing || _sprinting || _inRage) return;
        if (_chainPrefab == null || Target == null) return;
        StartCoroutine(ChainAttackRoutine());
    }

    private IEnumerator ChainAttackRoutine()
    {
        _throwing = true;
        _chainOnCooldown = true;

        while (_isFrozen) yield return null;

        Movement.Stop();
        Vector3 dir = (Target.position - transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);

        RegisterPooledPrefab(_chainPrefab, PoolCategory.Projectiles, 1);   // refcounted warm pool, trimmed after death
        var go = GameplayPool.Spawn(_chainPrefab, PoolCategory.Projectiles, transform.position, Quaternion.identity);
        var chain = go != null ? go.GetComponent<ChainProjectile>() : null;
        if (chain == null) { GameplayPool.Despawn(go); _throwing = false; _chainOnCooldown = false; yield break; }

        _activeChain = chain;
        chain.OnChainConnected += OnChainConnected;
        chain.OnChainMissed += OnChainMissed;
        chain.OnChainBroken += OnChainBroken;
        chain.Launch(Target.position,
            transform,
            _playerLayer,
            _tbData?.chainTravelTime ?? 1.25f,
            _tbData?.chainHitRadius ?? 0.55f,
            _tbData?.chainMashThreshold ?? 8,
            _tbData?.chainPullDuration ?? 1.2f,
            // All chain cues come from the ONE TetherBreaker book (R4-resolved VfxBook); the chain fires them
            // at its own lifecycle beats. Marker replaces the old reticle + gates the throw via its reveal.
            new ChainProjectile.ChainCueSet
            {
                book   = VfxBook,
                marker = FxIds.Enemy.TetherBreaker.On_TetherChainMarker,
                clank  = FxIds.Enemy.TetherBreaker.On_TetherChainClankEffect,
                miss   = FxIds.Enemy.TetherBreaker.On_TetherChainMiss,
                grab   = FxIds.Enemy.TetherBreaker.On_TetherChainGrab,
                drag   = FxIds.Enemy.TetherBreaker.On_TetherChainDrag,
            },
            _tbData?.chainMarkerWindup ?? 0.5f);

        FactionEnergySystem.Instance?.OnChainFired();
        _throwing = false;

        yield return new WaitUntil(() => _activeChain == null || chain == null);
    }

    private void OnChainConnected(Player player)
    {
        StartCoroutine(ChainConnectedRoutine(player));
    }

    private IEnumerator ChainConnectedRoutine(Player player)
    {
        yield return new WaitForSeconds(_tbData?.chainConnectDelay ?? 0.15f);
        if (_activeChain == null) yield break;

        _draggedPlayer = player;
        (player.Movement as IMovementFreezable)?.SetFrozen(true);
        player.SetGrabbed(true);
        OnChainGrabbed?.Invoke(player);

        _sprinting = true;

        float sprintSpeed = (Data?.moveSpeed ?? 3.5f) * (_tbData?.sprintSpeedMultiplier ?? 2.2f);
        Movement.SetSpeed(sprintSpeed);

        Vector3 sprintDir = GetMaxTetherDirection(player);

        while (_sprinting && _activeChain != null)
        {
            while (_isFrozen) { yield return null; continue; }
            Movement.MoveTowards(transform.position + sprintDir * 5f);

            if (player != null && !player.Health.IsDead)
            {
                Vector3 dragTarget = transform.position - sprintDir * 5f;
                dragTarget.y = player.transform.position.y;
                Vector3 dragDelta = dragTarget - player.transform.position;
                if (dragDelta.magnitude > 0.1f)
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null && cc.enabled)
                        cc.Move(dragDelta.normalized * Mathf.Min(dragDelta.magnitude, 8f) * Time.deltaTime);
                }
            }
            yield return null;
        }

        _draggedPlayer = null;
        (player.Movement as IMovementFreezable)?.SetFrozen(false);
        player.SetGrabbed(false);
        OnChainReleased?.Invoke();

        Movement.SetSpeed(Data?.moveSpeed ?? 3.5f);
        _sprinting = false;
        _chainOnCooldown = false;
        _activeChain = null;
    }

    private Vector3 GetMaxTetherDirection(Player draggedPlayer)
    {
        Player otherTwin = null;
        if (_leftPlayer != null && _leftPlayer != draggedPlayer) otherTwin = _leftPlayer;
        else if (_rightPlayer != null && _rightPlayer != draggedPlayer) otherTwin = _rightPlayer;

        if (otherTwin == null)
        {
            Vector3 fallback = (transform.position - draggedPlayer.transform.position).normalized;
            fallback.y = 0f;
            return fallback;
        }

        Vector3 dir = (draggedPlayer.transform.position - otherTwin.transform.position).normalized;
        dir.y = 0f;
        return dir.normalized;
    }

    private void OnChainMissed() => StartCoroutine(ChainMissCooldown());
    private void OnChainBroken()
    {
        _sprinting = false;
        _activeChain = null;
        _inRage = true;
        Movement.SetSpeed(Data?.moveSpeed ?? 3.5f);
        SetRageColour(true);
 // rage aura is Manpu-driven now: enter the Enraged mood so the Enraged vocabulary loopPrefab
        // holds the aura for as long as the rage. (Chain-broken had no mood transition before; this is the one
        // place TetherBreaker's rage needed a mood target. The rage BEHAVIOUR still comes from _inRage + the
        // GOAP flags, not the mood modifiers.) Held until death/pool → ManpuSlot.Clear stops the aura.
        GetComponent<EnemyMoodSystem>()?.TransitionTo(EnemyMood.Enraged, 0f, EnemyMood.Normal);
    }

    private IEnumerator ChainMissCooldown()
    {
        while (_isFrozen) yield return null;

        // Pull-back window: stand still and reel the fallen chain back in (the chain GO drives the
        // grounded + retract visual over the same chainPullDuration, so they stay in sync).
        Movement.Stop();
        float pull = _tbData?.chainPullDuration ?? 1.2f;
        float total = _tbData?.chainMissCooldown ?? 1.8f;
        yield return new WaitForSeconds(pull);

        // Chain is back in hand. Release the chain action (ChainActive=false) so the BT lets him
        // reposition during the remainder — but he still can't throw until the full cooldown ends.
        _activeChain = null;
        yield return new WaitForSeconds(Mathf.Max(0f, total - pull));
        _chainOnCooldown = false;
    }

    public void NotifyChainMash() => _activeChain?.NotifyMash();

    public void SetRageColour(bool active)
    {
        if (_renderer != null)
            MaterialTint.SetColor(_renderer.material, active ? RageColor : _originalColor);
    }

    protected override void HandleDeath()
    {
        if (_draggedPlayer != null)
        {
            (_draggedPlayer.Movement as IMovementFreezable)?.SetFrozen(false);
            _draggedPlayer.SetGrabbed(false);
            _draggedPlayer = null;
            OnChainReleased?.Invoke();
        }
        _activeChain?.ForceDisconnect();
        _activeChain = null;
        base.HandleDeath();
    }
}