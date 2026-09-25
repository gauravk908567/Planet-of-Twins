using UnityEngine;

public class SoulFrozenVFX : MonoBehaviour
{
    [SerializeField] private ParticleSystem _frozenPS;

    public void SetFrozen(bool frozen)
    {
        if (_frozenPS == null) return;
        _frozenPS.gameObject.SetActive(frozen);
    }
}