using System.Collections.Generic;
using UnityEngine;

public class SkillTreeUI : MonoBehaviour
{
    [SerializeField] private SkillTreeController controller;

    [Header("Panels")]
    [SerializeField] private Transform sharedPanel;
    [SerializeField] private Transform leftPanel;
    [SerializeField] private Transform rightPanel;

    [Header("Prefab")]
    [SerializeField] private SkillNodeUI nodePrefab;
    private List<SkillNodeUI> allNodes = new List<SkillNodeUI>();
    private void Start()
    {
        BuildTree();
    }

    private void BuildTree()
    {
        foreach (var data in controller.GetAllNodeData())
        {
            Transform parent = GetParentPanel(data.branch);

            var nodeUI = Instantiate(nodePrefab, parent);
            allNodes.Add(nodeUI);
            nodeUI.SetOnUpdate(RefreshNodeUI);
            nodeUI.Initialize(data, controller);
        }
    }

    private void RefreshNodeUI()
    {
        foreach (var node in allNodes)
        {
            node.Refresh();
        }
    }

    private Transform GetParentPanel(SkillBranch branch)
    {
        return branch switch
        {
            SkillBranch.Shared => sharedPanel,
            SkillBranch.Left => leftPanel,
            SkillBranch.Right => rightPanel,
            _ => sharedPanel
        };
    }
}