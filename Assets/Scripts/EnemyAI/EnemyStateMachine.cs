using UnityEngine;

public class EnemyStateMachine : MonoBehaviour
{
    private IEnemyState currentState;

    public IEnemyState CurrentState => currentState;

    public void ChangeState(IEnemyState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    private void Update()
    {
        currentState?.Update();
    }
}