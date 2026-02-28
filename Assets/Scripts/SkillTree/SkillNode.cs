public class SkillNode
{
    public SkillNodeData Data { get; private set; }
    public bool IsUnlocked { get; private set; }

    private ISkillEffect effect;

    public SkillNode(SkillNodeData data, ISkillEffect effect)
    {
        Data = data;
        this.effect = effect;
    }

    public void Unlock()
    {
        if (IsUnlocked)
            return;

        IsUnlocked = true;
        effect?.Apply();
    }
}