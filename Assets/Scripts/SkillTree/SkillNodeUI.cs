using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SkillNodeUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image lockOverlay;

    private SkillNodeData nodeData;
    private SkillTreeController controller;
    private Action onUpdate;
    public void Initialize(
        SkillNodeData data,
        SkillTreeController controller)
    {
        nodeData = data;
        this.controller = controller;

        titleText.text = data.displayName;
        costText.text = "Cost: " + data.cost;

        button.onClick.AddListener(OnClick);

        Refresh();
    }

    private void OnClick()
    {
        controller.TryLevelUp(nodeData.skillID);
        onUpdate?.Invoke();
        Refresh();
    }

    public void Refresh()
    {
        var runtime = controller.GetRuntimeNode(nodeData.skillID);

        levelText.text = runtime.Level + " / " + nodeData.maxLevel;

        bool unlocked = runtime.Level > 0;

        lockOverlay.gameObject.SetActive(!CanUnlock());

        button.interactable = CanUnlock();
    }

    private bool CanUnlock()
    {
        return controller.CanLevelUp(nodeData.skillID);
    }

    public void SetOnUpdate(Action onUpdate)
    {
        this.onUpdate = onUpdate;
    }
}