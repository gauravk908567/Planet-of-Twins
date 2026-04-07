using System.Collections;
using UnityEngine;

/// <summary>
/// Grief Rage state — entered when the Severed's partner dies.
/// Full aggro, boosted attack speed, 8s then auto-die.
/// </summary>
public class SeveredGriefRageState : IEnemyState
{
    private readonly SeveredEnemy _enemy;
    private readonly SeveredEnemyData _data;
    private Coroutine _rageCoroutine;

    public SeveredGriefRageState(SeveredEnemy enemy, SeveredEnemyData data)
    {
        _enemy = enemy;
        _data = data;
    }

    public void Enter()
    {
        // Boost attack speed via cooldown reduction
        _enemy.AttackController.SetAttackSlowdown(1f / _data.rageCooldownMultiplier);
        _rageCoroutine = _enemy.StartCoroutine(RageTimer());
    }

    public void Update()
    {
        if (_enemy.Target == null)
        {
            // Full aggro — find nearest target
            var players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
            Player nearest = null;
            float best = float.MaxValue;
            foreach (var p in players)
            {
                if (p is SoulPlayer) continue;
                float d = Vector3.Distance(_enemy.transform.position, p.transform.position);
                if (d < best) { best = d; nearest = p; }
            }
            if (nearest != null) _enemy.SetTarget(nearest.transform);
            return;
        }

        float dist = Vector3.Distance(_enemy.transform.position, _enemy.Target.position);
        if (dist > _enemy.AttackRange)
            _enemy.Movement.MoveTowards(_enemy.Target.position);
        else
            _enemy.AttackController.TryAttack();
    }

    public void Exit()
    {
        _enemy.AttackController.ClearAttackSlowdown();
        if (_rageCoroutine != null)
            _enemy.StopCoroutine(_rageCoroutine);
    }

    private IEnumerator RageTimer()
    {
        yield return new WaitForSeconds(_data.rageDuration);
        // Auto-die after rage duration
        _enemy.Health?.TakeDamage(new DamageData(9999f, DamageType.Environmental));
    }
}