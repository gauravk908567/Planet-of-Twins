using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovementController : MonoBehaviour, IMovementFreezable
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 720f;

    [Header("Gravity / Ground Check")]
    [SerializeField] private float gravity = -20f;   // downward force
    [SerializeField] private float groundCheckRadius = 0.25f;  // sphere radius
    [SerializeField] private float groundCheckOffset = 0.1f;   // centre offset from feet
    [SerializeField] private float groundSnapDistance = 0.3f;   // max snap distance
    [SerializeField] private LayerMask groundLayer;             // assign Ground layer

    private CharacterController _controller;
    private IMovementModifier _movementModifier;
    private bool _soulMode;
    private bool _frozen;
    private bool _locked;

    // Gravity accumulator — resets to small negative when grounded (keeps grounded=true)
    private float _verticalVelocity;
    private const float GroundedVelocity = -2f; // small constant keeps isGrounded reliable

    [SerializeField] private bool usePhaseThroughMovement = false;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    public void SetMovementModifier(IMovementModifier modifier) => _movementModifier = modifier;
    public void SetFrozen(bool frozen) => _frozen = frozen;
    public void SetSoulMode(bool value) => _soulMode = value;
    public void SetMovementLocked(bool locked) => _locked = locked;

    public void ExecuteCommand(IPlayerCommand command)
    {
        if (_frozen) return;
        if (_locked) return;

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
                    rotationSpeed * Time.deltaTime);
            }

            // ── Ground check + gravity ─────────────────────────
            bool isGrounded = CheckGrounded();

            if (isGrounded)
            {
                // Reset vertical velocity — tiny negative keeps CharacterController grounded
                _verticalVelocity = GroundedVelocity;
            }
            else
            {
                // Accumulate gravity when airborne
                _verticalVelocity += gravity * Time.deltaTime;
            }

            // Combine horizontal move with vertical velocity
            Vector3 finalMove = move * moveSpeed * Time.deltaTime;
            finalMove.y = _verticalVelocity * Time.deltaTime;

            if (usePhaseThroughMovement)
                transform.Translate(finalMove, Space.World);
            else
                _controller.Move(finalMove);
        }
    }

    // CS-style ground check — SphereCast downward from feet
    private bool CheckGrounded()
    {
        // Sphere centre: slightly above feet to avoid clipping into ground
        Vector3 sphereOrigin = transform.position +
                               Vector3.up * (groundCheckOffset + groundCheckRadius);

        // Cast downward — if hits ground within snap distance, we're grounded
        bool hit = Physics.SphereCast(
            sphereOrigin,
            groundCheckRadius,
            Vector3.down,
            out RaycastHit hitInfo,
            groundSnapDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore);

        return hit;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualise ground check sphere in Scene view
        Gizmos.color = Color.green;
        Vector3 origin = transform.position +
                         Vector3.up * (groundCheckOffset + groundCheckRadius);
        Gizmos.DrawWireSphere(origin, groundCheckRadius);

        // Show snap distance
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + Vector3.down * groundSnapDistance);
    }
}