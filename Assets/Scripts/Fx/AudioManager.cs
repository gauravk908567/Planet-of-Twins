using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Snapshot identities (index into <see cref="AudioManager"/>'s snapshot array).</summary>
public enum AudioSnapshotId { Default = 0, Paused = 1, Setsuna = 2, GameOver = 3 }

/// <summary>
/// Persistent singleton — the audio engine. 32 pooled <c>AudioSource</c>
/// voices with stealing (lowest priority, then oldest); per-cue cooldown + maxSimultaneous; the
/// <b>sole</b> writer of <c>AudioListener.pause</c> (owner set) and the <b>sole</b> caller of
/// <c>AudioMixerSnapshot.TransitionTo</c> (via <see cref="SnapshotArbiter"/>) — Banned Lazy Work #11.
///
/// R3: scene-resident — duplicate-destroy Awake, <c>Instance</c> nulled in OnDestroy, no DDOL.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Voices")]
    [SerializeField] private int voiceCount = 32;
    [SerializeField] private Transform voiceRoot;
    [Tooltip("Fallback mixer group when a cue specifies none.")]
    [SerializeField] private AudioMixerGroup defaultGroup;

    [Header("Snapshots — order: Default, Paused, Setsuna, GameOver (wire from the AudioMixer asset)")]
    [SerializeField] private AudioMixerSnapshot[] snapshots = new AudioMixerSnapshot[4];
    [Tooltip("Snapshot crossfade time (real/unscaled seconds — TransitionTo always uses real time).")]
    [SerializeField] private float snapshotTransition = 0.3f;

    private sealed class Voice
    {
        public AudioSource src;
        public int priority;
        public float startUnscaled;
        public SoundCueData cue;
        public Transform follow;
        public bool loop;
        public bool ui;
        public CueHandle handle;
    }

    private readonly List<Voice> _voices = new List<Voice>();
    private readonly Stack<Voice> _free = new Stack<Voice>();
    private readonly CueInstanceRegistry<Voice> _registry = new CueInstanceRegistry<Voice>();
    private readonly List<KeyValuePair<CueHandle, Voice>> _scratch = new List<KeyValuePair<CueHandle, Voice>>();
    private readonly List<CueHandle> _reclaim = new List<CueHandle>();
    private readonly Dictionary<SoundCueData, float> _lastPlay = new Dictionary<SoundCueData, float>(); // scaled time

    private readonly HashSet<object> _pausers = new HashSet<object>();
    private readonly SnapshotArbiter _snapshotArbiter = new SnapshotArbiter();

    // ── lifecycle ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (voiceRoot == null)
        {
            var root = new GameObject("VoicePool");
            root.transform.SetParent(transform, false);
            voiceRoot = root.transform;
        }
        for (int i = 0; i < Mathf.Max(1, voiceCount); i++)
        {
            var go = new GameObject($"Voice_{i}");
            go.transform.SetParent(voiceRoot, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            var v = new Voice { src = src };
            _voices.Add(v);
            _free.Push(v);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── play ─────────────────────────────────────────────────────────────────────
    public CueHandle Play(SoundCueData cue, Vector3 position) => PlayInternal(cue, position, null, ui: false);
    public CueHandle Play(SoundCueData cue, Vector3 position, bool loop) => PlayInternal(cue, position, null, ui: false, loopOverride: loop);
    public CueHandle Play(SoundCueData cue, Transform follow)
        => PlayInternal(cue, follow != null ? follow.position : Vector3.zero, follow, ui: false);
    public CueHandle Play(SoundCueData cue, Transform follow, bool loop)
        => PlayInternal(cue, follow != null ? follow.position : Vector3.zero, follow, ui: false, loopOverride: loop);

    /// <summary>2D, pause-proof UI sound (ignoreListenerPause) — stays audible while paused (F4).</summary>
    public CueHandle PlayUI(SoundCueData cue) => PlayInternal(cue, Vector3.zero, null, ui: true);

    private CueHandle PlayInternal(SoundCueData cue, Vector3 pos, Transform follow, bool ui, bool? loopOverride = null)
    {
        if (cue == null) { Debug.LogError("[AudioManager] Play called with a null cue.", this); return CueHandle.None; }
        var clip = cue.PickClip();
        if (clip == null) { Debug.LogWarning($"[AudioManager] SoundCue '{cue.name}' has no clips.", cue); return CueHandle.None; }
        bool loop = loopOverride ?? cue.loop;   // per-use override (Cue Book element) wins over the asset default

 // Anti-spam cooldown (scaled time).
        if (cue.cooldown > 0f && _lastPlay.TryGetValue(cue, out var last) && Time.time - last < cue.cooldown)
            return CueHandle.None;

        // Per-cue simultaneous cap: steal this cue's oldest voice rather than exceed it.
        if (cue.maxSimultaneous > 0 && CountActive(cue) >= cue.maxSimultaneous && !ReclaimOldestOf(cue))
            return CueHandle.None;

        var voice = AcquireVoice();
        if (voice == null) return CueHandle.None;

        var src = voice.src;
        src.clip = clip;
        src.volume = cue.RandomVolume();
        src.pitch = cue.RandomPitch();
        src.outputAudioMixerGroup = cue.outputGroup != null ? cue.outputGroup : defaultGroup;
        src.loop = loop;
        bool spatial = cue.spatial && !ui;
        src.spatialBlend = spatial ? 1f : 0f;
        src.minDistance = cue.minDistance;
        src.maxDistance = cue.maxDistance;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.ignoreListenerPause = ui;                 // UI keeps sounding while paused
        src.transform.position = spatial ? pos : Vector3.zero;
        src.Play();

        voice.priority = cue.priority;
        voice.startUnscaled = Time.unscaledTime;
        voice.cue = cue;
        voice.follow = spatial ? follow : null;
        voice.loop = loop;
        voice.ui = ui;

        if (cue.cooldown > 0f) _lastPlay[cue] = Time.time;

        voice.handle = _registry.Register(voice);
        return voice.handle;
    }

    public bool Stop(CueHandle handle)
    {
        if (!_registry.IsValid(handle)) return false;
        ReclaimVoice(handle);
        return true;
    }

    public bool IsPlaying(CueHandle handle) => _registry.IsValid(handle);

    /// <summary>Stop every active voice (soft reset, F5). Music/ambience live elsewhere — untouched.</summary>
    public void StopAllSfx()
    {
        _registry.GetActive(_scratch);
        foreach (var kv in _scratch) ReclaimVoice(kv.Key);
    }

    // ── pause (sole AudioListener.pause writer — F4) ─────────────────────────────
    public void SetPaused(object owner)
    {
        if (owner == null) return;
        _pausers.Add(owner);
        AudioListener.pause = _pausers.Count > 0;
    }

    public void ReleasePaused(object owner)
    {
        if (owner == null) return;
        _pausers.Remove(owner);
        AudioListener.pause = _pausers.Count > 0;
    }

    // ── snapshot arbiter (sole TransitionTo caller — F3) ─────────────────────────
    public void RequestSnapshot(object owner, AudioSnapshotId id, int priority)
    {
        if (_snapshotArbiter.Request(owner, (int)id, priority, out var winner)) ApplySnapshot(winner);
    }

    public void ReleaseSnapshot(object owner)
    {
        if (_snapshotArbiter.Release(owner, out var winner)) ApplySnapshot(winner);
    }

    private void ApplySnapshot(int winnerId)
    {
        var id = winnerId == SnapshotArbiter.DefaultSnapshot ? AudioSnapshotId.Default : (AudioSnapshotId)winnerId;
        var snap = GetSnapshot(id);
        if (snap != null) snap.TransitionTo(snapshotTransition);   // real time by Unity design
    }

    private AudioMixerSnapshot GetSnapshot(AudioSnapshotId id)
    {
        int i = (int)id;
        return (snapshots != null && i >= 0 && i < snapshots.Length) ? snapshots[i] : null;
    }

    // ── voice bookkeeping ────────────────────────────────────────────────────────
    private Voice AcquireVoice()
    {
        if (_free.Count > 0) return _free.Pop();

        // Steal: lowest priority, then oldest.
        Voice victim = null;
        CueHandle victimHandle = default;
        _registry.GetActive(_scratch);
        foreach (var kv in _scratch)
        {
            var v = kv.Value;
            if (victim == null
                || v.priority < victim.priority
                || (v.priority == victim.priority && v.startUnscaled < victim.startUnscaled))
            {
                victim = v;
                victimHandle = kv.Key;
            }
        }
        if (victim == null) return null;
#if UNITY_EDITOR
        Debug.Log($"[AudioManager] Voice pool exhausted — stealing voice (priority {victim.priority}).");
#endif
        ReclaimVoice(victimHandle);
        return _free.Count > 0 ? _free.Pop() : null;
    }

    private void ReclaimVoice(CueHandle handle)
    {
        if (!_registry.TryGet(handle, out var v)) return;
        v.src.Stop();
        v.src.clip = null;
        v.cue = null;
        v.follow = null;
        _registry.Reclaim(handle);
        _free.Push(v);
    }

    private int CountActive(SoundCueData cue)
    {
        int n = 0;
        _registry.GetActive(_scratch);
        foreach (var kv in _scratch) if (kv.Value.cue == cue) n++;
        return n;
    }

    private bool ReclaimOldestOf(SoundCueData cue)
    {
        Voice oldest = null;
        CueHandle handle = default;
        _registry.GetActive(_scratch);
        foreach (var kv in _scratch)
            if (kv.Value.cue == cue && (oldest == null || kv.Value.startUnscaled < oldest.startUnscaled))
            {
                oldest = kv.Value;
                handle = kv.Key;
            }
        if (oldest == null) return false;
        ReclaimVoice(handle);
        return true;
    }

    private void Update()
    {
        if (_registry.ActiveCount == 0) return;

        _registry.GetActive(_scratch);
        _reclaim.Clear();
        bool paused = _pausers.Count > 0;
        foreach (var kv in _scratch)
        {
            var v = kv.Value;
            if (v.follow != null) v.src.transform.position = v.follow.position;
            // Reclaim finished one-shots (skip while paused — a paused source isn't "finished").
            if (!v.loop && !paused && !v.src.isPlaying) _reclaim.Add(kv.Key);
        }
        foreach (var h in _reclaim) ReclaimVoice(h);
    }
}
