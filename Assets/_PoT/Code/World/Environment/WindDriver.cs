using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent singleton — the ONE owner of ambient world wind. Feeds three consumers from a
/// single set of dials:
///   • Shader globals <c>_PoTWind</c> (xz dir, w strength) + <c>_PoTWindGust</c> — consumed by the
///     PoT/Coexistence vertex sway (lanterns, banners, tassels; see CoexistenceCommon.hlsl).
///   • <see cref="DecorativeRope"/> ropes pull their force from <see cref="EvaluateWind"/>.
///   • An optional child <see cref="WindZone"/> — its windMain pulses with the gusts so
///     ParticleSystems (leaves) with External Forces react to the SAME wind.
/// Gusts are layered sines on SCALED time (R10: the world's wind slows under Setsuna and stops in
/// pause together with the shader's _Time-based sway — intended, they must stay in sync).
/// R3: lives in Persistent; globals zeroed in OnDestroy so nothing sways in a scene with no driver.
/// </summary>
public class WindDriver : MonoBehaviour
{
    public static WindDriver Instance { get; private set; }

    [Header("Wind")]
    [Tooltip("Compass direction the wind blows TOWARD, degrees (0 = +Z).")]
    [SerializeField, Range(0f, 360f)] private float _directionDegrees = 35f;
    [Tooltip("Master strength 0..1 — multiplies every material's own Sway Amount.")]
    [SerializeField, Range(0f, 1f)] private float _strength = 0.5f;

    [Header("Gusts")]
    [Tooltip("How much the gusts add on top of the base strength (0 = steady wind).")]
    [SerializeField, Range(0f, 1f)] private float _gustStrength = 0.6f;
    [Tooltip("Roughly how many gust swells per second (0.1 ≈ one every 10 s).")]
    [SerializeField, Range(0.02f, 1f)] private float _gustFrequency = 0.12f;

    [Header("Leaves / particles (optional)")]
    [Tooltip("Optional WindZone (child of this GO) kept in sync — point ParticleSystem External Forces at it.")]
    [SerializeField] private WindZone _windZone;
    [Tooltip("WindZone windMain at strength 1, before gusts.")]
    [SerializeField, Min(0f)] private float _windZoneMain = 1.2f;

    private static readonly int PoTWindID = Shader.PropertyToID("_PoTWind");
    private static readonly int PoTWindGustID = Shader.PropertyToID("_PoTWindGust");
    private static readonly int PoTWindPointsID = Shader.PropertyToID("_PoTWindPoints");
    private static readonly int PoTWindPointRadiiID = Shader.PropertyToID("_PoTWindPointRadii");

    // R5 registry — LocalWindZones (area-resident) self-register; null-purged before iterating.
    private readonly List<LocalWindZone> _localZones = new List<LocalWindZone>();

    private float _gustNow;   // 0..1, this frame

    /// <summary>Current normalized horizontal wind direction.</summary>
    public Vector3 Direction { get; private set; } = Vector3.forward;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }   // R3 duplicate guard
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            // Restart reloads Bootstrap — never leak a stale gust into the next boot.
            Shader.SetGlobalVector(PoTWindID, Vector4.zero);
            Shader.SetGlobalFloat(PoTWindGustID, 0f);
        }
    }

    private void Update()
    {
        float rad = _directionDegrees * Mathf.Deg2Rad;
        Direction = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

        // Layered sines → organic swell-and-fade gusts, never periodic-looking. SCALED time (R10).
        float t = Time.time * _gustFrequency * Mathf.PI * 2f;
        float g = Mathf.Sin(t) * 0.55f + Mathf.Sin(t * 2.7f + 1.3f) * 0.30f + Mathf.Sin(t * 5.9f + 4.1f) * 0.15f;
        _gustNow = Mathf.Clamp01(g * 0.5f + 0.5f) * _gustStrength;

        Shader.SetGlobalVector(PoTWindID, new Vector4(Direction.x, 0f, Direction.z, _strength));
        Shader.SetGlobalFloat(PoTWindGustID, _gustNow);

        if (_windZone != null)
        {
            _windZone.transform.rotation = Quaternion.LookRotation(Direction);
            _windZone.windMain = _windZoneMain * _strength * (1f + _gustNow);
            _windZone.windTurbulence = _windZoneMain * 0.5f * _strength;
        }

        PackLocalZones();
    }

    // Shader path takes the four strongest-by-proximity zones (camera-relative) per frame.
    // Rows: xyz = world position, w = extra strength; radii packed alongside.
    private void PackLocalZones()
    {
        _localZones.RemoveAll(z => z == null);   // R5 null purge

        Vector3 camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        _localZones.Sort((a, b) =>
            (a.transform.position - camPos).sqrMagnitude.CompareTo((b.transform.position - camPos).sqrMagnitude));

        var m = Matrix4x4.zero;
        var radii = Vector4.zero;
        int count = Mathf.Min(4, _localZones.Count);
        for (int i = 0; i < count; i++)
        {
            Vector3 p = _localZones[i].transform.position;
            m.SetRow(i, new Vector4(p.x, p.y, p.z, _localZones[i].ExtraStrength));
            radii[i] = _localZones[i].Radius;
        }
        for (int i = count; i < 4; i++) radii[i] = 0.01f;   // dead rows: tiny radius, zero strength
        Shader.SetGlobalMatrix(PoTWindPointsID, m);
        Shader.SetGlobalVector(PoTWindPointRadiiID, radii);
    }

    /// <summary>Area LocalWindZones self-register here (R5). Safe to call twice.</summary>
    public void Register(LocalWindZone zone)
    {
        if (zone != null && !_localZones.Contains(zone)) _localZones.Add(zone);
    }

    public void Unregister(LocalWindZone zone) => _localZones.Remove(zone);

    /// <summary>Wind force (m/s²-ish) at a world position — spatial Perlin variation so two ropes
    /// never move identically. Zero when the driver is disabled.</summary>
    public Vector3 EvaluateWind(Vector3 worldPos)
    {
        if (!isActiveAndEnabled) return Vector3.zero;
        float spatial = 0.6f + 0.8f * Mathf.PerlinNoise(
            worldPos.x * 0.11f + Time.time * 0.35f,   // scaled time — see class doc
            worldPos.z * 0.11f);

        // Local zones boost the force the same way they boost the shader sway.
        float boost = 0f;
        for (int i = 0; i < _localZones.Count; i++)
        {
            var z = _localZones[i];
            if (z == null) continue;
            float d = Vector3.Distance(worldPos, z.transform.position);
            boost += z.ExtraStrength * Mathf.Clamp01(1f - d / z.Radius);
        }

        return Direction * (_strength * (1f + _gustNow * 1.5f) * spatial * (1f + boost) * 6f);
    }
}
