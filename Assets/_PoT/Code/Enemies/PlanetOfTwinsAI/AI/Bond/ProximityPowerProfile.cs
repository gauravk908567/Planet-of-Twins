using UnityEngine;

/// <summary>
/// ScriptableObject defining combo powers and partner preferences for an enemy type.
///
/// preferredPartnerOrder — enemy prefers to pact with the first type in this list.
/// When multiple compatible allies are nearby, picks the highest-priority available one.
/// Partner search is passive — no special movement, just preference when choosing.
///
/// comboPowers — evaluated top to bottom, first match wins.
/// Most conditional entries go first. isFallback entry goes last.
///
/// CREATE: Right-click → Create → PlanetOfTwins/AI/Proximity Power Profile
/// </summary>
[CreateAssetMenu(fileName = "ProximityPowerProfile",
                 menuName = "PlanetOfTwins/AI/Proximity Power Profile")]
public class ProximityPowerProfile : ScriptableObject
{
    [Header("Partner Preference — highest priority first")]
    [Tooltip("When multiple compatible allies are nearby, prefer partners in this order.\n" +
             "Empty = no preference, pact with whoever is closest.")]
    public AlliedClanType[] preferredPartnerOrder;

    [Header("Combo Powers — evaluated top to bottom, first match executes")]
    public ProximityPowerEntry[] comboPowers;
}