using System.Collections.Generic;

public class SkillTree
{
    private Dictionary<string, SkillNode> nodes =
        new Dictionary<string, SkillNode>();

    public void RegisterNode(SkillNode node)
    {
        if (!nodes.ContainsKey(node.Id))
            nodes.Add(node.Id, node);
    }

    public SkillNode GetNode(string id)
    {
        nodes.TryGetValue(id, out SkillNode node);
        return node;
    }

    public bool TryLevelUp(string id, ref int skillPoints)
    {
        if (!nodes.ContainsKey(id))
            return false;

        SkillNode node = nodes[id];

        SkillNode prerequisiteNode = null;

        if (!string.IsNullOrEmpty(node.PrerequisiteId))
        {
            nodes.TryGetValue(node.PrerequisiteId, out prerequisiteNode);
        }

        if (!node.CanLevelUp(skillPoints, prerequisiteNode))
            return false;

        skillPoints -= node.Cost;
        node.LevelUp();

        return true;
    }

    public Dictionary<string, SkillNode> GetAllNodes()
    {
        return nodes;
    }
}