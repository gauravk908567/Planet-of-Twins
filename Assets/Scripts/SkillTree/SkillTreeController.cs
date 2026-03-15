using UnityEngine;
using System.Collections.Generic;

public class SkillTreeController : MonoBehaviour
{
    public static SkillTreeController Instance;

    [Header("Skill Database")]
    [SerializeField] private SkillNodeData[] allNodes;

    private Dictionary<string, SkillNodeRuntime> runtimeNodes = new Dictionary<string, SkillNodeRuntime>();

    public int SharedPoints;
    public int LeftPoints;
    public int RightPoints;

    [Header("Abilities")]
    [SerializeField] private AbilityData stunAbility;
    [SerializeField] private AbilityData possessionAbility;
    [SerializeField] private AbilityData gateAbility;
    [SerializeField] private TwinBondManager bondManager;

    private void Awake()
    {
        Instance = this;
        InitializeNodes();
    }

    private void InitializeNodes()
    {
        foreach (var data in allNodes)
        {
            runtimeNodes[data.skillID] = new SkillNodeRuntime(data);
        }
    }

    public bool IsNodeUnlocked(string id)
    {
        if (!runtimeNodes.ContainsKey(id))
            return false;

        return runtimeNodes[id].Level > 0;
    }

    public void TryLevelUp(string id)
    {
        if (!runtimeNodes.ContainsKey(id))
            return;

        var node = runtimeNodes[id];

        int points = GetPointsByBranch(node.Data.branch);

        if (!node.CanLevelUp(points))
            return;

        DeductPoints(node.Data.branch, node.Data.cost);

        node.LevelUp();
        ApplyEffect(node.Data);
    }

    private int GetPointsByBranch(SkillBranch branch)
    {
        return branch switch
        {
            SkillBranch.Shared => SharedPoints,
            SkillBranch.Left => LeftPoints,
            SkillBranch.Right => RightPoints,
            _ => 0
        };
    }

    private void DeductPoints(SkillBranch branch, int cost)
    {
        switch (branch)
        {
            case SkillBranch.Shared: SharedPoints -= cost; break;
            case SkillBranch.Left: LeftPoints -= cost; break;
            case SkillBranch.Right: RightPoints -= cost; break;
        }
    }

    private void ApplyEffect(SkillNodeData data)
    {
        switch (data.effectType)
        {
            case SkillEffectType.StunDuration:
                stunAbility.IncreaseDuration(data.value);
                break;

            case SkillEffectType.StunCooldown:
                stunAbility.ReduceCooldownPercent(data.value);
                break;

            case SkillEffectType.PossessionDuration:
                possessionAbility.IncreaseDuration(data.value);
                break;

            case SkillEffectType.PossessionCooldown:
                possessionAbility.ReduceCooldownPercent(data.value);
                break;

            case SkillEffectType.GateDuration:
                gateAbility.IncreaseDuration(data.value);
                break;

            case SkillEffectType.GateCooldown:
                gateAbility.ReduceCooldownPercent(data.value);
                break;

            case SkillEffectType.BondUpgrade:
                bondManager.IncreaseUpgradeLevel((int)data.value);
                break;
        }
    }

    public SkillNodeRuntime GetRuntimeNode(string id)
    {
        runtimeNodes.TryGetValue(id, out var node);
        return node;
    }

    public bool CanLevelUp(string id)
    {
        if (!runtimeNodes.ContainsKey(id))
            return false;

        var node = runtimeNodes[id];
        int points = GetPointsByBranch(node.Data.branch);

        return node.CanLevelUp(points);
    }

    public SkillNodeData[] GetAllNodeData()
    {
        return allNodes;
    }
}