public interface IStatusEffect
{
    void OnApply();
    void OnUpdate();
    void OnRemove();
    bool IsFinished { get; }
}