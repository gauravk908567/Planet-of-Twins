using System.Collections;
using UnityEngine;

/// <summary>
/// Witness ritual state — channels to summon a new melee ally.
///
/// FLOW:
///   Enter → colour turns purple (channelling)
///   → if player enters ritualInterruptRange OR Witness takes damage → interrupt
///   → on interrupt: flee to ritualFleeDistance then restart ritual
///   → ritual completes uninterrupted → spawn melee ally → ShadowState
/// </summary>
public class WitnessRitualState : IEnemyState
{
    private readonly WitnessEnemy _enemy;
    private readonly WitnessEnemyData _data;

    private float _ritualTimer;
    private bool _interrupted;
    private bool _fleeing;
    private bool _ritualComplete;

    private static readonly Color RitualColour = new Color(0.5f, 0f, 1f); // purple

    public WitnessRitualState(WitnessEnemy enemy, WitnessEnemyData data)
    {
        _enemy = enemy;
        _data = data;
    }

    public void Enter()
    {
        _ritualTimer = _data?.ritualDuration ?? 4f;
        _interrupted = false;
        _fleeing = false;
        _ritualComplete = false;

        _enemy.Movement.Stop();
        _enemy.SetRitualColour(true);
        _enemy.GetComponentInChildren<EnemyStateUIController>()?.ShowRitual(_data?.ritualDuration ?? 4f);
        _enemy.GetComponentInChildren<EnemyStateUIController>()?.ShowIkari();

        // Subscribe to damage for interrupt detection
        _enemy.Health.OnDamageTaken += OnDamageTaken;
    }

    public void Update()
    {
        if (_fleeing || _ritualComplete) return;

        // Proximity interrupt check
        var twin = _enemy.GetNearestTwin();
        if (twin != null)
        {
            float dist = Vector3.Distance(_enemy.transform.position, twin.transform.position);
            if (dist <= (_data?.ritualInterruptRange ?? 5f))
            {
                Interrupt();
                return;
            }
        }

        // Channel ritual
        _ritualTimer -= Time.deltaTime;
        if (_ritualTimer <= 0f)
        {
            CompleteRitual();
        }
    }

    public void Exit()
    {
        _enemy.Health.OnDamageTaken -= OnDamageTaken;
        _enemy.SetRitualColour(false);
        _enemy.GetComponentInChildren<EnemyStateUIController>()?.HideRitual();
        _fleeing = false;
        _interrupted = false;
        _ritualComplete = false;
    }

    private void OnDamageTaken(EnemyHealthComponent comp, float amount, Vector3 pos)
    {
        if (!_fleeing && !_ritualComplete)
            Interrupt();
    }

    private void Interrupt()
    {
        if (_fleeing) return;
        _interrupted = true;
        _fleeing = true;
        _enemy.StartCoroutine(FleeAndRestart());
    }

    private IEnumerator FleeAndRestart()
    {
        _enemy.SetRitualColour(false);
        float originalSpeed = _enemy.Data?.moveSpeed ?? 3.5f;
        _enemy.Movement.SetSpeed(originalSpeed * (_data?.fleeSpeedMultiplier ?? 1.6f));

        float fleeDistance = _data?.ritualFleeDistance ?? 10f;

        while (true)
        {
            var twin = _enemy.GetNearestTwin();
            if (twin == null) break;

            float dist = Vector3.Distance(_enemy.transform.position, twin.transform.position);
            if (dist >= fleeDistance) break;

            Vector3 fleeDir = (_enemy.transform.position - twin.transform.position).normalized;
            fleeDir.y = 0f;
            _enemy.Movement.MoveTowards(_enemy.transform.position + fleeDir * 5f);
            yield return null;
        }

        // Safe distance reached — restart ritual
        _enemy.Movement.SetSpeed(_enemy.Data?.moveSpeed ?? 3.5f);
        _enemy.Movement.Stop();
        _fleeing = false;
        _interrupted = false;
        _ritualTimer = _data?.ritualDuration ?? 4f;
        _enemy.SetRitualColour(true);
    }

    private void CompleteRitual()
    {
        _ritualComplete = true;
        _enemy.SummonAlly();
        _enemy.StateMachine.ChangeState(_enemy.ShadowState);
    }
}