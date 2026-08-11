using System;
using UnityEngine;

public enum RescueState
{
    Idle,        // no rescue event active
    Triggered,   // soul arrived near trapped player — mash prompt visible
    Mashing,     // player mashing F — both timers PAUSED
    Cooldown,    // failed mash — 0.75s wait, both timers RESUMED
    SoulDied,
    Success,     // bar filled — rescue complete
    Failed       // TTK ran out during cooldown or between attempts
}