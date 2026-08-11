using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Persistent singleton — music + ambience. Two <c>AudioSource</c>s crossfade
/// music on UNSCALED time (so Setsuna/pause never warp the fade), driven by
/// <c>SceneFlowManager.OnActiveLocationChanged</c> (R8 named handler). A third source loops the area
/// ambience bed; a coroutine fires the ambience one-shots through <c>AudioManager</c>. No-op when the
/// incoming track/bed equals what is already playing.
///
/// R3: scene-resident — duplicate-destroy Awake, <c>Instance</c> nulled in OnDestroy, no DDOL.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Routing")]
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup ambienceGroup;
    [Tooltip("The scene streamer, as IFxSceneEvents (P19 seam 1 — drag SceneFlowManager here; R1 same-scene).")]
    [UnityEngine.Serialization.FormerlySerializedAs("sceneFlow")]
    [SerializeField] private MonoBehaviour sceneFlowMono;   // → IFxSceneEvents
    private IFxSceneEvents _sceneEvents;

    private AudioSource _musicA, _musicB, _ambience;
    private AudioSource _activeMusic, _idleMusic;
    private MusicTrackData _currentTrack;
    private AmbienceData _currentAmbience;
    private Coroutine _fade, _oneShots;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _musicA = MakeSource("Music_A", musicGroup);
        _musicB = MakeSource("Music_B", musicGroup);
        _ambience = MakeSource("Ambience_Bed", ambienceGroup);
        _ambience.loop = true;
        _activeMusic = _musicA;
        _idleMusic = _musicB;
    }

    private void Start()
    {
        _sceneEvents = sceneFlowMono as IFxSceneEvents;                 // R1 slot → package interface (P19 seam 1)
        if (_sceneEvents != null)
        {
            _sceneEvents.OnLocationAudioChanged += HandleLocationAudioChanged;   // R8 named handler
            if (_sceneEvents.CurrentMusicTrack != null || _sceneEvents.CurrentAmbience != null)
                HandleLocationAudioChanged(_sceneEvents.CurrentMusicTrack, _sceneEvents.CurrentAmbience);
        }
        else
        {
            Debug.LogWarning("[MusicManager] Scene-events slot unwired (needs an IFxSceneEvents, e.g. SceneFlowManager) — music/ambience won't follow areas.", this);
        }
    }

    private void OnDestroy()
    {
        if (_sceneEvents != null) _sceneEvents.OnLocationAudioChanged -= HandleLocationAudioChanged;
        if (Instance == this) Instance = null;
    }

    private void HandleLocationAudioChanged(MusicTrackData music, AmbienceData ambience)
    {
        PlayMusic(music);
        PlayAmbience(ambience);
    }

    // ── music ────────────────────────────────────────────────────────────────────
    public void PlayMusic(MusicTrackData track)
    {
        if (track == _currentTrack) return;            // no-op when unchanged
        float outDur = _currentTrack != null ? _currentTrack.fadeOutSeconds : (track != null ? track.fadeInSeconds : 1f);
        _currentTrack = track;
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(CrossfadeMusic(track, outDur));
    }

    private IEnumerator CrossfadeMusic(MusicTrackData track, float outDur)
    {
        var fadeOut = _activeMusic;
        var fadeIn = _idleMusic;

        if (track != null && track.clip != null)
        {
            fadeIn.clip = track.clip;
            fadeIn.loop = track.loop;
            fadeIn.volume = 0f;
            fadeIn.Play();
        }

        float inDur = track != null ? track.fadeInSeconds : 0f;
        float dur = Mathf.Max(0.01f, Mathf.Max(outDur, inDur));
        float startOut = fadeOut.volume;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;               // R10 — music ignores timeScale
            float k = Mathf.Clamp01(t / dur);
            fadeOut.volume = Mathf.Lerp(startOut, 0f, k);
            if (track != null) fadeIn.volume = Mathf.Lerp(0f, 1f, k);
            yield return null;
        }

        fadeOut.Stop();
        fadeOut.volume = 0f;
        fadeOut.clip = null;
        if (track != null) { fadeIn.volume = 1f; _activeMusic = fadeIn; _idleMusic = fadeOut; }
        _fade = null;
    }

    // ── ambience ───────────────────────────────────────────────────────────────────
    public void PlayAmbience(AmbienceData ambience)
    {
        if (ambience == _currentAmbience) return;      // no-op when unchanged
        _currentAmbience = ambience;

        if (_oneShots != null) { StopCoroutine(_oneShots); _oneShots = null; }

        if (ambience != null && ambience.bedLoop != null)
        {
            if (_ambience.clip != ambience.bedLoop)
            {
                _ambience.clip = ambience.bedLoop;
                _ambience.volume = 1f;
                _ambience.Play();
            }
        }
        else
        {
            _ambience.Stop();
            _ambience.clip = null;
        }

        if (ambience != null && ambience.randomOneShots != null && ambience.randomOneShots.Length > 0)
            _oneShots = StartCoroutine(OneShotLoop(ambience));
    }

    private IEnumerator OneShotLoop(AmbienceData ambience)
    {
        while (true)
        {
            float wait = Random.Range(ambience.oneShotIntervalRange.x, ambience.oneShotIntervalRange.y);
            float t = 0f;
            while (t < wait) { t += Time.unscaledDeltaTime; yield return null; }   // unscaled — breathes during Setsuna

            var cue = ambience.randomOneShots[Random.Range(0, ambience.randomOneShots.Length)];
            if (cue != null) AudioManager.Instance?.Play(cue, transform.position);
        }
    }

    private AudioSource MakeSource(string name, AudioMixerGroup group)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;                          // music/ambience are 2D
        src.outputAudioMixerGroup = group;
        return src;
    }
}
