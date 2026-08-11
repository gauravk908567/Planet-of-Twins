using UnityEngine;

/// <summary>
/// One music track for a world location. Config only (R7); MusicManager
/// owns playback (A/B crossfade on unscaled time).
/// </summary>
[CreateAssetMenu(menuName = "PlanetOfTwins/Fx/Music Track", fileName = "Music_")]
public class MusicTrackData : ScriptableObject
{
    public AudioClip clip;
    [Min(0f)] public float fadeInSeconds = 1.5f;
    [Min(0f)] public float fadeOutSeconds = 1.5f;
    public bool loop = true;
}
