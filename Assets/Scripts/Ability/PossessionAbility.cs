using System.Collections.Generic;
using UnityEngine;

public class PossessionAbility : AbilityBase
{
    private LayerMask enemyLayer;
    private PossessEffect activeEffect;
    private AbilityController abilityController;
    private HashSet<StatusEffectController> affectedEnemies = new HashSet<StatusEffectController>();
    private int currectAffected = 0;

    public PossessionAbility(AbilityData data, LayerMask enemyLayer, AbilityController abilityController) : base(data)
    {
        this.enemyLayer = enemyLayer;
        this.abilityController = abilityController;
    }

    protected override bool Activate()
    {
        /*if (activeEffect != null)
            return false;*/
        abilityController.ShowPrimaryPreview(data.range);
        affectedEnemies.Clear();
        currectAffected = 0;
        return true; // SUCCESS
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
                Enemy enemy = hit.GetComponent<Enemy>();
                if (enemy == null)
                    continue;

                if (enemy.Faction.CurrentFaction == Faction.PossessedEnemy)
                    continue;

                var status = enemy.GetComponent<StatusEffectController>();

                if (currectAffected >= data.affectionLevel)
                    continue;
                if (status != null && affectedEnemies.Add(status))
                {
                    activeEffect = new PossessEffect(enemy.gameObject, data.duration);
                    currectAffected++;
                    status.ApplyEffect(activeEffect);
                }

            }
        }

        if (activeEffect != null && activeEffect.IsFinished)
        {
            activeEffect = null;
        }
    }

    protected override void End()
    {
        base.End();
        activeEffect = null;
        affectedEnemies.Clear();
        currectAffected = 0;
        abilityController.HidePrimaryPreview();
    }
}