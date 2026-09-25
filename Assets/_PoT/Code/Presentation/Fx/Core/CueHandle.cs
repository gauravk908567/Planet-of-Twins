using System;

/// <summary>
/// Opaque, version-stamped reference to a live cue instance. Hold this —
/// never a raw <c>ParticleSystem</c>/<c>AudioSource</c> — across frames: once the underlying pooled
/// instance is reclaimed and reissued, a stale handle is INERT on Stop/IsPlaying. Plain value type
/// in the dependency-free <c>PlanetOfTwins.FxCore</c> assembly.
/// </summary>
public readonly struct CueHandle : IEquatable<CueHandle>
{
    public readonly int Id;
    public readonly int Version;

    public CueHandle(int id, int version)
    {
        Id = id;
        Version = version;
    }

    /// <summary>The "nothing playing" handle. Returned when a Play call could not start anything.</summary>
    public static readonly CueHandle None = new CueHandle(-1, 0);

    public bool IsNone => Id < 0;

    public bool Equals(CueHandle other) => Id == other.Id && Version == other.Version;
    public override bool Equals(object obj) => obj is CueHandle h && Equals(h);
    public override int GetHashCode() => unchecked((Id * 397) ^ Version);
    public override string ToString() => IsNone ? "CueHandle.None" : $"CueHandle({Id}.{Version})";
}
