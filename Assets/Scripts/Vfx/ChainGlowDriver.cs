using UnityEngine;

/// <summary>
/// Stretches the chain-glow particle stream (ChainGlowFx) along the LIVE chain span each frame —
/// SOURCE at the grabbed player's end, flowing toward the TetherBreaker (the drain read, same tech
/// as the grab absorb). The particle shape's Z length is set to the current endpoint distance, so
/// the glow breathes as the chain drags the player (max span = chainAttackRange, currently 10).
/// Driven by ChainProjectile.UpdateChainLine while connected; hidden otherwise and on despawn.
/// Zero inspector wiring: resolves its own ParticleSystem, and ChainProjectile finds this driver
/// in children — drop the ChainGlowFx prefab under the chain with this component and it works.
/// </summary>
public class ChainGlowDriver : MonoBehaviour
{
    [Tooltip("The glow particle system. Null = first ParticleSystem on this object/children.")]
    [SerializeField] private ParticleSystem _glow;

    private void Awake()
    {
        if (_glow == null) _glow = GetComponentInChildren<ParticleSystem>(true);
        if (_glow == null)
        {
            Debug.LogError("[ChainGlowDriver] No ParticleSystem found — chain glow disabled.", this);
            enabled = false;
        }
    }

    /// <summary>Position at <paramref name="from"/> (player end), aim at <paramref name="to"/>
    /// (TetherBreaker), stretch the emitter shape's Z to the span.</summary>
    public void SetSpan(Vector3 from, Vector3 to)
    {
        if (_glow == null) return;

        Vector3 dir = to - from;
        float len = dir.magnitude;
        if (len < 0.05f) return;

        if (!_glow.gameObject.activeSelf) _glow.gameObject.SetActive(true);
        transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir / len));

        var shape = _glow.shape;
        var s = shape.scale;
        s.z = len;
        shape.scale = s;

        if (!_glow.isPlaying) _glow.Play();
    }

    /// <summary>Stop emitting and hide — called on release/miss/despawn (idempotent).</summary>
    public void StopGlow()
    {
        if (_glow == null) return;
        _glow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _glow.gameObject.SetActive(false);
    }
}
