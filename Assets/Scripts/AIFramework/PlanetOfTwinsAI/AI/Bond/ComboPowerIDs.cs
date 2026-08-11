/// <summary>
/// Central constants for combo power IDs.
/// Use these in ProximityPowerEntry.powerID — dropdown in Inspector via [ComboPowerID].
/// Eliminates string mismatch errors across all proximity power profiles.
///
/// Naming convention: [EnemyType]_[Action]
/// Each ID describes what THIS enemy does in the combo — not the combo name.
/// </summary>
public static class ComboPowerIDs
{
    // ── TetherBreaker actions ──────────────────────────────
    public const string TB_PinHold = "TB_PinHold";      // Hold chain pin, don't drag
    public const string TB_DragToAlly = "TB_DragToAlly";   // Drag twin toward ally
    public const string TB_Standard = "TB_Standard";     // Normal chain, ally coordinates

    // ── BasicMelee actions ─────────────────────────────────
    public const string Melee_ChargeOnPin = "Melee_ChargeOnPin";    // Charge pinned twin
    public const string Melee_PrePosition = "Melee_PrePosition";    // Move to drag destination
    public const string Melee_CornerAssist = "Melee_CornerAssist";   // Corner twin for chain

    // ── Severed actions ────────────────────────────────────
    public const string Sev_EarlyRage = "Sev_EarlyRage";    // Grief rage on combo activation
    public const string Sev_FlankAttack = "Sev_FlankAttack";  // Attack from opposite side

    // ── GroupGrab actions ──────────────────────────────────
    public const string GG_GrabAndHold = "GG_GrabAndHold";      // Grab, partner piles max priority
    public const string GG_GrabAndDistract = "GG_GrabAndDistract";  // Grab one twin, partner attacks other

    // ── Siphon actions ─────────────────────────────────────
    public const string Siph_EarlyGhost = "Siph_EarlyGhost";     // Ghost on combo not rescue
    public const string Siph_ExtendedBomb = "Siph_ExtendedBomb";   // Larger bomb trigger range

    // ── Ranged actions ─────────────────────────────────────
    public const string Range_SuppressiveFire = "Range_SuppressiveFire";  // Pin twin with arrows, ally closes
    public const string Range_ExecutionShot = "Range_ExecutionShot";    // High damage shot on grabbed twin
    public const string Range_Standard = "Range_Standard";          // Normal ranged, combo awareness

    // ── Witness actions ────────────────────────────────────
    public const string Wit_IntenseBuff = "Wit_IntenseBuff";    // Double buff strength on combo
    public const string Wit_Standard = "Wit_Standard";       // Normal shadow + buff

    // ── Summoner actions ───────────────────────────────────
    public const string Sum_EmergencySummon = "Sum_EmergencySummon";  // Summon immediately on combo
    public const string Sum_Standard = "Sum_Standard";         // Normal summon timing

    // ── Penitent actions ───────────────────────────────────
    public const string Pen_ReflectAmplify = "Pen_ReflectAmplify";  // Reflection damage boosted
    public const string Pen_Standard = "Pen_Standard";        // Normal approach + reflect

    // ── Generic ────────────────────────────────────────────
    public const string ThirdWheelBoost = "ThirdWheelBoost";  // Third pact member bonus attack
    public const string FallbackCombo = "FallbackCombo";    // Generic enhanced attack fallback
}