using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The POI energy feed. Sits NEXT TO a POIBase (ritual site / spawn point / barrier) and, at each
/// eligible enemy's own interval, feeds it a small amount of dark energy + health and plays the
/// feed-stream cue from the POI toward that enemy. Per-POI amounts/cadence come from the
/// PoiEnergyProfile asset; the dark-energy threshold buff itself lives on EnemyDarkEnergy.
///
/// Feeding is for enemies that are NOT engaging: it pauses while the enemy has a target, is
/// stunned/possessed/feared/grabbed, or is brain-held (freeze service / QTE) — Enemy.IsEngaged.
///
/// Registry (R5): emitters self-register in a static list so SeekEnergy AI can find the nearest
/// feed site without scene scans. Scene objects unregister OnDisable — nothing survives unload.
/// </summary>
[RequireComponent(typeof(POIBase))]
public class PoiEnergyEmitter : MonoBehaviour
{
    private static readonly List<PoiEnergyEmitter> _active = new List<PoiEnergyEmitter>();

    [SerializeField] private PoiEnergyProfile _profile;

    // Feed stream cue — always the Common book (CommonFx, zero wiring). Author a "poi_feed" id there.
    private const string PoiFeedCueId = "poi_feed";

    [Tooltip("Layers scanned for enemies (the enemy layer).")]
    [SerializeField] private LayerMask _enemyLayer;

    private POIBase _poi;
    private float _scanTimer;                                   // scaled — feeding is gameplay
    private readonly Collider[] _hits = new Collider[16];
    private readonly Dictionary<Enemy, float> _nextFeedTime = new Dictionary<Enemy, float>();
    private static readonly List<Enemy> _purge = new List<Enemy>();
    private const float ScanInterval = 1f;

    public float FeedRadius =>
        _profile != null && _profile.feedRadius > 0f ? _profile.feedRadius
        : _poi != null ? _poi.InfluenceRadius : 8f;

    /// <summary>Nearest active feed site, or null. Used by the SeekEnergy AI.</summary>
    public static PoiEnergyEmitter FindNearest(Vector3 from)
    {
        PoiEnergyEmitter best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < _active.Count; i++)
        {
            var e = _active[i];
            if (e == null || !e.isActiveAndEnabled) continue;
            float d = (e.transform.position - from).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = e; }
        }
        return best;
    }

    private void Awake()
    {
        _poi = GetComponent<POIBase>();
        if (_enemyLayer.value == 0) _enemyLayer = LayerMask.GetMask("Enemy");   // zero-config default
        if (_profile == null)
        {
            Debug.LogError($"[PoiEnergyEmitter] {name} — no PoiEnergyProfile assigned; feeding disabled.", this);
            enabled = false;
        }
    }

    private void OnEnable()  { if (!_active.Contains(this)) _active.Add(this); }
    private void OnDisable() { _active.Remove(this); _nextFeedTime.Clear(); }

    private void Update()
    {
        if (_poi == null || !_poi.IsActive) return;

        _scanTimer += Time.deltaTime;   // scaled — feeding slows under Setsuna with the rest of gameplay
        if (_scanTimer < ScanInterval) return;
        _scanTimer = 0f;

        float radius = FeedRadius;
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, _hits, _enemyLayer);
        for (int i = 0; i < count; i++)
        {
            var enemy = _hits[i].GetComponentInParent<Enemy>();
            if (enemy == null) continue;
            TryFeed(enemy);
        }

        PurgeStale(radius);
    }

    private void TryFeed(Enemy enemy)
    {
        if (enemy.Health == null || enemy.Health.IsDead) return;
        if (enemy.Health.CurrentHealth >= enemy.Health.MaxHealth * _profile.healthThresholdPct) return;
        if (enemy.IsEngaged) return;   // "mostly when they are not engaging" — combat/abilities/grab/QTE pause it

        if (_nextFeedTime.TryGetValue(enemy, out float next) && Time.time < next) return;

        var darkEnergy = enemy.GetComponent<EnemyDarkEnergy>();
        darkEnergy?.AddEnergy(_profile.energyPerFeed);
        enemy.Health.Heal(_profile.healthPerFeed);

        // Stream cue: fired at the POI, oriented toward the enemy being fed (Common book, no wiring).
        {
            Vector3 dir = enemy.transform.position - transform.position;
            dir.y = 0f;
            var rot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            CommonFx.Play(PoiFeedCueId, new CueContext(transform.position, rot));
        }

        // Corrupted enemies drink faster — interval drops past the dark-energy threshold.
        float interval = darkEnergy != null && darkEnergy.CurrentEnergy >= _profile.fastIntervalEnergyThreshold
            ? _profile.fastFeedInterval
            : _profile.feedInterval;
        _nextFeedTime[enemy] = Time.time + interval;
    }

    // Null-purge before the dictionary grows (dead/despawned/left-range enemies).
    private void PurgeStale(float radius)
    {
        if (_nextFeedTime.Count == 0) return;
        float sqr = (radius * 2f) * (radius * 2f);
        _purge.Clear();
        foreach (var kv in _nextFeedTime)
        {
            var e = kv.Key;
            if (e == null || !e.gameObject.activeInHierarchy || e.Health == null || e.Health.IsDead
                || (e.transform.position - transform.position).sqrMagnitude > sqr)
                _purge.Add(e);
        }
        for (int i = 0; i < _purge.Count; i++) _nextFeedTime.Remove(_purge[i]);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.6f, 0.5f);
        float r = _profile != null && _profile.feedRadius > 0f ? _profile.feedRadius
                : TryGetComponent<POIBase>(out var poi) ? poi.InfluenceRadius : 8f;
        Gizmos.DrawWireSphere(transform.position, r);
    }
}
