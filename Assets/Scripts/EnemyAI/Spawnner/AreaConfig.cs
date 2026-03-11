using UnityEngine;


[CreateAssetMenu(
    fileName = "AreaZoneConfig",
    menuName = "PlanetOfTwins/Area Zone Config")]
public class AreaZoneConfig : ScriptableObject
{
    [Tooltip("Display name — shown in Inspector and debug logs")]
    public string areaName = "New Area";

    [Header("Enemy Sets")]
    public SideSpawnConfig leftSide;
    public SideSpawnConfig rightSide;

    public SideSpawnConfig GetSideConfig(SpawnSide side)
        => side == SpawnSide.Left ? leftSide : rightSide;
}
