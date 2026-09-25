public interface IAlertReceiver
{
    /// <summary>
    /// Called when a grabber has caught a player.
    /// normalRange = standard alert range, grabs expand this greatly.
    /// </summary>
    void OnGrabAlert(UnityEngine.Transform grabbedPlayerTransform);

    /// <summary>
    /// Called when a grabber is chasing but hasn't caught yet.
    /// Shorter range than grab alert.
    /// </summary>
    void OnChaseAlert(UnityEngine.Transform chasedPlayerTransform);
}