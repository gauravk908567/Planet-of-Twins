using CommonCore;
using HybridGOAP;
using UnityEngine;

/// <summary>
/// Goal: Hold formation slot position relative to commander.
/// Valid when: commander alive, no twin detected, not possessed.
/// Priority: High (75) — yields to combat, maintains loose formation otherwise.
/// Works with any ICommander type via interface.
/// </summary>
public class GOAPGoalHoldFormation : GOAPGoalBase
{
    private ICommander _commander;
    private Vector3 _slotOffset;

    public void SetCommander(ICommander commander, Vector3 slotOffset = default)
    {
        _commander = commander;
        _slotOffset = slotOffset;
    }

    public void ClearCommander() => _commander = null;

    public Vector3 GetFormationPosition()
        => _commander == null ? Vector3.zero : _commander.GetSlotWorldPosition(_slotOffset);

    public override void PrepareForPlanning()
    {
        if (_commander == null || !_commander.IsAlive)
        { Priority = GoalPriority.DoNotRun; return; }

        bool commanderAlive = true;
        LinkedBlackboard.TryGet(PoTNames.CommanderAlive, out commanderAlive, true);
        if (!commanderAlive) { Priority = GoalPriority.DoNotRun; return; }

        bool isPossessed = false;
        LinkedBlackboard.TryGet(PoTNames.EnemyIsPossessed, out isPossessed, false);
        if (isPossessed) { Priority = GoalPriority.DoNotRun; return; }

        GameObject target = null;
        LinkedBlackboard.TryGet(CommonCore.Names.Awareness_BestTarget, out target, null);
        if (target != null) { Priority = GoalPriority.DoNotRun; return; }

        Priority = GoalPriority.High;
    }
}