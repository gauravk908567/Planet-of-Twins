using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
// SkillNodeButton
//
// One row in the skill tree panel. Shows node name, cost, and a stat preview
// line ("Duration  3.00s → 3.90s"). Changes colour based on state.
//
// STATES:
//   Purchased     — green bg, checkmark, locked out (already bought)
//   NextAffordable — blue bg, interactable, shows stat preview
//   NextLocked    — dark bg, shows cost, player can't afford
//   Locked        — dark bg, "unlock previous node first"
//   Maxed         — gold bg, "FULLY UPGRADED"
//
// PREFAB SETUP (create once, reused for every node):
//   Root:   Image (height 64) + Button component + SkillNodeButton.cs
//   Children:
//     NodeLabel  TMP_Text   bold, left-aligned       → drag to NodeLabel
//     StatLine   TMP_Text   small, muted, left        → drag to StatLine
//     CostBadge  TMP_Text   bold, right-aligned       → drag to CostBadge
//     LockIcon   Image      padlock sprite, right      → drag to LockIcon
//
// SkillTreeUI instantiates this prefab per node and calls Initialise().
// ─────────────────────────────────────────────────────────────────────────────
[RequireComponent(typeof(Button))]
public class SkillNodeButton : MonoBehaviour
{
    [Header("Scene assignment — set these in the Inspector per button")]
    [SerializeField] public AbilityUpgradeData NodeData;
    [SerializeField] public int NodeIndex;

    [Header("UI References — assign in prefab")]
    public TMP_Text NodeLabel;
    public TMP_Text StatLine;
    public TMP_Text CostBadge;
    public Image Background;
    public Image LockIcon;

    // ── Colours ───────────────────────────────────────────────────────────────
    static readonly Color BgPurchased = new Color(0.08f, 0.20f, 0.12f);
    static readonly Color BgAffordable = new Color(0.10f, 0.16f, 0.26f);
    static readonly Color BgLocked = new Color(0.09f, 0.09f, 0.11f);
    static readonly Color BgMaxed = new Color(0.18f, 0.15f, 0.04f);
    static readonly Color TextGreen = new Color(0.22f, 0.88f, 0.44f);
    static readonly Color TextBlue = new Color(0.31f, 0.76f, 0.97f);
    static readonly Color TextGold = new Color(0.90f, 0.75f, 0.20f);
    static readonly Color TextDim = new Color(0.40f, 0.40f, 0.45f);

    // ── Internal ──────────────────────────────────────────────────────────────
    internal AbilityUpgradeData _data;
    private int _nodeIndex;
    private ISkillTreePurchaser _purchaser;
    private IPointBank _pointBank;
    private Button _btn;

    private enum State { Purchased, NextAffordable, NextLocked, Locked, Maxed }

    // ── Init ──────────────────────────────────────────────────────────────────
    public void Initialise(AbilityUpgradeData data, int nodeIndex,
                            ISkillTreePurchaser purchaser, IPointBank pointBank)
    {
        _data = data;
        _nodeIndex = nodeIndex;
        _purchaser = purchaser;
        _pointBank = pointBank;
        _btn = GetComponent<Button>();
        if (_btn == null) _btn = GetComponentInParent<Button>();
        if (_btn == null) _btn = GetComponentInChildren<Button>();

        if (_btn == null)
        {
            Debug.LogError($"[SkillNodeButton] {gameObject.name} — no Button component found on this GO, parent, or children. Clicks will not work.", this);
            return;
        }

        //Debug.Log($"[SkillNodeButton] {gameObject.name} — Button found on '{_btn.gameObject.name}', wiring onClick");
        _btn.onClick.RemoveAllListeners();
        _btn.onClick.AddListener(OnClick);

        // Refresh whenever points change so interactable updates immediately
        _pointBank.OnPointsChanged -= OnPointsChanged;
        _pointBank.OnPointsChanged += OnPointsChanged;

        Refresh();
    }

    // Called by SkillTreeUI for hand-placed scene buttons
    public void InitialiseFromScene(ISkillTreePurchaser purchaser, IPointBank pointBank)
    {
        if (NodeData == null) { Debug.LogError($"[SkillNodeButton] {gameObject.name} — NodeData is NULL, assign SO in Inspector", this); return; }
        if (purchaser == null) { Debug.LogError($"[SkillNodeButton] {gameObject.name} — purchaser is NULL, check SkillTreeUI _purchaserMono", this); return; }
        if (pointBank == null) { Debug.LogError($"[SkillNodeButton] {gameObject.name} — pointBank is NULL, check SkillTreeUI _pointBankMono", this); return; }
        //Debug.Log($"[SkillNodeButton] {gameObject.name} initialised OK — SO={NodeData.name} nodeIndex={NodeIndex}");
        Initialise(NodeData, NodeIndex, purchaser, pointBank);
    }

    // ── Refresh visuals ───────────────────────────────────────────────────────
    public void Refresh()
    {
        var state = GetState();
        var node = _data.nodes[_nodeIndex];

        // Label
        if (NodeLabel) NodeLabel.text = node.label;

        // Background + interactable
        _btn.interactable = (state == State.NextAffordable);
        if (Background) Background.color = state switch
        {
            State.Purchased => BgPurchased,
            State.NextAffordable => BgAffordable,
            State.Maxed => BgMaxed,
            _ => BgLocked
        };
        
        // Lock icon — visible only when node is unreachable
        if (LockIcon) LockIcon.enabled = (state == State.Locked);

        // Cost badge
        if (CostBadge)
        {
            CostBadge.text = state == State.Purchased ? "✓"
                            : state == State.Maxed ? "MAX"
                            : $"{node.pointCost} pts";

            CostBadge.color = state == State.Purchased ? TextGold
                            : state == State.NextAffordable ? TextGreen
                            : TextDim;
        }

        // Stat preview line
        if (StatLine) StatLine.text = BuildStatLine(state, node);
    }

    // ── Stat line builder ─────────────────────────────────────────────────────
    string BuildStatLine(State state, AbilityUpgradeNode node)
    {
        return state switch
        {
            State.Purchased => $"<color=#3fb950>✓  {node.description}</color>",
            State.Maxed => "<color=#e3b341>FULLY UPGRADED</color>",
            State.Locked => "<color=#555>Unlock previous node first</color>",
            _ => string.IsNullOrEmpty(node.description) ? "" : node.description
        };
    }

    // ── State   ─────────────────────────────────────────────────────────────────
    State GetState()
    {
        if (_data.IsMaxed && _nodeIndex >= _data.TotalNodes) return State.Maxed;
        if (_nodeIndex < _data.currentNodeIndex) return State.Purchased;
        if (_nodeIndex > _data.currentNodeIndex) return State.Locked;
        // nodeIndex == currentNodeIndex → this is the next purchasable node
        return _purchaser.CanAfford(_data) ? State.NextAffordable : State.NextLocked;
    }

    // ── Click ─────────────────────────────────────────────────────────────────
    void OnPointsChanged(int _) => Refresh();

    void OnClick()
    {
        Debug.Log($"[SkillNodeButton] OnClick fired on {gameObject.name} | _data={(_data == null ? "NULL" : _data.name)} | _purchaser={(_purchaser == null ? "NULL" : "OK")} | nodeIndex={_nodeIndex}");

        if (_purchaser == null) { Debug.LogError("[SkillNodeButton] _purchaser is null — InitialiseFromScene was not called or SkillTreeManager not assigned"); return; }
        if (_data == null) { Debug.LogError("[SkillNodeButton] _data is null — NodeData not assigned in Inspector"); return; }

        Debug.Log($"[SkillNodeButton] HasNextNode={_data.HasNextNode} | NextNodeCost={_data.NextNodeCost} | CurrentPoints={_pointBank?.CurrentPoints}");

        if (!_purchaser.TryPurchaseNode(_data))
        {
            Debug.Log("[SkillNodeButton] TryPurchaseNode returned FALSE");
            return;
        }

        Debug.Log("[SkillNodeButton] Purchase SUCCESS — refreshing siblings");
        foreach (var btn in FindObjectsByType<SkillNodeButton>(FindObjectsSortMode.None))
            if (btn._data == _data)
                btn.Refresh();
    }
}