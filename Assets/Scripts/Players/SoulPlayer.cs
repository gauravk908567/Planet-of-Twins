using UnityEngine;

public class SoulPlayer : Player
{
    public void ShouldSoulSleep(bool status)
    {
        attackController?.SetSoulMode(status);
    }
}
