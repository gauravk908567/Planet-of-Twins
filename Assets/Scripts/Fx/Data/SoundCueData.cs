using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// One sound event. Played via <c>AudioManager</c> (arriving in P9.2); a leaf
/// step inside a sequence dispatches here through <c>FxManager.Play</c>. Raw <c>AudioSource.Play</c>
/// at a call site is Banned Lazy Work #10.
/// </summary>
[CreateAssetMenu(menuName = "PlanetOfTwins/Fx/Sound Cue", fileName = "Cue_Sound")]
public class SoundCueData : CueData
{
    [Tooltip("One clip is picked at random per play (cheap variation).")]
    public AudioClip[] clips;

    [Tooltip("Randomized per play.")] public Vector2 volumeRange = Vector2.one;
    [Tooltip("Randomized per play.")] public Vector2 pitchRange = Vector2.one;

    public AudioMixerGroup outputGroup;

    [Header("Spatialisation")]
    [Tooltip("True = 3D positional; false = 2D (UI/global).")]
    public bool spatial;
    [Min(0f)] public float minDistance = 1f;
    [Min(0f)] public float maxDistance = 30f;

    [Header("Behaviour")]
    public bool loop;
    [Tooltip("Higher steals later when all voices are busy (lowest priority stolen first).")]
    public int priority = 128;
    [Tooltip("Anti-spam: a re-trigger within this window is dropped (scaled seconds).")]
    [Min(0f)] public float cooldown;
    [Tooltip("0 = unlimited simultaneous voices of this cue.")]
    [Min(0)] public int maxSimultaneous;

    /// <summary>Random clip (or null if none assigned).</summary>
    public AudioClip PickClip()
        => (clips != null && clips.Length > 0) ? clips[Random.Range(0, clips.Length)] : null;

    public float RandomVolume() => Random.Range(volumeRange.x, volumeRange.y);
    public float RandomPitch() => Random.Range(pitchRange.x, pitchRange.y);

    public override float GetDuration()
    {
        if (loop) return 0f;
        var c = (clips != null && clips.Length > 0) ? clips[0] : null;
        return c != null ? c.length : 0f;
    }
}
