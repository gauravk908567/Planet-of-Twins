public class SkillNode
{
    public string Id { get; private set; }
    public int Cost { get; private set; }
    public int Level { get; private set; }
    public int MaxLevel { get; private set; }
    public string PrerequisiteId { get; private set; }

    private ISkillEffect effect;

    public SkillNode(
        string id,
        int cost,
        int maxLevel,
        string prerequisiteId,
        ISkillEffect effect)
    {
        Id = id;
        Cost = cost;
        MaxLevel = maxLevel;
        PrerequisiteId = prerequisiteId;
        this.effect = effect;
        Level = 0;
    }

    public bool CanLevelUp(int availablePoints, SkillNode prerequisiteNode)
    {
        if (Level >= MaxLevel)
            return false;

        if (availablePoints < Cost)
            return false;

        if (!string.IsNullOrEmpty(PrerequisiteId))
        {
            if (prerequisiteNode == null || prerequisiteNode.Level == 0)
                return false;
        }

        return true;
    }

    public void LevelUp()
    {
        Level++;
        effect?.Apply();
    }
}