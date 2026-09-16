using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    [SerializeField] private TwinBondManager bondManager;
    [SerializeField] private PlayerHealthComponent leftHealth;
    [SerializeField] private PlayerHealthComponent rightHealth;

    [SerializeField, Range(0, 3)] private int currentNode = 0;

    public int CurrentNode => currentNode;

    /// <summary>
    /// Called by skill tree UI when player purchases an upgrade node.
    /// GDD §8: nodes are 0–3 inclusive.
    /// </summary>
    public void SetUpgradeNode(int node)
    {
        currentNode = Mathf.Clamp(node, 0, 3);
        bondManager.SetUpgradeNode(currentNode);
        leftHealth.SetRegenUpgradeNode(currentNode);
        rightHealth.SetRegenUpgradeNode(currentNode);
    }
}
