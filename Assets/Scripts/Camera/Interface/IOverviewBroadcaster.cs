using System;

public interface IOverviewBroadcaster
{
    event Action<bool> OnOverviewToggled;
    bool IsOverviewActive { get; }
}