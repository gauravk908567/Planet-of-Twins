using UnityEngine;

/// <summary>
/// A single partner option within a PairSpawnConfig.
/// Defines which prefab can partner, on which side, with what bond.
/// </summary>
[System.Serializable]
public class PairPartnerEntry
{
    [Tooltip("Partner enemy prefab.")]
    public GameObject partnerPrefab;

    [Tooltip("Partner EnemyData SO.")]
    public EnemyData partnerData;

    [Tooltip("Relative weight for this partner. Higher = more frequent.")]
    [Range(1, 10)]
    public int weight = 1;

    [Tooltip("Bond type assigned to both enemies when paired.")]
    public EnemySocialBond.BondType bondType = EnemySocialBond.BondType.ComboPartner;

    [Tooltip("True = partner spawns same side as primary. False = opposite side.")]
    public bool sameSide = false;

    [Tooltip("Probability (0-1) this entry is even considered. Rolls before weight.")]
    [Range(0f, 1f)]
    public float spawnProbability = 1f;
}