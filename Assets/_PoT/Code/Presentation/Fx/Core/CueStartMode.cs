/// <summary>
/// When a Cue Book element begins, relative to the element before it ( / Cue Book redesign,
/// 2026-06-25). Lives in the dependency-free <c>PlanetOfTwins.FxCore</c> assembly so the timing math
/// (<see cref="CueSchedule"/>) is unit-testable without a scene.
///
/// Three modes, no overlap (the old <c>AfterPrevious</c> was a lie in a Cue Book — it chained on the
/// previous element's START, identical to <see cref="WithPrevious"/>; it was removed):
/// <list type="bullet">
///   <item><see cref="Immediate"/> — fire at the effect's t=0 (+ own delay); ignores the previous element.</item>
///   <item><see cref="WithPrevious"/> — fire at the previous element's START (+ delay); parallel/staggered.</item>
///   <item><see cref="AfterPreviousCompletion"/> — fire when the previous element actually STOPS (+ delay);
///         EVENT-DRIVEN in <c>CueBookRunner</c> (natural end OR a cut OR a gameplay Stop — whichever ends it).</item>
/// </list>
/// Explicit int values: existing assets stored <c>0</c> (old AfterPrevious) / <c>1</c> (WithPrevious). A one-time
/// migration converts the <c>0</c>s (now read as <see cref="Immediate"/>) — first element → keep Immediate,
/// otherwise → <see cref="WithPrevious"/> (preserves the old chain-on-start behavior). Do not reorder.
/// </summary>
public enum CueStartMode
{
    /// <summary>Start at the EFFECT's t=0 (plus this element's delay). Ignores the previous element — use for the
    /// first element, or any element that should fire the instant the effect id is played.</summary>
    Immediate = 0,

    /// <summary>Start at the previous element's START (plus this element's delay). Delay 0 = true parallel;
    /// delay &gt; 0 = staggered overlap. Use to pair a sound with its visual, or run two visuals together.</summary>
    WithPrevious = 1,

    /// <summary>Start when the previous element actually STOPS (plus this element's delay). "Stops" = its natural
    /// lifetime ends, OR a cut stops it, OR gameplay stops it — whichever comes first (EVENT-DRIVEN, resolved at
    /// runtime by CueBookRunner). NOTE: if the previous element LOOPS (VFX graph / looping particle), it has no
    /// natural end — it must be ended by code (CueHandle.Stop) or a cut, or this element will never fire.</summary>
    AfterPreviousCompletion = 2,
}
