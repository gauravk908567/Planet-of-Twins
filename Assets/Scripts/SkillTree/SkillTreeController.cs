using UnityEngine;
using System.Collections.Generic;

public class SkillTreeController : MonoBehaviour
{
    private Dictionary<string, SkillNode> nodes =
        new Dictionary<string, SkillNode>();

    public int SkillPoints { get; private set; }

    public void AddSkillPoints(int amount)
    {
        SkillPoints += amount;
    }

    public void RegisterNode(SkillNode node)
    {
        nodes.Add(node.Data.skillID, node);
    }

    public void Unlock(string skillID)
    {
        if (!nodes.ContainsKey(skillID))
            return;

        SkillNode node = nodes[skillID];

        if (node.IsUnlocked)
            return;

        if (SkillPoints < node.Data.cost)
            return;

        // Check prerequisites
        foreach (var prereq in node.Data.prerequisites)
        {
            if (!nodes[prereq.skillID].IsUnlocked)
                return;
        }

        SkillPoints -= node.Data.cost;
        node.Unlock();
    }
}