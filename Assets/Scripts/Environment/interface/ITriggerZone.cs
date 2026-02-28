using System;
public interface ITriggerZone
{
    bool IsActive { get; }
    event Action OnStateChanged;
}