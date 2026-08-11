using UnityEngine;

/// <summary>
/// The ENEMY VFX library — the enemy-domain mirror of <see cref="PlayerVfxLibrary"/>. ONE asset holding every
/// enemy archetype's <see cref="CueBookData"/>. An enemy no longer carries its own <c>[SerializeField] CueBookData</c>;
/// it pulls its archetype book from here through <see cref="VfxLibraryProvider"/> (Persistent, R4) and plays it with
/// <c>FxManager.PlayBook(book, FxIds.…, ctx)</c>. Enemies that spawn projectiles (arrow, bomb) hand this book to the
/// projectile so the cosmetics match the thrower (e.g. <c>BombProjectile.ConfigureCues</c>).
///
/// MODEL (same as the player library — do not re-derive): each book is a container of NAMED effects played by id;
/// this library is only the placement point, grouped by archetype. CONFIG ONLY (R7) — no runtime state.
///
/// Add an archetype = add a slot here + drag the asset in. Add an id = author it on the book + regenerate FxIds
/// (Tools ▸ Planet of Twins ▸ Cue Id Verifier ▸ Generate FxIds).
/// </summary>
[CreateAssetMenu(menuName = "PlanetOfTwins/Fx/Library/Enemy VFX Library", fileName = "EnemyVfxLibrary")]
public class EnemyVfxLibrary : ScriptableObject
{
    // Field names are PascalCase ON PURPOSE — the FxIds generator nests by slot name → FxIds.Enemy.Witness.<id>.

    [Header("Basic / ranged")]
    [Tooltip("Melee enemy — e.g. \"On_MeleeAttack\".")]
    public CueBookData Melee;
    [Tooltip("Ranged enemy — e.g. \"On_RangedAttack\".")]
    public CueBookData Ranged;
    [Tooltip("Summoner — e.g. \"On_smnSummon\", \"On_smmAttack\".")]
    public CueBookData Summoner;

    [Header("Grab / chain")]
    [Tooltip("Tether-Breaker — chain marker/clank/miss/grab/drag/melee (the break is driver-owned).")]
    public CueBookData TetherBreaker;
    [Tooltip("Group-Grab (Warden) — attack/grab/soul-consume/struggle.")]
    public CueBookData GroupGrab;

    [Header("Severed / Siphon")]
    [Tooltip("Severed — \"On_SevererdAttack\" (rage moves to the Manpu layer later).")]
    public CueBookData Severed;
    [Tooltip("Siphon — ranged / ghost-spawn + its bomb's fuse/impact ids (handed to the bomb).")]
    public CueBookData Siphon;
    [Tooltip("Siphon Ghost — chain marker/break/miss/grab + immune.")]
    public CueBookData SiphonGhost;

    [Header("Witness")]
    [Tooltip("Witness — ritual / aura.")]
    public CueBookData Witness;

    [Header("Weapons (shared projectile books — handed to the projectile by the thrower)")]
    [Tooltip("Arrow — arrow_Trail / arrow_Head (held Follow cues) + arrow_OnImpact.")]
    public CueBookData Arrow;
    [Tooltip("Witness bomb — On_WitnessBombFuseOn / On_WitnessBombExplode.")]
    public CueBookData WitnessBomb;
    [Tooltip("Siphon bomb — On_SiphonBombFuseOn / On_SiphonBombExplode.")]
    public CueBookData SiphonBomb;

    // Penitent (dropped from playtest) and the commanders (using player books, may not ship) are intentionally
    // omitted — add a slot here if/when they ship.
}
