/// <summary>
/// Couch co-op M0 seam: maps a twin to the <see cref="IInputProvider"/> that drives it.
///
/// TODAY (single-player / M0): <see cref="ProviderFor"/> returns the ONE shared reader for BOTH
/// twins, and <see cref="Shared"/> is that same reader — behaviour is identical to before.
///
/// LATER (M1, per-player split): the router becomes device-aware — P1's provider drives one twin,
/// P2's the other — fed by the character-select PlayerRoster. Because every per-player consumer
/// resolves its input through this seam instead of holding an <see cref="IInputProvider"/> directly,
/// the split is a change HERE, not a rewrite across the ~26 input consumers.
/// </summary>
public interface IPlayerInputRouter
{
    /// <summary>The provider that drives <paramref name="twin"/>. M0: the shared reader for all twins.</summary>
    IInputProvider ProviderFor(Player twin);

    /// <summary>
    /// The single shared reader — used by NON per-player consumers (pause / skill tree / overview /
    /// intro-skip / QTE / hints). In M1 this becomes an "any-of both devices" aggregator so either
    /// player can drive shared-UI input; per-player gameplay must use <see cref="ProviderFor"/>.
    /// </summary>
    IInputProvider Shared { get; }
}
