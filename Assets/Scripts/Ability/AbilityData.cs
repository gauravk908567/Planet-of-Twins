using UnityEngine;

[CreateAssetMenu(menuName = "Game/Ability Data")]
public class AbilityData : ScriptableObject
{
    public float cooldown = 3f;
    public float duration = 2f;
    public float range = 5f;
}