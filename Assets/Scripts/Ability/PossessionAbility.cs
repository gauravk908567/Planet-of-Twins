using UnityEngine;

public class PossessionAbility : AbilityBase
{
    private LayerMask enemyLayer;
    private PossessEffect activeEffect;
    private AbilityController abilityController;

    public PossessionAbility(AbilityData data, LayerMask enemyLayer, AbilityController abilityController) : base(data)
    {
        this.enemyLayer = enemyLayer;
        this.abilityController = abilityController;
    }

    protected override bool Activate()
    {
       // if (activeEffect != null)
        //    return false;
        abilityController.ShowPrimaryPreview(data.range);
        Collider[] hits = Physics.OverlapSphere(
            owner.transform.position,
            data.range,
            enemyLayer
        );

        Enemy closestEnemy = null;
        float closestDistance = float.MaxValue;

        foreach (var hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null)
                continue;

            if (enemy.Faction.CurrentFaction == Faction.PossessedEnemy)
                continue;

            float distance = Vector3.Distance(
                owner.transform.position,
                enemy.transform.position
            );

   /*         if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy;
            }
            if (closestEnemy == null)
                return false; // No enemy in radius
*/
            var status = enemy.GetComponent<StatusEffectController>();

            if (status == null)
                return false;

            activeEffect = new PossessEffect(
                enemy.gameObject,
                data.duration
            );

            status.ApplyEffect(activeEffect);

        }


        return true; // SUCCESS
    }

    public override void Tick()
    {
        base.Tick();

        if (activeEffect != null && activeEffect.IsFinished)
        {
            activeEffect = null;
        }
    }

    protected override void End()
    {
        abilityController.HidePrimaryPreview();
        activeEffect = null;
        base.End();
    }
}