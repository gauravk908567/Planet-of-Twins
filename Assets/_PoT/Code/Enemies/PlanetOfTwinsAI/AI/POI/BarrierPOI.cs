using UnityEngine;

/// <summary>
/// Barrier POI — the central dividing structure between twins.
/// Enemies near the barrier gain dark energy faster.
/// Different barrier GOs per level — just add this component.
///
/// ATTACH: To barrier GO in each level.
/// </summary>
public class BarrierPOI : POIBase
{
    protected override void Awake()
    {
        PoiType = POIType.Barrier;
        base.Awake();
    }

    private void Update()
    {
        if (!IsActive) return;

        var colliders = Physics.OverlapSphere(transform.position, InfluenceRadius);
        foreach (var col in colliders)
        {
            var de = col.GetComponent<EnemyDarkEnergy>()
                  ?? col.GetComponentInParent<EnemyDarkEnergy>();
            if (de != null)
                de.OnNearBarrier();
        }
    }
}