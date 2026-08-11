using System;
using UnityEngine;

/// <summary>
/// Handles wrong-twin-entered-checkpoint events for both Single and Dual modes.
/// One instance per step, subscribed to every checkpoint that can fire OnWrongTwinReached.
/// The shared _resetting guard prevents simultaneous events from racing each other.
/// Identity-based reset: left twin always goes to cpA.LeftResetPosition, right twin to
/// cpB.RightResetPosition (cpA in Single), regardless of which checkpoint fired or which
/// twin was wrong.
/// Suspends all checkpoint colliders before TriggerReset; FullReset re-arms them.
/// </summary>
public class WrongTwinResetHandler
{
    private readonly TutorialStepContext _ctx;
    private readonly TutorialCheckpoint _cpA;
    private readonly TutorialCheckpoint _cpB; // null in Single mode
    private readonly Func<string> _failureMessage;
    private readonly Action _onReset; // clears done-flags in the coroutine (Dual mode)

    private bool _resetting;

    public WrongTwinResetHandler(TutorialStepContext ctx,
        TutorialCheckpoint cpA, TutorialCheckpoint cpB,
        Func<string> failureMessage,
        Action onReset = null)
    {
        _ctx = ctx;
        _cpA = cpA;
        _cpB = cpB;
        _failureMessage = failureMessage;
        _onReset = onReset;
    }

    public void HandleWrongTwin(TutorialCheckpoint source, Player wrong)
    {
        if (_resetting) return;
        _resetting = true;

        // Suspend trigger colliders before teleport — prevents twins re-firing events mid-sequence
        _cpA.Suspend();
        _cpB?.Suspend();

        _ctx.failureNotice?.Show(_failureMessage());

        // Identity-based positions: left twin to cpA's left reset, right twin to cpB's right reset
        Vector3 leftPos  = _cpA.LeftResetPosition;
        Vector3 rightPos = (_cpB ?? _cpA).RightResetPosition;

        _ctx.resetSequencer?.TriggerReset(leftPos, rightPos, () =>
        {
            _cpA.FullReset(); // FullReset calls Resume() — re-arms the trigger collider
            _cpB?.FullReset();
            _onReset?.Invoke();
            _resetting = false;
        });
    }
}
