using UnityEngine;

/// <summary>
/// ScriptableObject defining mood transition rules and modifiers for a specific enemy type.
/// Designer-facing — no code changes needed to add new mood behaviours.
///
/// Modifier values are set as plain floats — no curves needed for mood.
/// Mood is a snap system (clear phase shifts) not a blend system.
/// Stat interpolation on transition is handled by EnemyMoodSystem (0.3s lerp).
///
/// CREATE: Right-click → Create → PlanetOfTwins/AI/Mood Transition Profile
/// </summary>
[CreateAssetMenu(fileName = "MoodProfile",
                 menuName = "PlanetOfTwins/AI/Mood Transition Profile")]
public class MoodTransitionProfile : ScriptableObject
{
    [Header("Base Mood")]
    [Tooltip("Starting mood when enemy spawns.")]
    public EnemyMood defaultMood = EnemyMood.Normal;

    [Header("HP Thresholds (fraction of max HP)")]
    [Tooltip("Below this HP fraction → Cautious (if no timed mood is active).")]
    [Range(0f, 1f)] public float cautiousThreshold = 0.50f;
    [Tooltip("Below this HP fraction → Wounded (if no timed mood is active).")]
    [Range(0f, 1f)] public float woundedThreshold = 0.25f;

    [Header("Modifier Lerp")]
    [Tooltip("Seconds to interpolate stat values (speed, cooldown, anticipation) when mood changes.\n" +
             "Keeps transitions feeling smooth even though mood itself snaps.\n" +
             "0 = instant snap on all stats.")]
    [Range(0f, 1f)] public float modifierLerpDuration = 0.3f;

    [Header("Mood Modifiers — one entry per mood state used by this enemy")]
    [Tooltip("Speed, cooldown, anticipation multipliers per mood.\n" +
             "Only list moods this enemy actually uses — others fall back to Normal defaults.")]
    public MoodModifierEntry[] modifiers;

    [Header("Transition Rules — evaluated in order, first match wins")]
    public MoodTransitionRule[] rules;

    [Header("Contagion")]
    [Tooltip("How far mood events spread to nearby allies. 0 = no contagion.")]
    public float moodContagionRadius = 8f;
    [Tooltip("How much nearby ally rage boosts own utility score. 0-1.")]
    [Range(0f, 1f)] public float rageContagionStrength = 0.3f;
}

/// <summary>
/// Modifier values for a single mood state.
/// All values are multipliers — 1.0 = no change from base.
/// </summary>
[System.Serializable]
public class MoodModifierEntry
{
    public EnemyMood mood;

    [Tooltip("Multiplier on GOAP utility goal scores.\n" +
             "1.0 = no change. 1.3 = 30% more eager to pursue goals.\n" +
             "Applied as post-multiplier on top of utility curve score.")]
    public float utilityMultiplier = 1.0f;

    [Tooltip("Multiplier on movement speed.\n" +
             "0.7 = 30% slower. 1.3 = 30% faster.\n" +
             "Rule: ±20% = clearly noticeable. ±30% = dramatic.")]
    public float speedMultiplier = 1.0f;

    [Tooltip("Multiplier on attack cooldown.\n" +
             "< 1.0 = faster attacks. > 1.0 = slower attacks.\n" +
             "Floor: 0.65 (below this feels unfair). Ceiling: 1.5 (above feels broken).")]
    [Range(0.65f, 1.5f)] public float attackCooldownMult = 1.0f;

    [Tooltip("How well enemy predicts twin position. 0 = reacts only. 1.5 = strong lead.\n" +
             "This has more impact on difficulty than speed — invisible to player but felt.")]
    [Range(0f, 1.5f)] public float anticipationFactor = 0.5f;
}