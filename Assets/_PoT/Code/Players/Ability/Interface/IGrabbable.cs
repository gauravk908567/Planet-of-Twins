public interface IGrabbable
{
    void GrabByTrap(float killDelay);   // possessed trap grabs this enemy
    void ReleaseFromTrap();
    bool IsGrabbedByTrap { get; }
}