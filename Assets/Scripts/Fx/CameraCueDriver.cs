using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Lives in **Persistent**. Applies a cue element's <see cref="CueElement.CameraCue"/> "feel" — touching NO
/// camera transform OR lens (design_camera_cue.md). Two channels:
///   • <b>Shake</b> — Cinemachine Impulse (this GO owns the source); every cam with a CinemachineImpulseListener
///     reacts, so it survives a camera switch. Fire-and-forget, Uniform (no distance falloff).
///   • <b>Depth</b> — drives the Persistent post-process <see cref="Volume"/>'s weight 0→target→0, swapping to
///     the cue's own <c>VolumeProfile</c>. This is also where a "zoom" feel lives now (a Lens Distortion
///     override in the profile) — the old real-FOV punch was removed because a GROUP camera computes its own
///     FOV each frame (to keep both twins framed), so any external FOV write fought it (weird zoom even at 1.0).
///
/// LAST-WRITER-WINS (user spec): ONE active depth target. A new <see cref="Apply"/> overwrites it (no stack).
/// The owner that started the feel <see cref="Release"/>s it when its effect ends (FxManager.Stop forwards the
/// owner) → blend depth→0 over blendOut. Unscaled time (feel must not slow under Setsuna).
///
/// R3 singleton (no DDOL). Clears on area unload + soft reset so a punch can't persist across a teardown.
/// </summary>
public class CameraCueDriver : MonoBehaviour
{
    public static CameraCueDriver Instance { get; private set; }

    [Tooltip("Persistent global post-process Volume the depth channel drives (weight 0→target→0). " +
             "Left null = depth channel is a no-op (logs once). Create a global Volume in Persistent.")]
    [SerializeField] private Volume depthVolume;

    [Tooltip("Optional tuned impulse source. Auto-added on this GO if empty.")]
    [SerializeField] private CinemachineImpulseSource impulseSource;

    // ── active depth feel (last-writer-wins) ────────────────────────────────────
    private bool   _depthActive;
    private object _depthOwner;
    private float  _depthTarget;            // target Volume weight
    private float  _depthCurrent;
    private float  _depthBlendIn = 0.05f, _depthBlendOut = 0.2f;
    private bool   _depthReleasing;
    private float  _depthStart;            // weight at the moment the current blend began
    private float  _depthElapsed;          // seconds into the current blend

    private bool _loggedNoVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (impulseSource == null)
            impulseSource = GetComponent<CinemachineImpulseSource>() ?? gameObject.AddComponent<CinemachineImpulseSource>();
        if (depthVolume != null) depthVolume.weight = 0f;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ── public API ──────────────────────────────────────────────────────────────

    /// <summary>Apply a camera cue. Shake fires now; depth overwrites the active target (last-writer-wins).
    /// <paramref name="worldPosition"/> = the cue's spawn position — used only when the element authors a
    /// shakeRange &gt; 0 (distance falloff).</summary>
    public void Apply(CueElement.CameraCue cue, object owner, Vector3 worldPosition = default)
    {
        if (cue == null || !cue.IsActive) return;

        if (cue.useShake && impulseSource != null)
        {
            // Stamp the cue's inline shape onto the one shared source, then fire. Impulses are near-instant and
            // sequential, so reusing/overwriting the shared definition between fires is safe (no race). R2-safe.
            var def = impulseSource.ImpulseDefinition;
            if (def != null)
            {
                def.ImpulseShape    = cue.shakeShape;
                if (cue.shakeShape == CinemachineImpulseDefinition.ImpulseShapes.Custom)
                    def.CustomImpulseShape = cue.shakeCustomShape;   // P18: author-your-own noise profile
                def.ImpulseDuration = Mathf.Max(0.01f, cue.shakeDuration);
                def.FrequencyGain   = cue.shakeFrequency;
                // shakeRange 0 (default) = UNIFORM: every camera listener reacts equally, NO distance falloff
                // (the source sits on this Persistent GO, not at the camera). shakeRange > 0 = DISSIPATING from
                // the cue's world position over that many metres — a distant camera feels less (P18).
                // Per-camera sensitivity = each cam's CinemachineImpulseListener.Gain (authoring, not code).
                if (cue.shakeRange > 0f)
                {
                    def.ImpulseType         = CinemachineImpulseDefinition.ImpulseTypes.Dissipating;
                    def.DissipationDistance = cue.shakeRange;
                }
                else
                {
                    def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
                }
                def.AmplitudeGain  = 1f;
            }
            // Direction of the kick (slash = sideways…). Zero → a default downward kick so it's visible.
            Vector3 vel = (cue.shakeDirection != Vector3.zero
                ? cue.shakeDirection.normalized : new Vector3(0f, -1f, 0f)) * cue.shakeAmplitude;
            if (cue.shakeRange > 0f)
                impulseSource.GenerateImpulseAtPositionWithVelocity(worldPosition, vel);   // falloff needs an origin
            else
                impulseSource.GenerateImpulseWithVelocity(vel);
        }

        if (cue.useDepth)
        {
            _depthActive = true; _depthReleasing = false; _depthOwner = owner;
            _depthStart = _depthCurrent; _depthElapsed = 0f;   // begin a fresh timed blend
            _depthTarget = Mathf.Clamp01(cue.depthWeight);
            _depthBlendIn = cue.blendIn; _depthBlendOut = cue.blendOut;
            // Per-cue profile (last-writer-wins): swap the feel-Volume to THIS cue's look (incl. a zoom via
            // Lens Distortion authored in the profile).
            if (depthVolume != null && cue.depthProfile != null) depthVolume.profile = cue.depthProfile;
        }
    }

    /// <summary>The owner's effect ended → blend depth→0 over blendOut. No-op if a LATER owner has since
    /// overwritten the feel (last-writer-wins: only the current owner can release it).</summary>
    public void Release(object owner)
    {
        if (_depthActive && ReferenceEquals(_depthOwner, owner) && !_depthReleasing)
        { _depthReleasing = true; _depthStart = _depthCurrent; _depthElapsed = 0f; }
    }

    /// <summary>Hard clear — area unload / soft reset. Snaps depth back to 0.</summary>
    public void ClearAll()
    {
        _depthActive = _depthReleasing = false; _depthOwner = null;
        _depthTarget = _depthCurrent = 0f;
        if (depthVolume != null) depthVolume.weight = 0f;
    }

    // ── per-frame drive (unscaled — feel must not slow under Setsuna) ────────────
    private void LateUpdate() => DriveDepth(Time.unscaledDeltaTime);

    private void DriveDepth(float dt)
    {
        if (!_depthActive) return;
        if (depthVolume == null)
        {
            if (!_loggedNoVolume) { Debug.LogWarning("[CameraCueDriver] depthVolume unassigned — depth channel ignored.", this); _loggedNoVolume = true; }
            _depthActive = false; return;
        }

        float goal  = _depthReleasing ? 0f : _depthTarget;
        float blend = _depthReleasing ? _depthBlendOut : _depthBlendIn;
        if (blend <= 0f) { _depthCurrent = goal; }
        else
        {
            _depthElapsed += dt;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_depthElapsed / blend));
            _depthCurrent = Mathf.Lerp(_depthStart, goal, t);
        }
        depthVolume.weight = _depthCurrent;

        if (_depthReleasing && _depthCurrent <= 0.0001f)
        {
            _depthActive = false; _depthReleasing = false; _depthOwner = null;
        }
    }
}
