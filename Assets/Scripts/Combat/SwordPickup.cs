using UnityEngine;

/// <summary>
/// Place on the ground sword GO with a Trigger collider.
/// When a twin walks into it:
///   - Ground sword GO disables (this GO)
///   - Player sword GO activates
///   - E melee unlocks on that twin only
///
/// SETUP:
///   - Wire _playerSwordGO to the sword GO on the player prefab (starts inactive)
///   - Wire _attackController to the PlayerAttackController on the correct twin
///   - Tick _isForLeftTwin on the LEFT sword pickup only
///   - Collider on this GO: Is Trigger ON
/// </summary>
public class SwordPickup : MonoBehaviour
{
    [Tooltip("Sword GO on the player prefab — activated on pickup")]
    [SerializeField] private GameObject _playerSwordGO;

    [Tooltip("PlayerAttackController on the twin this pickup belongs to")]
    [SerializeField] private PlayerAttackController _attackController;

    [Tooltip("Tick ON for Kai/left twin's sword. Leave OFF for Lyra/right twin's sword.")]
    [SerializeField] private bool _isForLeftTwin;

    public bool IsForLeftTwin => _isForLeftTwin;

    [Header("Idle Animation")]
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _bobHeight = 0.15f;
    [SerializeField] private float _bobSpeed = 1.5f;

    private Vector3 _startPosition;

    private void Awake()
    {
        _startPosition = transform.position;
    }

    private void Update()
    {
        transform.Rotate(0f, -_rotationSpeed * Time.deltaTime, 0f, Space.World);
        float newY = _startPosition.y + Mathf.Sin(Time.time * _bobSpeed * Mathf.PI * 2f) * _bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<Player>() ?? other.GetComponentInParent<Player>();
        if (player == null || player is SoulPlayer) return;

        var attackController = other.GetComponentInParent<PlayerAttackController>();
        if (attackController == null) return;

        Apply(attackController);
    }

    /// <summary>
    /// Called by CheckpointLoader after scene reload when this twin
    /// already had the sword at checkpoint save time.
    /// </summary>
    public void ForceApply()
    {
        if (_attackController == null)
        {
            Debug.LogWarning("[SwordPickup] ForceApply — _attackController not wired in Inspector!", this);
            return;
        }
        Debug.Log($"[SwordPickup] ForceApply on {gameObject.name} (isLeft={_isForLeftTwin})");
        Apply(_attackController);
    }

    private void Apply(PlayerAttackController attackController)
    {
        attackController.SetHasWeapon(true);
        if (_playerSwordGO != null) _playerSwordGO.SetActive(true);
        gameObject.SetActive(false);
    }
}