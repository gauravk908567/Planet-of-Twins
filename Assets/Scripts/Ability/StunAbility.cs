using System.Collections.Generic;
using UnityEngine;

public class StunAbility : AbilityBase
{
    private LayerMask enemyLayer;
    private AbilityController abilityController;

    private HashSet<StatusEffectController> affectedEnemies = new HashSet<StatusEffectController>();
    private int currectAffected = 0;
    public StunAbility(AbilityData data, LayerMask enemyLayer, AbilityController abilityController) : base(data)
    {
        this.enemyLayer = enemyLayer;
        this.abilityController = abilityController;
    }

    protected override bool Activate()
    {
        abilityController.ShowPrimaryPreview(data.range);
        affectedEnemies.Clear();
        currectAffected = 0;
        return true; // Always activate field
    }

    public override void Tick()
    {
        if (!isActive)
            return;

        base.Tick();
        if (currectAffected < data.affectionLevel)
        {
            Collider[] hits = Physics.OverlapSphere(owner.transform.position, data.range, enemyLayer);

            foreach (var hit in hits)
            {
                var status = hit.GetComponent<StatusEffectController>();

                if (status != null && affectedEnemies.Add(status) && currectAffected < data.affectionLevel)
                {
                    status.ApplyEffect(
                        new StunEffect(hit.gameObject, data.duration)
                    );
                    currectAffected++;
                }
            }
        }
    }

    protected override void End()
    {
        base.End();
        affectedEnemies.Clear();
        currectAffected = 0;
        abilityController.HidePrimaryPreview();
    }
}