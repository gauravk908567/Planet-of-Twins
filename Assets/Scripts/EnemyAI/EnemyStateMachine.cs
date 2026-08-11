using UnityEngine;

public class EnemyStateMachine : MonoBehaviour
{
    private IEnemyState _currentState;
    private bool _paused;

    public IEnemyState CurrentState => _currentState;

    public void ChangeState(IEnemyState newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        _currentState?.Enter();
    }

    public void Pause() => _paused = true;
    public void Resume() => _paused = false;

    private void Update()
    {
        if (!_paused)
            _currentState?.Update();
    }
}
