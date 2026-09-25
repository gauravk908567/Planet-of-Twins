using UnityEngine;

/// <summary>
/// ScriptableObject defining how a GOAP goal scores itself using weighted curves.
/// Designer draws AnimationCurves in Inspector — no code changes to tune behaviour.
///
/// CREATE: Right-click → Create → PlanetOfTwins/AI/Utility Weight Profile
///
/// EXAMPLE — AttackTwin profile:
///   Factor: Awareness_BestTarget  bool  weight=50  (binary — can I see a twin?)
///   Factor: EnemyHealthNorm       curve weight=30  (exponential — full HP = confident)
///   Factor: AllyCountNorm         curve weight=20  (log — more allies = more eager)
///   Factor: SharedHealthNorm      curve weight=25  (inverted — twins weak = press harder)
///   Factor: FactionEnergyNorm     curve weight=15  (linear — energy up = more aggressive)
///   Factor: TargetIsEngaged       bool  weight=20  (flat — ally has twin pinned = opportunity)
///   Factor: EnemyMoodMultiplier   curve weight=0   (mood applied as post-multiplier)
/// </summary>
[CreateAssetMenu(fileName = "UtilityProfile",
                 menuName = "PlanetOfTwins/AI/Utility Weight Profile")]
public class UtilityWeightProfile : ScriptableObject
{
    [Header("Score Threshold")]
    [Tooltip("Minimum total score for goal to be considered active. Below this = DoNotRun.")]
    public float activationThreshold = 30f;

    [Tooltip("Score is multiplied by mood modifier from EnemyMoodSystem.")]
    public bool applyMoodMultiplier = true;

    [Header("Factors")]
    public UtilityFactor[] factors;
}