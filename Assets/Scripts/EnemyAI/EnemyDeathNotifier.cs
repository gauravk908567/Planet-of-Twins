using System;
using UnityEngine;

public class EnemyDeathNotifier : MonoBehaviour, IEnemyDeathNotifier
{
    public event Action OnEnemyDied;

    public void Register(EnemyHealthComponent enemy)
    {
        enemy.OnDeath += OnEnemyDied;
    }

    public void Unregister(EnemyHealthComponent enemy)
    {
        enemy.OnDeath -= OnEnemyDied;
    }
}