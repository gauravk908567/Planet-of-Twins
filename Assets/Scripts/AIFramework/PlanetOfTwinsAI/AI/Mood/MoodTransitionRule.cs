using UnityEngine;

/// <summary>
/// A single rule that defines when a mood transition occurs.
/// Rules are evaluated in order — first match wins.
/// Uses MoodVFXTag enum — no string mismatch errors.
/// </summary>
[System.Serializable]
public class MoodTransitionRule
{
    [Header("Trigger")]
    [Tooltip("Social event that triggers this rule.")]
    public EnemySocialEvent triggerEvent;
    public bool useEventTrigger = false;

    [Header("Conditions (all must be true)")]
    [Range(0f, 1f)] public float maxOwnHP = 1.0f;
    [Range(0f, 1f)] public float minOwnHP = 0.0f;
    [Range(0f, 1f)] public float minEnergy = 0.0f;
    [Tooltip("Max range from event source. 0 = no range check.")]
    public float maxEventRange = 0f;

    [Tooltip("Moods that BLOCK this rule from firing. Empty = fires from any mood.\n" +
             "Use this instead of listing every allowed mood — just block the 1-2 exceptions.")]
    public EnemyMood[] excludeMoods = new EnemyMood[0];

    [Header("Result")]
    public EnemyMood targetMood;
    [Tooltip("Duration in seconds. 0 = permanent until next rule fires.")]
    public float duration = 0f;
    [Tooltip("Mood to fall back to after duration expires.")]
    public EnemyMood decayMood = EnemyMood.Normal;

    [Header("Response")]
    [Tooltip("VFX reaction to play on transition.")]
    public MoodVFXTag vfxTag = MoodVFXTag.None;
    public bool playReactionAnim = false;
}