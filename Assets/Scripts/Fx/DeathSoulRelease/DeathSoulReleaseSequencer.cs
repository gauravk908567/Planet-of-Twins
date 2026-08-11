using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Orchestrates the enemy-death "soul release" set-piece — the cross-effect TIMING glue that a single VFX Graph
/// can't express, because it has to reach into a SEPARATE dissolve material and spawn a SEPARATE collection
/// prefab. Everything that CAN live in the graph (the orbs riding the souler-coaster mesh, the combined-clan
/// glow, their look) lives in <see cref="orbVfx"/>; this script only sequences the hand-offs.
///
/// The authored timeline (one master 0→1 over <see cref="totalDuration"/>, unscaled so it survives Setsuna):
///   • orbs travel up the path  → drives <c>orbVfx</c>'s exposed "Progress" float 0→1.
///   • dissolve TRAILS the orbs  → sets the body material's "_DissolveAmount" to (orbProgress − <see cref="dissolveLagPercent"/>),
///     so orb 30% ⇒ dissolve 20%, orb 80% ⇒ dissolve 70%, … (the user's offset rule). Clamped 0..1.
///   • orbs reach 100%           → the glow (combined clan symbol) launches up; the remaining dissolve finishes to 100%.
///   • glow disappears (≈ dissolve hits 100%) → the soul-collection prefab spawns at the glow's end position and
///     pulls toward the nearest twin (it self-drives — see SoulParticleAttractor).
///
/// POOL-SAFE / SELF-DRIVING: resets on <see cref="OnEnable"/>; no external Begin needed — drop it on the
/// death-effect prefab and the Cue Book just spawns that prefab. FAILS LOUD (R4-style) if a required ref is unwired.
/// CONFIG via serialized fields (R7 — no SO runtime state here).
/// </summary>
public class DeathSoulReleaseSequencer : MonoBehaviour
{
    [Header("Orbs (VFX Graph — owns path-follow + glow)")]
    [Tooltip("The DeathSoulOrbs VFX Graph instance. Must expose a float property named by 'progressProperty'.")]
    [SerializeField] private VisualEffect orbVfx;
    [Tooltip("Exposed float on the VFX driving how far up the path the orbs have travelled (0→1).")]
    [SerializeField] private string progressProperty = "Progress";

    [Header("Dissolve (separate body material — trails the orbs)")]
    [Tooltip("Renderer whose material has the Disintegrate shader. Its '_DissolveAmount' is driven each frame.")]
    [SerializeField] private Renderer dissolveBody;
    [Tooltip("Shader float that disintegrates the body (0 = whole, 1 = gone). Disintegrate.shadergraph = _DissolveAmount.")]
    [SerializeField] private string dissolveProperty = "_DissolveAmount";
    [Tooltip("How far BEHIND the orbs the dissolve runs, as a fraction (0.1 = orb 30% → dissolve 20%).")]
    [Range(0f, 1f)] [SerializeField] private float dissolveLagPercent = 0.10f;

    [Header("Soul collection (spawned where the glow ends)")]
    [Tooltip("The soul-absorb collection prefab (carries SoulParticleAttractor). Spawned at the glow's end pose " +
             "when the dissolve completes. Left null = no collection spawned (logs once).")]
    [SerializeField] private GameObject soulCollectionPrefab;
    [Tooltip("Local point the combined-clan glow rises to / vanishes at — the collection spawns here. Defaults to " +
             "this transform if unset.")]
    [SerializeField] private Transform glowEndPoint;

    [Header("Timing")]
    [Tooltip("Seconds for the orbs to travel the full path (master 0→1). Unscaled — pause/Setsuna safe.")]
    [Min(0.01f)] [SerializeField] private float totalDuration = 3f;
    [Tooltip("After orbs reach 100%, extra seconds for the glow to rise + dissolve to finish before collection.")]
    [Min(0f)] [SerializeField] private float glowRiseDuration = 0.6f;

    // ── runtime ─────────────────────────────────────────────────────────────────
    private MaterialPropertyBlock _mpb;
    private int _dissolveId;
    private float _elapsed;
    private bool _running;
    private bool _glowPhase;
    private float _glowElapsed;
    private bool _collectionSpawned;
    private bool _loggedNoCollection;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _dissolveId = Shader.PropertyToID(dissolveProperty);
        if (orbVfx == null)
            Debug.LogError("[DeathSoulRelease] orbVfx unassigned — orbs won't play. Wire the DeathSoulOrbs VFX.", this);
        if (dissolveBody == null)
            Debug.LogError("[DeathSoulRelease] dissolveBody unassigned — body won't disintegrate. Wire the body Renderer.", this);
    }

    // Pool-safe reset: every reuse restarts the sequence from 0.
    private void OnEnable()
    {
        _elapsed = 0f;
        _glowElapsed = 0f;
        _running = true;
        _glowPhase = false;
        _collectionSpawned = false;
        ApplyOrbProgress(0f);
        ApplyDissolve(0f);
    }

    private void Update()
    {
        if (!_running) return;
        float dt = Time.unscaledDeltaTime;   // unscaled: the set-piece must not slow under Setsuna/pause (R10)

        if (!_glowPhase)
        {
            _elapsed += dt;
            float orb = Mathf.Clamp01(_elapsed / totalDuration);
            ApplyOrbProgress(orb);
            ApplyDissolve(orb - dissolveLagPercent);   // dissolve trails the orbs by the lag

            if (orb >= 1f)
            {
                _glowPhase = true;                     // orbs reached the top → glow launches, dissolve finishes
                _glowElapsed = 0f;
            }
            return;
        }

        // Glow phase: glow rises while the remaining dissolve completes to 100%.
        _glowElapsed += dt;
        float g = glowRiseDuration <= 0f ? 1f : Mathf.Clamp01(_glowElapsed / glowRiseDuration);
        // Finish the dissolve from its lagged value up to 1 across the glow rise.
        float dissolveStart = 1f - dissolveLagPercent;
        ApplyDissolve(Mathf.Lerp(dissolveStart, 1f, g));

        if (g >= 1f && !_collectionSpawned)
        {
            SpawnCollection();                         // glow vanished + dissolve done → soul collection begins
            _collectionSpawned = true;
            _running = false;                          // sequence complete (the collection self-drives from here)
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────────
    private void ApplyOrbProgress(float p)
    {
        if (orbVfx == null) return;
        if (orbVfx.HasFloat(progressProperty)) orbVfx.SetFloat(progressProperty, Mathf.Clamp01(p));
    }

    private void ApplyDissolve(float amount)
    {
        if (dissolveBody == null) return;
        float a = Mathf.Clamp01(amount);
        dissolveBody.GetPropertyBlock(_mpb);
        _mpb.SetFloat(_dissolveId, a);                 // MPB: per-instance, doesn't touch the shared material (pool-safe)
        dissolveBody.SetPropertyBlock(_mpb);
    }

    private void SpawnCollection()
    {
        if (soulCollectionPrefab == null)
        {
            if (!_loggedNoCollection)
            { Debug.LogWarning("[DeathSoulRelease] soulCollectionPrefab unassigned — no soul collection.", this); _loggedNoCollection = true; }
            return;
        }
        Transform end = glowEndPoint != null ? glowEndPoint : transform;
        // The collection prefab carries SoulParticleAttractor (self-resolves nearest twin on its OnEnable).
        Instantiate(soulCollectionPrefab, end.position, end.rotation);
    }
}
