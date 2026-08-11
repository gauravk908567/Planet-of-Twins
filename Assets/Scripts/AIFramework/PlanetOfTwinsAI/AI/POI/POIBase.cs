using UnityEngine;

/// <summary>
/// Base class for all Points of Interest.
/// Registers with POIManager on Awake.
/// Provides dark energy gain rate and active state.
/// </summary>
public abstract class POIBase : MonoBehaviour
{
    [Header("POI Config")]
    public POIType PoiType;
    [Tooltip("Dark energy gain per second for enemies within range.")]
    public float DarkEnergyGainRate = 0.002f;
    [Tooltip("Range within which enemies gain dark energy.")]
    public float InfluenceRadius = 8f;

    public bool IsActive { get; protected set; } = true;

    protected virtual void Awake()
    {
        POIManager.Instance?.Register(this);
    }

    protected virtual void OnDestroy()
    {
        POIManager.Instance?.Unregister(this);
    }

    protected virtual void OnEnable()
    {
        IsActive = true;
        POIManager.Instance?.Register(this);
    }

    protected virtual void OnDisable()
    {
        IsActive = false;
        POIManager.Instance?.Unregister(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsActive
            ? new Color(1f, 0.5f, 0f, 0.2f)
            : new Color(0.5f, 0.5f, 0.5f, 0.1f);
        Gizmos.DrawSphere(transform.position, InfluenceRadius);
        Gizmos.color = IsActive
            ? new Color(1f, 0.5f, 0f, 0.7f)
            : new Color(0.5f, 0.5f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, InfluenceRadius);
    }
}