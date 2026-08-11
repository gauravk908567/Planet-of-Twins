public interface IProjectileLauncher
{
    bool TryFire(UnityEngine.Transform target);
    float AttackRange { get; }
    float Cooldown { get; }
}