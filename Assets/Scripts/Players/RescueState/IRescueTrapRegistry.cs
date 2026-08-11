public interface IRescueTrapRegistry
{
    void RegisterTrap(IRescueTarget trap);
    void UnregisterTrap(IRescueTarget trap);
}