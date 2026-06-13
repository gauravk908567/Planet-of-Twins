# Planet of Twins — Game Systems Reference

> Living technical design document. Describes every gameplay system currently in the
> project, how they connect, how the **implementation diverges from the design docs**
> (`planet_of_twins_story_bible_v4.docx`, `controls_and_abilities.docx`), and a
> recommended production-grade folder structure.
>
> **Engine:** Unity **6000.3.5f2 (Unity 6.3)** · URP 17.3 · New Input System · Cinemachine 3 · NavMesh AI · Localization · VFX Graph
> **Last reviewed against branch:** `multiscenesetup` · **Rev 2 (2026-06-10):** corrected
> multi-scene architecture (§16 Rulebook is canonical), QTE rewrite (§13), tutorial
> multi-scene model (§12), checkpoint/soft-reset model (§11), reference-fix triage (§21).
> Companion docs: `instruction.md` (active correction work order), `changelog.md`,
> `CLAUDE.md` (working conventions), `WIRING_GUIDE.md`.

---

## 1. Premise (for context)

Two twins — **Kai** (Vethara / dark-energy, controlled as the **right** twin) and
**Lyra** (Luminari / soul-light, the **left** twin) — are torn apart when the villain
**Tahr** separates the temple idols on the Day of Accord, inverting the ancient curse.
Instead of binding twins together, the planet now pulls them apart. The corrupted
populace (the sub-clans) become the enemies. The twins fight back to the temple to
restore the idols.

Story-bible context worth keeping straight in content work (full detail in
`planet_of_twins_story_bible_v4.docx`): this is a **single-player** game — one player
drives both twins. Tahr is no local villain but the king of the off-world **Khal-Vor
Empire**, a centuries-patient devotee of dark energy pointed at this planet by the Archon
**Orveth**; his general **Vael-Kor** directs commanders into the city from a distance and
never appears in this game. The corruption is the **Voreth** — "the hunger beneath," a
residue of the old Luminari/Vethara war, not a sentient evil. The **five Archons**
(Yomiren/time, Zhuang/formation, Kanrei/memory, Mushin/balance, Orveth/dark energy)
created the twin boon-and-curse; the clan gods sealed their souls into the **Vow Vessels**
that Kai and Lyra carry. Defeated enemies are corrupted neighbours, and their freed souls
feed the Vessels — kills should read as grief, not triumph.

The two pillars that drive *every* mechanic:
- **The twins must stay close.** Distance between them drains shared health (the inverted curse).
- **Defeated enemies were people.** Their souls feed the Vow Vessels (Soul Convergence), framing power as grief.

---

## 2. The Gameplay Loop

```
Move both twins (WASD) → switch active twin (Shift) → fight corrupted enemies
  (melee E / ability Q) → manage distance (health drain) → collect soul + skill orbs
  → spend skill points (Tab) → charge Accord bar (X) & Soul Convergence (F)
  → survive rescue events when a twin is downed (Weaver's Gate C + mash F)
  → reach checkpoints → clear zones → open gates (QTE) → progress to next area.
```

Failure (game over) triggers on: a downed twin's TTK timer expiring, both twins
grabbed simultaneously, or the rescue soul dying with no time to recast the Gate.

---

## 3. Input & Twin Control

All input is read in one place and fanned out to dispatchers. **Both twins always move
together**; `Left Shift` only changes which twin is "selected" for abilities.

### Flow
```
TwinInputReader (IInputProvider)              ← raw keyboard/mouse, gated by tutorial
   ├── TwinMovementDispatcher  → PlayerMovementController (×2, via MoveCommand)
   ├── TwinAttackDispatcher    → PlayerAttackController   (selected twin)
   └── TwinAbilityDispatcher   → AbilityController        (selected twin)

TwinSelector (ITwinSelector, ISelectionLock, ISelectionBroadcaster)
   └── assigns NormalMovementModifier to selected twin,
       MirroredMovementModifier to the other (mirrors X input).
```

| Key | Action |
|-----|--------|
| WASD | Move both twins |
| Left Shift | Switch active twin / Dash (during Empower) |
| E / LMB | Melee attack (2 m) |
| Q / RMB | Primary ability (Kai = Stun, Lyra = Possession) |
| C | Weaver's Gate (rescue / low-health only) / Soul-break mash |
| F | Rescue mash / hold = Soul Convergence / hold = Setsuna (in Accord) / interact (QTE) |
| R | Hold = Empower / hold = Accord Spirits (in Accord) |
| B | Hold = top-down overview (5 s, then cooldown) |
| X | Hold = activate Accord State / cancel Gate, QTE, Empower |
| Tab | Skill Tree |
| L/O/P/I/K | **Debug** skill-point keys — remove before release |

- `TwinInputReader` uses **legacy `Input.GetKey`** directly (not the new Input System
  bindings) despite the Input System package being present. Hardcoded `KeyCode`s.
- `TutorialInputGate` (`ITutorialGate`) optionally gates each input category; null
  outside the tutorial scene so all input is allowed.
- `ISelectionLock` uses a **counter** so overlapping locks (teleport + grab) compose
  correctly — selection only unlocks when all locks release.

---

## 4. Health, Bond & Distance

The defining co-op mechanic. Each twin has its own `PlayerHealthComponent`, but they
share a single pool and take distance-based drain.

| Component | Role |
|-----------|------|
| `SharedHealthPool` (`ISharedHealthPool`) | Combined pool, **200 base** (2×100). Sums both twins' display health, raises `OnCombinedHealthChanged` / `OnSharedPoolEmpty`. `ForceSetHealth` used by Setsuna rewind. |
| `PlayerHealthComponent` | Per-twin health; distinguishes `CombatHealth` vs `DisplayHealth`. |
| `DistanceHealthSystem` | `CalculateDistanceModifier(distance, upgradeLevel)`: ≤6 m = full (1.0); 6–12 m lerp 100→50%; 12–16 m 50→25%; 16–18 m 25→1%; >18 m = 0. Upgrade bonus `9·L − L²`. |
| `DistanceModifierCalculator` / `OverMaxDistanceDrainCalculator` | Strategy objects for in-range modifier and over-max drain. |
| `TetherDistanceCalculator` | Measures twin separation for the above. |
| `DistanceZone` (`IDistanceAffected`) | Environmental zones that apply additional drain. |
| `TwinBondManager` | Coordinates the bond/shared mechanics across both twins. |
| `UpgradeManager` | Applies health-related skill upgrades into the calculation. |
| `HealthRegenHandler` | Passive/triggered regen (skill-tree gated). |

UI presenters: `SharedHealthPresenter`, `IndividualHealthPresenter`, `HealthBarView`,
plus world-space `WorldSpaceHealthUI`.

---

## 5. Combat & Damage

Single damage contract flows everywhere:

```csharp
public readonly struct DamageData {
    float Amount; DamageType Type;   // Combat, Ability, Environmental, LinkedDamage
    GameObject Source; Vector3 HitPoint;
}
```

- `IDamageable.TakeDamage(DamageData)` — implemented by enemies and players.
- `DamageType.LinkedDamage` exists specifically to break infinite loops in **Severed**
  shared-health propagation.
- `PlayerAttackController` + `IAttackStrategy` (`MeleeAttackStrategy`) — melee swing.
- Projectiles: `ProjectileBase` → `Arrow`, `BombProjectile`, `ChainProjectile`;
  launched via `IProjectileLauncher` (`ProjectileAttackLauncher`, `RaycastAttackLauncher`).
- `AttackRangeIndicator` / `AbilityRadiusPreview` for telegraphing.
- `IDamageMultiplier` for buffs (Empower, possession damage).
- `SwordPickup` — the twins acquire/lose a sword (checkpoint-saved sword state).

---

## 6. Player Abilities

Abilities live under `Players/Ability/`. The `AbilityController` (`IAbilityLock`) holds
a swappable **primary** ability and a **teleport** ability per twin, with composable
suppression locks (full lock + primary-only lock used by enemy bombs).

### Base abilities
| Ability | Owner | Key | Notes |
|---------|-------|-----|-------|
| **Stun** (`StunAbility`, `StunEffect`) | Kai | Q | Pulse freezes nearby enemies (3 s / 7 s cd). Pauses enemy brain. |
| **Possession** (`PossessionAbility`, `PossessEffect`) | Lyra | Q | Seizes one enemy to fight allies (3 s / 7.5 s cd). |
| **Coalesce** (`CoalesceSystem`, `CoalesceAura`) | auto | — | Stunned/possessed enemies emit damaging aura. Skill-gated, automatic. |
| **Soul Convergence** (`SoulConvergenceSystem`) | shared | hold F | Kills feed a soul counter (cap toned to ~8 for proto). At cap: +35% dmg / −35% taken for 7 s. Blocked during rescue. |
| **Empower** (`EmpowerSystem`) | shared | hold R | One twin anchors & emits shockwaves; other gains speed/damage + dash. |
| **Weaver's Gate / Teleport** (`TeleportAbility`, `EmergencyTeleportMonitor`) | shared | C | Only during rescue or health <20%. Soul crosses to dying twin. Has a cancel window (hold X 0.75 s). |

`SoulPulseSystem` powers a soul-side pulse used during rescue (Ashen Tide fear exclusion
tracking via `LastBlowEnemy`).

### Accord State (`AccordStateSystem`, `IAccordModeProvider`)
A second meter (`X`). Fills from kills (1 pt) and damage taken below health thresholds.
When full, **hold X 1.25 s** to activate (charge cancels if hit in first 0.7 s). For its
duration both twins' Q abilities are swapped to Accord variants. Blocked by rescue,
active Soul Convergence, or active Empower.

| Accord ability | Key | File |
|----------------|-----|------|
| Accord Melee (Daggertail + Radiant Sweep, 120° arc, slow) | E | `AccordMeleeAbility` |
| **Void Strike** (Kai) — seeds hazard points | Q | `VoidStrikeAbility` |
| **Radiant Seeker** (Lyra) — homing possess orb | Q | `RadiantSeekerAbility` + `RadiantSeekerOrb` |
| **Accord Spirits** — twin spirits cleanse + portal | hold R | `AccordSpiritSystem` + `AccordSpiritAgent` |
| **Setsuna** — ultimate | hold F (in Accord + SC charged) | `SetsunaSystem` |

**Setsuna** is the ceiling ability: snapshots twin positions + shared health, sets
`Time.timeScale = 0.15` while twins move on unscaled time, runs 7 s, then rewinds both
twins along their recorded path back to cast positions over 1.5 s and restores the health
snapshot. Auto-unlocks when both SC and Accord State are purchased (no separate node).

> ⚠️ **Setsuna uses global `Time.timeScale`.** Any system that must keep running in real
> time during Setsuna has to use `unscaledDeltaTime`. This couples Setsuna to the whole
> game's time flow — see the time-freeze system below, which is a *separate* mechanism.

---

## 7. Time Freeze / Soul Mode

Distinct from Setsuna's `Time.timeScale`. This is an **entity-level registry**:

- `TimeFactorManager` (`ITimeFactorRegistry`, `ITimeFactorController`) + `TimeFactorBootstrapper`.
- Entities implement `ITimeAffected` (`OnEffectStarted` / `OnEffectEnded`).
  - `Enemy.OnEffectStarted` → `PauseBrain()` (sets `IsBrainPaused`, freezes movement).
  - `Player.OnEffectStarted` → enters **soul mode** (movement + attack use soul settings).
- `TimeFactorRegistrar` registers an `ITimeAffected` with the registry (DIP-friendly, no `FindObjectOfType`).
- Enemy brains (`PoTGOAPBrainBase.Update`) early-out while `IsBrainPaused` is true —
  this is how stun, fear, grab, and possession-return all pause AI without a state machine.

---

## 8. Rescue Events (downed-twin save)

`RescueEventController` (`IRescueActive`, `ITutorialRescueProvider`) is a hand-rolled
state machine: `Idle → Triggered → Mashing → (Success | Cooldown | SoulDied | Failed)`
(states in `Players/RescueState/RescueState.cs`).

Flow when a twin is grabbed / downed:
1. Trap or `PlayerDeathRescueProxy` fires `OnPlayerGrabbed`; a **TTK countdown** begins.
2. Controller force-selects the free twin, locks selection, freezes the grabbed twin.
3. Free twin casts **Weaver's Gate (C)** → soul (`SoulPlayer`) travels to the grabbed twin.
4. On arrival within range, **mash F** to fill the rescue bar before the mash window expires.
5. Success → release with partial heal + invuln; the soul can die (`SoulDied`) and the
   Gate must be recast; both twins grabbed = instant fail.

Supporting:
- `SoulPlayer` (`Players/SoulPlayer/`) — the detached rescue soul; can be chained by a Siphon Ghost.
- Tier-1 traps allow **self-rescue** (`GetStruggleMash`, capped at 30% of TTK).
- Ghost cap = 1 active (`TryRegisterGhost`); `PauseTTK`/`ResumeTTK` delegated to the active target.
- `EmergencyTeleportMonitor` overrides selection for the endangered twin.

---

## 9. Enemy AI — the hybrid framework

This is the largest subsystem. It has **two layers**: a reusable engine-agnostic
framework (`AIFramework/`) and a **fully built** project layer (`PlanetOfTwinsAI/`).

> 🔴 **Major correction vs. earlier notes:** `PlanetOfTwinsAI/` is **not** a stub. It is
> the production enemy brain layer with per-type GOAP brains, mood, social bonds, faction
> energy, POIs, and a clan-war system. The old `EnemyStateMachine` / `EnemyDetection` /
> `OldFactionComponent` are **legacy and unused** (see `Enemy.cs` header).

### 9.1 Reusable framework (`AIFramework/`)
Three cooperating decision systems sharing a Blackboard:

| System | Namespace | Folder |
|--------|-----------|--------|
| Behaviour Tree | `BehaviourTree` | `BehaviourTree/` (nodes, decorators, services) |
| State Machine | `StateMachine` | `StateMachine/` (states, transitions) |
| Hybrid GOAP | `HybridGOAP` | `HybridGOAP/` (goals + actions that wrap BT/FSM) |

GOAP is the top-level planner; its actions (`GOAPActionBehaviourTree`, `GOAPAction_FSM`)
delegate execution to a BT or FSM. `CommonCore/` provides the shared infrastructure:

- `BlackboardManager` — per-entity + shared blackboards keyed by interned `FastName` (`NameManager`).
- `ServiceLocator` (`ILocatableService`) — global/local service registry, sync + async.
- `Perception` — `PerceptionManager` + `VisionSensor` / `HearingSensor` / `ProximitySensor`,
  `Perceivable` / `PerceptionListener`, with detection decay/memory.
- `Factions` — `FactionComponent` / `FactionDefination` (new system; replaces `OldFactionComponent`).
- `Singletons` — `MonoBehaviourSingleton<T>`, `StandaloneSingleton<T>`.
- `GameDebugger` — in-game overlay + editor window (`IDebuggable`).
- `Navigation` — `INavigationInterface`, `BaseNavigation` (NavMesh).

### 9.2 Project brain layer (`PlanetOfTwinsAI/`)
- `Core/PotGOAPBrainBase` — base for all enemy brains. Each tick: skips while
  `IsBrainPaused`, syncs enemy + **shared** blackboard state, computes per-tick
  **ClanWar** state and **TwinInDangerRange** (Phase 6D), writes current location, then ticks GOAP.
- `Core/PotWorldStateWriter`, `Core/PotNames` (blackboard key registry), `Core/ClanSoldier`,
  `Core/ClanAlignment`, `Core/MoodEventBus`, `Core/POIType`, `Core/EnemyMood`.
- `GOAP/Brains/` — **one brain per enemy archetype** (Melee, Ranged, Summoner, GrandSummoner,
  GroupGrab, Severed, Siphon, SiphonGhost, TetherBreaker, Witness, Penitent, ChainCommander,
  PenitentCommander).
- `GOAP/Goals/` + `GOAP/Actions/` — the planner vocabulary (AttackTwin variants per type,
  GrabTwin, GhostPursuit/Bind, Summon, ThrowBomb, Rage, GriefRage, EnergyBurst, HoldFormation,
  Investigate, Search, Wander, Possessed, StayInPact, WitnessRitual, DefendSpawn, …).
- `BehaviourTree/Action/` — the leaf behaviours those GOAP actions delegate to.
- `AI/Utility/` — utility scoring (`UtilityGOAPGoalBase`, weight profiles) for goal selection.

### 9.3 Enemy "ecology" systems (NOT in the design docs — emergent layer)
These give enemies group behaviour and mood contagion beyond the per-type spec:

| System | File | Purpose |
|--------|------|---------|
| **Faction Energy** | `AI/System/FactionEnergySystem` | Single shared resource (0–100). Sources (near barrier, ritual, twin hit, grief) vs drains (summon, chain, death, auras). 90+ triggers a coordinated **EnergyBurst** rush. Writes `FactionEnergyNorm` to shared blackboard. |
| **Mood** | `AI/Mood/EnemyMoodSystem` (+ `MoodTransitionProfile/Rule`, `MoodVfxTag`) | Per-enemy mood (Normal, Cautious, Wounded, Enraged, Aggressive, Grieving, Opportunistic, Contemptuous, Panicked, Frustrated, Confident). Mood snaps instantly (the readable "Ikari" moment); stat modifiers lerp. Drives speed / attack cooldown / anticipation. |
| **Social Bond** | `AI/Bond/EnemySocialBond` | Partner reference + bond type (SeveredPair, ComboPartner). Death-bond: partner dies ⇒ this enemy dies unless `BondBroken`. Cross-clan combo partners. |
| **Dark Energy / Combo** | `AI/Bond/EnemyDarkEnergy`, `EnemyProximityPower`, `ComboReadyRegistry`, `ProximityPowerProfile`, `ComboPowerIDs` | Per-enemy dark-energy pool; proximity-based power buffs and combo readiness. `BondBroken` flag gates the death bond. |
| **POI** | `AI/POI/` (`POIManager`, `BarrierPOI`, `SpawnPointPOI`, `RitualSitePOI`, `EnemyPOITracker`) | Points of interest enemies path to / defend. |
| **Perception Memory** | `AI/PerceptionMemory/PoTPerceptionMemory` | Last-known-position memory layered on the sensor system. |
| **Sound events** | `AI/System/SoundEventSystem` | Audible stimuli for hearing sensors. |
| **Ambient / ClanWar** | `AI/Ambient/EnemyAmbientState`, ClanWar flags in `PotGOAPBrainBase` | Enemies can fight *each other* (the lore's clan war) when no twin is in threat range. |

### 9.4 Enemy base (`EnemyAI/Enemy.cs`)
Implements `ITimeAffected, IStunnable, IPossessable, IGrabbable, IAlertReceiver,
IKnockbackReceiver, IFearReceiver, ISlowReceiver`. Owns Movement, AttackController,
Health, StatusEffects, VFX/State UI, pooling, knockback, possession-return animation.
Brain pause replaces the old state-machine pause/resume. Configured via `EnemyData` (and
per-type data SOs in `EnemyAI/Types/Data/`).

### 9.5 Enemy roster
| Enemy (in-game) | Sub-clan | Files | Mechanic |
|-----------------|----------|-------|----------|
| Melee | The Sworn | `BasicMeleeEnemy`, `MeleeEnemyData` | Chase + strike |
| Ranged | The Vigil | `RangedEnemy`, `RangedEnemyData` | Kite + projectiles |
| Summoner / GrandSummoner | The Callers | `SummonerEnemy`, `GrandSummoner`, `SummonerEnemyData` | Spawn minions + suppressing fire |
| Group Grab | The Wardens | `GroupGrabEnemy`, `GroupGrabEnemyData` | Grab → rescue event |
| Siphon (+ Ghost) | The Siphons | `SiphonEnemy`, `SiphonGhost`, `SiphonEnemyData` | Kite + panic bomb; spawns Ghost that chases rescue soul |
| Tether-Breaker | The Forge-Kin | `TetherBreakerEnemy`, `TetherBreakerEnemyData` | Chain projectile drags twin; rage on break |
| Severed (pair) | The Bonded | `SeveredEnemy`, `SeveredEnemyData` | Linked health (40% transfer); Grief Rage on solo kill |
| Witness | The Accord Keepers | `WitnessEnemy`, `WitnessEnemyData` | Buff aura; ritual re-summons ally |
| Penitent (+ Commander) | (latent) | `PenitentEnemy`, `PenitentCommander`, `PenitentEnemyData` | ⚠️ Marked "requires rework" in design + commits |
| Chain Commander | — | `ChainCommander`, `CommanderGroupDefination` | Leads a formation group |
| Boss | — | `BossEnemy` | Scaffolding for bosses |
| Skeleton Hand Trap | environment | `Traps/SkeletonHandTrap`, `TrapState` | Arms, grabs, drags → rescue event |

Bomb behaviour shared via `BombEffectData` + `AbilitySuppressionEffect` (suppresses Q
abilities for a window — see `AbilityController.LockPrimaryOnly`).

---

## 10. Spawn System (`SpawnSystem/`)

`EnemySpawner` is zone-driven. **Multi-scene status (verified):** the old serialized
`allZones[]` is gone — `SpawnZone`s self-register into a `SpawnZoneRegistry`, and the spawner
subscribes registry events with named handlers (R5/R8 compliant). Remaining gaps: the
`OnEnable` early-return when the registry isn't up yet (silent no-spawn ordering hazard) and
despawning a zone's live enemies when its area unloads — instruction.md 3.1/3.3.
`SpawnZone` raises enter/exit; the spawner activates one
zone's `AreaZoneConfig` at a time, picks shuffled spawn points per side of the barrier,
and pools enemies via `EnemyPool` (`IEnemyPoolProvider`).

- Config SOs: `SpawnSetupConfig`, `AreaZoneConfig`, `SideSpawnConfig`, `SideTypeEntry`,
  `GroupSpawnConfig`, `PairSpawnConfig`, `PairPartnerEntry`, `CommanderGroupDefination`,
  `FormationSlot`, `ZoneEnvironmentConfig`.
- **Pair / Severed bonding** wired at spawn (pending-partner dictionaries per side) so the
  Severed shared-health and combo bonds are established when both halves exist.
- `EnemySpawnSide` enum splits spawning across the barrier (left twin side / right twin side).
- `ZoneEnemyTracker` tracks per-zone population.
- Spawner injects Siphon references (twins, soul, rescue controller) into the pool so
  Siphon Ghosts can find their targets.

---

## 11. Progression — Skill Tree & Checkpoints

### Skill Tree (`SkillTree/`)
`SkillTreeManager` implements four roles: `IPointBank`, `ISkillUnlockState`,
`IAbilityDataStore`, `ISkillTreePurchaser`. Each ability is an `AbilityUpgradeData` SO
holding ordered `AbilityUpgradeNode`s (cannot skip). Purchasing raises unlock flags +
`OnNodePurchased`. Managed trees: Stun, Possess, Gate, Health Regen, Accord Spirits,
Coalesce, Soul Convergence, Empower, Accord State.

- `SkillPointOrb` collectibles (+1 each); `SkillPointsHUDView`, `SkillTreeUI`, `SkillNodeButton`, `SkillPreviewModel`.
- ⚠️ Upgrade SOs hold **runtime state** (`currentNodeIndex`) and are `ResetToBase()` on
  Awake / reset. Because `ScriptableObject` state persists in the editor, this is a known
  footgun — see production notes.

### Checkpoints (`CheckPointSystem/`)
`CheckPointManager` + `CheckPointTrigger` + `CheckPointData` save twin positions, skill points,
unlocked upgrades, sword state. `CheckPointFlashUI` confirms a save.

**Multi-scene change:** the old respawn path (full scene reload + `CheckPointLoader` DDOL
object) is **obsolete** — reloading "the scene" is meaningless when gameplay spans Persistent +
streamed areas, and DDOL violates Rulebook R3. `SoftResetController` replaces it: on **Load
Checkpoint** it repositions both twins (via `LocationEntrance`/saved positions), restores
HP + the skill snapshot, and resets enemies in place through the spawner — no reload.
**Restart** loads `Bootstrap` single-mode, which tears everything (including Persistent) down
cleanly and re-enters through the normal boot path. The skill snapshot will read from
`SkillTreeRuntimeState` once instruction.md Phase 4 lands (SO runtime-state extraction).

---

## 12. Tutorial System (`TutorialSystem/`)

Data-driven sequence runner, designed so teaching can happen **in any area scene with zero
per-scene UI wiring** — that is the product requirement driving the Persistent split.

- `TutorialDirector` — lightweight per-area sequencer (one per level, **not** a singleton).
  Iterates ordered `TutorialStepBase` SOs against a `TutorialStepContext`. Started by a
  Timeline Signal at the end of the intro cutscene (does not auto-start).
- `TutorialStepContext` — per-area context bag. Cross-scene deps (`overlay`, `hintDisplay`,
  `twinSelector`, input gate) resolve via Rulebook **R4** (`??= Singleton.Instance` in
  `Start`/`Resolve`); stale serialized slots from the old single-scene setup must stay cleared.
- Step SOs: `TutorialPromptStepSO`, `TutorialQTEStepSO`, `TutorialRescueWatchStepSO`,
  `TutorialTimelineStepSO`, `TutorialWaitStepSO`, `TutorialUnlockAllStepSO`,
  `TutorialCheckpointStepSO`.
- **Persistent tutorial UI singletons:** `TutorialOverlayController` (Schedule-1 video+text
  card, time-pausing — uses unscaled time), `TutorialHintDisplay` (inline hint strip), and —
  after instruction.md Phase 2 — `FailureResetSequencer` + `FailureNotice` **relocated into
  Persistent with their UI** (`BlackOverlay`, greyscale `Volume`, `NoticePanel`/`FailureText`
  become same-scene R1 refs; each gains `Instance`). No facade class — the components are
  already self-contained and fully unscaled (verified).
- `TutorialInputGate` (`ITutorialGate`) gates each input category per step. It is
  **area-resident** (lives in the tutorial area, not Persistent) and **push-registers** into
  the Persistent `TwinInputReader` via `SetGate(this)` in `OnEnable` / `SetGate(null)` in
  `OnDisable` — the canonical R5 input example. The reader treats a null gate as "all input
  allowed", so outside the tutorial everything just works. The director references the gate
  as a plain same-scene serialized ref (R1), locks input in `Start` (not `Awake`) and
  **fails open** if the slot is unwired — never trap the player because a ref was missing.
- Boundaries (`TutorialBoundary`, `TutorialOuterBoundary`), `TutorialZoneTrigger`; failure
  handling: `FailureResetSequencer.Instance` (Persistent) runs the full
  greyscale→black→teleport→restore sequence; `FailureNotice.Instance` shows the banner. Area
  steps resolve both via `TutorialStepContext.Resolve()` — no serialized slots.
- **Known gameplay workaround:** simultaneous wrong-twin entry on dual checkpoints is mitigated
  by physically spacing the checkpoints (one slightly ahead). The structural fix — identity-based
  reset resolution, a shared guard in both modes, checkpoint suspend-during-reset, and a
  re-entry-rejecting sequencer — is fully specified in instruction.md 7.6h; the spacing
  workaround retires once it lands.

**To add teaching to any current or future area:** drop a `TutorialDirector` + step SO list +
local anchors (checkpoints/zones) into that scene. Overlay, hints, notices and failure visuals
come from Persistent automatically.

---

## 13. QTE / Gate Puzzle (`QuickTimeEvents/`)

**Rewritten for multi-scene** (the old monolithic `QTEController` is replaced):

- `QTEManager` — **Persistent singleton**: the state machine only. It holds **no UI of its
  own** (verified — no serialized UI fields): at `BeginQTE(anchor)` it pulls the world-space
  UI refs (root panel, mash fill, timer ring, labels) from the anchor and drives them, then
  clears them on reset. One manager serves every QTE in the game. (Its approach/mash timers
  are currently scaled `Time.deltaTime` — conversion to unscaled is instruction.md 5.3.)
- `QTESceneAnchor` — per-QTE component in the **area scene**: bundles that QTE's definition,
  trigger points, framing camera, `IActivatable` targets, *and its world-space canvas UI*
  (all same-scene serialized refs, R1) and hands itself to `QTEManager.Instance.BeginQTE(this)`
  — the blessed **parameter-passing pattern** for area→Persistent data (no registry needed).
- `QTEDefinitionSO` — data asset: mash duration/count/key, instruction text key, event ID
  (e.g. `QTE_ParkGate.asset`).
- `QTEZoneTrigger` / `QTESuccessWatcher` — zone entry + success-reaction hooks.
- `EnemyFreezeService` (`IEnemyFreezeService`, lives on the QTEManager GO) — freezes enemies
  for the QTE duration using `FindObjectsByType<Enemy>` across **all loaded scenes** (the
  one sanctioned multi-scene sweep — enemies are pooled/transient, not registry members).

Player-facing flow is unchanged: both twins walk to their trigger points, press F to lock
in, then a mash phase fills the progress ring against a draining timer; hold X to cancel
(both release); failure releases for retry; success opens the gate permanently via
`GateActivatable`. Enemies stay frozen for the whole sequence including retries.

---

## 14. Dialogue & Localization (`DialogueSystem/`)

`LanguageManager` wraps Unity Localization (persistent, PlayerPrefs-backed). Dialogue is
SO-driven (`DialogueSequenceSO` → `DialogueLine`), played by `DialoguePlayer`, triggered by
`DialogueTrigger`, rendered via `SubtitleBarDisplay` + `NameplateDisplay`.
`AbilityFeedbackDisplay` surfaces ability messages.

---

## 15. Camera (`Camera/`)

Cinemachine 3-based. `CameraManager` coordinates; `CameraFollowController` frames both
twins; `TwinDistanceMonitor` (`IDistanceProvider`) drives zoom by separation;
`CameraObstruction` handles occlusion; `OverviewCamController` + `CameraSwitcher` provide
the hold-B top-down overview (`IOverviewBroadcaster`).

---

## 16. Scene Architecture & Area Streaming (`SceneLaoder/`) — current focus

The `multiscenesetup` branch replaces single-scene play with a **Bootstrap → Persistent →
streamed area scenes** model. This section is canonical; the old `AreaManager`/`AreaNode`
SetActive approach is **legacy and being deleted** (see instruction.md Phase 6).

### 16.1 Scene roles
```
Bootstrap.unity   index 0; the only scene loaded at boot. GameBootstrapper picks a mode
                  (intro / dev), additively loads Persistent, then Intro or the start area.
Intro.unity       skippable cutscene (IntroController) that background-loads gameplay scenes.
                  IntroTimelinePositioner snaps twins to gameplay start when the Timeline stops.
Persistent.unity  NEVER unloaded during play. Holds every manager singleton, the players +
                  twin systems, all screen-space HUD canvases, the one EventSystem, the one
                  AudioListener, the one MainCamera (+ Cinemachine Brain), EnemyPool root.
L1_Park / L2_Streets   area scenes (one folder per scene with co-located navmesh/occlusion
                  assets). Contain geometry, lights, NavMesh surface, spawn/QTE/tutorial
                  anchors, cinematic VCams, world-space canvases — and none of the things
                  Persistent owns (see Rulebook R9).
```

### 16.2 Streaming components
- `SceneFlowManager` (Persistent singleton) — occupant-based streaming: keeps the occupied
  area **plus its declared adjacents** loaded; loads/unloads additively and async; sets the
  occupied area as the **active scene** (so its render settings apply). **Occupancy is a
  per-location set of actors** (both twins + the rescue soul) — a location stays loaded while
  *any* of them is inside or adjacent; twins straddling a boundary keep both areas loaded.
  Raises `OnLocationWillUnload` **before** unloading (EnemySpawner despawns that zone's
  enemies, QTEManager cancels anchored QTEs) and exposes `NotifyTeleported(actor, location)`
  because triggers never see teleports — **every scripted reposition must call it**
  (checkpoint load, intro positioner, soul travel, debug warps).
  *Implementation status: this paragraph is the target. Current source has int-count
  occupancy and lacks `NotifyTeleported`, `OnLocationWillUnload`, and the `SetActiveScene`
  call — the delta is instruction.md 3.7 (a–e).*
- `WorldLocationSO` — per-area config asset: scene reference, adjacency list, entrance
  definitions. The data bridge between Persistent code and area scenes (SO = always-safe ref).
- `LocationEntrance` — named spawn point in an area scene; self-registers/unregisters so
  `SoftResetController`/`SceneFlowManager` can place twins by entrance ID after a load.
- `SceneLoadTrigger` — boundary strip placed **fully inside** the area being entered;
  on enter it *transitions* the crossing actor's current location to `targetLocation`
  (`comesFrom` drives `LocationEntrance` spawn resolution, not occupancy). Exits are ignored
  by design — see instruction.md 3.7a. *(Current source still calls `NotifyTwinExited` and
  passes no `Player` — part of the 3.7 delta.)*
- `SoftResetController` — checkpoint/respawn **without scene reload**. Must deliver the full
  player-facing contract the old scene-reload gave for free (instruction.md Phase 7.5):
  despawn + fresh-restart all enemies via the spawner, reset traps (release grabs, re-arm),
  force-Idle the rescue controller, force-end Accord/SC/Empower/Setsuna, `timeScale = 1`,
  reposition both twins at the saved `LocationEntrance` + `NotifyTeleported`, restore HP and
  the skill snapshot — all behind its own unscaled fade (verified correct). Replaces `CheckpointLoader`.
- Dev entry: `PersistentSceneAutoLoader` (editor-only) additively loads Persistent when Play
  is pressed directly in an area scene, so all four entry paths work (see instruction.md P0).

### 16.3 The Reference Rulebook (condensed — full text in instruction.md §1)
These laws govern every reference in the project. Violations are bugs.

| Law | Rule |
|-----|------|
| R1 | Same-scene serialized refs allowed (incl. Persistent→Persistent). Keep the `[SerializeField] MonoBehaviour` → interface-cast DI pattern. |
| R2 | Cross-scene serialized refs **forbidden** ("Scene mismatch" = wrong design, always). |
| R3 | Persistence = living in Persistent.unity. **`DontDestroyOnLoad` is banned** (it duplicates managers across the Bootstrap restart loop). |
| R4 | Area→Persistent: optional serialized slot, then `field ??= Manager.Instance` **in `Start()`** (never `Awake`, never `FindAnyObjectByType` for managers). Field stays interface-typed; the concrete singleton appears only on the resolve line. |
| R5 | Persistent→Area: registries only. Area objects self-register `OnEnable`, unregister `OnDisable`/`OnDestroy`; managers purge nulls and tolerate empty. |
| R6 | Area↔Area refs forbidden; mediate via Persistent or an SO event channel. |
| R7 | ScriptableObjects hold config only — never runtime state. |
| R8 | Lifecycle: `Awake` wires self; `Start` resolves others; subscribe/unsubscribe with **named handlers**; `OnDestroy` unregisters. |
| R9 | Persistent owns the only EventSystem / AudioListener / MainCamera / screen-space HUD. Area world-space canvases use `WorldSpaceCanvasCamera`. |
| R10 | All `Time.timeScale` writes go through the Persistent `TimeScaleService` (`Request(owner, value)`/`Release(owner)`, min-value-wins) — seven independent writers verified, direct writes banned once it lands (instruction.md 5.5). Every timer states scaled vs `unscaledDeltaTime` by comment. |
| R11 | Timeline law: track bindings are scene-local (cross-scene targets rebound at runtime by `TimelineBindingResolver`); Activation Tracks never control ancestors of gameplay-logic objects and always set an explicit Post-playback state; completion via `director.stopped`/state-poll with Wrap Mode None, end Signals ≥0.1 s before the end (instruction.md §1 R11). |

> 📁 Folder is misspelled `SceneLaoder` — see production notes.

---

## 17. UI (`UI/`) & VFX (`Vfx/`, `Particles/`)

- **HUD:** ability icons (`AbilityIconUI`, `AccordHUDController`, `AccordBarView`,
  `AccordIconSlot`), Weaver's Gate (`WeaversGateHUDView`), teleport cancel
  (`TeleportCancelHUDView`, `TeleportMarkerPreview`), suppression (`AbilitySuppressionUI`),
  skill points (`SkillPointsHUDView`), overview (`OverviewCamHUDView`), checkpoint flash.
- **Player:** `SelectedPlayerUI` (yellow = selected), `SoulTimerUI`, `RescueButtonUI`.
- **Enemy:** `EnemyStateUIController` (Ikari mood reactions), `EnemyVfxController`,
  `IkariMarkVFX`, `WitnessAuraVfx`.
- **World-space:** `UIBillboard`, `WorldSpaceHealthUI`, `WorldSpaceRescueUI`,
  `DamageDisplayUI`, `ProximityUIVisibility`. `WorldSpaceCanvasCamera` — attaches
  `Camera.main` as the Event Camera on World Space canvases at runtime (solves
  cross-scene Main Camera ref; add to any World Space canvas in an area scene).
- **Systems:** `GameOverController` (Battle Lost — loads Bootstrap on restart),
  `FadeController`, pause/settings menus.
- **Tutorial HUD (Persistent):** `TutorialOverlayController` (Schedule-1 video+text card,
  time-pausing), `TutorialHintDisplay` (inline hint strip). Both are singletons accessible
  from any scene via `Instance` — this is what makes "teach any skill/weapon in any area
  scene" work with zero per-scene wiring. `TutorialHUDCanvas` also hosts the **relocated**
  `FailureResetSequencer` and `FailureNotice` (instruction.md Phase 2): both moved into
  Persistent with their UI (`BlackOverlay`, greyscale `Volume`, `NoticePanel`, `FailureText`
  — all same-scene R1 refs) and expose `Instance`; area scripts resolve them via
  `TutorialStepContext.Resolve()`. The previously planned `TutorialHUDProvider` (raw
  GameObject hand-out) and the interim `TutorialHUD` facade idea are both **rejected — do not
  build either**.
- **VFX / FX:** All runtime effects migrate to `FxManager` + pooled `VfxPool` under
  Persistent (instruction.md §14, Phase 9). Until migration, per-script Instantiate sites
  are tracked in the §14.6 migration table. Current scripts: `StunVfxSystem`,
  `SoulFrozenVfx`, `HealthRegenVfx`, `SpawnShieldRipples`, `KillParticleSpawnner`,
  `SoulParticleAttractor`. Call-site change everywhere: `[SerializeField] GameObject` →
  `[SerializeField] CueData` + `_fx.Play(cue, ctx)`.
- **Audio (Phase 9):** `AudioManager` + `MusicManager` (both Persistent — §14.2).
  `GameAudioMixer` extends to Master → Music / Ambience / SFX / UI / Voice groups +
  snapshots Default / Paused / Setsuna / GameOver. `WorldLocationSO.musicTrack` /
  `.ambience` drive per-location audio (R7 config). Until P9.2, mixer + volume prefs
  exist but 12+ gameplay systems are silent (tracked in §14.7 hook-up table).

> **Multi-scene wiring note:** UI/system references follow the Reference Rulebook (§16.3,
> full text instruction.md §1) — same-scene serialized refs (R1), `Start()`-time singleton
> resolve for area→Persistent (R4), registries for Persistent→area (R5). The old guidance
> to scatter `FindAnyObjectByType<T>()` fallbacks in `Awake()` is **revoked** (it grabs
> wrong instances, can't populate interface-typed fields, and silently hides wiring
> errors). §21 lists the triage status of every ref broken by the migration.

---

## 18. Cross-Cutting Architecture

- **Dependency inversion via interfaces + `[SerializeField] MonoBehaviour` injection.**
  Systems take a `MonoBehaviour` slot in the Inspector and cast to the interface in `Awake`
  (e.g. `_rescueActiveMono as IRescueActive`). Avoids `FindObjectOfType` and keeps
  systems decoupled. This remains the dominant wiring pattern **within a scene** (Rulebook
  R1); across scenes it composes with R4 (`Start()`-time singleton resolve into the same
  interface-typed field) and R5 (registries). The full Reference Rulebook in §16.3 /
  instruction.md §1 governs every reference in the project — name the rule a new ref obeys
  when reviewing a diff.
- **Blackboard + ServiceLocator** for AI-side decoupling.
- **Event-driven** (`event Action<…>`) between gameplay and UI.
- **Counter-based locks** (`ISelectionLock`, `IAbilityLock`) for composable suppression.
- **ScriptableObject configuration** for all tunables (enemy data, ability upgrades,
  spawn configs, dialogue, mood/perception profiles).
- **Object pooling** for enemies.

---

## 19. Implementation vs. Design Docs — what actually changed

### Built & matching docs
Twin switch/mirror movement, distance health curve (6/12/16/18 m), Stun, Possession,
Coalesce, Soul Convergence, Empower, Weaver's Gate, **Accord State + Void Strike + Radiant
Seeker + Accord Spirits + Accord Melee + Setsuna**, rescue/TTK flow, Siphon Ghost, Severed
pair + Grief Rage, Tether-Breaker, Witness, skill tree (3 nodes/ability), checkpoints, QTE
gate, localization, debug skill-point keys.

### Added beyond the docs (emergent enemy ecology)
- **Faction Energy system** + coordinated **EnergyBurst**.
- **Mood system** with Ikari snap reactions and lerped stat modifiers.
- **Social Bond / Dark Energy / Combo / proximity power** between enemies.
- **ClanWar** behaviour — enemies fight each other when no twin is threatened.
- **POI system**, **perception memory**, **sound events**.
- **Commander / formation** spawning (`ChainCommander`, `PenitentCommander`).
- **Area streaming** (`AreaManager`/`AreaNode`) for multi-scene.
- Hybrid **GOAP + BT + FSM** brains per enemy type (the docs only described behaviours, not architecture).

### Designed but incomplete / reworking
- **Penitent** enemy — present (`PenitentEnemy`/`PenitentCommander`) but flagged
  "requires rework" in both the story bible (Ikari/grab-to-death difficulties) and commits.
- **Resonant** enemy — not implemented (deferred per docs).
- Planned future abilities (Ward, Surge, Twin Resonance, Accord Pulse/Echo, Tether Trail,
  Soul Armour, Accord Rift, Lyra Accord-state Solhari Bridge / Absolution) — not built.
- Boss content (`BossEnemy`) is scaffolding only.

### Legacy / dead code (safe-to-remove candidates)
- `EnemyAI/EnemyStateMachine.cs`, `EnemyAI/EnemyDetection.cs`, `EnemyAI/EnemyVisionCone.cs`,
  `EnemyAI/Interface/IEnemyState.cs` — replaced by GOAP + Perception (see `Enemy.cs` header).
- `Faction/OldFactionComponent.cs`, `Faction/Faction.cs` — replaced by
  `CommonCore/Factions/FactionComponent`.
- `Players/TwinManager.cs` — split into the `Twin*Dispatcher` classes; verify before deletion.
- `Players/TestPlayerMovement.cs`, `Debug/` scripts — dev-only.

---

## 20. Recommended Production Folder Structure

The current layout grew organically (557 scripts) and mixes a reusable framework, project
gameplay, and assets without clear boundaries. For a release-grade project, separate
**reusable framework** from **game code**, group by feature, and use **assembly definitions**
to enforce dependencies and cut compile times.

### 20.1 Assembly definitions (highest-impact change)
Add `.asmdef` files so dependencies are explicit and compilation is incremental:

```
PoT.Framework.asmdef        ← AIFramework/* (no game refs — fully reusable)
PoT.Gameplay.asmdef         ← all game systems (refs Framework)
PoT.UI.asmdef               ← UI (refs Gameplay interfaces only)
PoT.Editor.asmdef           ← editor tools (Editor-only)
PoT.Tests.asmdef            ← EditMode/PlayMode tests (refs the above)
```
Rule: **Framework must never reference Gameplay.** Today some `AIFramework/PlanetOfTwinsAI`
code references game types (`Enemy`, `TwinAnticipator`) — that project-specific layer
belongs in Gameplay, not Framework. Split accordingly.

### 20.2 Proposed `Assets/` layout
```
Assets/
├── _Project/                      # everything first-party, one root (underscore sorts to top)
│   ├── Art/                       # Models, Materials, Textures, Shaders, Skybox, VFX graphs
│   ├── Audio/
│   ├── Animation/
│   ├── Prefabs/
│   │   ├── Enemies/  Players/  Abilities/  Environment/  UI/  Systems/
│   ├── ScriptableObjects/         # all SO assets, mirrors data-class folders
│   │   ├── Enemies/  Abilities/  SkillTree/  Spawn/  Dialogue/  Mood/  Perception/
│   ├── Scenes/
│   │   ├── Boot/                  # bootstrap / persistent systems scene
│   │   ├── Levels/                # L1Park, L2Streets, …
│   │   └── Sandbox/               # SampleScene + test scenes
│   ├── Settings/                  # URP, Input Actions, Localization tables
│   └── Code/
│       ├── Framework/             # = AIFramework reusable core (PoT.Framework asmdef)
│       │   ├── AI/ (BehaviourTree, StateMachine, HybridGOAP)
│       │   ├── Blackboard/  Perception/  ServiceLocator/  Singletons/
│       │   ├── CharacterCore/  Factions/  Navigation/  Debugging/
│       └── Game/                  # = Gameplay (PoT.Gameplay asmdef)
│           ├── Players/           # input, selection, movement, abilities (incl. Accord, Setsuna)
│           ├── Enemies/           # Enemy core, types, data, traps, PoT brains + ecology
│           ├── Combat/  Health/   # (rename "Heath" → "Health")
│           ├── Progression/       # SkillTree + Checkpoints
│           ├── World/             # SpawnSystem, Environment, SceneStreaming (rename "SceneLaoder")
│           ├── Encounters/        # Rescue, QTE, Tutorial
│           ├── Presentation/      # Camera, UI, VFX, Dialogue/Localization
│           └── Core/              # shared interfaces, DamageData, enums, time-factor
├── Plugins/                       # third-party only
└── ThirdParty/                    # imported asset-store packages
```

### 20.3 Concrete cleanups
- **Fix typos that are now load-bearing folder names:** `Heath/` → `Health/`,
  `SceneLaoder/` → `SceneStreaming/`, `FactionDefination` → `FactionDefinition`,
  `CommanderGroupDefination` → `…Definition`, `KillParticleSpawnner`/`EnemySpawnner` →
  `…Spawner`, `ICouroutineRunner` → `ICoroutineRunner`. Do these as isolated rename
  commits (Unity regenerates `.meta`; keep GUIDs).
- **Delete legacy code** in §19 once confirmed unreferenced (start with `OldFactionComponent`,
  `EnemyStateMachine`, `EnemyDetection`, `TwinManager`).
- **Move ScriptableObject runtime state off the SO.** `AbilityUpgradeData.currentNodeIndex`
  mutates the asset — extract a runtime progression holder (or reset rigorously) so editor
  play sessions don't leave assets dirty.
- **Replace direct `Input.GetKey` in `TwinInputReader`** with the Input System actions that
  are already a dependency — gives rebinding + gamepad for free before release.
- **Consolidate the two `.sln` files** (`Planet Of Twins.sln`, `Planet-of-Twins.sln`).
- **Editor/Debug separation:** move `Debug/` and `GameDebugger/Editor` behind `PoT.Editor`
  asmdef or `#if UNITY_EDITOR` so debug keys never ship.
- **Add a tests assembly** — there is no test coverage today despite `com.unity.test-framework`.
- **Addressables** for level/area content to support the streaming work cleanly.

---

## 21. Known Open Issues / Risks

### Multi-scene Inspector ref breakage (branch `multiscenesetup`)

When the HUD canvases and system managers were moved from L1Park into Persistent, the
serialized Inspector refs that pointed across scenes became stale ("None" or "Scene mismatch").
Scripts that already use `FindAnyObjectByType` as a fallback survive at runtime; those that
don't will silently receive null and produce no output. **Current broken scripts:**

| Script | GameObject | Missing field(s) | Root cause |
|--------|-----------|-----------------|------------|
| `SkillTreeUI` | `SkillTreeCanvas` | `_dataStoreMono`, `_purchaserMono`, `_pointBankMono` | `SkillTreeManager` in Persistent — no fallback |
| `AccordBarView` | `AccordPowerbarPanel` | `accordSystem`, `unlockStateMono` | `AccordStateSystem` / `SkillTreeManager` in Persistent — no fallback |
| `SkillPointsHUDView` | `SkillPointsText` | `_pointBankMono` | `SkillTreeManager` in Persistent — no fallback |
| `AbilitiesHUDController` | `AbilitiesHUDPanel` | `accordSystem`, `empowerSystem`, `skillUnlockState` | All singletons in Persistent — no fallback |
| `OverviewCamHUDView` | `OverviewCamPanel` | `overviewController` | `OverviewCamController` in Persistent — no fallback |
| `KillParticleSpawnner` | `ParticleSystemManager` | `deathNotifier` | `EnemyDeathNotifier` in Persistent — "Scene mismatch", no fallback |
| `SharedHealthPresenter` | `SharedHealthPanel` | `sharedHealthPool`, `emergencyMonitor` | **Has** `FindAnyObjectByType` fallbacks — will work at runtime |
| `QTESceneAnchor` | `ParkGateQTEAnchor` | World UI: Root Panel, Fill Bar, Timer Ring, labels | Children of `QTEParkCanvasUI` — same scene, just not wired yet |
| `TutorialStepContext` | `TutorialManager` | `overlay` ("Scene mismatch"), `twinSelectorMono` | Stale ref to old L1Park scene; `Resolve()` now has fallbacks — clear the Inspector slot |
| `FailureResetSequencer` | `TutorialManager` | `_postProcessVolume`, `_blackOverlay`, `_leftTwin`, `_rightTwin` | **Component relocates to Persistent** (instruction.md P2) — all four become same-scene R1 refs; gains `Instance` |
| `FailureNotice` | `TutorialManager` | `_noticePanel`, `_noticeText` | **Relocates to Persistent with its UI**; gains `Instance`; internals unchanged |

**Fix protocol:** instruction.md Phase 1 carries the per-row triage (which rule applies to
each ref) — most HUD rows are *same-scene unwired* (both ends now in Persistent → re-wire in
Inspector + R4 belt-and-braces resolve), `TutorialManager` rows are solved by **relocating the
failure components into Persistent** (Phase 2). The earlier guidance to add
`FindAnyObjectByType` fallbacks in `Awake()` is **revoked** — do not apply it anywhere new,
and remove it from `SharedHealthPresenter`/`TutorialStepContext` as those rows are fixed.
`TutorialHUDProvider` is cancelled; so is the interim `TutorialHUD` facade idea — residency
replaces it.

### Other known issues
- Penitent enemy needs rework (Ikari + grab-to-death timing).
- Soul Convergence cap is toned to ~8 for prototype (design target 20).
- Setsuna manipulates global `Time.timeScale`; audit every timer for `unscaledDeltaTime`
  (decision table: instruction.md 5.3).
- Skill-tree SO state — `currentNodeIndex` mutates the `AbilityUpgradeData` **asset**, so
  upgrade levels persist across Editor play sessions and dirty `.asset` files in VCS
  (extraction plan: instruction.md Phase 4).
- Debug skill-point keys (L/O/P/I/K) still active.
- `WorldLocationSO` assets (Park, Streets) **not yet created** — `SceneFlowManager` cannot
  stream until they exist (instruction.md Phase 7.1).
- **Skill snapshot covers only 7 of 9 trees on BOTH sides** — `CheckpointManager.CaptureNodeLevels`
  *and* `SoftResetController.RestoreNodeLevels` hand-list trees, omitting **Empower and
  Accord State**, so those upgrades are never saved nor restored (verified against source;
  fix via `SkillTreeManager.AllTrees` — instruction.md Phase 7.5).
- **`SceneFlowManager` verified gaps:** occupancy is an int count and `LoadStartLocation`
  hardcodes it to 1, so the start area's occupant count gets pinned and it can **never
  unload**; trigger strips fire `OnTriggerExit` when walking *deeper into* an area, which
  the count model misreads as leaving; no `NotifyTeleported`, no pre-unload event, and
  `SetActiveScene` is never called after streaming (render settings stay bound to the boot
  area). Fixes: instruction.md 3.7 (transition occupancy model).
- **Intermittent "rescue checkpoint never activates" — root cause diagnosed (instruction.md
  Phase 7.6, Rulebook R11):** `TutorialCheckpoint.Activate()` is a `SetActive(true)` that
  **no-ops when any ancestor is inactive**, and the tutorial timeline drives Activation
  Tracks over scene groups whose final state depends on where evaluation stopped (natural
  end vs skip vs load-hitch frame). The in-code `[Activate]` log already prints
  `parent active=` — one failing run showing `False` is the definitive confirmation. Fix:
  move gameplay-logic objects out of Activation-controlled hierarchies, explicit
  Post-playback states, loud-fail `Activate()`.
- **Seven independent `Time.timeScale` writers** (overlay, pause, game-over, Setsuna, soul
  travel, soft reset, **skill tree**) stomp each other on overlap — replaced by
  `TimeScaleService`, min-value-wins (instruction.md 5.5, R10). The suspected eighth —
  `TutorialContext.rescueTimerScale = 0.25` — turned out to be **dead data with no consumer**;
  the tutorial's forgiving rescue comes from `TutorialTrap`'s long TTK instead.
- **Tutorial step SOs leak event subscriptions** (lambda `+=` with no `-=` in
  `TutorialCheckpointStepSO`/`TutorialRescueWatchStepSO`) — stacks dead handlers on every
  tutorial re-run (instruction.md 7.6c).
- Context checkpoint entry 12 "Rescue point B" is wired to `CheckpointsRescue**L**`
  (duplicate of entry 11) — latent Dual-mode wiring slip (instruction.md 7.6f).
- **`TutorialTrap` never unregisters** from the rescue trap registry (`OnEnable` registers,
  success path disables the GO — disabled ghost stays registered; instruction.md 7.6i). Its
  tutorial re-arm delay is also scaled time (2 s → 8 s under the rescue-tutorial 0.25 slow).
- **`SetsunaSystem` reads raw `Input.GetKey(F)`**, bypassing the tutorial input gate, and its
  "invulnerable" rewind only locks movement — a hit during the 1.5 s rewind can fire game-over
  before the health snapshot restores (instruction.md 7.6j).
- **ESC triple-consume:** the tutorial overlay, the **skill tree**, and the pause menu all
  read Escape in the same frame — ESC on an open overlay or open skill tree also opens pause,
  with colliding timeScale writes; the pause script's comment claiming SkillTreeUI "handles
  its own ESC" first is execution-order fiction (instruction.md 5.6, which also fixes
  SkillTreeUI's subscribe-before-assign bug).
- `IntroTimelinePositioner` exists but is **not attached** to the TutorialTimelineDirector —
  twins are not repositioned after the intro timeline (instruction.md Phase 7.3; commit `5fa951d`).
- **`TutorialTimelineDirector` is bound to a pre-multiscene world — 11 of 42 bindings null
  (BUG-032).** The cutscene was authored in the single-scene `Assets/Scenes/L1Park.unity`
  (no underscore, still in git at HEAD) before the multiscene split and the level re-greybox.
  Diffing the old scene's `m_SceneBindings` against the live `L1_Park/L1_Park.unity` and
  resolving each fileID to a GameObject name shows the 11 nulls are: **now-Persistent, rebound
  at runtime** (Cinemachine Track→`CinemachineBrain`, Signal Track→`CameraManager` receiver,
  Animation+Activation→`FadeCanvas`/`FadeController`, Activation→`HUD_Canvas`, Activation 1/2→
  `TutorialGroupTransposeClose/Top` camera groups — **moved to Persistent, NOT deleted**, toggled
  off by the timeline — plus `SkyboxChanger`); **the Persistent twins** (Activation 20/21 toggle
  the `Lyra`/`Kai` GOs, not "nameplates" — the old cutscene deactivated the twins to lock them;
  these tracks are deleted and the lock is now done in code, `IntroTimelinePositioner` lock-on-play);
  and **`MainLvl (1)/(2)` geometry, deleted by the re-greybox** (unrecoverable). Fix pattern is R11:
  runtime `SetGenericBinding` via the Persistent `TimelineTargetRegistry` for continuous cross-scene
  tracks + Signals→local-relay for cross-scene actions; delete the dead/twin tracks. Never hand-edit
  the `.playable`/scene binding YAML.
- `TutorialDirector.Awake()` locks input even when `inputGate` is unwired — players can be
  trapped or, inversely, free during the opening cutscene (fix: instruction.md triage row 12).
- `CommonStatic` GO in L1_Park was accidentally deleted and restored from git via `Restore.unity` — verify mesh/occlusion data is intact after drag-in.

---

## 22. Where to look first (onboarding shortcuts)

| I want to… | Start here |
|------------|-----------|
| Understand input → action | `Players/TwinInputReader.cs` → `Twin*Dispatcher.cs` |
| Add/modify a player ability | `Players/Ability/` (+ `AbilityController`, `SkillTreeManager`) |
| Touch Accord/Setsuna | `Players/Ability/Systems/AccordStateSystem.cs`, `SetsunaSystem.cs` |
| Add an enemy type | `EnemyAI/Types/` + data SO + `PlanetOfTwinsAI/GOAP/Brains/` |
| Tune enemy group behaviour | `PlanetOfTwinsAI/AI/{Mood,Bond,System}` |
| Spawning | `SpawnSystem/EnemySpawnner.cs` + zone configs |
| Rescue/death flow | `Players/RescueEventController.cs` + `Players/RescueState/` |
| Scene streaming / boot flow | `SceneLaoder/` (`SceneFlowManager`, `WorldLocationSO`, `GameBootstrapper`) + game.md §16 + instruction.md |
| Tutorial / teach-anywhere | `TutorialSystem/` (`TutorialDirector`, step SOs) + Persistent `TutorialOverlayController`/`TutorialHintDisplay`/`FailureResetSequencer`/`FailureNotice` |
| QTE | `QuickTimeEvents/` (`QTEManager` Persistent, `QTESceneAnchor` per area) |
| Checkpoint / soft reset | `CheckPointSystem/` + `SoftResetController` (game.md §11) |
| Health/distance | `Heath/` (SharedHealthPool, DistanceHealthSystem) |
| AI engine internals | `AIFramework/{HybridGOAP,BehaviourTree,StateMachine,CommonCore}` |
```
