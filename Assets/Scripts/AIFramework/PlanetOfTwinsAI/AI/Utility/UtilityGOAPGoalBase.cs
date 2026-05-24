using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Base class for all utility-scored GOAP goals.
/// Replaces fixed GoalPriority with a dynamic float score
/// calculated from weighted CurveShape factors.
///
/// USAGE:
///   Extend this instead of GOAPGoalBase.
///   Assign a UtilityWeightProfile SO in Inspector.
///   Override OnAdditionalScore() to add goal-specific bonuses.
/// </summary>
public abstract class UtilityGOAPGoalBase : GOAPGoalBase
{
    [SerializeField] protected UtilityWeightProfile _utilityProfile;

    private EnemyMoodSystem _moodSystem;

    private void Awake()
    {
        _moodSystem = GetComponent<EnemyMoodSystem>();
    }

    public override void PrepareForPlanning()
    {
        if (_utilityProfile == null)
        {
            Priority = GoalPriority.DoNotRun;
            return;
        }

        float score = CalculateScore();

        // Apply mood multiplier on top of curve score
        if (_utilityProfile.applyMoodMultiplier && _moodSystem != null)
            score *= _moodSystem.CurrentModifiers.utilityMultiplier;

        // Goal-specific bonus from subclass
        score += OnAdditionalScore();

        Priority = score < _utilityProfile.activationThreshold
            ? GoalPriority.DoNotRun
            : (int)Mathf.Clamp(score, 0f, 100f);
    }

    /// <summary>Override in subclass to add goal-specific score bonuses.</summary>
    protected virtual float OnAdditionalScore() => 0f;

    private float CalculateScore()
    {
        if (_utilityProfile.factors == null || _utilityProfile.factors.Length == 0)
            return 0f;

        float total = 0f;
        float maxPossible = 0f;

        foreach (var factor in _utilityProfile.factors)
        {
            maxPossible += factor.weight;
            float rawValue = ReadBlackboardValue(factor);
            total += factor.Evaluate(rawValue);
        }

        // Normalise to 0-100
        return maxPossible > 0f ? (total / maxPossible) * 100f : 0f;
    }

    private float ReadBlackboardValue(UtilityFactor factor)
    {
        if (string.IsNullOrEmpty(factor.blackboardKey)) return 0f;

        var key = new FastName(factor.blackboardKey);

        if (factor.isBool)
        {
            bool val = false;
            LinkedBlackboard.TryGet(key, out val, false);
            return val ? 1f : 0f;
        }

        float fval = 0f;
        LinkedBlackboard.TryGet(key, out fval, 0f);
        return Mathf.Clamp01(fval);
    }
}