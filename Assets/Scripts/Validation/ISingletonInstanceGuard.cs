/// <summary>
/// Opt-out for the duplicate-singleton canary (<see cref="SceneIntegrityChecker"/>). A component whose type has a
/// static <c>Instance</c> can implement this to declare a DELIBERATE second instance that is NOT the singleton —
/// e.g. the couch P2 <c>TwinInputReader</c> (a non-shared reader bound to a second device). When
/// <see cref="IsSingletonInstance"/> is false, the integrity check skips this object instead of flagging a false
/// R3 duplicate. Defined outside the editor/dev guard so implementers compile in every build.
/// </summary>
public interface ISingletonInstanceGuard
{
    /// <summary>True = this IS the canonical singleton instance; false = a deliberate non-singleton instance the
    /// duplicate-singleton check must ignore.</summary>
    bool IsSingletonInstance { get; }
}
