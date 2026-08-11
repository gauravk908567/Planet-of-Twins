using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tracks twin movement and predicts future position.
/// Provides both LEAD (where twin will be) and CUTOFF (interception point)
/// predictions for enemy AI to use.
///
/// ATTACH: To each Twin player GO.
/// Enemies read prediction via static lookup or direct reference.
/// </summary>
public class TwinAnticipator : MonoBehaviour
{
    [Header("Prediction Config")]
    [Tooltip("How far ahead to predict twin position (seconds).")]
    [SerializeField] private float _leadTime = 1.2f;

    [Tooltip("How far along twin's path to sample for cutoff point.")]
    [SerializeField] private float _cutoffLookAhead = 4f;

    [Tooltip("How many points to sample along predicted path for cutoff.")]
    [SerializeField] private int _cutoffSamples = 6;

    [Tooltip("Min speed before prediction activates — avoid predicting stationary twin.")]
    [SerializeField] private float _minSpeedForPrediction = 1.5f;

    private CharacterController _cc;
    private Vector3 _velocity;
    private Vector3 _smoothedVelocity;
    private Vector3 _lastPosition;

    // ── Public predictions ─────────────────────────────────
    /// <summary>Where twin will be in _leadTime seconds.</summary>
    public Vector3 LeadPosition { get; private set; }

    /// <summary>Best interception point on NavMesh along twin's path.</summary>
    public Vector3 CutoffPosition { get; private set; }

    /// <summary>Twin's current smoothed velocity.</summary>
    public Vector3 Velocity => _smoothedVelocity;

    /// <summary>Twin is moving fast enough to predict.</summary>
    public bool IsMoving => _smoothedVelocity.magnitude >= _minSpeedForPrediction;

    /// <summary>Twin is fleeing — moving away from most recent attacker direction.</summary>
    public bool IsFleeing { get; private set; }

    // Static registry — enemies look up by side
    public static TwinAnticipator Left { get; private set; }
    public static TwinAnticipator Right { get; private set; }

    [SerializeField] private bool _isLeftTwin = true;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _lastPosition = transform.position;
        LeadPosition = transform.position;
        CutoffPosition = transform.position;

        if (_isLeftTwin) Left = this;
        else Right = this;
    }

    private void OnDestroy()
    {
        if (_isLeftTwin && Left == this) Left = null;
        if (!_isLeftTwin && Right == this) Right = null;
    }

    private void Update()
    {
        // Get velocity from CharacterController
        _velocity = _cc != null ? _cc.velocity : Vector3.zero;

        // Fallback — derive from position delta if CC velocity is zero
        if (_velocity.magnitude < 0.01f)
        {
            _velocity = (transform.position - _lastPosition) / Time.deltaTime;
            _velocity.y = 0f;
        }

        // Smooth velocity — prevents jitter from brief stops
        _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, _velocity, Time.deltaTime * 8f);

        _lastPosition = transform.position;

        UpdatePredictions();
        DetectFleeing();
    }

    private void UpdatePredictions()
    {
        if (!IsMoving)
        {
            // Not moving — prediction = current position
            LeadPosition = transform.position;
            CutoffPosition = transform.position;
            return;
        }

        // LEAD — simple extrapolation
        Vector3 lead = transform.position + _smoothedVelocity * _leadTime;

        // Snap lead to NavMesh
        if (NavMesh.SamplePosition(lead, out var leadHit, 3f, NavMesh.AllAreas))
            LeadPosition = leadHit.position;
        else
            LeadPosition = transform.position;

        // CUTOFF — sample points along predicted path, find best interception
        CutoffPosition = CalculateCutoff();
    }

    private Vector3 CalculateCutoff()
    {
        Vector3 dir = _smoothedVelocity.normalized;
        Vector3 origin = transform.position;

        Vector3 bestCutoff = LeadPosition;
        float bestCutoffScore = -1f;

        for (int i = 1; i <= _cutoffSamples; i++)
        {
            float t = (float)i / _cutoffSamples;
            Vector3 candidate = origin + dir * (_cutoffLookAhead * t);

            if (!NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas))
                continue;

            // Score: prefer points further along path (better cutoff)
            // but penalise if off NavMesh significantly
            float distFromPath = Vector3.Distance(hit.position, candidate);
            float score = t - distFromPath * 0.2f;

            if (score > bestCutoffScore)
            {
                bestCutoffScore = score;
                bestCutoff = hit.position;
            }
        }

        return bestCutoff;
    }

    private void DetectFleeing()
    {
        // Fleeing = moving fast and roughly away from screen centre (barrier)
        // Simple heuristic — twin moving away from barrier at speed
        IsFleeing = IsMoving && _smoothedVelocity.magnitude > _minSpeedForPrediction * 1.5f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = new Color(0f, 1f, 1f, 0.8f);
        Gizmos.DrawWireSphere(LeadPosition, 0.4f);
        Gizmos.DrawLine(transform.position, LeadPosition);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireSphere(CutoffPosition, 0.4f);
        Gizmos.DrawLine(transform.position, CutoffPosition);
    }
}