using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovementController : MonoBehaviour, IMovementFreezable
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 720f;

    [Header("Gravity / Ground Check")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float groundCheckOffset = 0.1f;
    [SerializeField] private float groundSnapDistance = 0.3f;
    [SerializeField] private LayerMask groundLayer;

    private CharacterController _controller;
    private IMovementModifier _movementModifier;
    private bool _soulMode;
    private bool _frozen;
    private bool _locked;

    // ── Speed multiplier (applied by VigilSystem to the empowered twin) ───
    // Default 1f = no modification. VigilSystem sets this to e.g. 1.20f
    // and restores to 1f on deactivation.
    private float _speedMultiplier = 1f;

    // ── Unscaled time mode (Setsuna) ──────────────────────────
    // When true, movement uses Time.unscaledDeltaTime so twins
    // move at normal speed even when Time.timeScale is reduced.
    private bool _useUnscaledTime = false;
    public void SetUseUnscaledTime(bool value) => _useUnscaledTime = value;

    // ── Dash state ────────────────────────────────────────────
    private Coroutine _dashCoroutine;
    private bool _isDashing;

    private float _verticalVelocity;
    private const float GroundedVelocity = -2f;

    [SerializeField] private bool usePhaseThroughMovement = false;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    // ── External setters ──────────────────────────────────────
    public void SetMovementModifier(IMovementModifier modifier) => _movementModifier = modifier;
    public void SetFrozen(bool frozen) => _frozen = frozen;
    public void SetSoulMode(bool value) => _soulMode = value;
    public void SetMovementLocked(bool locked) => _locked = locked;

    /// <summary>
    /// Applied by VigilSystem to the empowered twin.
    /// 1.20f = +20% speed. Restore to 1f when Vigil ends.
    /// </summary>
    public void SetSpeedMultiplier(float multiplier) =>
        _speedMultiplier = Mathf.Max(0f, multiplier);

    /// <summary>
    /// Triggers a short forward burst for the empowered twin during Vigil.
    /// Called by VigilSystem when Shift is pressed.
    /// </summary>
    public void StartDash(float dashSpeed, float dashDuration)
    {
        if (_dashCoroutine != null) StopCoroutine(_dashCoroutine);
        _dashCoroutine = StartCoroutine(DashRoutine(dashSpeed, dashDuration));
    }

    private IEnumerator DashRoutine(float speed, float duration)
    {
        _isDashing = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Dash ignores lock/frozen — it's a committed movement burst.
            // Ground check still applies so the character doesn't fly.
            float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            Vector3 dashMove = transform.forward * speed * dt;
            dashMove.y = GroundedVelocity * dt;

            if (usePhaseThroughMovement)
                transform.Translate(dashMove, Space.World);
            else
                _controller.Move(dashMove);

            elapsed += _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        _isDashing = false;
        _dashCoroutine = null;
    }

    // ── Main movement ─────────────────────────────────────────
    public void ExecuteCommand(IPlayerCommand command)
    {
        if (_frozen) return;
        if (_locked) return;
        if (_isDashing) return; // dash coroutine owns movement for its duration

        Vector2 input = command.GetMovementInput();

        if (!_soulMode)
        {
            if (_movementModifier != null)
                input = _movementModifier.Modify(input);

            Vector3 move = new Vector3(input.x, 0f, input.y);

            if (move.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * (_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime));
            }

            bool isGrounded = CheckGrounded();

            float moveDt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (isGrounded)
                _verticalVelocity = GroundedVelocity;
            else
                _verticalVelocity += gravity * moveDt;

            // _speedMultiplier compounds with base moveSpeed.
            // Default is 1f so normal play is unchanged.
            Vector3 finalMove = move * (moveSpeed * _speedMultiplier) * moveDt;
            finalMove.y = _verticalVelocity * moveDt;

            if (usePhaseThroughMovement)
                transform.Translate(finalMove, Space.World);
            else
                _controller.Move(finalMove);
        }
    }

    private bool CheckGrounded()
    {
        Vector3 sphereOrigin = transform.position +
                               Vector3.up * (groundCheckOffset + groundCheckRadius);

        return Physics.SphereCast(
            sphereOrigin,
            groundCheckRadius,
            Vector3.down,
            out _,
            groundSnapDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 origin = transform.position +
                         Vector3.up * (groundCheckOffset + groundCheckRadius);
        Gizmos.DrawWireSphere(origin, groundCheckRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + Vector3.down * groundSnapDistance);
    }
}