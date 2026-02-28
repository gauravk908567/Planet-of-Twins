using System.Collections.Generic;

public class SkillTree
{
    private Dictionary<string, SkillNode> nodes =
        new Dictionary<string, SkillNode>();

    public void RegisterNode(SkillNode node)
    {
        nodes.Add(node.Data.skillID, node);
    }

    public bool Unlock(string skillID, ref int skillPoints)
    {
        if (!nodes.ContainsKey(skillID))
            return false;

        SkillNode node = nodes[skillID];

        if (node.IsUnlocked)
            return false;

        if (skillPoints < node.Data.cost)
            return false;

        foreach (var prereq in node.Data.prerequisites)
        {
            if (!nodes.ContainsKey(prereq.skillID) ||
                !nodes[prereq.skillID].IsUnlocked)
                return false;
        }

        skillPoints -= node.Data.cost;
        node.Unlock();
        return true;
    }
}