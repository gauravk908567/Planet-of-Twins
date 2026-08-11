// BirdAnimEventReceiver.cs
// Attach alongside BirdWaypointHopper.cs on your Blue Jay

using UnityEngine;

[RequireComponent(typeof(BirdWaypointHopper))]
[RequireComponent(typeof(AudioSource))]
public class BirdAnimEventReceiver : MonoBehaviour
{
    [Header("Idle / Singing Sounds")]
    public AudioClip[] SongClips;        // plays during sing animation
    public AudioClip[] ChirpClips;       // short random chirps during idle

    [Header("Action Sounds")]
    public AudioClip[] HopClips;         // plays on each hop
    public AudioClip PeckClip;           // plays during peck
    public AudioClip RuffleClip;         // plays during ruffle
    public AudioClip PreenClip;          // plays during preen
    public AudioClip WorriedClip;        // plays during worried

    [Header("Fly Sounds")]
    public AudioClip[] FlyClips;         // wing flap on takeoff
    public AudioClip LandingClip;        // plays on landing

    [Range(0f, 1f)] public float Volume = 1f;

    private AudioSource _audio;
    private BirdWaypointHopper _hopper;

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
        _hopper = GetComponent<BirdWaypointHopper>();
        _audio.spatialBlend = 1f; // 3D
    }

    // ─────────────────────────────────────
    // Called by sing animation event
    void PlaySong()
    {
        PlayRandom(SongClips);
    }

    // Called by hop animations
    void PlayHop()
    {
        PlayRandom(HopClips);
    }

    // Called by peck animation
    void PlayPeck()
    {
        Play(PeckClip);
    }

    // Called by ruffle animation
    void PlayRuffle()
    {
        Play(RuffleClip);
    }

    // Called by preen animation
    void PlayPreen()
    {
        Play(PreenClip);
    }

    // Called by worried animation
    void PlayWorried()
    {
        Play(WorriedClip);
    }

    // Called by fly animation
    void PlayFly()
    {
        PlayRandom(FlyClips);
    }

    // Called by landing animation
    void PlayLanding()
    {
        Play(LandingClip);
    }

    // ─────────────────────────────────────
    // These silence warnings from lb_Bird baked events
    // that we don't need but are in the animation clips
    void ResetHopInt() { }
    void ResetFlyingLandingVariables() { }

    // ─────────────────────────────────────
    void Play(AudioClip clip)
    {
        if (clip == null || _audio == null) return;
        _audio.PlayOneShot(clip, Volume);
    }

    void PlayRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0 || _audio == null) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) _audio.PlayOneShot(clip, Volume);
    }
}