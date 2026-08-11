using System;
using System.Collections;
using UnityEngine;

public class ChainProjectile : MonoBehaviour, ISpawnPoolable
{
    // ── ISpawnPoolable (P16 — pooled via GameplayPool.Projectiles) ─────────────
    public void OnSpawned(GameplayPool pool) { }
    public void OnDespawned()
    {
        // Precise despawn reset — a reused chain must be indistinguishable from a fresh one.
        ReleasePlayer();                       // drops the caught twin + stops the drag cue (idempotent)
        StopChainCue(ref _markerHandle);
        if (_beamDriver != null) _beamDriver.HideImmediate();
        _glowDriver?.StopGlow();               // never re-enter the pool glowing
        // Clear stale subscribers — the old thrower's handlers must never fire for the next thrower.
        OnChainConnected = null;
        OnChainMissed = null;
        OnChainBroken = null;
        _connected = false;
        _resolved = false;
        _mashCount = 0;
        _specificTarget = null;
        _tetherBreakerTransform = null;
        // Coroutines are stopped by the pool.
    }

    /// <summary>The chain's cue ids, all played from ONE book (the caster's). TetherBreaker fills these from
    /// its EnemyVfxLibrary book; a future thrower (a redesigned siphon-ghost) fills its own ids. An empty
    /// book or id is a no-op, so the chain still runs headless (legacy path).</summary>
    [Serializable]
    public struct ChainCueSet
    {
        public CueBookData book;
        public string marker;   // ground telegraph at the target — reveals 0→1, then the chain throws
        public string clank;    // random metallic accents along the chain during travel + drag
        public string miss;     // the chain fell short
        public string grab;     // a twin was caught (one-shot on the twin)
        public string drag;     // held on the caught twin while it is dragged
        public string snap;     // OPTIONAL break accent (the bind-ghost's on_chainbreak); TetherBreaker leaves it empty
    }

    [Header("Config — overridden by TetherBreakerEnemyData at runtime")]
    [SerializeField] private float _travelTime = 1.25f;
    [SerializeField] private float _hitRadius = 0.55f;
    [SerializeField] private int _mashThreshold = 8;
    [SerializeField] private LayerMask _playerLayer;

    [Header("VFX")]
    [Tooltip("Legacy fallback 2-point line. Used only when _beamDriver is not wired.")]
    [SerializeField] private LineRenderer _chainLine;
    [Tooltip("Mesh-free chain visual (multi-point line + sparks). Preferred; null = legacy _chainLine.")]
    [SerializeField] private ChainBeamDriver _beamDriver;
    [Tooltip("Glow stream stretched player→TetherBreaker while a twin is grabbed (ChainGlowFx child). " +
             "Null = auto-resolved from children; absent = no glow (fail-safe).")]
    [SerializeField] private ChainGlowDriver _glowDriver;

    [Header("Throw feel (Roadhog model — 2026-07-10)")]
    [Tooltip("Normalized time → normalized distance for the throw. Default: ~85% of the distance " +
             "covered by 60% of the time, then a readable deceleration into the landing spot.")]
    [SerializeField] private AnimationCurve _travelCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.2f),
        new Keyframe(0.6f, 0.85f, 0.55f, 0.55f),
        new Keyframe(1f, 1f, 0.25f, 0f));
    [Tooltip("Readiness pose: during the marker windup the chain hangs partially extended toward " +
             "the target (m). 0 = old behavior (chain appears only at the throw).")]
    [SerializeField] private float _windupReach = 0.75f;
    [Tooltip("Lateral bow at launch (m) — the chain leaves the hand curved and straightens as it " +
             "stretches to full length. 0 = dead-straight flight.")]
    [SerializeField] private float _launchBow = 1.1f;

    [Header("Chain cue cadence")]
    [Tooltip("Random seconds between 'clank' accents along the chain during travel + drag.")]
    [SerializeField] private float _clankIntervalMin = 0.15f;
    [SerializeField] private float _clankIntervalMax = 0.4f;

    public event Action<Player> OnChainConnected;
    public event Action OnChainMissed;
    public event Action OnChainBroken;

    private Vector3 _targetPosition;
    private Transform _tetherBreakerTransform;
    private Player _connectedPlayer;
    private int _mashCount;
    private bool _connected;
    private bool _resolved;
    private float _pullDuration = 1.2f; // reel-back window on miss (driven by ChainBeamDriver)

    // ── Cues (played from the caster's book) ───────────────────
    private ChainCueSet _cues;
    private float _markerWindup;        // telegraph time before the throw (marker reveals 0→1 over it)
    private CueHandle _markerHandle;    // held target telegraph, stopped when the chain lands
    private CueHandle _dragHandle;      // held "being dragged" cue on the caught twin
    private float _nextClankTime;
    private Player _specificTarget;     // set = catch ONLY this exact target (the bind-ghost's soul), never an overlap

    // ── Launch with SO data override ──────────────────────────
    public void Launch(
        Vector3 targetPosition,
        Transform casterTransform,
        LayerMask playerLayer,
        float travelTime = -1f,
        float hitRadius = -1f,
        int mashThreshold = -1,
        float pullDuration = -1f,
        ChainCueSet cues = default,
        float markerWindup = 0f,
        Player specificTarget = null)
    {
        _targetPosition = targetPosition;
        _tetherBreakerTransform = casterTransform;
        _playerLayer = playerLayer;
        _cues = cues;
        _markerWindup = Mathf.Max(0f, markerWindup);
        _specificTarget = specificTarget;   // null = OverlapSphere any twin; set = catch only this exact target

        // Override with SO values if provided
        if (travelTime > 0f) _travelTime = travelTime;
        if (hitRadius > 0f) _hitRadius = hitRadius;
        if (mashThreshold > 0) _mashThreshold = mashThreshold;
        if (pullDuration > 0f) _pullDuration = pullDuration;

        // Telegraph: the ground marker appears at the landing spot and reveals 0→1 over the windup (its own
        // MaterialRevealDriver drives that). It REPLACES the old reticle GameObject. When the windup ends the
        // chain throws; the marker is stopped when the chain lands. The marker is a FLAT GROUND decal, so it is
        // dropped onto the floor beneath the landing point (BUG-086) — the raw target is the twin's pivot
        // (mid-body), which floats the disc.
        _markerHandle = PlayChainCue(_cues.marker, new CueContext(GroundUnder(_targetPosition)));

        // Sync the marker's reveal to THIS throw's windup: the material float runs 0→1 over exactly
        // the telegraph window, so "fully revealed" = "the chain is coming NOW". Falls back to the
        // driver's own authored duration when the cue has no reveal driver (fail-safe no-op).
        if (_markerWindup > 0f)
            FxManager.Instance?.FindOnInstance<MaterialRevealDriver>(_markerHandle)
                     ?.Reveal(0f, 1f, _markerWindup);

        StartCoroutine(WindupThenTravel());
    }

    // Drop a world point onto the ground beneath it for a flat ground decal (the chain landing marker). NavMesh
    // first — the twins/enemies walk it, so its surface IS the floor, and (unlike a downward raycast) it can't be
    // occluded by the target twin standing on the exact spot. A player-excluded downward raycast is the
    // off-navmesh fallback; the raw point is the last resort. Only Y changes — the marker stays directly under
    // the landing point.
    private Vector3 GroundUnder(Vector3 pos)
    {
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var navHit, 3f, UnityEngine.AI.NavMesh.AllAreas))
            return new Vector3(pos.x, navHit.position.y, pos.z);
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f,
                Physics.DefaultRaycastLayers & ~_playerLayer.value, QueryTriggerInteraction.Ignore))
            return new Vector3(pos.x, hit.point.y, pos.z);
        return pos;
    }

    public void NotifyMash()
    {
        if (!_connected) return;
        _mashCount++;
        if (_mashCount >= _mashThreshold)
            BreakChain();
    }

    public void ForceDisconnect()
    {
        if (_resolved) return;
        _resolved = true;
        ReleasePlayer();
        StopChainCue(ref _markerHandle);
        if (_beamDriver != null) _beamDriver.HideImmediate();
        GameplayPool.Despawn(gameObject);
    }

    /// <summary>Externally trigger the break/snap — for an owner that drives its OWN mash (the bind-ghost).
    /// Plays the snap cue + the driver recoil, then destroys. Idempotent (no-op once resolved).</summary>
    public void Snap() => BreakChain();

    // Readiness pose while the marker reveals: the chain hangs partially extended toward the target
    // (the hook-anticipation read); the throw fires when the reveal (windup) completes. (R10: scaled —
    // gameplay telegraph.)
    private IEnumerator WindupThenTravel()
    {
        if (_markerWindup > 0f)
        {
            Vector3 handStart = transform.position;
            Vector3 dir = (_targetPosition - handStart).normalized;
            float elapsed = 0f;
            while (elapsed < _markerWindup)
            {
                elapsed += Time.deltaTime;
                // Ease out to the readiness reach over the first ~0.25s, then hold it, sagging.
                float k = _windupReach > 0f ? Mathf.Min(1f, elapsed / 0.25f) : 0f;
                transform.position = handStart + dir * (_windupReach * k);
                UpdateChainLine();
                yield return null;
            }
        }
        yield return StartCoroutine(TravelRoutine());
    }

    private IEnumerator TravelRoutine()
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;
        ScheduleNextClank();

        // Launch bow: the tip leaves the hand offset sideways and the offset dies as the chain
        // straightens to full stretch (× (1−distance)). Horizontal perpendicular; random side.
        Vector3 side = Vector3.Cross((_targetPosition - startPos).normalized, Vector3.up);
        if (side.sqrMagnitude < 1e-4f) side = Vector3.right;   // near-vertical throw — any side works
        side = side.normalized * (UnityEngine.Random.value < 0.5f ? -_launchBow : _launchBow);

        while (elapsed < _travelTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _travelTime);
            float d = Mathf.Clamp01(_travelCurve.Evaluate(t));   // fast out, decelerating arrival
            Vector3 pos = Vector3.Lerp(startPos, _targetPosition, d);
            pos += side * (Mathf.Sin(Mathf.PI * t) * (1f - d));
            transform.position = pos;
            UpdateChainLine();
            TryClank();
            yield return null;
        }

        transform.position = _targetPosition;
        StopChainCue(ref _markerHandle);   // the chain has arrived — the telegraph is done
        CheckConnection();
    }

    private void CheckConnection()
    {
        if (_specificTarget != null)
        {
            // Catch ONLY this exact target (the bind-ghost's soul) by proximity — never anything else, no layer guess.
            if (!_specificTarget.IsGrabbed &&
                Vector3.Distance(_targetPosition, _specificTarget.transform.position) <= _hitRadius)
            {
                Connect(_specificTarget);
                return;
            }
        }
        else
        {
            // Twin catch: any grabbable twin at the landing spot, never the soul.
            var hits = Physics.OverlapSphere(_targetPosition, _hitRadius, _playerLayer);
            foreach (var hit in hits)
            {
                var player = hit.GetComponent<Player>() ?? hit.GetComponentInParent<Player>();
                if (player == null || player is SoulPlayer) continue;
                if (player.IsGrabbed) continue;

                Connect(player);
                return;
            }
        }

        // Missed — the chain fell short at the landing spot.
        PlayChainCue(_cues.miss, new CueContext(_targetPosition));
        OnChainMissed?.Invoke();
        StartCoroutine(MissReelRoutine());
    }

    // On miss: lay the chain on the ground, then reel it back to the hand over the pull window,
    // then despawn. Aligned to the enemy nulling _activeChain at _pullDuration. (R10: scaled time.)
    private IEnumerator MissReelRoutine()
    {
        if (_beamDriver != null)
        {
            Vector3 hand = _tetherBreakerTransform != null
                ? _tetherBreakerTransform.position + Vector3.up * 0.5f
                : transform.position + Vector3.up * 0.5f;
            Vector3 fwd = _targetPosition - hand; fwd.y = 0f;
            _beamDriver.SetGroundedMiss(hand, fwd, _targetPosition);
            _beamDriver.Retract(_pullDuration);
            yield return new WaitForSeconds(_pullDuration);
            _beamDriver.HideImmediate();
        }
        else
        {
            yield return new WaitForSeconds(0.5f); // legacy fallback
        }
        GameplayPool.Despawn(gameObject);
    }

    private void Connect(Player player)
    {
        _connected = true;
        _connectedPlayer = player;
        _mashCount = 0;
        // Caught: a one-shot grab burst on the twin.
        PlayChainCue(_cues.grab, CueContext.Follow(player.transform));

        // BUG-087 — the "whole chain length" drag VFX (On_TetherChainDrag / ChainDrag.prefab) is REMOVED for the
        // playtest. A single particle prefab Follow-attached to the dragged twin cannot render ALONG the live
        // chain span: the span length changes every frame as the twin is hauled in, so a fixed-size prefab riding
        // the player read as a broken clump instead of an effect running down the chain. The working per-frame
        // span visual during a drag is the ChainGlowDriver stream (UpdateChainLine → SetSpan stretches the emitter
        // Z hand↔twin each frame) — that stays and is unaffected.
        // WHERE IT GOES NEXT TIME: rebuild the full-length drag effect as a span-stretched DRIVER (scale a
        // LineRenderer / emitter Z to the live endpoint distance, like ChainGlowDriver/ChainBeamDriver already do),
        // NOT as a static Follow cue. When that driver exists, restore the held handle right here:
        //   _dragHandle = PlayChainCue(_cues.drag, CueContext.Follow(player.transform));
        // The _cues.drag id is still wired in the TetherBreaker book, so it survives for the redo; ReleasePlayer's
        // StopChainCue(ref _dragHandle) is a safe no-op while the handle stays None.
        OnChainConnected?.Invoke(player);
        StartCoroutine(FollowPlayer());
    }

    private IEnumerator FollowPlayer()
    {
        while (_connected && _connectedPlayer != null)
        {
            transform.position = _connectedPlayer.transform.position;
            UpdateChainLine();
            TryClank();
            yield return null;
        }
    }

    private void BreakChain()
    {
        if (_resolved) return;
        _resolved = true;
        _connected = false;
        Vector3 snapAt = transform.position;   // the chain GO rides the caught target during the bind
        ReleasePlayer();
        // Optional snap accent (the bind-ghost's on_chainbreak); TetherBreaker leaves it empty — its snap is the driver.
        PlayChainCue(_cues.snap, new CueContext(snapAt));
        // Snap effect: sparks + recoil whip over the break window, then the driver self-hides.
        const float breakFx = 0.22f;
        if (_beamDriver != null) _beamDriver.Break(breakFx);
        OnChainBroken?.Invoke();
        GameplayPool.Despawn(gameObject, breakFx + 0.05f);   // let the snap finish before the GO returns
    }

    private void ReleasePlayer()
    {
        StopChainCue(ref _dragHandle);   // stop the held drag cue on every release path (break / force / death)
        if (_connectedPlayer != null)
        {
            _connectedPlayer.SetGrabbed(false);
            _connectedPlayer = null;
        }
        _connected = false;
        // Beam hide/break is owned by the caller (BreakChain plays the snap; ForceDisconnect/MissReel hide).
        if (_chainLine != null) _chainLine.enabled = false;
    }

    // ── Chain cue helpers (play the caster's book by id; no-op if the book/id is unset) ──
    private CueHandle PlayChainCue(string id, in CueContext ctx)
    {
        if (_cues.book == null || string.IsNullOrEmpty(id)) return CueHandle.None;
        return FxManager.Instance?.PlayBook(_cues.book, id, ctx) ?? CueHandle.None;
    }

    private void StopChainCue(ref CueHandle handle)
    {
        if (handle.IsNone) return;
        FxManager.Instance?.Stop(handle);
        handle = CueHandle.None;
    }

    private void ScheduleNextClank()
        => _nextClankTime = Time.time + UnityEngine.Random.Range(_clankIntervalMin, _clankIntervalMax);

    // A short metallic accent at a random point along the current chain span (hand → live end).
    private void TryClank()
    {
        if (_cues.book == null || string.IsNullOrEmpty(_cues.clank)) return;
        if (Time.time < _nextClankTime) return;
        ScheduleNextClank();

        Vector3 hand = _tetherBreakerTransform != null
            ? _tetherBreakerTransform.position + Vector3.up * 0.5f
            : transform.position + Vector3.up * 0.5f;
        Vector3 end = _connectedPlayer != null
            ? _connectedPlayer.transform.position + Vector3.up * 0.5f
            : transform.position + Vector3.up * 0.5f;
        Vector3 point = Vector3.Lerp(hand, end, UnityEngine.Random.value);
        PlayChainCue(_cues.clank, new CueContext(point));
    }

    private void UpdateChainLine()
    {
        if (_tetherBreakerTransform == null) return;

        Vector3 hand = _tetherBreakerTransform.position + Vector3.up * 0.5f;
        // During travel use chain GO position; after connect use player position.
        Vector3 endPoint = _connectedPlayer != null
            ? _connectedPlayer.transform.position + Vector3.up * 0.5f
            : transform.position + Vector3.up * 0.5f;

        // Grab glow: a stream flowing TetherBreaker -> player, stretched to the live span (only while
        // grabbed). Direction flipped 2026-07-10 — user playtest read the player->enemy flow as backwards.
        if (_glowDriver == null) _glowDriver = GetComponentInChildren<ChainGlowDriver>(true);
        if (_glowDriver != null)
        {
            if (_connected) _glowDriver.SetSpan(hand, endPoint);
            else _glowDriver.StopGlow();
        }

        if (_beamDriver != null)
        {
            // Taut & wobbling; high tension while a twin is grabbed and being dragged.
            float tension = _connected ? 0.9f : 0.15f;
            _beamDriver.SetBeam(hand, endPoint, tension);
            return;
        }

        // Legacy fallback (2-point line) until a ChainBeamDriver is wired on the prefab.
        if (_chainLine == null) return;
        _chainLine.enabled = true;
        _chainLine.SetPosition(0, hand);
        _chainLine.SetPosition(1, endPoint);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _hitRadius);
    }
}
