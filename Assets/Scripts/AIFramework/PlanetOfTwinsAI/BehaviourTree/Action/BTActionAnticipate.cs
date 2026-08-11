using BehaviourTree;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BT Action: Move to intercept twin using anticipation.
/// Chooses between LEAD and CUTOFF based on context:
///   - Twin fleeing → CUTOFF (try to block escape route)
///   - Twin in combat → LEAD (position to intercept)
///   - Enemy far from twin → CUTOFF (most efficient path)
///   - Enemy close to twin → normal chase (no anticipation needed)
///
/// Sits in GOAP selector before BTActionChaseTarget.
/// Returns Failed when twin is close enough to just attack.
/// Returns Succeeded when enemy reaches anticipated position.
/// Returns InProgress while moving to intercept.
/// </summary>
public class BTActionAnticipate : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "Anticipate";

    [Header("Anticipation Config")]
    private const float AnticipateMinDist = 6f;   // only anticipate if > 6 units away
    private const float AnticipateArrivalDist = 2.0f;
    private const float CutoffPreference = 0.6f; // 0=always lead, 1=always cutoff

    private TwinAnticipator _anticipator;
    private Vector3 _targetPoint;
    private AnticipateMode _mode;

    private enum AnticipateMode { Lead, Cutoff, None }

    protected override void OnEnter()
    {
        base.OnEnter();

        _anticipator = GetAnticipator();
        if (_anticipator == null) return;

        SelectTargetPoint();

        if (_mode != AnticipateMode.None)
        {
            _enemy.Movement.SetSpeed(_enemy.Data?.moveSpeed ?? 3.5f);
            Debug.Log($"[Anticipate] {_enemy.name} mode={_mode} target={_targetPoint}");
        }
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_anticipator == null || _mode == AnticipateMode.None)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        if (!HasTarget)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // Twin close enough — stop anticipating, let chase take over
        float distToTwin = Vector3.Distance(
            _enemy.transform.position, _anticipator.transform.position);
        if (distToTwin <= AnticipateMinDist)
        {
            _enemy.Movement.Stop();
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);
        }

        // Recalculate target each tick — twin keeps moving
        SelectTargetPoint();

        float distToTarget = Vector3.Distance(_enemy.transform.position, _targetPoint);
        if (distToTarget <= AnticipateArrivalDist)
        {
            _enemy.Movement.Stop();
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);
        }

        _enemy.Movement.SetSpeed(_enemy.Data?.moveSpeed ?? 3.5f);
        _enemy.Movement.MoveTowards(_targetPoint);

        return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();
        if (_enemy?.Data != null)
            _enemy.Movement.SetSpeed(_enemy.Data.moveSpeed);
    }

    private void SelectTargetPoint()
    {
        if (_anticipator == null || !_anticipator.IsMoving)
        {
            _mode = AnticipateMode.None;
            return;
        }

        float distToTwin = Vector3.Distance(
            _enemy.transform.position, _anticipator.transform.position);

        // Too close — just chase normally
        if (distToTwin <= AnticipateMinDist)
        {
            _mode = AnticipateMode.None;
            return;
        }

        // Choose mode based on context
        if (_anticipator.IsFleeing || distToTwin > 12f)
        {
            // Twin fleeing or far away — try to cut off
            _mode = AnticipateMode.Cutoff;
            _targetPoint = _anticipator.CutoffPosition;
        }
        else
        {
            // Twin nearby and in combat — lead to predicted position
            _mode = AnticipateMode.Lead;
            _targetPoint = _anticipator.LeadPosition;
        }

        // Validate target is reachable
        if (!NavMesh.SamplePosition(_targetPoint, out var hit, 2f, NavMesh.AllAreas))
        {
            _mode = AnticipateMode.None;
            return;
        }
        _targetPoint = hit.position;
    }

    private TwinAnticipator GetAnticipator()
    {
        // Try to find the right twin anticipator
        // Enemies on left side target left twin, right side target right twin
        // Fall back to nearest
        if (TwinAnticipator.Left != null || TwinAnticipator.Right != null)
        {
            // Find nearest
            float distL = TwinAnticipator.Left != null
                ? Vector3.Distance(_enemy.transform.position, TwinAnticipator.Left.transform.position)
                : float.MaxValue;
            float distR = TwinAnticipator.Right != null
                ? Vector3.Distance(_enemy.transform.position, TwinAnticipator.Right.transform.position)
                : float.MaxValue;

            return distL < distR ? TwinAnticipator.Left : TwinAnticipator.Right;
        }

        // Fallback — find in scene
        return Object.FindAnyObjectByType<TwinAnticipator>();
    }
}