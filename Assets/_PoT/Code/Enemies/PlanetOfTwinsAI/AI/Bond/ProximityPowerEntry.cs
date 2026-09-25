using UnityEngine;

/// <summary>
/// Defines a single combo power variant.
/// Multiple entries per profile — evaluated top to bottom, first match wins.
/// Designer controls priority by reordering in Inspector.
/// Use [ComboPowerID] dropdown for powerID — no manual string typing.
/// </summary>
[System.Serializable]
public class ProximityPowerEntry
{
    [Header("Partner Requirement")]
    [Tooltip("This combo activates when near this clan type.")]
    public AlliedClanType requiredPartnerClan;

    [Header("Execution Range")]
    [Tooltip("Both enemies must be within this distance of each other to execute.")]
    public float executionRange = 8f;

    [Header("Dark Energy Requirements")]
    [Tooltip("Both enemies must have at least this dark energy to activate.")]
    [Range(0f, 1f)] public float minDarkEnergyBoth = 0.6f;

    [Header("Power")]
    [Tooltip("ID read by BTActionComboAttack to select attack type. Pick from dropdown.")]
    [ComboPowerID]
    public string powerID = "";

    [Tooltip("Damage/effect multiplier for this combo.")]
    [Range(0f, 2f)] public float powerStrength = 1.0f;

    [Header("Conditional Selection")]
    [Tooltip("Only activate when twin HP is below this. 1.0 = always.")]
    [Range(0f, 1f)] public float maxTwinHPNorm = 1.0f;
    [Tooltip("Only activate when twin HP is above this. 0.0 = always.")]
    [Range(0f, 1f)] public float minTwinHPNorm = 0.0f;
    [Tooltip("Only activate within this distance of twin. 999 = always.")]
    public float maxRangeToTwin = 999f;
    [Tooltip("Only activate beyond this distance of twin. 0 = always.")]
    public float minRangeToTwin = 0f;
    [Tooltip("Partner must be Enraged or Aggressive to activate.")]
    public bool requiresPartnerRaging = false;
    [Tooltip("This is the fallback — no conditions, always valid if partner present.")]
    public bool isFallback = false;
}