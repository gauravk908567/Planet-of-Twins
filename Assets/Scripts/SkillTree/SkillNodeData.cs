using UnityEngine;

[CreateAssetMenu(menuName = "Game/Skill Node")]
public class SkillNodeData : ScriptableObject
{
    public string skillID;
    public string displayName;
    public int cost = 1;

    public SkillNodeData[] prerequisites;
}