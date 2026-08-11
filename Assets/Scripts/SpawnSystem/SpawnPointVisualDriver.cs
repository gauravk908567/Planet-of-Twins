using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Presentation driver for a breakable spawn point. Reads the recharge timer on the sibling
/// <see cref="SpawnPointPOI"/> and ramps the point's OWN embedded visuals as it recharges:
///   • recharge VFX     — SpawnRate/ConvergeSpeed/spawnradius intensify + converge toward ready
///   • structure mats   — _Vertex_Intensity 0 while down → the authored value when ready
///   • broken husk       — shrinks out over the last stretch of the recharge
///   • active portal      — visible only while the point is up
///
/// These are the object's own STATE visuals, not transient cues: a multi-second converge whose
/// params are a live function of a gameplay timer cannot be a fire-and-forget pool cue, so it
/// lives on the object. The one true cue here stays on the POI (spawn_hit — a momentary reaction).
///
/// R1 same-scene serialized refs (all children of this prefab). R8 named handlers, unsub in OnDisable.
/// R7 reads the authored material value, never writes the shared asset (per-renderer MaterialPropertyBlock).
/// </summary>
[RequireComponent(typeof(SpawnPointPOI))]
public class SpawnPointVisualDriver : MonoBehaviour
{
    [Header("Active portal — shown only while the point is up")]
    [SerializeField] private GameObject _portal;

    [Header("Recharge VFX — plays while down, intensifies toward ready")]
    [SerializeField] private VisualEffect _rechargeVfx;
    [Tooltip("SpawnRate exposed on the recharge VFX: down → ready. Ready capped at your limit (400).")]
    [SerializeField] private Vector2Int _spawnRate = new Vector2Int(20, 400);
    [Tooltip("ConvergeSpeed exposed on the recharge VFX: down → ready. Ready capped at your limit (5).")]
    [SerializeField] private Vector2 _convergeSpeed = new Vector2(1.5f, 5f);
    [Tooltip("spawnradius exposed on the recharge VFX: down → ready. Ready capped at your limit (5).")]
    [SerializeField] private Vector2 _spawnRadius = new Vector2(1f, 5f);

    [Header("Broken husk — shrinks out near the end")]
    [SerializeField] private Transform _disableHusk;
    [Range(0f, 1f)]
    [Tooltip("Recharge fraction after which the husk starts shrinking. 0.7 = last 30%.")]
    [SerializeField] private float _huskShrinkStart = 0.7f;

    // Structure renderers (_Vertex_Intensity ramps 0 → authored) are auto-found in Awake — every child
    // mesh whose material carries _Vertex_Intensity. No manual wiring, and the VFX renderers are excluded.

    static readonly int VertexIntensityID = Shader.PropertyToID("_Vertex_Intensity");
    static readonly int SpawnRateID = Shader.PropertyToID("SpawnRate");
    static readonly int ConvergeSpeedID = Shader.PropertyToID("ConvergeSpeed");
    static readonly int SpawnRadiusID = Shader.PropertyToID("spawnradius");

    private SpawnPointPOI _poi;
    private MaterialPropertyBlock _mpb;
    private Renderer[] _renderers;
    private float[] _baseIntensity;   // authored per-renderer value, captured once at Awake
    private Vector3 _huskBaseScale = Vector3.one;

    private void Awake()
    {
        _poi = GetComponent<SpawnPointPOI>();
        _mpb = new MaterialPropertyBlock();
        GatherRenderers();
        if (_disableHusk != null) _huskBaseScale = _disableHusk.localScale;
    }

    // Auto-find every child mesh whose material carries _Vertex_Intensity (the vertex-displacement structure
    // parts). Excludes the VFX renderers (portal/recharge) automatically → no manual ref wiring needed.
    private void GatherRenderers()
    {
        var list = new List<Renderer>();
        var baseVals = new List<float>();
        foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
        {
            var m = r.sharedMaterial;   // read authored asset value, never mutate it (R7)
            if (m == null || !m.HasFloat(VertexIntensityID)) continue;
            list.Add(r);
            baseVals.Add(m.GetFloat(VertexIntensityID));
        }
        _renderers = list.ToArray();
        _baseIntensity = baseVals.ToArray();
    }

    private void OnEnable()
    {
        _poi.OnSpawnPointDestroyed += HandleDestroyed;
        _poi.OnSpawnPointRespawned += HandleRespawned;
        ApplyReady();   // the point boots up "up"
    }

    private void OnDisable()
    {
        _poi.OnSpawnPointDestroyed -= HandleDestroyed;
        _poi.OnSpawnPointRespawned -= HandleRespawned;
    }

    private void Update()
    {
        if (!_poi.IsRespawning) return;
        ApplyProgress(_poi.RechargeProgress);   // scaled time via POI — freezes with pause/Setsuna (R10)
    }

    // ── State transitions ──────────────────────────────────
    private void HandleDestroyed(SpawnPointPOI _)
    {
        if (_portal != null) _portal.SetActive(false);
        if (_rechargeVfx != null) { _rechargeVfx.gameObject.SetActive(true); _rechargeVfx.Play(); }
        if (_disableHusk != null) { _disableHusk.gameObject.SetActive(true); _disableHusk.localScale = _huskBaseScale; }
        ApplyProgress(0f);
    }

    private void HandleRespawned(SpawnPointPOI _) => ApplyReady();

    private void ApplyReady()
    {
        if (_portal != null) _portal.SetActive(true);
        if (_rechargeVfx != null) { _rechargeVfx.Stop(); _rechargeVfx.gameObject.SetActive(false); }
        if (_disableHusk != null) _disableHusk.gameObject.SetActive(false);
        SetIntensity(1f);   // full authored intensity
    }

    // ── Per-frame ramp (t = 0 down → 1 ready) ──────────────
    private void ApplyProgress(float t)
    {
        if (_rechargeVfx != null)
        {
            _rechargeVfx.SetInt(SpawnRateID, Mathf.RoundToInt(Mathf.Lerp(_spawnRate.x, _spawnRate.y, t)));
            _rechargeVfx.SetFloat(ConvergeSpeedID, Mathf.Lerp(_convergeSpeed.x, _convergeSpeed.y, t));
            _rechargeVfx.SetFloat(SpawnRadiusID, Mathf.Lerp(_spawnRadius.x, _spawnRadius.y, t));
        }
        if (_disableHusk != null)
        {
            float shrink = _huskShrinkStart < 1f ? Mathf.InverseLerp(_huskShrinkStart, 1f, t) : 0f;
            _disableHusk.localScale = _huskBaseScale * (1f - Mathf.SmoothStep(0f, 1f, shrink));
        }
        SetIntensity(t);   // 0 → authored
    }

    private void SetIntensity(float t)
    {
        if (_renderers == null) return;
        t = Mathf.Clamp01(t);
        for (int i = 0; i < _renderers.Length; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(VertexIntensityID, _baseIntensity[i] * t);
            r.SetPropertyBlock(_mpb);
        }
    }
}
