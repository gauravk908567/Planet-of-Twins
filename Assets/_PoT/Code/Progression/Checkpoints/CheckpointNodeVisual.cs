using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// The look of ONE dual-checkpoint node (game.md §11.2 "Visual"): its orb (the node's VisualEffect, running the
/// checkpoint's own <c>CheckpointOrb.vfx</c>), its spiral trails (a ParticleSystem) and the joint-hold ring. It only
/// READS gameplay: this node's occupant and the parent <see cref="DualCheckpoint"/>'s state and events.
///
///   • idle (nobody on this node)   — orb texture still, a few trails at random intervals
///   • a twin on THIS node          — orb texture scroll spins up, trails near-continuous (Max Particles caps them)
///   • solo press on the checkpoint — a trail right away on BOTH nodes; the idle countdown restarts
///   • saved                        — the trails burst and vanish, orb power ramps up (the clip reads as a fade)
///   • re-armed                     — orb power back to the authored value, idle trails resume
///
/// The scroll OFFSET is integrated here and fed to the shader: the stock orb shader multiplied game time by the
/// speed, so changing the speed made the pattern jump. These are the object's own STATE visuals (same call as
/// <see cref="SpawnPointVisualDriver"/>, instruction §14.1c), so they live on the prefab instead of going through
/// FxManager. R1 refs (all in the same prefab); R7 reads the authored orb values, never writes the asset.
/// Timers use SCALED time — pause freezes the checkpoint and Setsuna slows it, like the rest of the world.
/// </summary>
public class CheckpointNodeVisual : MonoBehaviour
{
    private static readonly int PowerId  = Shader.PropertyToID("mainTexPower");
    private static readonly int ScrollId = Shader.PropertyToID("ScrollOffset");

    [Header("Reads (same prefab — R1)")]
    [SerializeField] private DualCheckpoint _checkpoint;
    [SerializeField] private CheckpointNode _node;

    [Header("Orb — the node's VisualEffect (CheckpointOrb.vfx)")]
    [SerializeField] private VisualEffect _orb;
    [Tooltip("Orb power once consumed. The shader clips against it, so a high value reads as the orb fading away. " +
             "The idle value is whatever mainTexPower the orb is authored with.")]
    [SerializeField] private float _consumedPower = 10f;
    [Tooltip("Seconds for the orb to fade out after a save.")]
    [SerializeField, Min(0.01f)] private float _consumeSeconds = 0.5f;
    [SerializeField] private AnimationCurve _consumeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Seconds for the orb to come back when the checkpoint re-arms.")]
    [SerializeField, Min(0.01f)] private float _returnSeconds = 1f;
    [SerializeField] private AnimationCurve _returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Texture scroll speed (UV per second) while a twin stands on this node.")]
    [SerializeField] private Vector2 _occupiedScroll = new Vector2(0.2f, 0.2f);
    [Tooltip("Seconds for the texture scroll to spin up from still to full speed (and back down).")]
    [SerializeField, Min(0.01f)] private float _scrollRampSeconds = 0.6f;

    [Header("Spiral trails — ParticleSystem (its Max Particles caps how many are alive)")]
    [SerializeField] private ParticleSystem _trails;
    [Tooltip("Idle: seconds between trail bursts (random between the two).")]
    [SerializeField] private Vector2 _idleInterval = new Vector2(3f, 6f);
    [Tooltip("Idle: trails per burst (random between the two, inclusive).")]
    [SerializeField] private Vector2Int _idleBurst = new Vector2Int(3, 4);
    [Tooltip("A twin on this node: trails emitted per second.")]
    [SerializeField, Min(0f)] private float _occupiedRate = 4f;
    [Tooltip("Solo press ('come here'): trails fired at once on this node.")]
    [SerializeField, Min(1)] private int _callBurst = 2;
    [Tooltip("Minimum seconds between two solo-press bursts, so button mashing can't flood it.")]
    [SerializeField, Min(0f)] private float _callCooldown = 0.4f;
    [Tooltip("Save: extra trails fired in the final burst (on top of Max Particles).")]
    [SerializeField, Min(0)] private int _consumeBurst = 5;
    [Tooltip("Save: outward speed of the final-burst trails.")]
    [SerializeField, Min(0f)] private float _consumeBurstSpeed = 2.5f;
    [Tooltip("Save: lifetime (s) of the final-burst trails, and the most any live trail keeps — so they all vanish.")]
    [SerializeField, Min(0.05f)] private float _consumeTrailLifetime = 0.6f;

    [Header("Joint-hold ring (optional) — shown while both twins are on their nodes")]
    [SerializeField] private GameObject _holdRingRoot;
    [SerializeField] private UIRingTimerView _holdRing;

    private float _idlePower;          // the authored orb power (read once — R7)
    private float _power;
    private float _powerFrom, _powerTo, _powerT, _powerSeconds;
    private AnimationCurve _powerCurve;
    private bool _tweening;
    private Vector2 _scrollSpeed;      // current UV/s, ramps toward the target
    private Vector2 _scrollOffset;     // integrated here so a speed change never jumps the pattern
    private float _nextIdleBurst;      // SCALED countdown
    private float _callReadyAt;        // SCALED Time.time
    private bool _consumed;
    private int _baseMaxParticles;
    private ParticleSystem.Particle[] _buffer;

    private void Awake()
    {
        if (_checkpoint == null || _node == null || _orb == null)
        {
            Debug.LogError("[CheckpointNodeVisual] Checkpoint, node or orb unassigned — node visual disabled.", this);
            enabled = false;
            return;
        }
        _idlePower = _orb.HasFloat(PowerId) ? _orb.GetFloat(PowerId) : 0.6f;
        _power = _idlePower;
        if (_orb.HasVector2(ScrollId)) _scrollOffset = _orb.GetVector2(ScrollId);
        if (_trails != null) _baseMaxParticles = _trails.main.maxParticles;
        if (_holdRingRoot != null) _holdRingRoot.SetActive(false);
        ScheduleIdleBurst();
    }

    private void OnEnable()
    {
        _checkpoint.Saved += OnSaved;
        _checkpoint.Rearmed += OnRearmed;
        _checkpoint.OnSoloSaveAttempt += OnSoloSaveAttempt;
    }

    private void OnDisable()
    {
        _checkpoint.Saved -= OnSaved;
        _checkpoint.Rearmed -= OnRearmed;
        _checkpoint.OnSoloSaveAttempt -= OnSoloSaveAttempt;
    }

    private void Start()
    {
        if (_trails != null && !_trails.isPlaying) _trails.Play();   // emission is script-driven; it must be running
        if (!_checkpoint.IsConsumed) return;
        _consumed = true;                                             // enabled mid-consume → match it, no tween
        _power = _consumedPower;
        _orb.SetFloat(PowerId, _power);
    }

    private void Update()
    {
        float dt = Time.deltaTime;   // SCALED — see class summary
        bool occupied = _node.IsOccupied && !_consumed;
        UpdateScroll(occupied, dt);
        UpdatePower(dt);
        UpdateTrails(occupied, dt);
        UpdateHoldRing();
    }

    private void UpdateScroll(bool occupied, float dt)
    {
        Vector2 target = occupied ? _occupiedScroll : Vector2.zero;
        _scrollSpeed = Vector2.MoveTowards(_scrollSpeed, target, _occupiedScroll.magnitude / _scrollRampSeconds * dt);
        if (_scrollSpeed == Vector2.zero) return;
        _scrollOffset += _scrollSpeed * dt;
        _scrollOffset.x -= Mathf.Floor(_scrollOffset.x);   // the texture repeats every 1 UV — keep the number small
        _scrollOffset.y -= Mathf.Floor(_scrollOffset.y);
        _orb.SetVector2(ScrollId, _scrollOffset);
    }

    private void StartPowerTween(float to, float seconds, AnimationCurve curve)
    {
        _powerFrom = _power;
        _powerTo = to;
        _powerT = 0f;
        _powerSeconds = seconds;
        _powerCurve = curve;
        _tweening = true;
    }

    private void UpdatePower(float dt)
    {
        if (!_tweening) return;
        _powerT = Mathf.Min(1f, _powerT + dt / _powerSeconds);
        _power = Mathf.LerpUnclamped(_powerFrom, _powerTo, _powerCurve.Evaluate(_powerT));
        _orb.SetFloat(PowerId, _power);
        if (_powerT >= 1f) _tweening = false;
    }

    private void UpdateTrails(bool occupied, float dt)
    {
        if (_trails == null) return;
        var emission = _trails.emission;
        float rate = occupied ? _occupiedRate : 0f;
        if (!Mathf.Approximately(emission.rateOverTime.constant, rate)) emission.rateOverTime = rate;

        if (_consumed || occupied) return;   // idle bursts only while this node is empty and the checkpoint is live
        _nextIdleBurst -= dt;
        if (_nextIdleBurst > 0f) return;
        _trails.Emit(Random.Range(_idleBurst.x, _idleBurst.y + 1));
        ScheduleIdleBurst();
    }

    private void ScheduleIdleBurst() => _nextIdleBurst = Random.Range(_idleInterval.x, _idleInterval.y);

    private void UpdateHoldRing()
    {
        if (_holdRingRoot == null) return;
        bool show = !_consumed && _checkpoint.CurrentState == DualCheckpoint.State.BothOccupied;
        if (_holdRingRoot.activeSelf != show) _holdRingRoot.SetActive(show);
        if (show && _holdRing != null) _holdRing.SetProgress(_checkpoint.HoldProgress);
    }

    // A lone twin pressed save — BOTH nodes answer (each node's visual hears the same event).
    private void OnSoloSaveAttempt(CheckpointNode vacant)
    {
        if (_consumed || _trails == null || Time.time < _callReadyAt) return;
        _callReadyAt = Time.time + _callCooldown;
        _trails.Emit(_callBurst);
        ScheduleIdleBurst();   // restart the countdown so the next idle burst doesn't land right after
    }

    private void OnSaved()
    {
        _consumed = true;
        StartPowerTween(_consumedPower, _consumeSeconds, _consumeCurve);
        if (_holdRingRoot != null) _holdRingRoot.SetActive(false);
        if (_trails == null) return;

        var emission = _trails.emission;
        emission.rateOverTime = 0f;
        CapTrailLifetimes(_consumeTrailLifetime);

        // Room for the final burst on top of the normal cap; restored on re-arm.
        var main = _trails.main;
        main.maxParticles = _baseMaxParticles + _consumeBurst;
        var p = new ParticleSystem.EmitParams { startLifetime = _consumeTrailLifetime };
        for (int i = 0; i < _consumeBurst; i++)
        {
            Vector3 outward = Quaternion.Euler(0f, i * 360f / _consumeBurst, 0f) * Vector3.forward;
            p.velocity = (outward + Vector3.up * 0.5f) * _consumeBurstSpeed;
            _trails.Emit(p, 1);
        }
    }

    private void OnRearmed()
    {
        _consumed = false;
        StartPowerTween(_idlePower, _returnSeconds, _returnCurve);
        if (_trails != null)
        {
            var main = _trails.main;
            main.maxParticles = _baseMaxParticles;
        }
        _nextIdleBurst = 0f;   // announce the return with a burst straight away
    }

    // Shorten every live trail so they all vanish together with the save burst.
    private void CapTrailLifetimes(float maxRemaining)
    {
        int max = _trails.main.maxParticles;
        if (_buffer == null || _buffer.Length < max) _buffer = new ParticleSystem.Particle[max];
        int n = _trails.GetParticles(_buffer);
        for (int i = 0; i < n; i++)
            if (_buffer[i].remainingLifetime > maxRemaining) _buffer[i].remainingLifetime = maxRemaining;
        _trails.SetParticles(_buffer, n);
    }
}
