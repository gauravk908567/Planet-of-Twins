using TMPro;
using UnityEngine;

public class FactionComponent : MonoBehaviour
{
    [SerializeField] private Faction faction;
    [SerializeField] private TMP_Text currentFaction;


    public Faction CurrentFaction
    {
        get => faction;
        set
        {
            faction = value;
            if (currentFaction != null)
                currentFaction.text = faction.ToString();
        }
    }
}