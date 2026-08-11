using System;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// IStunEvents
// Implemented by StunAbility. Injected into DualCastSystem + CoalesceSystem.
// Replaces static events on StunAbility.
// ─────────────────────────────────────────────────────────────────────────────
public interface IStunEvents
{
    event Action OnStunCast;            // stun successfully applied to any enemy
    event Action<GameObject> OnStunApplied;         // arg = the stunned enemy GO
    event Action<GameObject> OnStunEnded;           // arg = enemy GO, stun expired or enemy died
    void ReduceCurrentCooldown(float fraction);      // called by DualCastSystem on sync
}