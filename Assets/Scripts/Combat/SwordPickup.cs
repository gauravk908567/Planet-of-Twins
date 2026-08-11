using UnityEngine;

/// <summary>
/// Place on the ground sword GO with a Trigger collider. When a twin walks into it, that twin gains the melee
/// weapon: the twin OWNS and activates its own sword GO via <c>PlayerAttackController.SetHasWeapon</c> (same
/// prefab — R1). There is NO cross-scene reference to the Persistent twin's sword GO (the old `_playerSwordGO`
/// was R2-broken — Unity nulled it because this pickup lives in the area scene). Checkpoint restore goes
/// through <c>SoftResetController</c> → the twins' <c>SetHasWeapon</c> directly, not this pickup.
///
/// SETUP:
///   - Collider on this GO: Is Trigger ON.
///   - On each twin prefab, wire <c>PlayerAttackController._swordGO</c> to that twin's sword mesh (inactive).
///   - Tick _isForLeftTwin on the LEFT sword pickup (save/restore mapping only).
/// </summary>
public class SwordPickup : MonoBehaviour
{
    [Tooltip("Tick ON for Kai/left twin's sword. Leave OFF for Lyra/right twin's sword (save/restore mapping).")]
    [SerializeField] private bool _isForLeftTwin;

    public bool IsForLeftTwin => _isForLeftTwin;

    [Header("Idle Animation")]
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _bobHeight = 0.15f;
    [SerializeField] private float _bobSpeed = 1.5f;

    private Vector3 _startPosition;

    private void Awake() => _startPosition = transform.position;

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

        attackController.SetHasWeapon(true);   // the twin activates its own sword GO (R1) — no cross-scene ref
        gameObject.SetActive(false);
    }
}
