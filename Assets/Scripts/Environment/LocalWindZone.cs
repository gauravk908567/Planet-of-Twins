using UnityEngine;

/// <summary>
/// LocalWindZone — a sphere where the world wind blows HARDER (crack energy leak, vents,
/// storm pockets). Area-resident; self-registers into the Persistent <see cref="WindDriver"/>
/// (R5 registry — no cross-scene refs). Everything that consumes the wind reacts:
///   • Coexistence vertex sway (lanterns/banners/cloth) gains amplitude + a fast flutter inside,
///   • <see cref="DecorativeRope"/> forces via <see cref="WindDriver.EvaluateWind"/>,
///   • NOT the global WindZone/particles (those stay ambient — emit local particles yourself).
/// The shader path packs the FOUR strongest zones nearest the camera per frame — placing more
/// than four per area is fine, only the closest four displace vertices at once.
/// </summary>
public class LocalWindZone : MonoBehaviour
{
    [Tooltip("Sphere radius (m) — full boost at the centre, fades to zero at the edge.")]
    [SerializeField, Min(0.5f)] private float _radius = 6f;
    [Tooltip("Extra wind at the centre, on top of the global strength (1 = double).")]
    [SerializeField, Min(0f)] private float _extraStrength = 1.2f;

    public float Radius => _radius;
    public float ExtraStrength => _extraStrength;

    private void OnEnable()
    {
        if (WindDriver.Instance != null) WindDriver.Instance.Register(this);
        // else: WindDriver registers late-joiners itself via Start-order — see Start().
    }

    private void Start()
    {
        // Direct-play in an area scene can run OnEnable before Persistent finishes loading (R4 timing).
        if (WindDriver.Instance != null) WindDriver.Instance.Register(this);
        else Debug.LogError("[LocalWindZone] WindDriver not found (Persistent not loaded?) — zone inert.", this);
    }

    private void OnDisable()
    {
        if (WindDriver.Instance != null) WindDriver.Instance.Unregister(this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
#endif
}
