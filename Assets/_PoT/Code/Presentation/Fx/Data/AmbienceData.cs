using UnityEngine;

/// <summary>
/// Ambience for a world location: a looping bed plus occasional one-shots.
/// Config only (R7). One-shot timing is UNSCALED so ambience keeps breathing during Setsuna.
/// </summary>
[CreateAssetMenu(menuName = "PlanetOfTwins/Fx/Ambience", fileName = "Ambience_")]
public class AmbienceData : ScriptableObject
{
    [Tooltip("Looping bed (wind, room tone). Null = no bed.")]
    public AudioClip bedLoop;

    [Tooltip("Occasional one-shots layered over the bed (bird calls, distant clanks). " +
             "Each is a normal SoundCueData, played through AudioManager.")]
    public SoundCueData[] randomOneShots;

    [Tooltip("Unscaled seconds between one-shots — ambience keeps breathing during Setsuna.")]
    public Vector2 oneShotIntervalRange = new Vector2(5f, 15f);
}
