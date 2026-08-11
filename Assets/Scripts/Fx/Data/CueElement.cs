using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

/// <summary>Which engine a Cue Book element drives — picked from a dropdown so it points straight at the
/// right play path. <c>Sound</c> = pure audio (no visual). <c>Manpu</c> = a glyph pulsed on the cue target's
/// ManpuSlot (timed like any element; transient).</summary>
public enum CueElementKind { Particle, Vfx, Sound, Manpu }

/// <summary>
/// One inline effect inside a named <c>CueBookData</c> effect: a visual
/// (particle / VFX graph) plus its OWN list of <see cref="CueAudio"/> sounds, authored directly in the book —
/// drop a prefab/clip, pick the kind, set timing, optionally cut earlier elements. Audio rides on the element
/// (auto-synced to the visual) and — by default — is killed when the visual stops. CONFIG ONLY (R7): no
/// runtime state; <c>FxManager</c>/<c>CueBookRunner</c> own the handles.
/// </summary>
[Serializable]
public class CueElement
{
    [Tooltip("Optional name, just for reading the list.")]
    public string label;

    [Tooltip("Particle / Vfx = a visual (with its own audio list) · Sound = pure audio (no visual).")]
    public CueElementKind kind = CueElementKind.Particle;

    // ── Variants (P18 — user-locked model) ───────────────────────────────────────
    [Tooltip("CONSECUTIVE elements marked Is Variant form ONE variant group — each Play picks exactly one of " +
             "them at random (equal weights) and skips the rest. The chosen element keeps its own timing; " +
             "elements after the group schedule off the chosen one. Same book, same id — never a new asset.")]
    public bool isVariant;

    // ── Visual (Particle / Vfx) ────────────────────────────────────────────────
    [Tooltip("Kind=Particle: pooled ParticleSystem prefab.")]
    public ParticleSystem particlePrefab;
    [Tooltip("Kind=Vfx: pooled VisualEffect (VFX Graph) prefab.")]
    public VisualEffect vfxPrefab;
    [Tooltip("FromPrefab (default) reads the prefab (particle simulationSpace → Local=Follow/World; a " +
             "VFX graph → World). Pick an explicit mode only to override.")]
    public FxAttachMode attachMode = FxAttachMode.FromPrefab;
    [Tooltip("Local position offset from the spawn pose (in follow mode: relative to the target's rotation).")]
    public Vector3 localOffset;
    [Tooltip("Local rotation offset (euler°) applied on top of the spawn/follow rotation — fix a prefab that " +
             "faces the wrong way per-cue. In follow mode it stays relative to the target each frame.")]
    public Vector3 localRotation;
    [Tooltip("Per-element scale multiplier on the prefab's authored scale (1,1,1 = prefab size). Combines with " +
             "any CueContext.scale. Non-uniform allowed (stretch a beam).")]
    public Vector3 localScale = Vector3.one;
    [Tooltip("Draw this visual OVER world geometry (grass, props) — for gameplay-critical ground telegraphs: " +
             "cast circles, meteor decals, AOE rings. FxManager moves the spawned instance onto the GroundVFX " +
             "layer, which a RenderObjects feature draws depth-test-off after opaques (fog still veils it). " +
             "Pool-safe (layers restored on return). Leave OFF for normal cosmetic VFX.")]
    public bool drawOnTop;

    // ── Manpu glyph (kind = Manpu) ───────────────────────────────────────────────
    [Tooltip("Kind=Manpu: a glyph PULSED on the cue target's ManpuSlot (enemies). Timed before/with/after the " +
             "effect's other elements like any element; transient (a ~1s pulse), dropped if an ability owns the " +
 "slot (R1). Held glyphs are the ability arc (vocabulary ClosingSequence, ), NOT this.")]
    public Sprite glyphSprite;
    public Color glyphColorA = Color.white;
    public Color glyphColorB = Color.white;

    // ── Per-element audio ────────────────────────────────────────────────────────
    [Tooltip("Sounds that fire WITH this element (auto-synced). Each can loop or one-shot and, for a visual " +
             "element, stop when the visual stops (Kill With Visual). Several one-shots per element allowed.")]
    public List<CueAudio> audio = new List<CueAudio>();

    // ── Per-element camera "feel" (FOV punch + impulse shake + post-proc depth) ───
    [Tooltip("Optional camera feel fired WHEN this element plays. Enable via the + Camera button. FOV is a " +
             "percentage of the ACTIVE cam's base (switch-proof, never touches a transform — no Y change); shake " +
             "is Cinemachine Impulse (needs CinemachineImpulseListener on the cam); depth drives the Persistent " +
             "post-process Volume weight. Last element wins on overlap. See design_camera_cue.md.")]
    public CameraCue camera = new CameraCue();   // always present; "authored" = any channel toggled on (IsActive)

    // ── Timing (relative to the previous element in the effect) ───────────────────
    [Tooltip("Immediate = fire at the effect's t=0, ignoring the previous element (use for the first element). · " +
             "WithPrevious = fire at the previous element's START (delay 0 = parallel, delay > 0 = staggered). · " +
             "AfterPreviousCompletion = fire when the previous element STOPS (natural end / cut / gameplay Stop). " +
             "If the previous element LOOPS, end it via code (CueHandle.Stop) or a cut, or this never fires.")]
    public CueStartMode startMode = CueStartMode.WithPrevious;
    [Tooltip("Extra delay on top of startMode, in the book's timeMode (R10).")]
    [Min(0f)] public float startDelay;

    [Tooltip("ON = use the prefab's natural length (particle lifetime; a VFX graph / looping particle is held " +
             "until stopped). OFF = run for the explicit Duration below, then auto-stop.")]
    public bool useDefaultDuration = true;
    [Tooltip("Used only when 'Use Default Duration' is OFF — seconds before this element auto-stops.")]
    [Min(0f)] public float duration;

    [Tooltip("FromPrefab (default) defers to the prefab's useUnscaledTime. Scaled = slows under Setsuna; " +
             "Unscaled = pause-proof (UI). Comment the choice (R10).")]
    public TimeMode timeMode = TimeMode.FromPrefab;

    // ── Cut (stop an earlier, still-running element at a scripted beat) ───────────
    [Tooltip("ON = this element stops one or more earlier elements after a delay (the 'VFX1 → VFX2, then cut " +
             "VFX1' case). Gameplay-driven stops call FxManager.Stop(handle) instead.")]
    public bool canCut;
    [Tooltip("Each cut stops the element at Target Index, AfterSeconds after THIS element starts.")]
    public List<CueCut> cuts = new List<CueCut>();

    [Serializable]
    public struct CueCut
    {
        [Tooltip("Index (in this effect's element list) of the earlier element to stop.")]
        public int targetIndex;
        [Tooltip("Seconds after THIS element starts before the target is stopped.")]
        [Min(0f)] public float afterSeconds;
    }

    /// <summary>One sound bound to its owning element — routed to AudioManager (pooled voice). Reuse one
    /// SoundCueData across elements; the per-use loop / kill / delay live here, not on the shared asset.</summary>
    [Serializable]
    public class CueAudio
    {
        [Tooltip("The sound (mixer group / spatial / priority live on the SoundCueData). Reuse across cues.")]
        public SoundCueData sound;
        [Tooltip("Loop this sound (held until the element stops) instead of playing it once.")]
        public bool loop;
        [Tooltip("Stop this sound when the owning visual stops (default ON). Ignored on a Sound-kind element.")]
        public bool killWithVisual = true;
        [Tooltip("Delay after the element fires before this sound starts (book timeMode).")]
        [Min(0f)] public float startDelay;
    }

    /// <summary>Per-element camera feel — applied by <c>CameraCueDriver</c> (Persistent) when this element
    /// plays. All three channels are switch-proof and touch NO camera transform (the user's Y-rule):
    /// FOV = a percentage of the active cam's base lens; shake = Cinemachine Impulse (per-cam listeners);
    /// depth = the Persistent post-process Volume's weight. On the owning effect's end the driver blends
    /// FOV→100% and depth→0 over <see cref="blendOut"/>. Last-writer-wins on FOV/depth when cues overlap.
    /// CONFIG ONLY (R7).</summary>
    [Serializable]
    public class CameraCue
    {
        // NOTE: real-FOV punch was removed — a GROUP camera computes its own FOV every frame (to keep both
        // twins framed), so any external FOV write fought it (weird zoom even at factor 1). For a "zoom" feel,
        // author a Lens Distortion override (scale) into the Depth VolumeProfile below — post-proc, no cam touch.

        [Header("Shake (Cinemachine Impulse — survives cam switch)")]
        public bool useShake;
        [Tooltip("Shake shape preset (Recoil = sharp kick, Bump, Explosion, Rumble). The driver stamps these " +
                 "values onto its one shared impulse source, then fires — no asset/object reference (R2-safe).")]
        public Unity.Cinemachine.CinemachineImpulseDefinition.ImpulseShapes shakeShape =
            Unity.Cinemachine.CinemachineImpulseDefinition.ImpulseShapes.Recoil;
        [Tooltip("Overall strength of the kick.")]
        [Min(0f)] public float shakeAmplitude = 1f;
        [Tooltip("Seconds the shake plays out.")]
        [Min(0f)] public float shakeDuration = 0.2f;
        [Tooltip("Wobble frequency multiplier (higher = faster shaking).")]
        [Min(0f)] public float shakeFrequency = 1f;
        [Tooltip("Kick direction (camera-shake axis). (1,0,0)=sideways slash, (0,1,0)=vertical, (0,0,1)=push. " +
                 "Zero = use the source's default direction. Driver sets DefaultVelocity to this before firing.")]
        public Vector3 shakeDirection = new Vector3(1f, 0f, 0f);
        [Tooltip("P18: used only when Shake Shape = Custom — author your own impulse shape (X 0..1 normalised " +
                 "time, Y amplitude). The 4 presets ignore this.")]
        public AnimationCurve shakeCustomShape = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
        [Tooltip("P18: distance falloff. 0 = UNIFORM (every camera shakes equally — the default, switch-proof). " +
                 "> 0 = the shake DISSIPATES over this many metres from the cue's world position — distant " +
                 "cameras feel less. Per-camera sensitivity lives on each cam's CinemachineImpulseListener.Gain.")]
        [Min(0f)] public float shakeRange = 0f;

        [Header("Depth (per-cue post-process profile)")]
        public bool useDepth;
        [Tooltip("This cue's OWN post-process VolumeProfile (e.g. fire = orange/bloom, ice = blue/desat). The " +
                 "driver swaps the Persistent feel-Volume to this profile and blends its weight 0→target→0.")]
        public VolumeProfile depthProfile;
        [Tooltip("Target weight for this cue's profile (0 = off, 1 = full).")]
        [Range(0f, 1f)] public float depthWeight = 0.5f;

        [Header("Timing — the per-ability fast/smooth control (depth blend)")]
        [Tooltip("Seconds to blend depth IN to target (small = snappy, larger = smooth).")]
        [Min(0f)] public float blendIn = 0.05f;
        [Tooltip("Seconds to blend depth→0 OUT when the effect ends.")]
        [Min(0f)] public float blendOut = 0.2f;

        [Header("Hit-stop (micro time-freeze on this element's fire)")]
        [Tooltip("Punch HitStopService when this element fires — the Platinum-style contact freeze. " +
                 "Routes through TimeScaleService (R10, min-value-wins); author on the IMPACT element " +
                 "of an effect, not on charge/travel beats.")]
        public bool useHitStop;
        [Tooltip("Time scale during the stop. 0.05 = near-freeze, 0.3 = soft emphasis.")]
        [Range(0.01f, 0.5f)] public float hitStopScale = 0.05f;
        [Tooltip("UNSCALED seconds the stop lasts. Melee-ish 0.04–0.08; heavy finisher up to ~0.15.")]
        [Range(0.02f, 0.3f)] public float hitStopDuration = 0.06f;

        /// <summary>Nothing to drive (all channels off) → the driver can ignore this cue.</summary>
        public bool IsActive => useShake || useDepth;
    }

    /// <summary>True = plays until explicitly stopped (a looping visual, or a Sound element with a looping
    /// sound). Such an element keeps its book alive so a gameplay <c>Stop</c> can still reach it.</summary>
    public bool IsHeld()
    {
 if (kind == CueElementKind.Manpu) return false; // a cue glyph is a transient pulse (held = ability arc)
        if (!useDefaultDuration) return false;          // explicit duration always auto-stops
        switch (kind)
        {
            case CueElementKind.Vfx:      return true;   // a VFX graph has no natural end
            case CueElementKind.Particle: return particlePrefab != null && particlePrefab.main.loop;
            case CueElementKind.Sound:    return AnyAudioLoops();
            default:                      return false;
        }
    }

    /// <summary>Finite length used ONLY for AfterPrevious chaining / total duration. Held/looping → 0
    /// (a following step never waits on an unbounded effect).</summary>
    public float GetScheduleDuration()
    {
        if (kind == CueElementKind.Manpu) return 0f;     // a glyph never blocks the element sequence
        if (!useDefaultDuration) return duration;
        switch (kind)
        {
            case CueElementKind.Particle:
                if (particlePrefab == null || particlePrefab.main.loop) return 0f;
                return FxLifetime.Compute(particlePrefab);
            case CueElementKind.Vfx:
                return 0f;                               // held until Stop
            case CueElementKind.Sound:
                return AnyAudioLoops() ? 0f : MaxAudioDuration();
            default:
                return 0f;
        }
    }

    private bool AnyAudioLoops()
    {
        if (audio == null) return false;
        for (int i = 0; i < audio.Count; i++)
        {
            var a = audio[i];
            if (a != null && (a.loop || (a.sound != null && a.sound.loop))) return true;
        }
        return false;
    }

    private float MaxAudioDuration()
    {
        float max = 0f;
        if (audio == null) return 0f;
        for (int i = 0; i < audio.Count; i++)
        {
            var a = audio[i];
            if (a?.sound == null) continue;
            float d = a.startDelay + a.sound.GetDuration();
            if (d > max) max = d;
        }
        return max;
    }
}
