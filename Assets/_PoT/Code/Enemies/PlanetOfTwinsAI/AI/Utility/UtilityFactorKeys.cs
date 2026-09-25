/// <summary>
/// Central constants for UtilityWeightProfile factor blackboard keys.
/// Use these instead of typing strings directly in the SO Inspector.
/// Eliminates string mismatch errors across all utility profiles.
///
/// USAGE in UtilityFactor.blackboardKey field — type the const value:
///   UtilityFactorKeys.HasTarget        → "Self.Awareness.BestTarget.GameObject"
///   UtilityFactorKeys.EnemyHealthNorm  → "PoT.Enemy.HealthNorm"
///   etc.
/// </summary>
public static class UtilityFactorKeys
{
    // ── Perception ─────────────────────────────────────────
    // Must match CommonCore.Names.Awareness_BestTarget (the canonical key perception writes).
    public const string HasTarget = "Self.Awareness.BestTarget.GameObject";
    public const string PerceptionConfidence = "PoT.Enemy.PerceptionConfidence";

    // ── Enemy state ────────────────────────────────────────
    public const string EnemyHealthNorm = "PoT.Enemy.HealthNorm";
    public const string EnemyIsPossessed = "PoT.Enemy.IsPossessed";
    public const string EnemyIsStunned = "PoT.Enemy.IsStunned";
    public const string EnemyIsGrabbing = "PoT.Enemy.IsGrabbing";
    public const string EnemyMoodState = "PoT.Enemy.MoodState";
    public const string EnemyDarkEnergyNorm = "PoT.Enemy.DarkEnergyNorm";  // Phase 3

    // ── Game state ─────────────────────────────────────────
    public const string SharedHealthNorm = "PoT.Game.SharedHealthNorm";
    public const string IsRescueActive = "PoT.Game.IsRescueActive";
    public const string AccordStateActive = "PoT.Game.AccordStateActive";
    public const string SoulIsActive = "PoT.Game.SoulIsActive";

    // ── Squad / shared ─────────────────────────────────────
    public const string AllyCountNorm = "PoT.Game.AllyCountNorm";
    public const string FactionEnergyNorm = "PoT.Game.FactionEnergyNorm";
    public const string EnergyBurstActive = "PoT.Game.EnergyBurstActive";
    public const string TargetIsEngaged = "PoT.Squad.TargetIsEngaged";
    public const string AllyGrabbed = "PoT.Squad.AllyGrabbed";

    // ── Bond / Phase 3 ─────────────────────────────────────
    public const string BondPartnerAlive = "PoT.Bond.PartnerAlive";
    public const string BondBroken = "PoT.Bond.Broken";
    public const string ComboReady = "PoT.Bond.ComboReady";
    public const string NearCompatibleAlly = "PoT.Bond.NearCompatibleAlly";

    // ── Spawn / Ritual ─────────────────────────────────────
    public const string SpawnUnderAttack = "PoT.World.SpawnUnderAttack";
    public const string RitualSiteAvailable = "PoT.POI.RitualSiteAvailable";
}