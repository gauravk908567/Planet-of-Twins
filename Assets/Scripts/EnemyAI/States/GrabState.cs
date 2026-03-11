using UnityEngine;

public class GrabState : IEnemyState
{
    private readonly GroupGrabEnemy _enemy;

    public GrabState(GroupGrabEnemy enemy) => _enemy = enemy;

    public void Enter()
    {
        _enemy.Movement.Stop();
        _enemy.StartGrab(); // GroupGrabEnemy implements IRescueTarget, fires OnPlayerGrabbed
    }

    public void Update()
    {
        // Stay frozen — RescueEventController manages everything from here.
        // GrabState exits when ReleasePlayer() or KillPlayer() is called
        // by RescueEventController, which calls EndGrab() on the enemy.
    }

    public void Exit() { }
}