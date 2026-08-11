using UnityEngine;

/// <summary>
/// A particle graph's natural lifetime, read from the prefab and recursed across sub-emitters
/// (child ParticleSystems) — never hand-summed into a cue field (Banned Lazy Work #1/#6).
/// FxManager caches the result per prefab at prewarm.
/// </summary>
public static class FxLifetime
{
    public static float Compute(ParticleSystem root)
    {
        if (root == null) return 0f;
        return Max(root) + 0.1f;   // small tail so the last particles finish
    }

    private static float Max(ParticleSystem ps)
    {
        var m = ps.main;
        float self = m.duration + m.startDelay.constantMax + m.startLifetime.constantMax;
        float deepest = self;
        foreach (Transform c in ps.transform)
            if (c.TryGetComponent(out ParticleSystem child))
                deepest = Mathf.Max(deepest, Max(child));   // recurse — no manual summing
        return deepest;
    }
}
