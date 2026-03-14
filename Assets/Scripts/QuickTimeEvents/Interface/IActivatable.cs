public interface IActivatable
{
    void Activate();
    void Deactivate();
    bool IsActivated { get; }
}