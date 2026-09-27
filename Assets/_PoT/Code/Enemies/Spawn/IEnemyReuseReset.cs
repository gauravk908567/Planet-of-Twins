/// <summary>
/// Per-life state reset for a pooled enemy (BUG-135). <see cref="EnemyPool.Get"/> calls
/// <see cref="ResetForReuse"/> on every component of the instance it issues (fresh or reused), so a reused
/// enemy starts exactly like a fresh spawn: default mood, base dark energy, no memory, no bond, no pact.
///
/// Runs at ISSUE time, never during the death event: later OnDeath subscribers still read the dying state
/// (same rule as the BUG-058 health reset — see Enemy.ResetForPool). Reset silently (no change events): the
/// pool already cleared the presentation (Manpu slot, cues) on return.
///
/// Implement it on any enemy component that keeps state for one life.
/// </summary>
public interface IEnemyReuseReset
{
    void ResetForReuse();
}
