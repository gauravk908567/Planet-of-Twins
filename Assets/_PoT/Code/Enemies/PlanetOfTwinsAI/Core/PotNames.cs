using CommonCore;

/// <summary>
/// All Blackboard keys used by the Planet of Twins AI system.
///
/// CONVENTIONS:
///   PoT.Game.*     - Written by existing PoT game systems (RescueEventController, AccordStateSystem etc)
///   PoT.Enemy.*    - Written by individual enemy brains (per-enemy blackboard)
///   PoT.Squad.*    - Written to shared blackboard for cross-enemy coordination
///   PoT.World.*    - Written by environmental systems (spawn points, cracks, shrines)
/// </summary>
public static class PoTNames
{
    // ── Game State ─────────────────────────────────────────────
    // Written by RescueEventController
    public static readonly FastName IsRescueActive = new("PoT.Game.IsRescueActive");
    public static readonly FastName HasRescueTarget = new("PoT.Game.HasRescueTarget");
    public static readonly FastName RescuedTwin = new("PoT.Game.RescuedTwin");

    // Written by AccordStateSystem
    public static readonly FastName AccordStateActive = new("PoT.Game.AccordStateActive");
    public static readonly FastName AccordBarFill = new("PoT.Game.AccordBarFill");

    // Written by SharedHealthPool
    public static readonly FastName SharedHealthNorm = new("PoT.Game.SharedHealthNorm");

    // Written by SoulPlayer activation
    public static readonly FastName SoulIsActive = new("PoT.Game.SoulIsActive");
    public static readonly FastName SoulPosition = new("PoT.Game.SoulPosition");

    // ── World State ────────────────────────────────────────────
    // Written by destroyable spawn points
    public static readonly FastName SpawnUnderAttack = new("PoT.World.SpawnUnderAttack");
    public static readonly FastName AttackedSpawnPoint = new("PoT.World.AttackedSpawnPoint");

    // Written by barrier/crack system
    public static readonly FastName BarrierWidening = new("PoT.World.BarrierWidening");
    public static readonly FastName DarkEnergyAvailable = new("PoT.World.DarkEnergyAvailable");
    public static readonly FastName NearestCrackPosition = new("PoT.World.NearestCrackPosition");

    // ── Squad Coordination ─────────────────────────────────────
    // Written to SHARED blackboard — cross-enemy awareness
    public static readonly FastName TargetIsEngaged = new("PoT.Squad.TargetIsEngaged");
    public static readonly FastName EngagedTarget = new("PoT.Squad.EngagedTarget");
    public static readonly FastName AllyGrabbed = new("PoT.Squad.AllyGrabbed");
    public static readonly FastName PileOnTarget = new("PoT.Squad.PileOnTarget");
    public static readonly FastName ActiveGhostCount = new("PoT.Squad.ActiveGhostCount");

    // ── Per-Enemy State ────────────────────────────────────────
    // Written by individual enemy brains to their own blackboard
    public static readonly FastName EnemyHealth = new("PoT.Enemy.Health");
    public static readonly FastName EnemyHealthNorm = new("PoT.Enemy.HealthNorm");
    public static readonly FastName EnemyIsPossessed = new("PoT.Enemy.IsPossessed");
    public static readonly FastName EnemyIsStunned = new("PoT.Enemy.IsStunned");
    public static readonly FastName EnemyMood = new("PoT.Enemy.Mood");           // Penitent
    public static readonly FastName EnemyIsGrabbing = new("PoT.Enemy.IsGrabbing");
    public static readonly FastName EnemyFollowTarget = new("PoT.Enemy.FollowTarget");   // Witness
    public static readonly FastName EnemyAllyAlive = new("PoT.Enemy.AllyAlive");      // Witness / Severed
    public static readonly FastName ReflectionActive = new("PoT.Enemy.ReflectionActive"); // Penitent
    public static readonly FastName BombOnCooldown = new("PoT.Enemy.BombOnCooldown"); // Witness / Siphon
    public static readonly FastName EnemyPartnerDead = new("PoT.Enemy.PartnerDead");
    public static readonly FastName EnemyInGriefRage = new("PoT.Enemy.InGriefRage");
    public static readonly FastName EnemyInRage = new("PoT.Enemy.InRage");
    public static readonly FastName ChainOnCooldown = new("PoT.Enemy.ChainOnCooldown");
    public static readonly FastName CanSummon = new("PoT.Enemy.CanSummon");
    public static readonly FastName WitnessAllyAlive = new("PoT.Enemy.WitnessAllyAlive");
    public static readonly FastName WitnessIsRitualing = new("PoT.Enemy.WitnessIsRitualing");

    // Utility factors (normalised 0-1, written by PoTWorldStateWriter or brains)
    public static readonly FastName AllyCountNorm = new("PoT.Game.AllyCountNorm");
    public static readonly FastName FactionEnergyNorm = new("PoT.Game.FactionEnergyNorm");
    public static readonly FastName EnergyBurstActive = new("PoT.Game.EnergyBurstActive");

    // Mood
    public static readonly FastName EnemyMoodState = new("PoT.Enemy.MoodState");
    public static readonly FastName PerceptionConfidence = new("PoT.Enemy.PerceptionConfidence");
    public static readonly FastName LastKnownPosition = new("PoT.Enemy.LastKnownPosition");
    public static readonly FastName HasPerceptionMemory = new("PoT.Enemy.HasPerceptionMemory");
    public static readonly FastName EnemySearchState = new("PoT.Enemy.SearchState");

    public static readonly FastName EnemyDarkEnergyNorm = new("PoT.Enemy.DarkEnergyNorm");
    public static readonly FastName BondPartnerAlive = new("PoT.Bond.PartnerAlive");
    public static readonly FastName BondBroken = new("PoT.Bond.Broken");
    public static readonly FastName ComboReady = new("PoT.Bond.ComboReady");
    public static readonly FastName NearCompatibleAlly = new("PoT.Bond.NearCompatibleAlly");
    public static readonly FastName IsCommitting = new("PoT.Bond.IsCommitting");
    public static readonly FastName IsRallying = new("PoT.Bond.IsRallying");
    public static readonly FastName ComboRallyPoint = new("PoT.Bond.RallyPoint");
    public static readonly FastName NearBarrier = new("PoT.POI.NearBarrier");
    public static readonly FastName SpawnUnderAttackPOI = new("PoT.POI.SpawnUnderAttack");
    public static readonly FastName NearestSpawnDist = new("PoT.POI.NearestSpawnDist");
    public static readonly FastName RitualSiteAvailable = new("PoT.POI.RitualSiteAvailable");
    public static readonly FastName BombReady = new("PoT.Witness.BombReady");
    public static readonly FastName TwinInBombRange = new("PoT.Witness.TwinInBombRange");
    public static readonly FastName GhostIsBinding = new FastName("Ghost.IsBinding");
    public static readonly FastName GhostIsRetreating = new FastName("Ghost.IsRetreating");
    public static readonly FastName GhostKillWindow = new FastName("Ghost.KillWindow");
    public static readonly FastName GhostSoulInRange = new FastName("Ghost.SoulInRange");
    // Commander
    public static readonly FastName CommanderAlive = new("PoT.Commander.Alive");
    public static readonly FastName CommanderPosition = new("PoT.Commander.Position");

    // Clan War
    public static readonly FastName ClanWarActive = new("PoT.World.ClanWarActive");
    public static readonly FastName ClanWarIntensity = new("PoT.World.ClanWarIntensity");

    // Twin threat (per-enemy, written by each brain)
    public static readonly FastName TwinInDangerRange = new("PoT.Enemy.TwinInDangerRange");
    // All enemies use the same shared blackboard ID for squad coordination
    public const int SharedBlackboardID = 0;
}