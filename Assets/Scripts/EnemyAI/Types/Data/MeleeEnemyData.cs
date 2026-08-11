using UnityEngine;

[CreateAssetMenu(fileName = "MeleeEnemyData", menuName = "PlanetOfTwins/Enemy Data/Melee")]
public class MeleeEnemyData : EnemyData
{
    // Melee has no extra config beyond base.
    // Separate type prevents wrong SO being assigned to wrong prefab.
}
