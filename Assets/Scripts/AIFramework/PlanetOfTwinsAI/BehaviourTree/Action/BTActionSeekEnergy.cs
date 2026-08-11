using BehaviourTree;
using UnityEngine;

/// <summary>
/// BT Action: walk to the nearest energy-feeding POI (PoiEnergyEmitter) and stand inside its feed
/// radius while the emitter ticks feeds. Succeeds after the dwell, or immediately if a target
/// appears (feeding pauses while engaged — Enemy.IsEngaged — so there is nothing to stay for).
/// Fails when no feed site exists.
/// </summary>
public class BTActionSeekEnergy : PoTBTActionBase
{
    public override string DebugDisplayName { get; protected set; } = "SeekEnergy";

    private PoiEnergyEmitter _site;
    private float _dwellRemaining;   // scaled — the feed itself ticks on scaled time too

    // Seconds spent standing at the site once arrived — long enough for at least one feed
    // (the emitter feeds an eligible enemy immediately on first contact, then at its interval).
    private const float DwellSeconds = 6f;

    protected override void OnEnter()
    {
        base.OnEnter();
        _site = _enemy != null ? PoiEnergyEmitter.FindNearest(_enemy.transform.position) : null;
        _dwellRemaining = DwellSeconds;
    }

    protected override bool OnTick_NodeLogic(float InDeltaTime)
    {
        if (_enemy == null || _site == null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Failed);

        // A target showed up — feeding pauses while engaged, so hand control back to combat goals.
        if (_enemy.Target != null)
            return SetStatusAndCalculateReturnValue(EBTNodeResult.Succeeded);

        float arriveDist = _site.FeedRadius * 0.8f;   // stand comfortably INSIDE the feed radius
        float dist = Vector3.Distance(_enemy.transform.position, _site.transform.position);

        if (dist > arriveDist)
        {
            _enemy.Movement.MoveTowards(_site.transform.position);
            return SetStatusAndCalculateReturnValue(EBTNodeResult.InProgress);
        }

        _enemy.Movement.Stop();
        _dwellRemaining -= InDeltaTime;
        return SetStatusAndCalculateReturnValue(
            _dwellRemaining <= 0f ? EBTNodeResult.Succeeded : EBTNodeResult.InProgress);
    }

    protected override void OnExit()
    {
        base.OnExit();
        _enemy?.Movement.Stop();
    }
}
