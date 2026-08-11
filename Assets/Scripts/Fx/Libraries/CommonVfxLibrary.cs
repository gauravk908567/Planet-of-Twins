using UnityEngine;

/// <summary>
/// The COMMON VFX library — the cross-cutting mirror of <see cref="PlayerVfxLibrary"/> / <see cref="EnemyVfxLibrary"/>.
/// Holds the shared <see cref="CueBookData"/> books that belong to NEITHER domain alone because both player and enemy
/// (or the world) trigger them — hit sparks, buff/debuff auras, enemy-spawn, social reactions. Consumers on either
/// side pull their book from here through <see cref="VfxLibraryProvider"/> (Persistent, R4) and play it with
/// <c>FxManager.PlayBook(book, FxIds.Common.…, ctx)</c>.
///
/// MODEL (same as the other libraries — do not re-derive): each book is a container of NAMED effects played by id;
/// this library is only the placement point, grouped by role. CONFIG ONLY (R7) — no runtime state.
///
/// Add a shared book = add a slot here + drag the asset in. Add an id = author it on the book + regenerate FxIds
/// (Tools ▸ Planet of Twins ▸ Cue Id Verifier ▸ Generate FxIds).
/// </summary>
[CreateAssetMenu(menuName = "PlanetOfTwins/Fx/Library/Common VFX Library", fileName = "CommonVfxLibrary")]
public class CommonVfxLibrary : ScriptableObject
{
    // Field names are PascalCase ON PURPOSE — the FxIds generator nests by slot name → FxIds.Common.Effects.<id>.

    [Header("Shared effects")]
    [Tooltip("Cross-cutting status/combat feedback — hit spark, ally buff, any debuff, enemy spawn. " +
             "Triggered by whichever actor caused the event (enemy hit, buff aura, spawn…).")]
    public CueBookData Effects;

    // CueBook_Reactions (betrayed / ally_down) drops in here as a second slot if it proves cross-cutting.
}
