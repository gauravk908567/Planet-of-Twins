public interface IStunnable
{
    void ApplyStun(float duration);
    bool IsStunned { get; }
}
