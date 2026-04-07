using System.Collections;
using UnityEngine;

/// <summary>
/// Tether-Breaker — pair enemy (one each side) that throws a chain to drag twins apart.
///
/// FLOW:
///   Chase → within chainAttackRange → lock position → throw chain (1.25s travel)
///   Chain misses → 1.8s cooldown (pull-back) → resume chase
///   Chain hits → player mashes E to break free
///   → 0.15s after connect: sprint away from barrier at 2.2x speed
///   → chain broken: RAGE (full aggro chase until death)
///   → not broken: player health drains → rescue triggers
///   Panic: player within 2.5m → drop bomb → retreat
///
/// POOL SETUP:
///   Assign TetherBreakerEnemyData, _bombPrefab, _bombMuzzle, _chainPrefab on prefab.
///   Spawns as independent pair — no cross-enemy linking needed.
///
/// IMPORTANT: Uses Awake subscription (not OnEnable) to avoid hiding Enemy.OnEnable.
/// </summary>
public class TetherBreakerEnemy : Enemy
{
    [Header("Tether-Breaker — project assets")]
    [SerializeField] private TetherBreakerEnemyData _tbData;
    [SerializeField] private GameObject _chainPrefab;
    [SerializeField] private LayerMask _playerLayer;

    // Injected at spawn
    private Player _leftPlayer;
    private Player _rightPlayer;

    // ── States ─────────────────────────────────────────────────
    public TetherBreakerRageState RageState { get; private set; }

    // ── Runtime ────────────────────────────────────────────────
    private bool _isFrozen;
    private bool _chainOnCooldown;
    private bool _throwing;
    private bool _sprinting;
    private bool _inRage;
    private ChainProjectile _activeChain;
    private Player _draggedPlayer; // tracked for death cleanup

    // ── UI events ──────────────────────────────────────────────
    public event System.Action<Player> OnChainGrabbed;   // player chained — show Press E
    public event System.Action OnChainReleased;  // chain gone — hide prompt

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
    // Release still called by pool Return() for cleanup

    public override void ApplyData(EnemyData data)
    {
        base.ApplyData(data);
        if (data is TetherBreakerEnemyData td)
        {
            _tbData = td;
            RageState = new TetherBreakerRageState(this, td);
            AttackState = new TetherBreakerAttackState(this, td);
            // Set AttackRange to chainAttackRange so ChaseState transitions at correct distance
            // RageState reads attackRange from base EnemyData (short melee range)
            SetAttackRange(td.chainAttackRange);
            FindTwinRefs();
        }
    }

    public override void OnEffectStarted() { base.OnEffectStarted(); _isFrozen = true; }
    public override void OnEffectEnded() { base.OnEffectEnded(); _isFrozen = false; }

    protected override void InitStates()
    {
        base.InitStates();
        // RageState created in ApplyData
    }

    // ── Update — panic bomb + chain mash input ────────────────
    private void Update()
    {
        // Read E mash via struggle input — consistent with Penitent crush
        if (_isFrozen) return;
        if (_activeChain != null)
        {
            if (Input.GetKeyDown(KeyCode.E))
                _activeChain.NotifyMash();
        }

        if (_inRage || _throwing || _sprinting || _chainOnCooldown) return;
        if (Target == null) return;

        float dist = Vector3.Distance(transform.position, Target.position);

    }

    // ── Chain attack ───────────────────────────────────────────
    // Called by TetherBreakerAttackState (overrides standard attack)
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
        // Lock position — face target
        StateMachine.Pause();
        Movement.Stop();

        Vector3 dir = (Target.position - transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero) transform.rotation = Quaternion.LookRotation(dir);

        // Spawn and launch chain
        var go = Instantiate(_chainPrefab, transform.position, Quaternion.identity);
        var chain = go.GetComponent<ChainProjectile>();
        if (chain == null) { Destroy(go); _throwing = false; _chainOnCooldown = false; StateMachine.Resume(); yield break; }

        _activeChain = chain;

        // Subscribe to chain events
        chain.OnChainConnected += OnChainConnected;
        chain.OnChainMissed += OnChainMissed;
        chain.OnChainBroken += OnChainBroken;

        chain.Launch(Target.position, transform, _playerLayer);

        _throwing = false;

        // Wait for chain to resolve
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

        // Freeze player — ragdoll on a rope, no control
        _draggedPlayer = player;
        (player.Movement as IMovementFreezable)?.SetFrozen(true);
        player.SetGrabbed(true);
        OnChainGrabbed?.Invoke(player);

        _sprinting = true;
        StateMachine.Resume();

        float sprintSpeed = (Data?.moveSpeed ?? 3.5f) * (_tbData?.sprintSpeedMultiplier ?? 2.2f);
        Movement.SetSpeed(sprintSpeed);

        // Calculate sprint direction — away from other twin to maximize tether distance
        Vector3 sprintDir = GetMaxTetherDirection(player);

        while (_sprinting && _activeChain != null)
        {
            while (_isFrozen) { yield return null; continue; }
            // Move Tether-Breaker in sprint direction
            Movement.MoveTowards(transform.position + sprintDir * 5f);

            // Drag player — maintain 5m gap behind Tether-Breaker
            if (player != null && !player.Health.IsDead)
            {
                // Target position is 5m behind Tether-Breaker in sprint direction
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

        // Release player
        _draggedPlayer = null;
        (player.Movement as IMovementFreezable)?.SetFrozen(false);
        player.SetGrabbed(false);
        OnChainReleased?.Invoke();

        Movement.SetSpeed(Data?.moveSpeed ?? 3.5f);
        _sprinting = false;
        _chainOnCooldown = false;
        _activeChain = null;

        if (!_inRage)
            StateMachine.ChangeState(ChaseState);
    }

    /// Returns direction that maximizes tether distance between twins
    private Vector3 GetMaxTetherDirection(Player draggedPlayer)
    {
        // Find the other twin
        Player otherTwin = null;
        if (_leftPlayer != null && _leftPlayer != draggedPlayer) otherTwin = _leftPlayer;
        else if (_rightPlayer != null && _rightPlayer != draggedPlayer) otherTwin = _rightPlayer;

        if (otherTwin == null)
        {
            // Fallback — just sprint away from dragged player
            Vector3 fallback = (transform.position - draggedPlayer.transform.position).normalized;
            fallback.y = 0f;
            return fallback;
        }

        // Sprint in direction that increases X distance (tether is X-axis based)
        Vector3 dir = (draggedPlayer.transform.position - otherTwin.transform.position).normalized;
        dir.y = 0f;
        return dir.normalized;
    }

    private void OnChainMissed()
    {
        StartCoroutine(ChainMissCooldown());
    }

    private IEnumerator ChainMissCooldown()
    {
        // Pull-back animation window
        while (_isFrozen) yield return null;
        yield return new WaitForSeconds(_tbData?.chainMissCooldown ?? 1.8f);
        _chainOnCooldown = false;
        _activeChain = null;
        StateMachine.Resume();
    }

    private void OnChainBroken()
    {
        // Player mashed free — enter rage
        _sprinting = false;
        _activeChain = null;
        _inRage = true;
        Movement.SetSpeed(Data?.moveSpeed ?? 3.5f);
        StateMachine.ChangeState(RageState);
    }

    // ── Chain mash input — called by TwinAttackDispatcher or input reader ──
    public void NotifyChainMash()
    {
        _activeChain?.NotifyMash();
    }

    // ── Colour ─────────────────────────────────────────────────
    public void SetRageColour(bool active)
    {
        if (_renderer != null)
            _renderer.material.color = active ? RageColor : _originalColor;
    }

    protected override void HandleDeath()
    {
        // Unfreeze and release player before chain disconnect
        if (_draggedPlayer != null)
        {
            (_draggedPlayer.Movement as IMovementFreezable)?.SetFrozen(false);
            _draggedPlayer.SetGrabbed(false);
            _draggedPlayer = null;
            OnChainReleased?.Invoke();
        }
        if (_activeChain != null && _activeChain.gameObject != null)
            _activeChain.ForceDisconnect();
        _activeChain = null;
        base.HandleDeath();
    }
}