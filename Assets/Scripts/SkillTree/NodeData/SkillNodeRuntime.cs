public class SkillNodeRuntime
{
    public SkillNodeData Data { get; private set; }
    public int Level { get; private set; }

    public SkillNodeRuntime(SkillNodeData data)
    {
        Data = data;
        Level = 0;
    }

    public bool CanLevelUp(int availablePoints)
    {
        if (Level >= Data.maxLevel)
            return false;

        if (availablePoints < Data.cost)
            return false;

        foreach (var prereq in Data.prerequisites)
        {
            if (!SkillTreeController.Instance
                .IsNodeUnlocked(prereq.skillID))
                return false;
        }

        return true;
    }

    public void LevelUp()
    {
        Level++;
    }
}