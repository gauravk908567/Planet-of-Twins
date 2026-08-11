public interface IPossessable
{
    void ApplyPossession(float duration, float damageMultiplier);
    bool IsPossessed { get; }
    void OnHitByPossessed(Enemy attacker); // called when a possessed enemy strikes this
}