/// <summary>
/// Freezes and unfreezes all active enemies in the scene.
/// Used by QTEController to stop enemy movement/attacks during a QTE sequence,
/// regardless of pass or fail, until the next retry attempt begins.
/// </summary>
public interface IEnemyFreezeService
{
    void FreezeAll();
    void UnfreezeAll();
    bool IsFrozen { get; }
}