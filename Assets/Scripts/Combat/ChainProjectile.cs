using System;
using System.Collections;
using UnityEngine;

public class ChainProjectile : MonoBehaviour
{
    [Header("Config — overridden by TetherBreakerEnemyData at runtime")]
    [SerializeField] private float _travelTime = 1.25f;
    [SerializeField] private float _hitRadius = 0.55f;
    [SerializeField] private int _mashThreshold = 8;
    [SerializeField] private LayerMask _playerLayer;

    [Header("VFX")]
    [SerializeField] private GameObject _groundMarker;
    [SerializeField] private LineRenderer _chainLine;

    public event Action<Player> OnChainConnected;
    public event Action OnChainMissed;
    public event Action OnChainBroken;

    private Vector3 _targetPosition;
    private Transform _tetherBreakerTransform;
    private Player _connectedPlayer;
    private int _mashCount;
    private bool _connected;
    private bool _resolved;

    // ── Launch with SO data override ──────────────────────────
    public void Launch(
        Vector3 targetPosition,
        Transform casterTransform,
        LayerMask playerLayer,
        float travelTime = -1f,
        float hitRadius = -1f,
        int mashThreshold = -1)
    {
        _targetPosition = targetPosition;
        _tetherBreakerTransform = casterTransform;
        _playerLayer = playerLayer;

        // Override with SO values if provided
        if (travelTime > 0f) _travelTime = travelTime;
        if (hitRadius > 0f) _hitRadius = hitRadius;
        if (mashThreshold > 0) _mashThreshold = mashThreshold;

        // Detach marker from this GO so it stays at target when chain moves
        if (_groundMarker != null)
        {
            _groundMarker.transform.SetParent(null); // detach from chain GO
            _groundMarker.transform.position = targetPosition;
            _groundMarker.SetActive(true);
        }

        StartCoroutine(TravelRoutine());
    }

    public void NotifyMash()
    {
        if (!_connected) return;
        _mashCount++;
        if (_mashCount >= _mashThreshold)
            BreakChain();
    }

    public void ForceDisconnect()
    {
        if (_resolved) return;
        _resolved = true;
        ReleasePlayer();
        CleanupMarker();
        Destroy(gameObject);
    }

    private IEnumerator TravelRoutine()
    {
        Debug.Log($"[Chain] TravelRoutine starting — travelTime={_travelTime}");
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
        CleanupMarker();
        CheckConnection();
    }

    private void CheckConnection()
    {
        var hits = Physics.OverlapSphere(_targetPosition, _hitRadius, _playerLayer);
        foreach (var hit in hits)
        {
            var player = hit.GetComponent<Player>() ?? hit.GetComponentInParent<Player>();
            if (player == null || player is SoulPlayer) continue;
            if (player.IsGrabbed) continue;

            Connect(player);
            return;
        }

        OnChainMissed?.Invoke();
        Destroy(gameObject, 0.5f);
    }

    private void Connect(Player player)
    {
        _connected = true;
        _connectedPlayer = player;
        _mashCount = 0;
        OnChainConnected?.Invoke(player);
        StartCoroutine(FollowPlayer());
    }

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

    private void CleanupMarker()
    {
        if (_groundMarker != null)
        {
            _groundMarker.SetActive(false);
            // Re-parent so it gets cleaned up with the chain GO on destroy
            _groundMarker.transform.SetParent(transform);
        }
    }

    private void UpdateChainLine()
    {
        if (_chainLine == null || _tetherBreakerTransform == null) return;
        _chainLine.enabled = true;
        _chainLine.SetPosition(0, _tetherBreakerTransform.position + Vector3.up * 0.5f);

        // During travel use chain GO position, after connect use player position
        Vector3 endPoint = _connectedPlayer != null
            ? _connectedPlayer.transform.position + Vector3.up * 0.5f
            : transform.position + Vector3.up * 0.5f; // ← was _targetPosition, now chain GO pos

        _chainLine.SetPosition(1, endPoint);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _hitRadius);
    }
}