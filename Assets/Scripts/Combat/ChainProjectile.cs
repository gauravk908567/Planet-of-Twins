using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Chain/rope projectile for the Tether-Breaker enemy.
///
/// FLOW:
///   Spawn at Tether-Breaker position → show ground marker at target position
///   → travel for _travelTime seconds → on arrival check for player in _hitRadius
///   → if hit: connect chain, call OnChainConnected → Tether-Breaker starts sprint
///   → player mashes E → OnChainBroken fires → Tether-Breaker enters rage
///   → if miss or TTK: chain detaches, Tether-Breaker enters pullback animation
///
/// SETUP:
///   Prefab: root GO + ChainProjectile + LineRenderer (chain visual) + 
///   child sphere (marker at target position, shows for travel duration).
/// </summary>
public class ChainProjectile : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private float _travelTime = 1.25f;  // seconds to reach target
    [SerializeField] private float _hitRadius = 0.55f;  // connection radius
    [SerializeField] private int _mashThreshold = 8;      // E presses to break free
    [SerializeField] private LayerMask _playerLayer;

    [Header("VFX")]
    [Tooltip("Ground marker showing where chain will land — shown during travel.")]
    [SerializeField] private GameObject _groundMarker;
    [Tooltip("LineRenderer for the rope visual between Tether-Breaker and target.")]
    [SerializeField] private LineRenderer _chainLine;

    // ── Events ────────────────────────────────────────────────
    public event Action<Player> OnChainConnected; // fires when chain hits player
    public event Action OnChainMissed;    // fires when chain misses
    public event Action OnChainBroken;    // fires when player mashes free

    // ── Runtime ───────────────────────────────────────────────
    private Vector3 _targetPosition;
    private Transform _tetherBreakerTransform;
    private Player _connectedPlayer;
    private int _mashCount;
    private bool _connected;
    private bool _resolved;

    // ── Public API ────────────────────────────────────────────
    public void Launch(
        Vector3 targetPosition,
        Transform casterTransform,
        LayerMask playerLayer)
    {
        _targetPosition = targetPosition;
        _tetherBreakerTransform = casterTransform;
        _playerLayer = playerLayer;

        // Show ground marker at target position
        if (_groundMarker != null)
        {
            _groundMarker.transform.position = targetPosition;
            _groundMarker.SetActive(true);
        }

        StartCoroutine(TravelRoutine());
    }

    /// <summary>Called by TwinAttackDispatcher when E is pressed during chain bind.</summary>
    public void NotifyMash()
    {
        if (!_connected) return;
        _mashCount++;
        if (_mashCount >= _mashThreshold)
            BreakChain();
    }

    /// <summary>Force disconnect — called on rescue success, Tether-Breaker death, etc.</summary>
    public void ForceDisconnect()
    {
        if (_resolved) return;
        _resolved = true;
        ReleasePlayer();
        Destroy(gameObject);
    }

    // ── Travel ────────────────────────────────────────────────
    private IEnumerator TravelRoutine()
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < _travelTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _travelTime;
            transform.position = Vector3.Lerp(startPos, _targetPosition, t);

            UpdateChainLine();
            yield return null;
        }

        transform.position = _targetPosition;
        _groundMarker?.SetActive(false);

        CheckConnection();
    }

    private void CheckConnection()
    {
        var hits = Physics.OverlapSphere(_targetPosition, _hitRadius, _playerLayer);
        foreach (var hit in hits)
        {
            var player = hit.GetComponent<Player>() ?? hit.GetComponentInParent<Player>();
            if (player == null || player is SoulPlayer) continue;
            if (player.IsGrabbed) continue; // already grabbed by something else

            Connect(player);
            return;
        }

        // Miss
        _groundMarker?.SetActive(false);
        OnChainMissed?.Invoke();
        Destroy(gameObject, 0.5f);
    }

    private void Connect(Player player)
    {
        _connected = true;
        _connectedPlayer = player;
        _mashCount = 0;

        OnChainConnected?.Invoke(player);

        // Start following the player
        StartCoroutine(FollowPlayer());
    }

    // ── Connected state ───────────────────────────────────────
    private IEnumerator FollowPlayer()
    {
        while (_connected && _connectedPlayer != null)
        {
            transform.position = _connectedPlayer.transform.position;
            UpdateChainLine();
            yield return null;
        }
    }

    private void BreakChain()
    {
        if (_resolved) return;
        _resolved = true;
        _connected = false;

        ReleasePlayer();
        OnChainBroken?.Invoke();
        Destroy(gameObject, 0.2f);
    }

    private void ReleasePlayer()
    {
        if (_connectedPlayer != null)
        {
            _connectedPlayer.SetGrabbed(false);
            _connectedPlayer = null;
        }
        _connected = false;
        if (_chainLine != null) _chainLine.enabled = false;
    }

    private void UpdateChainLine()
    {
        if (_chainLine == null || _tetherBreakerTransform == null) return;
        _chainLine.enabled = true;
        _chainLine.SetPosition(0, _tetherBreakerTransform.position + Vector3.up * 0.5f);
        // During travel: draw to target position (full rope visible immediately)
        // After connect: draw to player position (rope tethers to player)
        Vector3 endPoint = _connectedPlayer != null
            ? _connectedPlayer.transform.position + Vector3.up * 0.5f
            : _targetPosition;
        _chainLine.SetPosition(1, endPoint);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _hitRadius);
    }
}