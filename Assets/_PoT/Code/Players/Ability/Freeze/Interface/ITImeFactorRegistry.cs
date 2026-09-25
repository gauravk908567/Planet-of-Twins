public interface ITimeFactorRegistry
{
    void Register(ITimeAffected entity);
    void Unregister(ITimeAffected entity);
}