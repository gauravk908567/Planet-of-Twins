using UnityEngine;

/// <summary>
/// Plays a looping, spatial ambient sound for a single world object — a crack's dark-energy buzz, a
/// flame's crackle — through the pooled <see cref="AudioManager"/>, NEVER a raw AudioSource. Optionally
/// proximity-gated so it only claims one of AudioManager's 32 voices while the listener is within earshot,
/// then frees it; the held voice is also stopped on disable, so pooled/streamed objects never leak a voice.
///
/// Author the loop on a <see cref="SoundCueData"/>: <c>loop = true</c>, <c>spatial = true</c>, min/max
/// distance set, an Ambience mixer group, and a LOW priority (so gameplay SFX steal this, not vice versa —
/// a stolen voice is re-acquired on the next check). For DENSE static set-dressing where even gating can't
/// keep the active count under the voice budget, a plain prefab AudioSource on the Ambience group is the
/// pragmatic alternative.
/// </summary>
[DisallowMultipleComponent]
public class AmbientLoopEmitter : MonoBehaviour
{
    [Tooltip("Looping spatial SoundCueData (loop = true, spatial = true, min/max distance, Ambience group).")]
    [SerializeField] private SoundCueData _loop;

    [Tooltip("Listener must be within this distance for the loop to play (frees the voice when far). " +
             "0 = always on (no proximity gate) — fine when there are only a few emitters.")]
    [Min(0f)] [SerializeField] private float _hearingRadius = 25f;

    [Tooltip("Seconds (unscaled) between proximity checks.")]
    [Min(0.1f)] [SerializeField] private float _checkInterval = 0.4f;

    [Tooltip("Optional explicit AudioManager (R1, Persistent). Resolves to .Instance otherwise (R4).")]
    [SerializeField] private AudioManager _audio;

    private CueHandle _handle = CueHandle.None;
    private Transform _listener;
    private float _timer;

    private void OnEnable() => _timer = 0f;   // re-evaluate on the next Update tick
    private void OnDisable() => StopLoop();

    private void Update()
    {
        // Unscaled — the gating shouldn't slow with Setsuna; the voice itself follows the cue's timeMode.
        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;
        _timer = _checkInterval;

        _audio ??= AudioManager.Instance;   // R4
        if (_audio == null || _loop == null) return;

        bool wantOn = _hearingRadius <= 0f || ListenerInRange();
        bool playing = !_handle.IsNone && _audio.IsPlaying(_handle);

        if (wantOn && !playing) _handle = _audio.Play(_loop, transform);  // start, or re-acquire a stolen voice
        else if (!wantOn && playing) StopLoop();
    }

    private void StopLoop()
    {
        if (_handle.IsNone) return;
        (_audio ?? AudioManager.Instance)?.Stop(_handle);
        _handle = CueHandle.None;
    }

    private bool ListenerInRange()
    {
        if (_listener == null)   // re-resolves if the listener was destroyed (scene reload)
        {
            var al = FindFirstObjectByType<AudioListener>();
            _listener = al != null ? al.transform : (Camera.main != null ? Camera.main.transform : null);
            if (_listener == null) return false;
        }
        return (_listener.position - transform.position).sqrMagnitude <= _hearingRadius * _hearingRadius;
    }
}
