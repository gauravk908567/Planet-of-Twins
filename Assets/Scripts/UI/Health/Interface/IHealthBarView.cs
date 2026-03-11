public interface IHealthBarView
{
    /// <summary>Update the fill amount. 0 = empty, 1 = full.</summary>
    void SetFill(float normalised);

    /// <summary>Flash or colour-change when health is critically low.</summary>
    void SetCriticalState(bool isCritical);
}