using UnityEngine;

public class StunAbility : AbilityBase
{
    private LayerMask enemyLayer;
    private AbilityController abilityController;
    public StunAbility(AbilityData data, LayerMask enemyLayer, AbilityController abilityController) : base(data)
    {
        this.enemyLayer = enemyLayer;
        this.abilityController = abilityController;
    }

    protected override bool Activate()
    {
        abilityController.ShowPrimaryPreview(data.range);
        Collider[] hits = Physics.OverlapSphere(
            owner.transform.position,
            data.range,
            enemyLayer
        );
        foreach (var hit in hits)
        {
            var status = hit.GetComponent<StatusEffectController>();
            if (status != null)
            {
                status.ApplyEffect(
                    new StunEffect(hit.gameObject, data.duration)
                );
            }
        }

        if (hits.Length > 0)
            return true;

        return false;


    }

    protected override void End()
    {
        base.End();
        abilityController.HidePrimaryPreview();
    }
}