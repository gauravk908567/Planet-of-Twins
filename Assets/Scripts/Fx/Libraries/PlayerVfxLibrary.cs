using UnityEngine;

/// <summary>
/// The PLAYER VFX library (VFX Library layer, 2026-06-22). ONE asset that is the central
/// container of every player-side <see cref="CueBookData"/> — abilities, melee, locomotion, Accord. A caller no
/// longer carries its own <c>[SerializeField] CueBookData</c>; it pulls the relevant book from here through
/// <see cref="VfxLibraryProvider"/> (Persistent, R4) and plays it with <c>FxManager.PlayBook(book, FxIds.…, ctx)</c>.
///
/// MODEL (do not re-derive): the book is still a container of NAMED effects played by id. This
/// library is only the *placement point* for the books, grouped by domain. Effect DURATION is NOT here — a looping
/// effect is stopped by its caller via the <c>CueHandle</c> when the gameplay window closes (e.g. StunAbility stops
/// its held handle in End()), so an upgraded 7s stun shows VFX for exactly 7s. CONFIG ONLY (R7) — no runtime state.
///
/// Add a book = add a slot here + drag the asset in. Add an id = author it on the book + regenerate FxIds
/// (Tools ▸ Planet of Twins ▸ Cue Id Verifier ▸ Generate FxIds). The id is written ONCE, on the book.
/// </summary>
[CreateAssetMenu(menuName = "PlanetOfTwins/Fx/Library/Player VFX Library", fileName = "PlayerVfxLibrary")]
public class PlayerVfxLibrary : ScriptableObject
{
    // NOTE: field names are PascalCase ON PURPOSE — the FxIds generator nests by slot name, so these become
    // FxIds.Player.Stun.<id>, FxIds.Player.Attack.<id>, … (conventional C# member casing at the call site).

    [Header("Q / Primary abilities")]
    [Tooltip("Stun ability — e.g. \"OnStun_Active\" held cast on caster, \"OnStun_Hit\" per stunned enemy.")]
    public CueBookData Stun;
    [Tooltip("Possess ability book.")]
    public CueBookData Possess;
    [Tooltip("Teleport / Gate ability book.")]
    public CueBookData Teleport;
    [Tooltip("Radiant Seeker orb ability book.")]
    public CueBookData RadiantSeeker;
    [Tooltip("Coalesce book — the aura + burning-aura upgrade.")]
    public CueBookData Coalesce;

    [Header("Melee / attack")]
    [Tooltip("Twin melee attack — e.g. \"swing\" on the attacker, \"hit\" on each enemy struck.")]
    public CueBookData Attack;

    [Header("Soul / Gate systems")]
    [Tooltip("Soul pulse — e.g. \"pulse\".")]
    public CueBookData SoulPulse;
    [Tooltip("Soul convergence — e.g. \"charge\" per twin.")]
    public CueBookData SoulConvergence;

    [Header("Accord")]
    [Tooltip("Accord Spirit — e.g. \"charge\" per twin, \"impact\".")]
    public CueBookData AccordSpirit;
    [Tooltip("Accord State — charge-up / active / buff / shockwave per twin.")]
    public CueBookData AccordState;
    // (AccordMelee slot removed 2026-07-11 — accord melee consolidated into the Attack book;
    //  the old slot pointed at a deleted CueBookData and nothing read it.)
    [Tooltip("Void Strike (Accord State) — e.g. held hazard-point loop.")]
    public CueBookData VoidStrike;

    [Header("Time")]
    [Tooltip("Setsuna — charge / trail per twin.")]
    public CueBookData Setsuna;

    [Header("Buffs")]
    [Tooltip("Empower — e.g. \"impact\".")]
    public CueBookData Empower;

    // Footsteps / locomotion books drop in here as authored — keep player-domain books in THIS library only.
}
