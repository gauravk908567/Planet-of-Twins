using UnityEngine;

[CreateAssetMenu(menuName = "Game/Ability Data")]
public class AbilityData : ScriptableObject
{
    public float cooldown = 3f;
    public float duration = 2f;
    public float range = 5f;
    public int affectionLevel = 1;

    public void IncreaseDuration(float amount)
    {
        duration += amount;
    }

    public void ReduceCooldownPercent(float percent)
    {
        cooldown *= (1f - percent);
    }
}

