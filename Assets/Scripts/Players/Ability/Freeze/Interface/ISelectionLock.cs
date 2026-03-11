// Uses a COUNTER not a bool. Reason: teleport AND grab can both lock
// selection simultaneously. If bool, the first Unlock() would clear both.
// Counter: each locker calls Lock() (+1) and Unlock() (-1).
// Selection is blocked whenever counter > 0.
// ───────────────────────────────────────────────────────────────────────
public interface ISelectionLock
{
    void LockSelection();
    void UnlockSelection();
    bool IsSelectionLocked { get; }
}