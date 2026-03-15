using UnityEngine;

public enum SkillBranch
{
    Shared,
    Left,
    Right
}

public enum SkillEffectType
{
    StunDuration,
    StunCooldown,
    PossessionDuration,
    PossessionCooldown,
    GateDuration,
    GateCooldown,
    BondUpgrade
}

[CreateAssetMenu(menuName = "Game/Skill Node")]
public class SkillNodeData : ScriptableObject
{
    [Header("Identity")]
    public string skillID;
    public string displayName;

    [Header("Branch")]
    public SkillBranch branch;

    [Header("Progression")]
    public int cost = 1;
    public int maxLevel = 1;

    public SkillNodeData[] prerequisites;

    [Header("Effect")]
    public SkillEffectType effectType;
    public float value; // increase amount or percent
}