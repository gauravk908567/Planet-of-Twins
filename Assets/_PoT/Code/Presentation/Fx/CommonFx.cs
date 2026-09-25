using UnityEngine;

/// <summary>
/// Thin play helper for the CROSS-CUTTING <c>CommonVfxLibrary.Effects</c> book (hit spark, ally buff, any debuff,
/// enemy spawn). These cues are triggered from many unrelated systems that don't otherwise deal with VFX libraries
/// (spawner, enemy attack controller, status-effect system, buff auras) — so instead of each one re-resolving the
/// provider, they call this. Resolution is the R4 pattern (<see cref="VfxLibraryProvider"/> singleton); a missing
/// provider/library/book is a silent no-op here (the provider itself LogErrors on a null Common slot at Awake).
///
/// Not a new wiring pattern — just one place that does <c>VfxLibraryProvider.Instance.Common.Effects</c> +
/// <c>FxManager.PlayBook</c>, so the ~8 shared-cue call sites stay one-liners and can't drift. Ids: FxIds.Common.Effects.*
/// </summary>
public static class CommonFx
{
    public static CueHandle Play(string id, CueContext ctx)
    {
        var book = VfxLibraryProvider.Instance?.Common?.Effects;
        if (book == null) return CueHandle.None;
        return FxManager.Instance?.PlayBook(book, id, ctx) ?? CueHandle.None;
    }
}
