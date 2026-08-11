using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Persistent singleton — the ONE entry point for playing any cue: particle, VFX graph, sound,
/// camera shake, or a composed sequence. Owns the VFX pool (FxPoolRoot) and
/// every live instance through a version-stamped registry, so a stale <c>CueHandle</c> is inert.
///
/// R3: scene-resident singleton — duplicate-destroy Awake guard, <c>Instance</c> nulled in OnDestroy,
/// NO DontDestroyOnLoad. R8: named SceneFlowManager handler, unsubscribed in OnDestroy.
///
/// Sound and camera-shake are deliberately fail-LOUD stubs until P9.2/the impulse pass — FxManager
/// must never play audio directly (Banned Lazy Work #10), it routes to AudioManager when that exists.
/// </summary>
public class FxManager : MonoBehaviour
{
    public static FxManager Instance { get; private set; }

    /// <summary>P19 seam 3 — hit-stop. The stop itself is a GAME service (HitStopService, a
    /// TimeScaleService owner) this package cannot reference; the game registers its handler at boot
    /// (HitStopService.Awake) and clears it on destroy. Unregistered = cue hit-stop silently no-ops
    /// (an empty URP project still compiles the package). Args: (timeScale, unscaledDuration).</summary>
    public static System.Action<float, float> HitStopHook;

    [Tooltip("Pooled instances live under here. Auto-created as a child if left empty.")]
    [SerializeField] private Transform fxPoolRoot;

    [Tooltip("The scene streamer, as IFxSceneEvents (P19 seam 1 — drag SceneFlowManager here; R1 same-scene). " +
             "Unwired = F1 unload reclaim disabled (warned).")]
    [UnityEngine.Serialization.FormerlySerializedAs("sceneFlow")]
    [SerializeField] private MonoBehaviour sceneFlowMono;   // → IFxSceneEvents
    private IFxSceneEvents _sceneEvents;

    private VfxPool _pool;

    // ── live instances ──────────────────────────────────────────────────────────
    private abstract class ActiveCue
    {
        public object owner;
        public Transform followTarget;   // for StopAllOn / F1 scene checks; null = world

        /// <summary>Per-frame update. Returns true when the cue has finished and should be reclaimed.</summary>
        public abstract bool Update(float scaledDt, float unscaledDt);

        /// <summary>Graceful stop + release of any pooled resources.</summary>
        public abstract void Stop(FxManager mgr);
    }

 // One sound authored ON a Cue Book element, scheduled to start relative to the element's fire.
    private struct PendingAudio
    {
        public SoundCueData sound;
        public bool loop;
        public bool killWithVisual;
        public float startAt;             // in the owning element's time domain
    }

    private sealed class ActiveFx : ActiveCue
    {
        public GameObject prefabKey;
        public GameObject instance;       // null = pure-audio element (no visual)
        public Transform tr;
        public ParticleSystem ps;
        public VisualEffect vfx;
        public FxAttachMode attachMode;
        public Transform faceTarget;       // orientation anchor (null = keep the position anchor's own rotation)
        public Vector3 localOffset;
        public Quaternion localRotation;   // authored rotation offset (identity = none)
        public Vector3 localScale;         // authored per-element scale multiplier (one = prefab size)
        public TimeMode timeMode;
        public float expireAt;            // in the matching time domain; +Inf = held until Stop
        public bool layerStamped;         // drawOnTop moved this instance to GroundVFX — restore on return

        // Per-element audio: sounds authored on this element, started when due and — when killWithVisual —
        // stopped with the visual. liveAudio holds the AudioManager handles still to stop.
        public List<PendingAudio> pendingAudio;
        public List<CueHandle> liveAudio;
        public Vector3 audioPos;          // world fallback when not following

        public override bool Update(float sdt, float udt)
        {
            if (pendingAudio != null && pendingAudio.Count > 0) StartDueAudio();

            bool following = attachMode != FxAttachMode.World;
            if (following && followTarget == null)
            {
                if (attachMode == FxAttachMode.FollowDetachOnTargetDeath)
                {
                    attachMode = FxAttachMode.World;           // freeze in place, let it finish
                    following = false;
                }
                else
                    return true;                                // hard-follow + target gone → reclaim
            }

            // Position: ride the POSITION anchor (followTarget) when following, else stay put (world/detached).
            // Orientation: aim at the ORIENTATION anchor (faceTarget) when set, else keep the anchor's rotation.
            // The two are independent — "ride A, face B" (drain), "stand still, face B" (gaze), or plain follow.
            if (tr != null && (following || faceTarget != null))
            {
                // FollowPositionOnly: world-axis offset, never the target's rotation (BUG-060/061).
                bool posOnly = attachMode == FxAttachMode.FollowPositionOnly;
                Vector3 pos = following
                    ? (posOnly ? followTarget.position + localOffset
                               : followTarget.position + followTarget.rotation * localOffset)
                    : tr.position;                             // world/detached: keep current spawn position
                Quaternion rot;
                if (faceTarget != null)
                {
                    Vector3 dir = faceTarget.position - pos;
                    rot = (dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : tr.rotation) * localRotation;
                }
                else if (posOnly)
                    rot = tr.rotation;                             // keep the spawn orientation — no target spin
                else
                    rot = followTarget.rotation * localRotation;   // authored offset, relative to the target
                tr.SetPositionAndRotation(pos, rot);
            }

            float now = timeMode == TimeMode.Unscaled ? Time.unscaledTime : Time.time;
            return now >= expireAt;
        }

        private void StartDueAudio()
        {
            float now = timeMode == TimeMode.Unscaled ? Time.unscaledTime : Time.time;
            var am = AudioManager.Instance;
            for (int i = pendingAudio.Count - 1; i >= 0; i--)
            {
                var pa = pendingAudio[i];
                if (now < pa.startAt) continue;
                pendingAudio.RemoveAt(i);
                if (am == null || pa.sound == null) continue;
                CueHandle h = followTarget != null ? am.Play(pa.sound, followTarget, pa.loop)
                                                   : am.Play(pa.sound, audioPos, pa.loop);
                if (pa.killWithVisual && !h.IsNone) (liveAudio ??= new List<CueHandle>()).Add(h);
            }
        }

        public override void Stop(FxManager mgr)
        {
            if (liveAudio != null)
            {
                var am = AudioManager.Instance;
                if (am != null) for (int i = 0; i < liveAudio.Count; i++) am.Stop(liveAudio[i]);
                liveAudio.Clear();
            }
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (vfx != null) vfx.Stop();
            if (instance != null)
            {
                // Pooled reuse contract: a drawOnTop stamp must not leak onto the next (unflagged) cue.
                if (layerStamped) RestoreLayersFrom(prefabKey.transform, instance.transform);
                mgr._pool.Return(prefabKey, instance);
            }
        }
    }

    private sealed class ActiveBook : ActiveCue
    {
        public CueBookRunner runner;
        public TimeMode timeMode;

        public override bool Update(float sdt, float udt)
        {
            runner.Tick(timeMode == TimeMode.Unscaled ? udt : sdt);
            return runner.IsFinished;
        }

        // A gameplay Stop of the whole book halts every still-running (held/looping) element, so a
        // Loop/Pull book stopped by gameplay never leaks a held instance (the trap-reset contract).
        public override void Stop(FxManager mgr) => runner?.StopAll();
    }

    private readonly CueInstanceRegistry<ActiveCue> _registry = new CueInstanceRegistry<ActiveCue>();
    private readonly List<KeyValuePair<CueHandle, ActiveCue>> _scratch = new List<KeyValuePair<CueHandle, ActiveCue>>();
    private readonly List<CueHandle> _reclaim = new List<CueHandle>();
    private readonly Dictionary<ParticleSystem, float> _lifetimeCache = new Dictionary<ParticleSystem, float>();

    // ── lifecycle ───────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }   // R3 duplicate guard
        Instance = this;

        if (fxPoolRoot == null)
        {
            var root = new GameObject("FxPoolRoot");
            root.transform.SetParent(transform, false);
            fxPoolRoot = root.transform;
        }
        _pool = new VfxPool(fxPoolRoot);
    }

    private void Start()
    {
        _sceneEvents = sceneFlowMono as IFxSceneEvents;                 // R1 slot → package interface (P19 seam 1)
        if (_sceneEvents != null)
            _sceneEvents.OnSceneWillUnload += HandleSceneWillUnload;    // R8 named handler
        else
            Debug.LogWarning("[FxManager] Scene-events slot unwired (needs an IFxSceneEvents, e.g. SceneFlowManager) — F1 unload reclaim is disabled.", this);
    }

    private void OnDestroy()
    {
        if (_sceneEvents != null) _sceneEvents.OnSceneWillUnload -= HandleSceneWillUnload;
        if (Instance == this) Instance = null;                          // R3: clear stale static on Restart
    }

    // ── public API ──────────────────────────────────────────────────────────────
    /// <summary>Play a single low-level cue — currently a <c>SoundCueData</c>. Visual effects go through
    /// <see cref="PlayBook"/> (a Cue Book) or <see cref="PlayParticle"/> (a raw prefab). Camera feel (FOV
    /// punch / shake / depth) rides on a cue element's <c>CameraCue</c> block → <see cref="CameraCueDriver"/>.</summary>
    public CueHandle Play(CueData cue, in CueContext ctx)
    {
        if (cue == null) { Debug.LogError("[FxManager] Play called with a null cue.", this); return CueHandle.None; }
        switch (cue)
        {
            case SoundCueData s:         return PlaySound(s, ctx);
            default:
                Debug.LogError($"[FxManager] No handler for cue type {cue.GetType().Name} ('{cue.name}').", cue);
                return CueHandle.None;
        }
    }

    /// <summary>Play a single pooled <c>ParticleSystem</c> prefab directly (no Cue Book) — for a system that
    /// owns exactly one effect, e.g. the Manpu glyph burst. A looping prefab is held until <see cref="Stop"/>.</summary>
    public CueHandle PlayParticle(ParticleSystem prefab, in CueContext ctx)
        => prefab != null
            ? SpawnParticle(prefab, FxAttachMode.FromPrefab, Vector3.zero, TimeMode.FromPrefab, 0f, ctx)
            : CueHandle.None;

    /// <summary>Play the effect named <paramref name="id"/> out of a Cue Book — the container is addressed by
 /// the string id, never "play the whole book". The effect's elements fire together, each
    /// with its own delay / loop-or-once / cut and its own per-element audio. Returns a handle to Stop/query
    /// the effect, or <c>CueHandle.None</c> if it has no live/held content. A wrong id LogWarns (the
    /// per-call-site verifier catches typos at author time).</summary>
    public CueHandle PlayBook(CueBookData book, string id, in CueContext ctx)
    {
        if (book == null) { Debug.LogError("[FxManager] PlayBook called with a null book.", this); return CueHandle.None; }
        var elements = book.GetElements(id);
        if (elements == null)
        {
            Debug.LogWarning($"[FxManager] Cue Book '{book.name}' has no effect id '{id}'.", book);
            return CueHandle.None;
        }
        if (elements.Count == 0) return CueHandle.None;   // empty effect — not an error

        var runner = new CueBookRunner(PlayElement, h => Stop(h));
        var ab = new ActiveBook
        {
            // The book sequences (delays/cuts) on this clock; each element's own timeMode runs its instance.
            timeMode = book.timeMode == TimeMode.Unscaled ? TimeMode.Unscaled : TimeMode.Scaled,
            runner = runner,
            owner = ctx.owner,
            followTarget = ctx.followTarget,
        };
        runner.Begin(elements, ctx);
        if (runner.IsFinished) return CueHandle.None;   // all-immediate, nothing held → already done
        return _registry.Register(ab);
    }

    /// <summary>Stop a specific cue. No-op (returns false) for a stale/finished handle.</summary>
    public bool Stop(CueHandle handle)
    {
        if (!_registry.TryGet(handle, out var ac)) return false;
        var owner = ac.owner;
        // A throw mid-stop (destroyed instance, dead runner) must still reclaim the registry slot —
        // and must NOT propagate into the caller (a pool Return aborted halfway left live "stuck"
        // arrows with their cues still following them — BUG-056).
        try { ac.Stop(this); }
        catch (System.Exception ex) { Debug.LogException(ex, this); }
        finally { _registry.Reclaim(handle); }
        // Release any held camera feel this owner started (restores FOV→100% / depth→0 over blendOut).
        if (owner != null) CameraCueDriver.Instance?.Release(owner);
        return true;
    }

    public bool IsPlaying(CueHandle handle) => _registry.IsValid(handle);

    /// <summary>Find a component on the live visual instance(s) behind a handle — for a caller that must
    /// drive a per-instance script in sync with its own gameplay timing (the chain marker's
    /// MaterialRevealDriver revealing over the throw windup). Walks book-element instances recursively.
    /// Null for stale handles and pure-audio cues.</summary>
    public T FindOnInstance<T>(CueHandle handle) where T : Component
    {
        if (!_registry.TryGet(handle, out var ac)) return null;
        if (ac is ActiveFx af)
            return af.instance != null ? af.instance.GetComponentInChildren<T>(true) : null;
        if (ac is ActiveBook ab && ab.runner != null)
        {
            var hs = ab.runner.Handles;
            for (int i = 0; i < hs.Count; i++)
            {
                var found = FindOnInstance<T>(hs[i]);
                if (found != null) return found;
            }
        }
        return null;
    }

    /// <summary>Like <see cref="FindOnInstance{T}"/> but collects EVERY match across all of a book's
    /// element instances — for cues whose elements each carry one driver (the gate travel helix:
    /// orb A and orb B are separate elements, both must be wired). Appends to <paramref name="results"/>.</summary>
    public void FindAllOnInstance<T>(CueHandle handle, System.Collections.Generic.List<T> results) where T : Component
    {
        if (results == null || !_registry.TryGet(handle, out var ac)) return;
        if (ac is ActiveFx af)
        {
            if (af.instance != null)
                results.AddRange(af.instance.GetComponentsInChildren<T>(true));
            return;
        }
        if (ac is ActiveBook ab && ab.runner != null)
        {
            var hs = ab.runner.Handles;
            for (int i = 0; i < hs.Count; i++)
                FindAllOnInstance(hs[i], results);
        }
    }

    /// <summary>Stop every live cue following <paramref name="target"/> or one of its children (F2).</summary>
    public void StopAllOn(Transform target)
    {
        if (target == null) return;
        _registry.GetActive(_scratch);
        foreach (var kv in _scratch)
        {
            var t = kv.Value.followTarget;
            if (t != null && (t == target || t.IsChildOf(target)))
            {
                kv.Value.Stop(this);
                _registry.Reclaim(kv.Key);
            }
        }
    }

    /// <summary>Stop everything (soft reset, F5).</summary>
    public void StopAll()
    {
        _registry.GetActive(_scratch);
        foreach (var kv in _scratch)
        {
            kv.Value.Stop(this);
            _registry.Reclaim(kv.Key);
        }
    }

    // ── drawOnTop layer stamping (GroundVFX) ──────────────────────────────────────
    // Gameplay-critical ground telegraphs (cast circles, meteor decals) must never be hidden by
    // grass/props: a RenderObjects feature draws the GroundVFX layer depth-test-off after opaques.
    // The stamp is applied per spawn and RESTORED on pool return (see ActiveFx.Stop) so a reused
    // instance never leaks the layer onto an unflagged cue.
    private static int _groundVfxLayer = -2;   // -2 = unresolved, -1 = missing (warned)

    private static bool StampGroundVfxLayer(GameObject inst)
    {
        if (_groundVfxLayer == -2)
        {
            _groundVfxLayer = LayerMask.NameToLayer("GroundVFX");
            if (_groundVfxLayer < 0)
                Debug.LogError("[FxManager] drawOnTop needs a 'GroundVFX' layer (Project Settings → Tags and Layers) — flag ignored.");
        }
        if (_groundVfxLayer < 0) return false;
        SetLayerRecursively(inst.transform, _groundVfxLayer);
        return true;
    }

    private static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayerRecursively(t.GetChild(i), layer);
    }

    // Pool clones mirror the prefab hierarchy — copy layers back index-by-index.
    private static void RestoreLayersFrom(Transform prefab, Transform inst)
    {
        inst.gameObject.layer = prefab.gameObject.layer;
        int n = Mathf.Min(prefab.childCount, inst.childCount);
        for (int i = 0; i < n; i++) RestoreLayersFrom(prefab.GetChild(i), inst.GetChild(i));
    }

    // ── per-element spawn (pooled) ─────────────────────────────────────────────────
    /// <summary>Shared particle spawn — used by <see cref="PlayParticle"/> and by an inline CueBook element.
    /// <paramref name="explicitLifetime"/> 0 = read the prefab (a looping system is held until Stop).</summary>
    private CueHandle SpawnParticle(ParticleSystem prefab, FxAttachMode attachMode, Vector3 localOffset,
                                    TimeMode timeMode, float explicitLifetime, in CueContext ctx,
                                    Quaternion localRotation = default, Vector3 localScale = default,
                                    bool drawOnTop = false)
    {
        var key = prefab.gameObject;
        var inst = _pool.Get(key);
        if (inst == null) return CueHandle.None;
        bool stamped = drawOnTop && StampGroundVfxLayer(inst);

        var ps = inst.GetComponent<ParticleSystem>();
        var attach = ResolveAttach(attachMode, ps);   // FromPrefab → read main.simulationSpace
        var tmode = ResolveTimeMode(timeMode, ps);     // FromPrefab → read main.useUnscaledTime

        // Position-only context: Follow without an anchor is meaningless — degrade to World so the cue
        // plays at ctx.position instead of being reclaimed on its first Update (BUG-048: every Local-sim
        // particle played via a position-only ctx died the frame it spawned — the death helix was one).
        // Update()'s null-target check still reclaims cues whose anchor dies LATER.
        if (attach != FxAttachMode.World && ctx.followTarget == null)
            attach = FxAttachMode.World;

        var af = new ActiveFx
        {
            prefabKey = key,
            instance = inst,
            tr = inst.transform,
            ps = ps,
            attachMode = attach,
            localOffset = localOffset,
            localRotation = localRotation == default ? Quaternion.identity : localRotation,
            localScale = localScale == default ? Vector3.one : localScale,
            timeMode = tmode,
            owner = ctx.owner,
            followTarget = attach != FxAttachMode.World ? ctx.followTarget : null,
            faceTarget = ctx.faceTarget,   // orientation anchor is independent of the follow/World decision
            layerStamped = stamped,
        };
        Place(af, ctx);

 // A looping prefab is held until Stop — the prefab is the source of truth.
        // An explicit lifetime override still wins (deliberately auto-stop a loop).
        bool looping = ps != null && ps.main.loop;
        if (looping && explicitLifetime <= 0f)
        {
            af.expireAt = float.PositiveInfinity;
        }
        else
        {
            float life = ResolveLifetimeFor(prefab, explicitLifetime);   // override, or prefab lifetime (sub-emitters)
            float now = tmode == TimeMode.Unscaled ? Time.unscaledTime : Time.time;
            af.expireAt = now + (life > 0f ? life : 0f);
        }

        // Runtime footprint sizing (parameter-driven, NOT transform): scale only CueScalableEmitter-marked systems'
        // startSize + shape radius so the ring matches the live gameplay radius while decorative children keep their
        // authored size. Pool-safe — the marker caches its prefab base and sets base×scale each spawn.
        if (ctx.emitterScale != 1f)
        {
            var markers = inst.GetComponentsInChildren<CueScalableEmitter>(true);
            if (markers.Length == 0)
                Debug.LogWarning($"[FxManager] emitterScale={ctx.emitterScale:0.##} on '{key.name}' but no CueScalableEmitter on its footprint emitter — size unchanged.", key);
            else
                for (int i = 0; i < markers.Length; i++) markers[i].ApplyScale(ctx.emitterScale);
        }

        // Runtime sim-speed: retime the whole cue. Set EVERY child system's simulationSpeed from the PREFAB base
        // (read each spawn → pool-safe) × the factor, so the parent value cascades to all children.
        if (ctx.simSpeed != 1f)
        {
            var instSys = inst.GetComponentsInChildren<ParticleSystem>(true);
            var prefSys = prefab.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < instSys.Length && i < prefSys.Length; i++)
            {
                var m = instSys[i].main;
                m.simulationSpeed = prefSys[i].main.simulationSpeed * ctx.simSpeed;
            }
        }

        if (af.ps != null) { af.ps.Clear(true); af.ps.Play(true); }
        return _registry.Register(af);
    }

    /// <summary>Shared VFX-graph spawn — used by an inline CueBook element. A graph effect has no natural
    /// end FxManager can read, so with <paramref name="explicitLifetime"/> 0 it is held until Stop()
    /// (Pattern B). A one-shot graph element (hit flashes etc.) MUST author an explicit duration —
    /// fire-and-forget plays of a duration-less Vfx element leaked instances forever (BUG-054).</summary>
    private CueHandle SpawnVfx(VisualEffect prefab, FxAttachMode attachMode, Vector3 localOffset,
                               TimeMode timeMode, float explicitLifetime, in CueContext ctx,
                               Quaternion localRotation = default, Vector3 localScale = default,
                               bool drawOnTop = false)
    {
        var key = prefab.gameObject;
        var inst = _pool.Get(key);
        if (inst == null) return CueHandle.None;
        bool vfxStamped = drawOnTop && StampGroundVfxLayer(inst);

        var attach = ResolveAttach(attachMode, null);   // VFX graph has no simulationSpace → World
        var tmode = ResolveTimeMode(timeMode, null);

        // Same degrade as SpawnParticle: explicit Follow authored on a VFX element but played with a
        // position-only ctx → World, never reclaim-on-arrival.
        if (attach != FxAttachMode.World && ctx.followTarget == null)
            attach = FxAttachMode.World;

        var af = new ActiveFx
        {
            prefabKey = key,
            instance = inst,
            tr = inst.transform,
            vfx = inst.GetComponent<VisualEffect>(),
            attachMode = attach,
            localOffset = localOffset,
            localRotation = localRotation == default ? Quaternion.identity : localRotation,
            localScale = localScale == default ? Vector3.one : localScale,
            timeMode = tmode,
            owner = ctx.owner,
            followTarget = attach != FxAttachMode.World ? ctx.followTarget : null,
            faceTarget = ctx.faceTarget,   // orientation anchor is independent of the follow/World decision
            layerStamped = vfxStamped,
        };
        // Explicit element duration = auto-return (one-shot graph); 0 = held until Stop() (Pattern B).
        if (explicitLifetime > 0f)
        {
            float now = tmode == TimeMode.Unscaled ? Time.unscaledTime : Time.time;
            af.expireAt = now + explicitLifetime;
        }
        else
            af.expireAt = float.PositiveInfinity;
        Place(af, ctx);

        if (af.vfx != null)
        {
            if (ctx.simSpeed != 1f) af.vfx.playRate = ctx.simSpeed;   // retime a VFX-graph cue
            af.vfx.Reinit(); af.vfx.Play();
        }
        return _registry.Register(af);
    }

    // Plays ONE inline Cue Book element — the per-element dispatch (visual + its own audio).
    private CueHandle PlayElement(CueElement e, CueContext ctx)
    {
        if (e == null) return CueHandle.None;
        // Per-element camera feel (FOV % / impulse shake / post-proc depth) → the Persistent driver.
        // Shake fires immediately; FOV/depth are held (last-writer-wins) until the owner's effect releases.
        if (e.camera != null && e.camera.IsActive)
            CameraCueDriver.Instance?.Apply(e.camera, ctx.owner, ctx.position);   // position feeds shakeRange falloff (P18)
        // Per-element hit-stop: authored on the impact element of an effect. Routed through the
        // game-registered HitStopHook (P19 seam 3 — the service is a TimeScaleService owner, R10).
        if (e.camera != null && e.camera.useHitStop)
            HitStopHook?.Invoke(e.camera.hitStopScale, e.camera.hitStopDuration);
        switch (e.kind)
        {
            case CueElementKind.Particle:
                if (e.particlePrefab == null) { Debug.LogWarning("[FxManager] CueBook particle element has no prefab.", this); return CueHandle.None; }
                var hp = SpawnParticle(e.particlePrefab, e.attachMode, e.localOffset, e.timeMode,
                                       e.useDefaultDuration ? 0f : e.duration, ctx,
                                       Quaternion.Euler(e.localRotation), e.localScale, e.drawOnTop);
                AttachAudio(hp, e, ctx);
                return hp;
            case CueElementKind.Vfx:
                if (e.vfxPrefab == null) { Debug.LogWarning("[FxManager] CueBook vfx element has no prefab.", this); return CueHandle.None; }
                var hv = SpawnVfx(e.vfxPrefab, e.attachMode, e.localOffset, e.timeMode,
                                  e.useDefaultDuration ? 0f : e.duration, ctx,
                                  Quaternion.Euler(e.localRotation), e.localScale, e.drawOnTop);
                AttachAudio(hv, e, ctx);
                return hv;
            case CueElementKind.Sound:
                return PlayElementSoundOnly(e, ctx);
            case CueElementKind.Manpu:
                return PlayElementManpu(e, ctx);
            default:
                return CueHandle.None;
        }
    }

    // A Manpu element: pulse a cue-authored glyph on the target's ManpuSlot (transient, R1-respecting), timed
    // before/with/after the effect's other elements by the runner. May also carry its own accent sound(s).
    private CueHandle PlayElementManpu(CueElement e, in CueContext ctx)
    {
        var target = ctx.followTarget;
        if (e.glyphSprite != null)
        {
            if (target == null)
                Debug.LogWarning("[FxManager] CueBook Manpu element needs a follow target with an IManpuGlyphTarget (ManpuSlot).", this);
            else
            {
                var slot = target.GetComponentInChildren<IManpuGlyphTarget>(true);   // P19 seam 2 — package interface
                if (slot != null) slot.RequestCuePulse(e.glyphSprite, e.glyphColorA, e.glyphColorB);
                else Debug.LogWarning($"[FxManager] CueBook Manpu element: target '{target.name}' has no IManpuGlyphTarget (ManpuSlot).", this);
            }
        }
        // A glyph may carry its own accent sound(s) — route them through the sound-only path.
        return (e.audio != null && e.audio.Count > 0) ? PlayElementSoundOnly(e, ctx) : CueHandle.None;
    }

    // Attach an element's per-element audio to the ActiveFx backing its visual (started when due; stopped
    // with the visual for the sounds flagged killWithVisual).
    private void AttachAudio(CueHandle visualHandle, CueElement e, in CueContext ctx)
    {
        if (e.audio == null || e.audio.Count == 0) return;
        if (!_registry.TryGet(visualHandle, out var ac)) return;   // visual failed to spawn — nothing to ride
        if (ac is ActiveFx af) SetupAudio(af, e.audio, soundOnly: false, ctx, af.timeMode);
    }

    // A pure-audio element (kind = Sound): no visual, just its audio list. Held while any of its sounds loop.
    private CueHandle PlayElementSoundOnly(CueElement e, in CueContext ctx)
    {
        if (e.audio == null || e.audio.Count == 0) return CueHandle.None;
        var af = new ActiveFx
        {
            attachMode = FxAttachMode.World,   // no visual to follow; spatial audio still follows via followTarget
            timeMode = TimeMode.Scaled,
            owner = ctx.owner,
            followTarget = ctx.followTarget,
        };
        SetupAudio(af, e.audio, soundOnly: true, ctx, af.timeMode);
        if (af.pendingAudio == null) return CueHandle.None;   // no valid sounds
        af.expireAt = e.IsHeld() ? float.PositiveInfinity : Time.time + Mathf.Max(0.05f, e.GetScheduleDuration());
        return _registry.Register(af);
    }

    private void SetupAudio(ActiveFx af, List<CueElement.CueAudio> audio, bool soundOnly, in CueContext ctx, TimeMode tmode)
    {
        af.audioPos = ctx.position;
        float now = tmode == TimeMode.Unscaled ? Time.unscaledTime : Time.time;
        for (int i = 0; i < audio.Count; i++)
        {
            var a = audio[i];
            if (a?.sound == null) continue;
            (af.pendingAudio ??= new List<PendingAudio>()).Add(new PendingAudio
            {
                sound = a.sound,
                loop = a.loop || a.sound.loop,
                killWithVisual = soundOnly || a.killWithVisual,
                startAt = now + Mathf.Max(0f, a.startDelay),
            });
        }
    }

    private CueHandle PlaySound(SoundCueData cue, in CueContext ctx)
    {
        var am = AudioManager.Instance;
        if (am == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[FxManager] SoundCue '{cue.name}' — no AudioManager in the scene.", cue);
#endif
            return CueHandle.None;
        }
        // Dispatch to the audio engine. Fire-and-forget through FxManager: the voice handle belongs
        // to AudioManager's own registry, so FxManager.Stop can't honor it — hold/stop a looping
        // sound via AudioManager directly. Sequence sound steps want exactly this (one-shots).
        if (ctx.followTarget != null) am.Play(cue, ctx.followTarget);
        else am.Play(cue, ctx.position);
        return CueHandle.None;
    }

    // ── helpers ───────────────────────────────────────────────────────────────────
    private static void Place(ActiveFx af, in CueContext ctx)
    {
        // localRotation = the authored per-element rotation offset, composed onto the spawn/follow rotation
        // (fixes a prefab that faces the wrong way). Update() re-applies it each frame in follow mode.
        Vector3 pos;
        Quaternion rot;
        if (af.attachMode == FxAttachMode.FollowPositionOnly && ctx.followTarget != null)
        {
            // Position-only follow: world-axis offset, spawn orientation = the ctx rotation (never the
            // target's live rotation — Update() keeps this orientation for the cue's whole life).
            pos = ctx.followTarget.position + af.localOffset;
            rot = ctx.rotation;
        }
        else if (af.attachMode != FxAttachMode.World && ctx.followTarget != null)
        {
            pos = ctx.followTarget.position + ctx.followTarget.rotation * af.localOffset;
            rot = ctx.followTarget.rotation;
        }
        else
        {
            pos = ctx.position + ctx.rotation * af.localOffset;
            rot = ctx.rotation;
        }
        // Face-target seeds the spawn orientation too, so frame 0 already aims at the target (Update() re-aims).
        if (ctx.faceTarget != null)
        {
            Vector3 dir = ctx.faceTarget.position - pos;
            if (dir.sqrMagnitude > 0.0001f) rot = Quaternion.LookRotation(dir);
        }
        af.tr.SetPositionAndRotation(pos, rot * af.localRotation);

        // Runtime size: prefab's authored localScale × per-element localScale × ctx.scale. SET (not accumulate)
        // from the prefab each spawn so a pooled instance never compounds a prior cue's scale (F2).
        // Guard: a localScale of (0,0,0) means "unset" (a pre-existing element serialized before this field, or
        // an authoring mistake) — treat it as (1,1,1) so the particle isn't invisible.
        if (af.prefabKey != null)
        {
            Vector3 ls = af.localScale == Vector3.zero ? Vector3.one : af.localScale;
            af.tr.localScale = Vector3.Scale(af.prefabKey.transform.localScale, ls) * ctx.scale;
        }
    }

 // ── prefab-source-of-truth resolution ───────────────────────────────
    // A FromPrefab cue defers to what the artist authored; we read the prefab, never re-store it.

    private float ResolveLifetimeFor(ParticleSystem prefab, float explicitLifetime)
    {
        if (explicitLifetime > 0f) return explicitLifetime;
        if (prefab == null) return 0f;
        if (!_lifetimeCache.TryGetValue(prefab, out var life))
        {
            life = FxLifetime.Compute(prefab);
            _lifetimeCache[prefab] = life;
        }
        return life;
    }

    private static FxAttachMode ResolveAttach(FxAttachMode mode, ParticleSystem ps)
    {
        if (mode != FxAttachMode.FromPrefab) return mode;
        if (ps == null) return FxAttachMode.World;   // VFX graph / no system → world space
        return ps.main.simulationSpace == ParticleSystemSimulationSpace.Local
            ? FxAttachMode.Follow
            : FxAttachMode.World;
    }

    private static TimeMode ResolveTimeMode(TimeMode mode, ParticleSystem ps)
    {
        if (mode != TimeMode.FromPrefab) return mode;
        return (ps != null && ps.main.useUnscaledTime) ? TimeMode.Unscaled : TimeMode.Scaled;
    }

    private void Update()
    {
        if (_registry.ActiveCount == 0) return;

        float sdt = Time.deltaTime;            // scaled
        float udt = Time.unscaledDeltaTime;    // unscaled (R10)

        _registry.GetActive(_scratch);
        _reclaim.Clear();
        foreach (var kv in _scratch)
            if (kv.Value.Update(sdt, udt)) _reclaim.Add(kv.Key);

        foreach (var h in _reclaim)
        {
            if (_registry.TryGet(h, out var ac)) ac.Stop(this);
            _registry.Reclaim(h);
        }
    }

    // F1: stop + reclaim every live instance whose follow target lives in the unloading scene.
    private void HandleSceneWillUnload(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        _registry.GetActive(_scratch);
        foreach (var kv in _scratch)
        {
            var t = kv.Value.followTarget;
            if (t != null && t.gameObject.scene.name == sceneName)
            {
                kv.Value.Stop(this);
                _registry.Reclaim(kv.Key);
            }
        }
    }
}
