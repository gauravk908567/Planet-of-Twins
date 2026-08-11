using UnityEngine;

/// <summary>
/// Marks this enemy as a clan soldier and stores its clan alignment.
/// Enemies without this component are not clan soldiers and are ignored
/// by BTActionAttackEnemy during clan war target selection.
///
/// PREFAB SETUP:
///   Add to any enemy prefab that participates in clan war.
///   Set Clan to Luminari or Vethara in Inspector.
///   Do NOT add to commanders — handled separately in Phase 7.
/// </summary>
public class ClanSoldier : MonoBehaviour
{
    [Tooltip("Which main clan this enemy belongs to.")]
    public ClanAlignment Clan;
}