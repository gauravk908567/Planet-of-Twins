using System;

public interface IEnemyDeathNotifier
{
    event Action OnEnemyDied;
}