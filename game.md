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

## System Inventory — the systems behind the "23+" claim

Quick reference for pitch/answers. These are the distinct gameplay/engine systems in the
project — **~29 at headline granularity**, which is why "**23+ systems**" is the conservative
number we quote (collapse the four ecology rows 13–16 into one, and the four ability rows 6–9
into "abilities & time," and it still lands at 23). Each links to its detailed section below.

| # | System | Key type(s) | Ref |
|---|--------|-------------|-----|
| 1 | Dual-twin input & control | `TwinInputReader` (IInputProvider) + movement/attack/ability dispatchers, `TwinSelector` | §3 |
| 2 | Shared-health bond | `SharedHealthPool` (200 HP), `PlayerHealthComponent`, `TwinBondManager` | §4 |
| 3 | Distance bonding | `DistanceHealthSystem` (≤6 m full → 0 at >18 m), `DistanceZone` | §4 |
| 4 | Combat & damage pipeline | `DamageData`, `LinkedDamage` (Severed loop breaker) | §5 |
| 5 | Ability system | `AbilityController`, `AbilityData` SOs (Stun, Possession, Weaver's Gate) | §6 |
| 6 | Accord State | `AccordStateSystem` (Void Strike, Radiant Seeker, Accord Melee, Accord Spirits) | §6 |
| 7 | Soul Convergence & Empower | `SoulConvergenceSystem`, `EmpowerSystem` | §6 |
| 8 | Setsuna | `SetsunaSystem` (global slow + position/health rewind) | §6 |
| 9 | Time-freeze / soul mode | `TimeFactorManager` (entity freeze registry) + `TimeScaleService` (min-wins arbiter) | §7 |
| 10 | Rescue events & detached soul | `RescueEventController`, `SoulPlayer`, `EmergencyTeleportMonitor` | §8 |
| 11 | Hybrid AI engine | GOAP + BehaviourTree + StateMachine on a shared Blackboard; `PerceptionManager` + sensors | §9.1 |
| 12 | Per-archetype enemy brains | one GOAP brain each (Melee, Ranged, Summoner, GroupGrab, Severed, Siphon, TetherBreaker, Witness…) | §9.2, §9.5 |
| 13 | Mood / Ikari | `EnemyMoodSystem` (moods snap, stats lerp) | §9.3 |
| 14 | Social bonds | `EnemySocialBond` (SeveredPair / combo partners, death-bond) | §9.3 |
| 15 | Faction energy | `FactionEnergySystem` (shared 0–100 → coordinated EnergyBurst) | §9.3 |
| 16 | Clan war + POIs + perception memory | ClanWar flags, `POIManager`, `PoTPerceptionMemory` | §9.3 |
| 17 | Spawn & pooling | `EnemySpawner`/`EnemyPool` + generic `GameplayPool` (projectiles/summons/hazards) | §10 |
| 18 | Skill tree / progression | `SkillTreeManager` (nine upgrade trees, points) | §11 |
| 19 | Checkpoints & soft reset | `CheckPointManager`, `SoftResetController` (respawn without scene reload) | §11 |
| 20 | Scene streaming & bootstrap | `SceneFlowManager` (occupancy/adjacency), `GameBootstrapper` | §16 |
| 21 | Tutorial system | `TutorialSystem` steps + `TutorialInputGate`, teach-anywhere overlay/hint/failure UI | §12 |
| 22 | QTE / gate puzzle | `QTEManager`, `EnemyFreezeService` | §13 |
| 23 | Camera | Cinemachine brain, `CameraManager`, `OverviewCamController` | §15 |
| 24 | UI / HUD | screen-space HUD, skill-tree UI, pause menu, world-space canvases | §17 |
| 25 | VFX cue system | `FxManager` (`Play(CueData, CueContext)`), Cue Books, VFX pool | §17, §23.11 |
| 26 | Manpu emotion glyphs | `ManpuDirector`/`ManpuSlot`/`ManpuVocabulary` (mood/ability/reaction glyphs) | §24 |
| 27 | Audio | `AudioManager` (32 pooled voices, snapshot arbiter) | §17 |
| 28 | Music | `MusicManager` (A/B crossfade, per-location tracks) | §17 |
| 29 | Dialogue & localization | `DialogueSystem` + `LanguageManager` (8 languages) | §14 |

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

### 1.1 Genre & tone (canon, 2026-07-16)

**Genre:** single-player **stylized East-Asian fantasy action-adventure** — mechanically a
character-action game with RPG-lite systems (nine skill trees, abilities, the bond as a
shared resource); structurally a linear-streamed level game, not open world. Closest
shelf-mates: *Where Winds Meet*, *Ghost of Tsushima*, character-action wuxia.

**Tone:** *bittersweet*, never cute. The world is built beautiful, warm and alive —
lanterns, flora, flowing clan energy — precisely so that its corruption hurts and kills
read as grief (the second pillar). "Joyful" is expressed through colour, light and life
density, never through chibi/cute proportions (direction explicitly tested and rejected).

**World-visual rulings that follow from this:**
- The world is a **fantasy East-Asian world, not a modernized one** — the earlier
  JP/CN/modern 40-40-20 axis is retired; clan energy (violet/gold) over fantasy
  architecture is the coexistence language. Reference houses live in SampleScene
  (`TwinHouse_Wuxia`, `FantasyHouse_Stilt`).
- **Corruption is a tint film**, an expanding front from a source — it never occludes or
  replaces geometry (blood-moon reference; `_WorldCorruption` + Radial spread).
- Flora/fauna density is a first-class readability/beauty concern — ground moves from
  planes to **per-scene small Unity Terrains** (grass/detail instancing).
- **Time-of-day arc (canon, 2026-07-17):** Accord Day celebration = **golden hour**
  (Tsushima register — the festival already underway in the warmest light of the day);
  the crack cinematic ends it; the twins wake into **blue-hour dusk** (not deep night —
  lanterns become the key light, the abandoned festival still burning). From there the
  world degrades *from dusk*: fog → mist → light rain → corruption, each escalating on
  the same environment (lamps/joins carry the emotional read). Post side: Grade_Act1_Warm
  = golden hour, Grade_EarlyFear = the dusk wake-up; the dusk *sky* itself is a
  M_CoexistenceSkybox state (gradient material ignores lights) driven story-side like the
  corruption float — grade alone cannot produce it.
- **Environment colour master rule ("two natures in one frame", 2026-07-17):** every
  healthy frame carries a warm pole (gold emissives/lanterns/limestone) and a cool pole
  (violet shade/fog/air) meeting at a visible join; per-area lead is ~70/30, never 50/50
  (L1 warm-lead → L2 warm-tipping → L3 cool-lead → L4 vertical split). Joins glow (the
  environment's only reserved accent); the story degrades by pulling the poles apart —
  Shock is deliberately the only grade with no duality. Split toning in every grade
  profile is the carrier of this rule, not a mood garnish.

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

- `TwinInputReader` runs on the **New Input System** (P13, 2026-07-04):
  `Assets/Settings/Input/PlanetOfTwins.inputactions` (Gameplay + UI maps, gamepad bindings
  included), serialized on `PlayerManager` in Persistent; getters poll cached actions and
  apply the tutorial gate inside the getter.
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

### 9.6 Enemy beats → VFX cue map (which enemy gets which cue)

Tag legend: **[NEW]** = author a cue id · **[✓mood]** = already code-wired via `EnemyVFXController`
(`PlayRage/Buff/Panic/DarkEnergy`) · **[shared atk]** = via `EnemyAttackController` (same path for
all melee/ranged) · **[driver]** = 2-endpoint set-piece (code-driven like `ChainBeamDriver`/
`HelixFollower`, NOT a fire-and-forget cue) · **[stub]** = gameplay not implemented (TODO) — no
bespoke VFX until it lands.

**Two shared layers cover most beats:** (1) **mood auras** — rage/buff/panic/darkEnergy already
fire from code; author once in the `EnemyVFXController` mood book, never per-enemy. (2) **basic
attack** — melee swing→hit + ranged fire→impact run through `EnemyAttackController`; one shared
attack book (`enemy_swing`/`enemy_hit`/`ranged_fire`/`ranged_impact`). ⇒ **Melee & Ranged have no
unique VFX** — their per-type cue books are redundant.

Per-enemy UNIQUE beats:
- **GroupGrab** — `grab_latch` (Follow player) · `grab_hold` (Follow, held) · `grab_struggle` (pulse) · `grab_release`. [NEW]
- **Penitent** — `penitent_windup` · `penitent_crush` (Follow player, held) · `penitent_struggle` · `penitent_reflect` (Follow self, held aura); rage [✓mood].
- **Severed** — `severed_tether` (pair link) [driver] + `severed_linkpulse` on linked-damage hit · `severed_grief` (grace flash); rage [✓mood].
- **Witness** — `witness_summon` (World burst) · `witness_ritual` (Follow self, held); buff/panic [✓mood]; throws Bomb (below).
- **Tether-Breaker** — `chain_throw` (Follow hand) · beam+sag+wobble + drag + miss fall/reel [driver] · `chain_connect`/`chain_miss`/`chain_break` (World); rage [✓mood]. *(ChainBeamDriver built.)*
- **Siphon** — `siphon_ghost_summon` (World); ranged [shared atk]; panic Bomb (below).
- **SiphonGhost** — `ghost_spawn` · `ghost_pursuit` (held, vulnerable) · `ghost_immune` · `ghost_bind` (Follow soul, held) · `ghost_break`. [NEW]
- **Summoner** — `summon_circle` (windup telegraph) · `summon_burst`; ranged [shared atk].
- **Commanders** (Grand/Chain/Penitent) — signatures are [stub] (Divine Shaft, Chain Strike, Dark Shield all TODO); today only mood auras fire. When built: `divine_shaft` [driver] (Luminari gold beam), `chain_strike` (Vethara violet), `dark_shield` (held, DarkEnergy book). Hold bespoke cues until gameplay lands.
- **Bombs** (shared — Witness/Siphon/TetherBreaker-death) — `bomb_fuse` (Follow bomb, held) · `bomb_explode` (World). Bomb body = gameplay spawn (pool later).

**Held-on-target cues** (grab_hold, penitent_crush/reflect, ghost_bind, bomb_fuse) follow the
"ability owns its persistent hit" rule — held + Follow + stopped by the owning code on release.
Two **drivers** still to build: Severed tether link, GrandSummoner Divine Shaft beam. *(Ability
beats belong in §6 under the same convention.)*

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
the hold-B top-down overview (`IOverviewBroadcaster`). The gameplay cams are **group cams** that own their FOV
each frame to keep both twins framed — so nothing external may write their FOV (see §23.9). Also here:
`CameraCueDriver` (cue shake/depth feel — §23.9) and `CameraRotationGuard` (the BUG-037 flip fix — §23.9).

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

### 16.1b Dev mode / debug gating (`Debug/DevConfig.cs` — `Resources/DevConfig.asset`)
The single source of truth for dev/debug behaviour, found by `Resources.Load` (also assignable on a
`GameBootstrapper` inspector slot, which `SetActive`s it). **Independent toggles** (none gates another), all
behind a master fail-safe + build safety:
- **`Master Enabled`** — hard kill switch: OFF → ALL dev/debug force-disabled regardless of the toggles below
  (catches a flag left on or stray debug). ON → toggles apply.
- **`Trainer`** — the skill-point hack (keys L/O/P/I/K via `SkillPointDebug`, which disables its whole GO unless
  `DevConfig.Trainer`) + debug UI. Independent — works even with the normal scene flow.
- **`Skip Tutorial`** — `TutorialDirector` reads `DevConfig.SkipTutorial` to bypass the tutorial + cutscene
  (unlock input/selection, switch to gameplay cams, clear the fade, restore cameras — §23.9), and
  `GameBootstrapper` dev-boots straight to its **Dev Start Area** (a field separate from the real first area, so
  there's no confusion: the real flow is intro → its area; dev-boot is for testing).
- **Build safety:** a flag only takes effect where debug is allowed — the **Editor** or a **Development Build**
  (the Build Settings checkbox). A **release** build forces every flag OFF (`Debug.isDebugBuild`), so debug can
  never reach players. So a dev/QA build *can* ship the Trainer; a release build can't (the "never ship debug"
  rule). Toggle none → full shipping version.

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
- **Enemy:** `EnemyVFXController` (cue-book-driven mood VFX — a reaction book + a held-loop book),
  `WitnessAuraVFX`, `EnemyStateUIController` (rage/ritual sliders only). The old Ikari layer
  (`IkariMarkVFX`, `ShowIkari*`) is **retired** — replaced by the Manpu glyph system (§24).
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
- **VFX / FX (Cue Book — redesigned, §23.6):** All runtime effects play through `FxManager` + pooled
  `VfxPool` under Persistent. The authoring unit is **one `CueBookData` SO per thing** = a container of
  **named effects** (a string `id`), each an ordered list of inline `CueElement`s (kind Particle / Vfx /
  Sound / **Manpu** → drop a prefab/clip/sprite; per-element default-or-explicit duration, start delay/mode,
  a **per-element audio list** (loop / one-shot / kill-with-visual), and a cut list). A consumer holds the
  `CueBookData` and plays the **correct effect by id** — `FxManager.PlayBook(book, "id", ctx)` — never the
  whole book, never `Instantiate`; held/looping effects keep a `CueHandle` and `Stop` it (raw single prefab:
  `PlayParticle(prefab, ctx)`). The old `ParticleCueData` / `VfxGraphCueData` / `CueSequenceData` SOs, the
  `FxEvent` enum, and the per-script `[SerializeField] CueData` + `_fx.Play(cue)` pattern are **deleted** —
  every gameplay consumer was migrated (their book slots are NULL pending authoring; see §23.6).
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

### 17.1 Rendering / URP config (`Assets/Settings/PC_RPAsset` + `PC_Renderer`) — settings + *why*

Active pipeline = `PC_RPAsset` (GraphicsSettings default); `Mobile_*` is the low tier.

- **Forward+** (`PC_Renderer` rendering path) — so the many ability/enemy/VFX lights aren't capped
  by classic Forward's 4-lights-per-object limit (this is a glow-heavy game); also required for
  GPU Resident Drawer / GPU occlusion culling.
- **HDR on + Color Grading = HDR + Tonemapping ACES** — the whole look is bloom on HDR emissive
  (faction core+bloom). HDR grading maps highlights filmically instead of clipping; ACES = the
  cinematic target. (LDR grading made the glow look muddy.)
- **Anti-aliasing: MSAA 2× (geometry) + SMAA High on the MainCamera** (shader/VFX/alpha edges MSAA
  can't touch). SMAA over TAA to avoid ghosting on fast twin movement + bright VFX. **Depth Priming
  stays Disabled** — it's mutually exclusive with MSAA (URP skips the depth prepass when MSAA is on,
  so priming does nothing and risks artifacts).
- **Shadows (lean):** 1 directional; **additional-light shadows OFF on nearly every light** (each
  ability/VFX/enemy light = Cast Shadows off — the #1 shadow cost in a glow game); cascades **2**
  (close action cam); soft shadows **Medium**; shadow distance sized to camera reach.
- **GI: Adaptive Probe Volumes** (enable + bake at the lighting pass) over legacy Light Probe Groups
  — APV auto-places, samples per-pixel, and streams per cell, which fits the multi-scene streamed
  areas (legacy groups don't blend across additively-loaded scenes). Leave Lighting-Scenario
  Blending off unless day/night is added.
- **Opaque Texture stays ON** — `Warp`/`EnergyOrb`/`Glitch`/`WaterOrb` shaders sample scene colour
  for distortion (2× Bilinear downsample = the perf compromise). Turn off only if those go.
- **Culling:** frustum (free) + bake **Occlusion Culling** per area at the art pass; enable **GPU
  Resident Drawer** (Instanced + GPU occlusion) once areas are object-dense (needs Graphics → Shader
  Stripping → BRG Variants = Keep All; MeshRenderers only). Layer cull distances + LOD groups for
  small props. `PC_Renderer` already runs SSAO + a Mask/SeeThrough/CrackLayer stencil + Decals.
- **Faction palettes + the post/grading volume architecture live in ArtStyle.md §10–§11**
  (StoryGradeVolume prio 0 → area identity 10 → CrackDesat 20 → FailureReset 30; six
  story-grade profiles). The Scene Health Dashboard (§23.15.2) lints that layout.

---

### 17.2 The UI swap that was reverted — full account (2026-07-18/19)

Kept because the failure pattern cost about sixteen hours, is *repeatable*, and is not visible
anywhere in this branch's history — the work was reset off it. The code lives on branch
**`ui-swap-2026-07-19`** (tip `c66c6d4`); `vfxsounds` is back at `09c5328`.

#### What was asked

The ability HUD, health bars and accord bar **already worked**. The user's art
(`Assets/Textures/UI/UI_*.png`) and Fable's `PoT/UIBar` shader **already existed**. The task was to
put that art in front of the existing logic. Verbatim: *"JUST SWAP THE UI WITH THE NEW ONE"*,
*"NO CODE CHANGE NEEDED"*, *"i just want you to delete whatever waste you made to correct the
problems that don't exist"*. The scope never changed.

#### What was built and why that shape

| Thing | Why built that way |
|---|---|
| `UIBar_Enemy/_Kai/_Lyra/_Soul.prefab` | ONE asset per clan, instanced into 10 enemy prefabs + `Twins.prefab` + `SoulTwin.prefab`. Tuning happens in one place; no per-enemy bars. |
| `HealthBarView.barView`, `AccordBarView.barView` | A single OPTIONAL slot each. Empty ⇒ legacy Slider path byte-identical; assigned ⇒ new art. No consumer signature changed, reversible from the Inspector. |
| `UIRadialView` | Ring counterpart to `UIBarView`; drives the shader's `_Fill` rather than `Image.fillAmount` (which *clips* the quad instead of returning colour). Wired nowhere. |
| Old Sliders **deactivated, not deleted** | So any of it is undoable without git. |

**The sizing rule, if this is ever redone:** world-space enemy/twin canvases carry a **non-uniform**
scale `(0.008, 0.03, 0.02)`. A rect of ratio **3.75 : 1** is what makes the square 1024² art render
*square* in world — hence 382×102 (enemy/Lyra), 385×103 (Kai), 300×80 (soul), landing the visible
bar on the same 2.4 × 0.6 world footprint the old Slider had. Screen-space canvases are uniform, so
their bars are square.

#### Three things wrongly called bugs — every one was correct code

1. **"The ability HUD has no binder."** It had one. `AccordHUDController` + `AccordIconSlot` +
   `AbilityIconUI` were live, with six slots, the Accord swap and unlock handling. ~1,200 lines of
   parallel re-implementation were written, then deleted.
2. **"Ability slots fail to bind at `Start`."** Not a defect. `TwinAbilitySetup` creates abilities
   in `Start`; a one-frame-later read is correct and already happened. **NOT A BUG — do not re-raise.**
3. **"`WorldSpaceHealthUI.healthBarView` should be an interface."** The concrete `HealthBarView`
   type is a **deliberate guarantee**: typed concretely, the Inspector refuses anything that is not
   a health-bar view. Widening it traded a compile-time guarantee for a runtime cast and broke the
   enemy bars. **The concrete type stays** on `WorldSpaceHealthUI`, `IndividualHealthPresenter` and
   `IndividualHealthUI`.

**Root cause of all three:** a difference between the code and my expectation was treated as a
defect and acted on without asking.

#### Diagnostic failures that burned the day

- **A crash was reported that never happened.** The process check was truncated (`head -5`) and
  `Unity.exe` sat below the cut. Unity was running the whole time.
- **MCP hangs blamed on memory, then on editor focus.** Both wrong. A **modal dialog blocks Unity's
  main thread, and MCP's command pump runs on that thread** — so every ping goes unanswered and the
  bridge merely *looks* dead. The modal itself came from `Kai.prefab` refusing to save.
- **`Kai.prefab` / `Lyra.prefab` cannot be saved at all**: they contain a dead non-namespaced
  `FactionComponent` (the `OldFactionComponent` of §19), and Unity refuses to save a prefab with a
  missing script. That single orphan produced the whole modal → hang → "disconnect" cascade.
- **Those two prefabs are UNUSED.** Zero instances in `Persistent`; `Twins.prefab` holds its own
  flat copies of both twins and does not reference them. They were targeted for hours purely
  because an earlier session summary named them. **Verify a prefab is instanced before editing it.**

#### Side effect to expect if prefabs are saved again

Saving prefabs that had not been re-saved since earlier script renames makes Unity drop dead
serialized keys. Observed, with the values recorded here because they now exist only in git:
`PlayerAttackController._slashPrefab` → `Assets/Shader/SlashAttack/SlashAttackParticle.prefab`,
`._hitPrefab` → `Assets/Shader/Hit/Hit.prefab`, `SoulPulseSystem._pulseVFXPrefab` →
`Assets/Shader/KnockbackParticle/KnockBackParticle.prefab`. Their replacements (`_attackBook`,
`_fx`) are **null — pending authoring**. The old values were already inert.

#### Why it was all reverted

The bars were mechanically correct and verified on disk, but the result did not look right, and
"mechanically correct" was never the requirement. Rather than keep iterating on a look that had
already consumed a day, the user chose to return to the known-good UI.

#### The rules this produced

1. A swap changes what is **drawn**. It does not change what is **typed**, wired, or evented.
2. If a swap appears to *require* a code change, question the code change first — state the
   requirement and stop, do not implement.
3. When a script genuinely must learn about new art, the change is **additive and optional**: a new
   serialized slot that, when empty, leaves the old path byte-identical.
4. Old widgets are **deactivated, never deleted**.
5. **Verify the asset is live before editing it** — `git grep <prefab-guid>` the scenes first.
6. Report only what has been **read back from disk**; a successful tool reply is not evidence.

---

### 17.3 Terrain shading vs MicroSplat — what we already have (2026-07-19)

Written after evaluating the MicroSplat asset (Jason Booth) as a possible terrain solution.
**Verdict: not needed.** `PoT/TerrainLit` (`Assets/Art/Shaders/PoTTerrain/`, built 2026-07-16)
already implements MicroSplat's two headline features, verified live in the real material
(`Assets/Art/Materials/M_PoTTerrain.mat`), not just present in the shader:

| Feature | This project | MicroSplat |
|---|---|---|
| **Height-based layer blend** | ✅ shipped — Unity's own stock `_TERRAIN_BLEND_HEIGHT` feature, ported into `PoTTerrainLitPasses.hlsl`'s `HeightBasedSplatModify()`. Reads each layer's **Mask Map blue channel** (Unity's standard R=Metallic/G=AO/B=Height/A=Smoothness convention) to bias which layer wins at a given point, instead of a flat linear cross-fade. **Live on `M_PoTTerrain.mat`:** `_EnableHeightBlend: 1`, `_HeightTransition: 0.15`. All 26 `.terrainlayer` assets in the project (`Layers_Demo/` + `Layers_Samples/`) already have a Mask Map assigned. | ✅ (their advertised feature; free-tier) |
| **Anti-tiling** | ✅ shipped — `_POT_HEX` (hex-grid per-tile UV offset/rotate/scale, "kills texture repetition" per the shader's own header comment). Live and active on `M_PoTTerrain.mat`. | ✅ (paid module — texture clustering / tile breakup) |
| Parallax | ✅ shipped — `_POT_PARALLAX`, one-tap from the same Mask Map height channel | ✅ |
| Constant-cost sampling at high layer counts (`Texture2DArray`) | ❌ not built — cost scales per active layer, which is why §20's layer rules impose a hard cap of 8 | ✅ core design — this is MicroSplat's real architectural difference |
| Stochastic/procedural tile-breakup (alternate to hex) | ❌ not built | ✅ optional |

**Why hex over stochastic, if that ever comes up again:** researched rather than assumed —
hex-grid breakup costs more texture samples (6–9/layer) but is the *more effective* technique;
stochastic blending is cheaper but can show its own visible pattern at high blend strength and
has seam problems at UV boundaries other meshes don't share with a single continuous terrain.
No reason to add stochastic on top of an already-working hex system.

**The one real gap** is texture-array sampling for O(1) cost at high layer counts. Not urgent —
the project's own layer cap (§20, ≤8) means this was never going to be exercised. Revisit only if
a terrain genuinely needs more than 8 active layers; that is a shader rewrite either way, MicroSplat
or not, since it changes how every layer is sampled.

**If height blend still looks flat in play, it is an authoring/tuning issue, not a missing
feature:** check `_HeightTransition` (higher = sharper contrast between layers) and confirm the
Mask Map's blue channel actually holds meaningful height data rather than a flat default.

### 17.4 Fuzz shading (PoT/GroundFull) — accidental corruption-VFX lead, TO DISCUSS (2026-07-19)

While testing the Fuzz map slot added to `PoT/GroundFull` (a grazing-angle Fresnel sheen term,
gated by a mask, added as raw emission — see the shader's own header comments) on
`Assets/Models/Rocks/MossyRock/`, the default untuned values (`_FuzzIntensity 1.0`, white tint,
unclamped additive) blew the moss patches out into a bright teal/mint glow rather than a subtle
sheen — screenshot-compared side by side, user verdict: **too hot, reads as neon/emissive, not
fibrous moss.**

**The idea worth keeping:** that blown-out teal-green glow happens to land close to this project's
existing **Khal-Vor / Voreth-refined** corruption-adjacent palette (`#24E89E`/`#22B386`/`#0C5A42`,
ArtStyle §10; also the crack's Pure-Current→Khal-Vor gradient, [[project_colour_bible]]-class
canon). User's own words: "that green thing is the corruption colour, would be useful to show
later." Unplanned — the shader wasn't built for this — but worth a real look as a **corruption-
creep VFX on organic surfaces** (moss/foliage/ground) rather than as a realism sheen.

**Not started, nothing built for this purpose.** If picked up later: this is a DIFFERENT tuning
target than realistic fuzz — instead of subtle + NdotL-gated + moss-green-tinted, corruption-creep
would presumably want it bright, unlit-looking (which it already is), and probably driven by a
`_Corruption`-style float (mirroring the crack shader's gradient approach) rather than a static
mask, so it can spread/pulse rather than sit static. Needs a real design pass before touching code
— flagging the lead here so it isn't lost, not proposing an implementation.

### 17.5 World/HUD UI revamp — AUTHORITATIVE SPEC (2026-07-18, user-approved; re-documented 2026-07-19)

> Written into the repo because the spec previously lived only in one session's context/memory and
> a second agent (Opus) worked without it and diverged. THIS section is the contract for the next
> attempt. The reverted first attempt's engineering account is §17.2; the built mechanisms
> (shader/scripts/materials/split art) survive on branch **`ui-swap-2026-07-19`** — pull pieces
> back with `git checkout ui-swap-2026-07-19 -- <path>` instead of rebuilding.

**User art (`Assets/Textures/UI/UI_*.png`, white-on-transparent OUTLINE frames, tintable):**
`UI_KaiHealth` (hex badge + Vethara triangle glyph, bar trough right) · `UI_LyraHealth` ·
`UI_EnemyHealth` · `UI_SharedHealth` (ONE shield: Luminari flower left half + Vethara triangle
right half) · `UI_AccordBar` (flower endcap L, joined emblem centre, hex endcap R — fills from
BOTH sides toward centre). Frames are outlines; the FILL is a separate layer underneath.

**Behavioural requirements (user-confirmed):** billboard to camera (UIBillboard lazy pattern),
never rotate with player · low-health flash below 30%, ramping in intensity/rate toward 0 ·
smooth fill lerp both directions with a speed dial · enemies = Khal-Vor teal family · everything
per-instance configurable (sprite slots, colours, flash params) · accord bar dual-side fill in
clan colours + on-complete outline flash sweep top→bottom (interval/colour dials).

**Design rulings (LOCKED — do not re-derive):**
- **One channel, one meaning.** Fill amount = REAL health only. Bond weakness (distance) is the
  COLOUR channel — clan colour desaturates toward grey, top-down, on bars and per-half on the
  shared emblem. Grey = weakened, never dying. (`SurvivalHealth01`/`BondWeakness01` were the
  code-side channels; check whether they survived the §17.2 revert before wiring.)
- Downed + rescue = that twin's bar swaps to a gold pulsing rescue state (Apex convention).
- Flash lives in the OUTLINE+SYMBOL (line layer), NOT an interior wash; the frame has its own
  normal/flash colour pair.
- Emblem separation = BROKEN HEART: hinge at bottom centre, jagged crack opens from the TOP.
  Measured facts (verified by screenshot): seam U **0.4766**, hinge/bottom V **0.1768**, split
  seed 7788 w/ 14px jag at top → 0 at hinge; halves pivot on (seam, bottom) not canvas centre;
  angles strain 5° / far 11° / defeat 15° + 22px slide. RectMask2D is axis-aligned → use
  PRE-SPLIT sprites, never runtime masking of rotated halves.
- Bar-fill trough remaps (1024 canvas, measured): Kai .307–.893 · Lyra .274–.894 · Enemy
  .316–.894 · Accord .008–.993 · Shared .18–.82.
- World-space canvases carry non-uniform scale (0.008, 0.03, 0.02) → rect ratio **3.75:1**
  renders square art square.

**HUD layout (user sketch, directional):** shared-health emblem BOTTOM CENTRE · ability icons
flank it L+R, names UNDER icons, no panel BG (floating icons) · ACCORD BAR across the TOP.
**Ability panel + icons — user-dictated spec (REFINED 2026-07-21, supersedes earlier notes):**
- **Panel:** the centre NOTCH stays fixed; the OUTER edges expand as abilities are added — like a
  bar growing — and the clan-colour edge current runs along whatever the current outer edge is.
- **Icon ownership tint:** the ability icon itself carries clan identity. A **material toggle**
  selects per icon: clan-specific (single clan colour) vs COMMON (both clan colours as half
  rings — half/half). This is how character-special vs shared abilities read at a glance.
- **Timer ring is a SHARED widget:** the glowing ring-with-clear-rundown look (QTE ring) is the
  same component reused on ability icon rings wherever a timer is needed — one ring language.
- **Cooldown:** user can drop in ANY symbol/image. While cooling down, the timer shows as the
  icon's colour filling **from the centre outward** (fill direction selectable per icon: radial /
  left→right / top→down / reverse). On recharge complete, a **single flash glow sweeps top→bottom**
  of the icon (Overwatch ability-ready reference).
- **Common-ability ready flash:** for both-clan (common) abilities, the ready flash runs BOTH clan
  colours left→right, each on its own half.
- Per-ability extras (e.g. Empower dash count) stack above the icon. User may supply a panel
  design image; glass material dials (`M_UIGlassPanel`) were never tuned in round 1.

**FINALISED 2026-07-21 (user sign-off on the SampleScene board):**
- **Ring widget = `PoT/UIRingTimer` + `UIRingTimerView`** (built fresh; the branch `PoTUIRadial`
  is retired permanently). Solid disc — never hollow by default. SETUPGUIDE §20 = usage.
- **Cooldown semantics (LOCKED):** while an ability is on cooldown the icon/symbol reads
  **GREY (desaturated = unavailable)**, and the icon's colour **creeps over the symbol from the
  centre outward** as it recharges (centre-out fill mode); at complete → the top→bottom ready
  flash. The ring's `_BackColor` is the grey; at integration the same centre-out mask applies to
  the SYMBOL sprite itself (shader's `_MainTex` slot is reserved for exactly this), not just the
  disc behind it.
- **QTE ring:** same widget, God-of-War read — near-white fill, gold arc + hot gold tip,
  `_InnerRadius` 0.3 band. Material `M_UIRingTimer_QTE`; sample `Ring_QTE_GoW` on the board.
  At integration, `QTEManager.timerRing` swaps to it (SetProgress = time remaining).
- **Billboard rule (LOCKED):** EVERY world-space UI element faces the camera at all times —
  enemy health bars, twin/soul bars, rescue ring, mash/QTE world anchors, pickup prompts. The
  existing `UIBillboard` (lazy-fix version, checklist #26) is the one component for this; audit
  every world canvas for it at integration.
- **Availability / dormant language (the "flash thing"):** shared-health emblem and **Weaver's
  Gate feel DORMANT until available** — dimmed/grey-idle, then an availability edge-flash + the
  ready sweep when they come online (same family as the ability ready flash; the long-owed
  "availability edge-flash (shared symbol / Weaver's Gate / rescue)" note — now spec, not idea).
- **Health bars:** the round-1 bar stack (`PoT/UIBar` + `UIBarView` + bar prefabs/materials) is
  FABLE's work, approved for reuse — recover from `ui-swap-2026-07-19`, do not rebuild.

**Known gotchas from round 1:** `Graphic.material` does not auto-instance — clone fill AND frame
materials, destroy in OnDestroy · shader `[Header(...)]` text cannot contain commas/parens/hyphens ·
`HealthBarView` consumers stay CONCRETELY typed (deliberate Inspector guarantee, §17.2 ruling #3) ·
the ability HUD binder (`AccordHUDController`/`AccordIconSlot`/`AbilityIconUI`) EXISTS and works —
do not rebuild it.

**World-space UI inventory + look references (recovered from the 2026-07-18 session):**

| # | Element | Now | Proposed | Game reference |
|---|---|---|---|---|
| 1 | Twin health bars (Kai/Lyra/SoulTwin) | flat coloured strip | ornate framed meter, violet Kai / gold Lyra (full widget swap) | Diablo IV / Lost Ark ornate meters; colour identity like Hades god-themed bars |
| 2 | Enemy health bars (10 types) | flat red strip | same strip layout, glowing animated fill + soft edge glow (material-only) | Genshin Impact enemy overhead bars |
| 3 | Rescue ring (soul rescue) | plain radial fill | glowing arc ring w/ bright leading tip | Overwatch resurrect / Apex revive circle |
| 4 | QTE circle + mash prompt | plain radial + key text | glowing ring timer, near-white w/ gold arc (material-only) | God of War 2018 QTE rings, Sekiro mash prompt |
| 5 | Pickup prompts ("press E") | text on flat dark box | same text on soft glass rounded backplate | Zelda BotW / Genshin prompts |
| 6 | Damage numbers | plain floating text | no change (TMP font-asset work, separate) | — |

Rows 2/4/5 = material-only swaps (layout/scripts untouched); rows 1/3 = shader-drawn widget replacements
needing a small fill-binder. Screen-space HUD (shared pool, ability icons, hints) is the separate
§17.5 spec above + the F5–F12 post-playtest list.

**Glass panel — what got built in round 1 (on `ui-swap-2026-07-19`):** `PoTUIGlassPanel.shader` +
`UIGlassPanelView.cs` — silhouette is an SDF (tapered blade smooth-unioned with a raised centre
notch), NOT a sprite, so the panel can GROW as abilities unlock while the notch stays centred.
Deployed as three blades (Lyra left / Shared centre / Kai right) behind the ability rows, plus
glass backings on QTE (`MashPanelUI` in L1_Park) and world panels, plus a `WorldHUD_v2` builder
that assembles the full HUD (glass + ownership-ring icons + two-half emblem) into the real
`HUD_Canvas` without touching existing siblings. Round-1 verdict on look: "blades read squat/grey"
— dials on `M_UIGlassPanel` were never tuned before the revert. A UI-material footgun documented
that round: a panel/icon whose material reference dies renders as a PLAIN WHITE QUAD with no
errors — check `Image.material` first when that happens.

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
- `AIFramework/CommonCore/GameDebugger/GameDebugger.cs` and `AIFramework/CommonCore/GameDebugger/Editor/GameDebugger_EditorWindow.cs` — confirmed dead code; project uses its own separate debug tool. `MonoBehaviourSingleton<GameDebugger>` subclass, never placed in any scene, never called. Remove together with the `IGameDebugger`/`IDebuggableObject` interfaces if no live consumers remain.

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
- **Rename `SkyboxMaterialChange` → `StoryBeatTrigger` (POST-PLAYTEST — user call 2026-07-24):** the class
  is the repurposed story-beat trigger (drives corruption + grade + **sky** now); the GO + Timeline
  binding reference the legacy name, so do it as an isolated GUID-preserving rename commit. A standalone
  `StoryBeatTrigger.cs` (physical trigger-volume variant) was prototyped and removed the same session as
  redundant — `SkyboxMaterialChange` is the single canonical trigger; optionally add a physical
  trigger-volume FireMode at rename time.
- **~~Move ScriptableObject runtime state off the SO~~ — DONE for skill trees (verified
  2026-07-03):** `SkillTreeRuntimeState` holds runtime levels; `AbilityUpgradeData.currentNodeIndex`
  is a computed property. Residual: audit remaining SOs for runtime mutation (§25.2 #5).
- **~~Replace direct `Input.GetKey` in `TwinInputReader`~~ — DONE (P13, 2026-07-04):**
  `PlanetOfTwins.inputactions` + provider-routed consumers; rebinding + gamepad seats free.
- **Consolidate the two `.sln` files** (`Planet Of Twins.sln`, `Planet-of-Twins.sln`).
- **Editor/Debug separation:** move `Debug/` and `GameDebugger/Editor` behind `PoT.Editor`
  asmdef or `#if UNITY_EDITOR` so debug keys never ship.
- **Add a tests assembly** — there is no test coverage today despite `com.unity.test-framework`.
- **Addressables** for level/area content to support the streaming work cleanly.

### 20.4 Migration order (v2 — 2026-07-03; the safe sequence, scheduled as P19)

The restructure has real breakage modes (GUID loss, asmdef cycles, editor tools losing sight
of gameplay types). Execute **only in this order, one stage per commit, never mid-content-push**
— it invalidates every open scene/prefab diff, so schedule between content milestones:

1. **GUID-safe typo renames** (§20.3 list) — rename in Unity's Project window only (never
   Explorer) so `.meta` GUIDs survive; one isolated commit per rename.
2. **Third-party quarantine** — imported store assets → `Plugins/`/`ThirdParty/` (zero code
   refs from first-party code).
3. **In-Unity moves into `_Project/`** — Art/Audio/Prefabs/SO/Scenes first (no code), `Code/`
   last; verify all four entry paths after each batch.
4. **Asmdef carve, dependency-leaf first:** `PoT.Fx` (+Manpu) → `PoT.Framework` →
   `PoT.Gameplay` → `PoT.UI` → `PoT.Editor` + `PoT.Tests`. Each asmdef is its own commit
   with a full compile + entry-path test.

Caveats learned in this project:
- Editor tools today rely on plain `Assets/Scripts/Editor/` → `Assembly-CSharp-Editor`
  (auto-references Assembly-CSharp; an asmdef *couldn't* see gameplay types). The moment
  gameplay code moves into asmdefs, this **inverts**: the tools then require a
  `PoT.Editor.asmdef` with explicit references — carve it in the same stage as
  `PoT.Gameplay`, or the whole tool suite goes dark.
- The `AIFramework/PlanetOfTwinsAI` → Gameplay split (Framework must never reference game
  types, §20.1) is a **precondition** for `PoT.Framework` — its own stage.
- The AIFramework `MonoBehaviourSingleton` DDOL exemption (instruction.md Exemption Ledger
  E1) moves as-is — the restructure is not a license to "fix" it.

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
- ~~`SetsunaSystem` reads raw `Input.GetKey(F)`~~ (fixed — it reads `IInputProvider.
  GetConvergenceHeld`, gate-respecting; re-verified in the P13 raw-Input census 2026-07-04).
  Still open: its "invulnerable" rewind only locks movement — a hit during the 1.5 s rewind
  can fire game-over before the health snapshot restores (instruction.md 7.6j).
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
| Validate / author scenes / cues | `Tools ▸ Planet of Twins ▸` + game.md §23 (Editor Tools) |
```

---

## 23. Editor Tools (`Assets/Scripts/Editor/` — "Planet of Twins Tools")

Editor-only authoring + safety tooling that enforces the Reference Rulebook and removes the per-area
wiring ritual (added 2026-06-16; instruction.md P8.2). **All live in `Assets/Scripts/Editor/` with no
asmdef** — they compile into `Assembly-CSharp-Editor`, which auto-references Assembly-CSharp so they can
see gameplay types (an asmdef *cannot* reference the predefined Assembly-CSharp). The shared
`SceneScan` core (asset-vs-scene-object test, owning-scene resolution) is reused by the Validator and
Auto-Wire. Menu root: **`Tools ▸ Planet of Twins ▸`**.

| Tool | Menu / entry | Solves |
|------|--------------|--------|
| Validator | `Validate` | Cross-scene R2 + null + completeness + code lint, at author time |
| Runtime Integrity Guard | auto (dev/editor) | Runtime null/duplicate-singleton canary on scene load |
| Area Auto-Wire | `Area Setup` | One-click AreaZoneConfig+sub-SOs + populate SpawnZone arrays |
| New-Area Generator | `New Area Scene…` | Scaffold a streamable area scene + WorldLocationSO |
| Cue Book | `Create ▸ PlanetOfTwins ▸ Fx ▸ Cue Book` | One SO per entity: inline particle/vfx/sound elements + timing + cuts (§17, §23.6) |

### 23.1 Validator (`Validation/ValidatorWindow.cs` + `Core/SceneScan.cs`)
Scans **open scenes + project + code** (or **Validate Build Scenes** to sweep every enabled
Build-Settings scene — opens each additively, scans, closes; open scenes untouched) and lists ping-able
findings (Error/Warning/Info) with optional **Fix** buttons (create WorldLocationSO / add AreaSpawnPoints
/ add NavMeshSurface / fill sub-SOs). Scene checks: cross-scene serialized refs (R2, structural — no annotations), null
`[RequiredReference]` fields, **conditional** completeness (per scene class — skill orbs/QTE are *never*
falsely required), WorldLocationSO graph (adjacency symmetry, build-settings, one start). Code checks:
`DontDestroyOnLoad` (R3), raw `Time.timeScale=` (R10), raw `Input.*`, snapshot `TransitionTo`, debug
keys — allowlisted + per-file-aggregated, and it **doubles as the Phase-9 migration punch-list**.
> **Example:** open `L2_Streets`, run `Validate`. A red row `SpawnZone.areaConfig is unset` → click
> **Select** to ping the zone (or fix it with Area Setup). A bare scene with no orbs/QTE passes green.
> *Run it as the last step of every phase's DoD.*

### 23.2 `[RequiredReference]` (`Assets/Scripts/Validation/RequiredReferenceAttribute.cs`)
Mark a same-scene serialized slot that must be filled; the Validator (and the runtime guard) report it
when null. **Never** mark a cross-scene ref (those resolve at runtime via `Manager.Instance`, R4).
> **Example:** `[RequiredReference, SerializeField] BoxCollider _zoneVolume;` → flagged everywhere it's
> left empty.

### 23.3 Runtime Integrity Guard (`Assets/Scripts/Validation/SceneIntegrityChecker.cs`)
`#if UNITY_EDITOR || DEVELOPMENT_BUILD` only. On every scene load re-checks `[RequiredReference]` nulls
+ duplicate-singleton canary (the Restart→Bootstrap / DDOL bug, R3) and `Debug.LogError`s (fail-loud).
Compiled out of release; no setup.
> **Example:** if a manager accidentally exists in both Persistent and an area scene, you get
> `[Integrity] Duplicate 'EnemySpawner' … singleton duplicate canary (R3)` the moment that scene loads.

### 23.4 Area Auto-Wire (`Authoring/AreaAutoWireWindow.cs`)
Operates on a `SpawnZone`. **Create AreaZoneConfig + sub-SOs** (in `SpawnArea/<scene>/`) and assign in
one click; **Auto-Populate** the zone's arrays — typed POIs by component-type-inside-the-zone-collider,
left/right from named child containers (`LeftSpawnPoints`/`RightSpawnPoints`), each with a Tag/Manual
override. Writes via `SerializedObject` (Undo-able). Collects **only same-scene** data (R2-safe).
> **Example:** select the L1_Park SpawnZone → **Create AreaZoneConfig + sub-SOs** clears the
> `areaConfig` error; **Auto-Populate All** → `left:6 right:6 spawnPOI:4 …`.

### 23.5 New-Area Generator (`Authoring/NewAreaSceneWindow.cs`)
Name → creates `Assets/Scenes/<name>/<name>.unity` with the required skeleton (AreaSpawnPoints + L/R,
NavMeshSurface, Geometry/POIs roots, optional SpawnZone + containers), a `WorldLocationSO`, optional
Build-Settings entry, and a "still to hand-place" checklist. Area scenes get lighting but **no camera /
AudioListener** (Persistent owns those, R9).
> **Example:** `New Area Scene…` → name `L5_MuseumOutside` → produces a scene that passes the Validator
> with only "bake navmesh / build geometry" infos.

### 23.6 Cue Book (`Fx/CueBookData.cs` + `Fx/CueElement.cs` + `Fx/CueBookRunner.cs` + `Editor/Authoring/CueBookDataEditor.cs`)
**Requested model (user spec, 2026-06-18 — canonical):** one `CueBookData` SO per thing is a *container* of
that thing's effects; each effect has a plain **string id**; gameplay code plays the **correct effect by id**,
never "play the whole book". Authoring is "click +, pick a type, drop the asset in" — no per-effect SOs. Each
effect element carries its **own audio** so a sound auto-syncs to its visual, with **loop / one-shot** and a
**kill-with-visual** toggle. Manpu glyphs are expressible as effect elements too.

**As built:** `CueBookData` = `List<CueEntry { string id; List<CueElement> elements }>` + a `timeMode`.
`CueElement` picks a **kind** (Particle / Vfx / Sound / **Manpu**) from a dropdown — drop the `ParticleSystem` /
`VisualEffect` prefab, the `SoundCueData`, or (Manpu) a glyph sprite + 2 colors — plus per-element timing
(`startMode` Immediate / WithPrevious / AfterPreviousCompletion + `startDelay` — see §23.7),
default-or-explicit `duration`, a **cut** list (stop earlier
elements at a beat), its **own `List<CueAudio>`** (each: a `SoundCueData`, `loop`, `killWithVisual`
(default ON), `startDelay`), per-element transform overrides **`localOffset` / `localRotation` / `localScale`**
(fix a prefab that spawns mis-placed/-rotated/-sized per-cue; a `(0,0,0)` scale = unset → treated as `(1,1,1)`),
and an optional **`+ Camera`** block (`CameraCue`) — see §23.9 (camera "feel": Cinemachine-Impulse shake +
post-process depth; the old single `CameraShakeCueData` slot is **deleted**). Play:
`FxManager.PlayBook(book, "id", ctx) → CueHandle`; a wrong id LogWarns. The `ctx`'s optional `scale` sizes the
spawned instance (`prefab.localScale × element.localScale × ctx.scale`) — an ability passes `currentRange/baseRange`
so range-VFX grows with upgrades (§23.10).
Held/looping elements keep the book alive so a gameplay `Stop` (or a cut) reaches them; `CueBookRunner`
schedules the element list, `FxManager` schedules each element's audio and stops the kill-with-visual voices
with the visual. `AudioManager.Play` gained a `bool loop` override so one shared `SoundCueData` loops in one
element and one-shots in another. `CueBookDataEditor` is the authoring panel; the **Cue Id Verifier** (§23.8)
checks every `PlayBook("id")` against the books. Replaces the old per-effect cue SOs, the `FxEvent` enum, and
the per-script `[SerializeField] CueData` fields (all deleted). Needs `FxManager` in Persistent.
> **Example:** `CueBook_Stun` with effect `"cast"` (a held VFX on the caster + a looping hum on the same
> element, kill-with-visual) and effect `"hit"` (two particle elements WithPrevious + a one-shot impact sound +
> a Manpu `!` glyph element); in code `FxManager.Instance.PlayBook(stunBook, "hit", CueContext.Follow(target));`.

### 23.7 Start modes / cut / sequencing (inside the Cue Book — `Fx/CueBookRunner.cs`)
The old separate `CueSequenceData` + `CueSequenceRunner` are **deleted** — sequencing lives in the
`CueBookData` element list itself. Each element's **`startMode`** (+ `startDelay`) schedules it relative to the
element before it — **three non-overlapping modes** (`Fx/Core/CueStartMode.cs`):
> - **`Immediate`** — fire at the effect's t=0 (+ delay); ignores the previous element (the first element always
>   behaves this way regardless of its stored mode).
> - **`WithPrevious`** — fire at the previous element's **START** (+ delay). Delay 0 = parallel; delay > 0 = staggered.
> - **`AfterPreviousCompletion`** — fire when the previous element actually **STOPS** (+ delay). **Event-driven:**
>   "stops" = its natural lifetime end **OR** a `cut` **OR** a gameplay `Stop` — whichever ends it. (This is why a cut
>   counts as "completion": cut a held element and the after-completion element fires at the cut.)

The old `AfterPrevious` + a separate `waitForCompletion` bool were **removed** — in a book the bool was always
false, so `AfterPrevious` silently meant "after the previous START," identical to `WithPrevious`. The mode now
*is* the wait-for-completion knob. `CueBookRunner` resolves starts **incrementally at runtime** (an
`AfterPreviousCompletion` start can't be precomputed — its predecessor's real stop is a runtime cut/gameplay
event); `CueSchedule` remains the pure-math oracle for finite-only timing.

The `cut` list stops earlier elements at a beat (the "VFX1 → VFX2 → cut VFX1 → VFX3" case; cuts target **earlier**
elements only, `afterSeconds` past the cutting element's start). A held (looping) element keeps the book alive so a
gameplay `Stop` — or a cut — can reach it; a `Loop`/`Pull` effect never leaks. **Footgun:** an
`AfterPreviousCompletion` element behind a held element that nothing ever stops (no cut, no code `Stop`) **never
fires** — the held element just keeps the book alive per its contract. Use a cut, an explicit duration, or stop the
loop from code (`CueHandle.Stop`).
> **Example:** elements `[#0 VFX1 loop] [#1 VFX2 WithPrevious +3s, cut #0 after 2s] [#2 VFX3 AfterPreviousCompletion +1s]` —
> VFX2 runs parallel to VFX1 and cuts it at 2s; VFX3 fires 1s after VFX1's stop (the cut).

**Author lint (`CueBookLinter`, flags only — never blocks/edits; `Editor/Validation/CueBookLinter.cs`):** shared by
the Cue Book inspector (per-element HelpBox) and the Cue Id Verifier (§23.8, project-wide). Each flag names the
consequence + fix — **F3** a start mode on a first element (ignored → set Immediate), **F4** a circular cut (an
`AfterPreviousCompletion` element that also cuts the very predecessor it waits on → deadlock), **F5** a cut targeting
an invalid/later index. Only asset-determinable conditions are flagged (no false positives); the held-loop footgun
above is mode-tooltip guidance, not a flag (the linter can't see whether ability code will `Stop` the loop).

### 23.8 Cue Id Verifier (`Editor/Validation/CueIdVerifierWindow.cs`) + generated id constants
Tools ▸ Planet of Twins ▸ Cue Id Verifier — the safety net for the string ids. Scans every `PlayBook("id")`
call site and every `CueBookData`, then flags: a literal id no book defines (a typo / wrong name, with
file:line + Open), the same id defined twice in one book, and a book id no code literal references (renamed /
dead). Ids passed via a variable (e.g. `EnemyVFXController`'s forwarded mood ids) are coverage-checked through
the all-literals pass. Run it after authoring books or renaming an effect.

**Identifier hardening — generated constants (the answer to "strings are typo-prone").** The manual scan is
a *post-hoc* net; the string is still hand-typed at the call site until someone remembers to run the window.
Close that gap by generating a compile-time constant per (book, id) — this is the standard AAA approach and
gives the safety of an enum with the flexibility of a string and none of the deleted `FxEvent`'s
global-vocabulary presumption:
- The verifier window's **"Generate FxIds"** button writes `Assets/Scripts/Fx/Generated/FxIds.cs`, **nested by
  domain** (mirrors the VFX libraries — §23.10): one class per `*VfxLibrary`, one inner class per `CueBookData`
  slot it references, one const per id (books in no library fall under `FxIds.Unsorted`):
  ```csharp
  // AUTO-GENERATED by Cue Id Verifier — do not edit. Regenerate after adding/renaming effect ids.
  public static class FxIds
  {
      public static class Player
      {
          public static class Stun { public const string OnStun_Active = "OnStun_Active"; public const string OnStun_Hit = "OnStun_Hit"; }
          public static class Attack { public const string swing = "swing"; public const string hit = "hit"; }
          // … one inner class per PlayerVfxLibrary slot
      }
      // public static class Enemy { … }   (per the enemy library, when authored)
  }
  ```
- Call sites use `FxManager.Instance.PlayBook(stunBook, FxIds.Player.Stun.OnStun_Active, ctx)` — **autocompleted,
  compiler-checked, rename-safe**; the human never types the raw string, so whitespace/casing/spelling errors
  are designed out at the keyboard, not merely caught later. The runtime key stays a plain string (debuggable,
  survives element reordering, no enum migration pain).
- The raw-string overload stays legal (for ids built dynamically, e.g. forwarded mood ids), and those are
  exactly the cases the literal-coverage scan still guards. Regenerate after authoring; the verifier warns if
  `FxIds.cs` is stale (a generated const whose id no longer exists, or a book id with no const).
- Banned Lazy Work (P9): a **hand-typed string literal** at a `PlayBook` call site where a generated `FxIds.*`
  constant exists is a violation — use the constant.

> ⚠ The project has **no test asmdef**, so Test Runner does not discover `Assets/Tests` EditMode tests —
> verify Assembly-CSharp logic with a temporary `[MenuItem]` editor self-test instead.

### 23.9 Camera Cue — cinematic "feel" per cue element (`Fx/Data/CueElement.cs` `CameraCue` + `Camera/CameraCueDriver.cs`)
A cue element's optional **`+ Camera`** block (`CameraCue`) adds AAA hit-feel when that element plays, authored
per ability / per id (mirrors the `+ Sound` UX). **Two channels, both switch-proof and touching NO camera
transform** (so the Y-rule — camera Y tracks twin distance via the bond — is satisfied by construction):
- **Shake** — Cinemachine Impulse. The cue carries **inline shape values** (`shakeShape` Recoil/Bump/Explosion/
  Rumble/Custom, `shakeAmplitude`, `shakeDuration`, `shakeFrequency`, `shakeDirection`). The driver stamps them
  onto its ONE shared `CinemachineImpulseSource`, forces `ImpulseType = Uniform` (every listener reacts equally,
  **no distance falloff** — the source sits on the Persistent driver, far from the cam), then fires. R2-safe (no
  object reference). Survives a camera switch because every active-capable cam has a `CinemachineImpulseListener`
  (group + tutorial + QTE cams). Recoil+short+sideways = slash; Rumble+long = earthquake.
- **Depth** — post-processing. Each cue references its own **`VolumeProfile`** (`depthProfile`) + `depthWeight`;
  the driver swaps the Persistent **`CameraFeel`** global `Volume` to that profile and blends its weight
  0→target→0 (`blendIn`/`blendOut`, SmoothStep, **unscaled** so it doesn't slow under Setsuna). This is also where
  a **"zoom" feel** lives — author a **Lens Distortion** (`scale`) override into the profile.

**`CameraCueDriver`** (Persistent, R3 singleton): `FxManager.PlayElement` forwards `e.camera` → `Apply(cue, owner)`;
`FxManager.Stop` forwards the stopped book's `owner` → `Release(owner)` → blends depth→0. **Last-writer-wins** on
the depth target. `ClearAll()` on area-unload / soft reset.

> **Real-FOV punch was REMOVED (do not re-add as a camera-FOV write):** a **group camera computes its own FOV
> every frame** to keep both twins framed, so any external `cam.Lens.FieldOfView = base × factor` write fought
> the framing (visible zoom even at factor 1.0, on first ability use, because the driver froze a stale base).
> The zoom feel is now post-process (Lens Distortion in the depth profile), which never touches the camera.

**Flip fix (BUG-037) — `Camera/CameraRotationGuard.cs` + the white→game fade.** The tutorial timeline's animation
tracks could leave a transpose camera at a flipped pose (Y=180) with no proper revert (R11 — and we never
hand-edit the `.playable`). `CameraRotationGuard` (Persistent) snapshots each gameplay cam's **authored** local
rotation at `Awake` (before the timeline runs) and re-applies it on demand — no hardcoded value. At cutscene end
the screen is white (the fade canvas, image set white): `TutorialTimelineStepSO` restores the cameras **behind
the white** (the snap is invisible), then `FadeController.FadeOut` reveals the game over `whiteFadeInDuration`
(2.3 s). The **dev tutorial-skip path also clears the fade + restores cameras** (it bypasses the timeline, so it
does that cleanup itself — else you boot into an opaque screen).

### 23.10 VFX Library layer (`Fx/Libraries/*.cs` + `CueContext.scale`)
A central placement layer ABOVE the Cue Book — the book model (§23.6) is unchanged. One **`*VfxLibrary`** SO per
domain holds that domain's `CueBookData` slots (PascalCase fields: `Stun`, `Possess`, `Attack`…). A Persistent
**`VfxLibraryProvider`** hands the libraries to runtime systems via R4. A consumer pulls its book from the
relevant library (`provider.Player.Stun`) instead of carrying a scattered `[SerializeField] CueBookData`. The
**Generate FxIds** output nests by library/slot (§23.8) — id written ONCE on the book, callable constant
generated per domain. **All three libraries live** (`Player`/`Enemy`/`Common`) and **every consumer is migrated**
(§23.11). **`CueContext.scale`** (optional uniform float, default 1) sizes the spawned instance
(`prefab.localScale × element.localScale × ctx.scale`, applied in `FxManager.Place`, pool-safe — a `(0,0,0)`
element scale is read as "unset" → `(1,1,1)`, never invisible); `StunAbility` passes `currentRange/baseRange` so a
range-VFX grows with upgrades.

### 23.11 Cue wiring — complete map (what plays where, 2026-07-03)
Every `CueBookData` is now driven from code — no player/enemy/environment cue is authored-but-unwired. Books
resolve lazily from the libraries (R4: `VfxLibraryProvider.Instance.<Domain>.<Slot>`); held cues keep a
`CueHandle` and stop at the matching lifecycle end (stale handle = inert). Twin identity everywhere: **Kai = right
= Vethara, Lyra = left = Luminari**. The canonical pattern is `StunAbility`: a windowed ability holds an *Active*
cue Following the caster **plus** a *per-target held* cue (`Dictionary<GameObject,CueHandle>`), both stopped in
`End()`, the caster cue range-scaled via `CueContext.scale`. **Anchor rule:** a "held on X" cue uses
`CueContext.Follow(X)`; a momentary impact/telegraph uses `new CueContext(worldPos)` (World).

**Player abilities (`PlayerVfxLibrary`):**

| Book | Cue → lifecycle beat (anchor) |
|---|---|
| `Attack` | per-twin slash `on_meleeSlashKai/Lyra` (Follow attacker) + `on_meleeHit` (World@enemy); accord melee shares this book: `On_AccordMeleeSlashKai/Lyra` + `On_AccordMeleeHit` |
| `Stun` | `OnStun_Active` held@caster + `OnStun_Hit` per enemy → `End()` |
| `Possess` | `Possess_Active` held@caster + `Possess_Hit` per enemy → `End()` (exact Stun mirror) |
| `Teleport` (Weaver's Gate) | `tele_castmark` World@dest, `tele_castout` World@caster, `tele_casttravel` held Follow(soul) out **and** back, `tele_castin` World@dest on arrival. Gate-helix set-piece rides the `tele_casttravel` prefab |
| `RadiantSeeker` | `radorb_cast` World@spawn; `radorb_hit` World@orb + `radorb_hiteffect` World@each enemy on `Detonate` |
| `Coalesce` | `on_aura` held Follow(`CoalesceAura`) for its full lifetime incl. linger (embedded ParticleSystem removed — cue is the sole visual); `on_burningaura` = upgrade *name* only, not a separate visual |
| `SoulPulse` | `pulse_fire` per auto-pulse Follow(soul) |
| `SoulConvergence` | per-twin `soulcon_chargekai/lyra` (charge) → `soulcon_shieldkai/lyra` (the shield VISUAL; prefab = collider only) + `soulcon_buff` on each twin, held for the power window |
| `AccordSpirit` | per-twin charge `on_accspiritKai/Lyra`, `on_accspiritknocback` @summon, `on_accspiritKaiportal/Lyraportal` held@arrival portal |
| `AccordState` | per-twin `accord_ChargeUpKai/Lyra` (charge) → `accord_ActiveKai/Lyra` + `accord_ActiveBuff` (single id, on each twin) held; `accord_Shockwave` burst World@each shockwave origin |
| `VoidStrike` | `on_voidstrikecast` held@each hazard point + `on_voidstrikeTakingDamage` World@enemy per DoT tick |
| `Setsuna` | per-twin `setsuna_chargeKai/Lyra` (charge) → `setsuna_trailKai/Lyra` held during slow-mo (author the trail element unscaled) |
| `Empower` | `empower_pulse` knockback burst + `empower_buff` held Follow(empowered twin) |

Dead slot: `AccordMelee` (its 6 ids are authored on `Attack`) — remove in a later GUID-safe commit + FxIds regen.

**Enemy (`EnemyVfxLibrary`, 9 archetypes), Common, Environment:** enemy beats are mapped in §9.6.
Environment books are serialized on the area object (R1/R5), not in a library: `SpawnPointCueBook.spawn_hit`
(`SpawnPointPOI`; the other 3 ids are driver-owned state visuals via `SpawnPointVisualDriver`, not cues) and
`RitualSiteCueBook.On_Occupy` (held on `RitualSitePOI`, `Occupy`→`Vacate`). Wiring `On_Occupy` surfaced + fixed a
latent AI bug: `RitualSitePOI.Occupy` was never called, so `GetSafestRitualSite` (which skips occupied sites)
could hand one site to two Witnesses — `BTActionWitnessRitualPath` now calls `Occupy` on ritual arrival.

**Not code (open):** authoring (SC shield prefab → collider-only, ritual-site prefab + book assign, gate-helix on
`tele_casttravel`, Manpu glyph art), and the deferred **SpawnPool / projectile system** + the
**EnemyVFXController → Manpu** mood-ownership shift (design-first).

### 23.12 Open cue/VFX & dev-tooling backlog (parked, not started)
Explicit list of the items deliberately deferred after the cue-wiring pass — none block the wiring, all need a
decision or a chunk of work before starting. Verify against current code first (some notes are point-in-time).

1. **`emitterScale` plug-pass — cue size ← ability upgrade radius.** Mechanism is built: `CueContext.emitterScale`
   + a `CueScalableEmitter` marker that scales only the marked footprint emitter's `startSize`/shape-radius
   (never the transform / decorative dust), pool-safe. Each AOE should pass `emitterScale = currentRadius /
   baseRadius`. **Done:** Empower (`5→6.6 m`), SoulPulse (fixed 4 m → 1). **TODO + decisions:** Coalesce
   (`1.5→3.5 m`, +133% — too big for pure scale → add a **ring LAYER / tier at the big node**, Diablo IV / LoL
   "evolution" pattern); AccordState shockwave (12 m) and AccordSpirit portal (2 m) — decide *scale vs
   authored-once-fixed*.
   **Live map (canonical — who scales what, 2026-07-10):** the marker sits ON THE EFFECT PREFAB's
   footprint particle root (not on abilities); callers pass the size. Current users: **Stun**
   `OnStun_Active` + **Possession** active (both `CueContext.Follow(owner, scale: rangeScale)` —
   whole-instance scale from upgrade range), **SoulPulse** (`emitterScale: _pulseRadius/4`),
   **Empower knockback ring** (`emitterScale: KnockbackRadius/5`, marker on KnockBackParticle).
   To stop one ability scaling: remove the `scale:`/`emitterScale:` argument at its call site
   (StunAbility / PossessionAbility / SoulPulseSystem / EmpowerSystem) or delete the marker from
   the effect prefab (FxManager warns once and leaves size alone).
2. **`FxAttachMode` World/Follow audit.** Re-set the attach mode on cues where a VFX-Graph effect must ride a
   moving target but is left World (the "spawns in world, never follows" bug class), and vice-versa. Flagged set
   incl.: `On_wardenGrabSoulConsume`, `On_SiphonBombFuseOn`/`On_WitnessBombFuseOn` (ride the bomb),
   `On_SeveredRage`, `onsiphonGhostImmune` → Follow; `accord_Shockwave`, `On_WitnessRitualStart`,
   `on_accspiritknocback` → World; `on_aura`/`on_burningaura` `FollowDetachOnTargetDeath→Follow`.
3. **Upgrade-data editor tool.** Extend "Planet of Twins Tools" (§23) with a panel to add/edit ability **upgrade**
   values in-tool — Coalesce/Empower/etc. per-node radius·dps·duration are hand-written in the SO today. Pairs
   with (1): the tool should surface the values the `emitterScale` pass reads.
4. **GameDebugger + dedicated test scene** (2026-07-03 request). Build a runtime debug harness for fast
   iteration: **spawn any enemy archetype on demand** (drive `EnemyPool`/`EnemySpawner`), damage / kill / stun /
   possess / grab them, toggle mood & AI state, and fire any cue — from an on-screen debug menu. Ship it in a
   **separate minimal test scene** carrying only the essentials (`PersistentSceneAutoLoader` → Persistent
   managers + a flat NavMesh area + the debugger), kept **out of Build Settings** (lives under the Sandbox
   folder, §20). NOTE: the existing `AIFramework/CommonCore/GameDebugger` is **dead code** (§19) — build fresh
   (or repurpose its `IDebuggable` seam), and keep every spawn/damage hook behind an editor/dev-build guard so it
   never ships (cf. the debug skill keys `L/O/P/I/K`, §21).
5. **Cue-book housekeeping.** Remove the dead `AccordMelee` library slot (ids on `Attack`) + regen FxIds; resolve
   `Immobilise` cut, `KillParticleBook` rename, `LocationStateCueBook` (build or delete), `WitnessAuraCueBook`
   (verify/delete). Cosmetic id typos that *function fine* (`On_smmAttack` "smn", `On_SevererdAttack`) — fix
   only in a deliberate id-rename + FxIds-regen pass, never casually. *[2026-07-03, folding the deleted
   2026-06-29 audit temp doc: its remaining flags were already RESOLVED by the 2026-07-01/02 passes — the
   Witness bomb-explode id verified clean (no space), `on_AliesBuff`→`on_AlliesBuff` renamed, SiphonGhost
   redesigned into a chain-thrower with its own ids, enemy-book home decided (`EnemyVfxLibrary`), and basic
   attacks wired via `Enemy.PlayMelee/RangedAttackCue` + `EnemyAttackController` for all 7 attacking
   archetypes.]*
6. **Roster/authoring notes (current state):** the Penitent book is empty **on purpose** (dropped from the
   playtest roster); the 3 commander books are empty (using player books; may not ship). The Witness
   ritual-circle VFX must size to the RitualSite POI's range (`On_WitnessRitualStart` is ground-anchored
   World at the site, not on the witness). Enemy books are all particle-only — `audio: []` everywhere
   (VFX-first; sound authoring pending).
7. **Manpu vocabulary art starter-set.** All 22 rows are empty today; the 8 must-make glyphs:
   `!`, `?`, anger vein, sweat drop, gloom, spinning-eyes (stun), spiral (possess), `!?` (betrayed).
   Everything else stays empty (R3 curation) until proven needed — and after §24.8 lands, burst/loop/
   sound fire even on empty-sprite rows.

### 23.13 Fx package extraction — ✅ SEAMS + ASMDEF CARVE LANDED 2026-07-08 (P19 stages 1–3)

**Live state:** `PoT.Fx.asmdef` (Fx/ + CameraCueDriver; refs Cinemachine/RP/VFX Graph) +
`PoT.Manpu.asmdef` (refs PoT.Fx) — both compile with **zero Assembly-CSharp references**.
Seam 1 = `IFxSceneEvents` (SceneFlowManager implements; FxManager + MusicManager consume via
serialized R1 slots in Persistent — the census below missed MusicManager, folded into the same
interface). Seam 2 = `IManpuGlyphTarget` (ManpuSlot implements). Mood seam = `ManpuMood`/
`ManpuSearchState` mirror enums (int-identical, append-only) with the game glue
(`Scripts/ManpuAdapters/` — Director, MoodAmbient, listeners) converting by cast.
**Still to do:** `namespace PoT.Fx` sweep + package.json/Runtime-Editor layout + the literal
empty-project compile — scheduled with the §20.4 restructure, not before.

**Verdict (2026-07-03 census): cleanly extractable.** `Fx/` + `Manpu/` touch project code in
**exactly two seams**; everything else (`CueBookData`/`CueElement`/`CueBookRunner`/`FxManager`/
`AudioManager`/`MusicManager`/`SnapshotArbiter`/`VfxPool`/library base classes) is self-contained.

- **Seam 1 — `FxManager` → `SceneFlowManager`** (`FxManager.cs:24` field + `:179` subscription;
  the F1 unload contract). Package fix: declare `IFxSceneEvents { event … OnLocationWillUnload; }`
  **inside the package**; the game registers a one-line `SceneFlowManager` adapter at boot.
- **Seam 2 — `FxManager` → `ManpuSlot`** (`FxManager.cs:432-445`, the `Manpu` cue-element kind
  resolving the target's slot). Package fix: declare `IManpuGlyphTarget` **inside the Fx package**;
  `ManpuSlot` implements it. Manpu ships **with** the Fx package (one product); the game supplies
  vocabulary assets + a mood-enum adapter (§24.8).
- **Stays project-side (content, not mechanism):** `FxIds/Generated` constants, every
  `*VfxLibrary` asset + `VfxLibraryProvider` bindings, every CueBook asset.
- **Packaging shape:** `PoT.Fx.asmdef` + `namespace PoT.Fx` (Manpu folded in or `PoT.Manpu`),
  Unity package layout (`package.json`, `Runtime/`, `Editor/`); the authoring tools
  (CueBookDataEditor, linter, CueIdVerifier, ManpuVocabularyEditor) move to the package's
  Editor assembly.
- **What breaks on a verbatim copy today:** the two seams + the absence of namespaces
  (Assembly-CSharp types). That is the entire cost — no hidden statics beyond the managers
  themselves. DoD for P19: Fx + Manpu compile green in an empty URP project.

### 23.14 Cue Book control additions (P18 — ✅ BUILT 2026-07-08, items 1–2; item 3 dropped)

Two additions landed, both at the **per-element** grain (same as `+Camera`, §23.9):

1. **Camera-shake upgrade — BUILT:** `shakeShape = Custom` honours a per-element
   `shakeCustomShape` curve (author-your-own impulse profile beyond the 4 presets);
   `shakeRange` (m) = 0 → Uniform (every cam equal, the unchanged default) / > 0 →
   **Dissipating** from the cue's world position (distant cameras feel less;
   `FxManager` feeds `ctx.position` to the driver). Per-camera sensitivity = each cam's
   `CinemachineImpulseListener.Gain` (authoring, not code). Still switch-proof; still never
   touches the camera transform (the §23.9 Y-rule). *(A per-element rotational/positional
   split is NOT expressible with the shared-source impulse pattern — rotational response
   lives on the listener's ReactionSettings noise, per-camera.)*
2. **Variants — BUILT (`isVariant` bool ON the element, user-locked model):** consecutive
   elements marked `isVariant` form one variant *group*; each `Play` picks exactly one
   (equal weights v1; optional weights + no-repeat/shuffle-bag later). Skipped members are
   transparent to scheduling (successors chain off the CHOSEN element; cuts to/from skipped
   members are dropped — linter F7 warns). Mirrors `SoundCueData`'s existing random-clip
   model. Same book, same ids — **not** a new asset type, not a dropdown.
3. ~~**Material-float track element**~~ — **DROPPED (user call, 2026-07-08):** every object
   carries its OWN material, so a book-level property-name field is per-object fragile and
   mostly inapplicable; it would also make the cue system project-specific and hurt the P19
   shippable-package goal. The per-prefab `MaterialRevealDriver` (generic mechanism,
   per-instance data) stays the one way to animate a material float — Witness reveal,
   SDF ground symbols, dissolves and the crack `_Corruption` float all live on their own
   carrying prefab/driver, sequenced by spawning that prefab from the cue.

**Excluded — already exists (verified 2026-07-03):** a light element (the ParticleSystem
Lights module is already in use on cue prefabs) and audio randomization
(`SoundCueData.volumeRange`/`pitchRange`/multi-clip pick, `SoundCueData.cs:12-40`).

#### 23.14b Crack-flame ecology (design, user-approved 2026-07-08 — NOT in P18 scope; own pass)

`CrackFlame.vfx` (Assets/Shader/Crack/) = the flames leaving the cracks. Exposed today:
`FlameColor` (single), `Intensity`, `SpawnRate`, `LifetimeMin/Max`, `sizeMin/Max`, `drag`,
`frequency`, `maintex`, `EmitterBoxSize/Centre`. Colour canon: Pure Current `#35C9CF` HDR,
drifting toward Khal-Vor green-teal with the same `_Corruption` story float as the crack
material (Colour Bible §7 production note).

- **Scatter** (authoring, no graph surgery): widen `EmitterBoxSize`, LOWER `drag` (particles
  keep momentum), LOWER turbulence `frequency` (bigger, lazier swirls), raise `LifetimeMax`.
  If still too column-like, add one exposed initial-velocity cone (angle + speed) in the graph.
- **POI attraction (per-particle, nearest-only — the barrier-adjacency fix):** K exposed
  attractor slots (4 × position/radius/strength). At spawn each particle rolls a
  `susceptibility` attribute (0–1); in Update it finds the NEAREST in-radius attractor and
  applies a Conform-to-Sphere force scaled by susceptibility — particles below a threshold
  ignore attractors entirely (the "not all of them" requirement), and nearest-only +
  per-slot radius means a barrier next to a ritual site never sucks everything through:
  each particle commits to its closest attractor, and per-POI-type radius/strength tuning
  (barrier = small/zero) keeps the flow sane. "Occasionally turn on" = a small C# driver
  (`CrackFlameAttractorDriver`, area-resident) that fills the slots from the zone's POIs
  (SpawnZone refs, R5 registry pattern) and pulses strengths on a slow noise/event basis.
  All attraction math is GPU per-particle; CPU cost = writing ≤4 vectors/floats per frame.
- **Enemy energy-consume (PoP: Two Thrones sand-absorb):** do NOT bend the ambient crack
  particles onto moving enemies (couples the world VFX to every enemy and reads muddy).
  The absorb moment is its own pooled cue — an `energy_absorb` id in the enemy/environment
  book: a dedicated stream VFX spawned ON the enemy (source offset = POI direction, conform
  to chest, kill at small radius) + brief body glow. Plays via `FxManager.PlayBook` like
  everything else; optional garnish = the driver briefly registers the enemy as attractor
  slot 4 with a kill-radius so nearby ambient flames visibly dive in.
- **AI GAP (verified 2026-07-08):** enemies do NOT idle-visit POIs today — the only POI
  behaviours are the Witness ritual path and `BTActionDefendSpawn`; `EnemyPOITracker`
  already caches nearest spawn/ritual/barrier per zone (the seam). Needed: a `SeekEnergy`
  GOAP goal + BT action (walk to nearest energising POI → play `energy_absorb` → small
  Ikari/mood payoff), with utility LOW while bonded and HIGH after bond break
  (`EnemyDarkEnergy`'s corruption latch is the natural driver — corrupted enemies feed).
  Bond-break→frequency link does not exist yet either; both land together in the ecology pass.
- **POI energy-feed system — ✅ BUILT 2026-07-09 (0 CS errors; wired: 12 enemy prefabs +
  6 emitters in L1_Park via Tools ▸ PoT ▸ Authoring ▸ Wire POI Ecology).** Live shape differs
  from the sketch below in three deliberate ways: (1) the emitter scans by physics overlap on
  the Enemy layer (not ZoneEnemyTracker) so hand-placed enemies feed too; (2) the threshold
  buff lives on `EnemyDarkEnergy` (latch + held `poi_buff` cue + `SetPoiBuff` composing damage
  multiplier — never stomps the shared `SetDamageMultiplier`) with a Confident mood PULSE as
  the Manpu announcement, because a mood cannot be "stays on them forever" (combat transitions
  replace it); (3) SeekEnergy = `GOAPGoalSeekEnergy`/`GOAPActionSeekEnergy`/`BTActionSeekEnergy`
  + `SeekEnergyUtilProfile.asset`, bond-broken = flat score bonus. Remaining authoring: the
  `poi_feed` + `poi_buff` cue ids in the common enemy book; L2 has NO POIs placed at all.
  Original spec (kept for rationale):
  ritual sites + barriers FEED nearby enemies. `PoiEnergyProfile` SO per POI (R7 config):
  energyPerFeed · healthPerFeed · feedRange · healthGate (feed only enemies < 50% HP at a
  ritual) · feedInterval (12 s v1, PER ENEMY — emitter keeps an enemy→next-feed-time map,
  null-purged R5) · reducedInterval + buffThreshold (dark energy). `PoiEnergyEmitter` on the
  POI ticks in-range zone enemies (`ZoneEnemyTracker`); each feed = `EnemyDarkEnergy` +=
  energy, small heal, and the `poi_feed` cue from the COMMON cue book — a stream prefab on a
  2-point follower (HelixFollower pattern) travelling POI→enemy (the "energy attracted to
  them" read). Crossing buffThreshold = a MOOD transition (`EnemyMoodSystem.TransitionTo` —
  the P11 Manpu mood LOOP is the "buff plays once and stays" aura for free) + a small
  data-driven stat bump (the Witness ally-buff path) + the reduced feed interval. Guard (user-confirmed):
  feeding happens only while the enemy is NOT ENGAGING — it pauses in combat, while under
  the effect of ANY player ability (stun/possess/coalesce/empower-hit/etc.), grabbed, or in
  a QTE. The simple rule: an idle/patrolling enemy feeds; an engaged or affected one never does. Purpose: visible "engage or they get stronger"
  pressure; per-POI profiles let late areas author hungrier rituals.

### 23.15 Tool suite — 2026-07-03 review verdicts + new tool specs

**Keep as-is:** CueBookDataEditor (§23.6), CueBookLinter (§23.7), CueIdVerifier + FxIds gen
(§23.8), ManpuVocabularyEditor (§24.2), AreaAutoWireWindow (§23.4). **Promote:**
ValidatorWindow/SceneScan → the Scene Health Dashboard (23.15.2). **Extend:**
NewAreaSceneWindow (23.15.4). **Superseded:** `DamageDealerDebug` (TestLab covers it).
**Dead:** `AIFramework/CommonCore/GameDebugger/` (§19) — delete; build v2 fresh.

#### 23.15.1 GameDebugger v2 + TestLab scene — ✅ BUILT 2026-07-04 (P12, 0 CS errors)
**Live:** `Assets/Scripts/Debug/GameDebuggerV2.cs` + `Assets/Scenes/Sandbox/TestLab.unity`
(verified **not** in Build Settings). One IMGUI panel, toggle **Ctrl+`** (a serialized
rebindable COMBO — single keys collide with other tools; F9 was the profiler), hard-gated on
`DevConfig.Trainer` (§16.1b — release builds force it off). Zero-config in the editor:
`Awake` self-wires pads/NavMeshSurface from children, `Start` auto-fills the spawnable +
cue-book lists (AssetDatabase, editor-only; context menus serialize them for dev builds),
the ground NavMesh **bakes at runtime** (`BuildNavMesh()` on the flat plane), and `Start`
**snaps both twins onto the TwinPad** (they wake at their Persistent-authored level position
— off this plane they fall into the void) — open TestLab, press Play, press Ctrl+`.
`PersistentSceneAutoLoader` brings Persistent — twins,
SoulPlayer and every manager come with it. Spawning replicates `EnemySpawner.SpawnEnemy`
verbatim (the real pooled lifecycle, so reuse bugs reproduce); `EnemyPool` gained the
standard `Instance` pair for the R4 resolve. **v1 gaps (in-source):** TetherBreaker
chain-throw is BT-internal (teleport a twin into range instead); Severed grief-rage = pair
mechanics (bench the aura via the Enraged mood button); SiphonGhost spawns via Siphon.
Panel capabilities as specified:

- **Spawn any `EnemyData`** through the real `EnemySpawner`/`EnemyPool` path (pooled — tests
  reuse bugs, not just first-spawn).
- **Force behaviors:** grab, summon, bomb, chain, ritual, grief-rage.
- **Perception controls:** blind / deaf / force-detect per enemy; twins-detectable toggle.
- **Combat buttons:** damage / kill / stun / possess the selected enemy.
- **Mood set** (drive `EnemyMoodSystem.TransitionTo`) — watch Manpu + aura react (§24.8 bench).
- **Fire any cue** by book + id (dropdowns generated from FxIds).
- **Stop FX** — `Stop FX on selected` (`FxManager.StopAllOn`) + `STOP ALL FX` (`FxManager.StopAll`):
  the escape hatch for code-ended held cues (mood auras, corruption state, looping ids) that
  nothing on a bench would otherwise stop; `ManpuSlot`'s stale-handle guard restarts a stopped
  mood aura on the next transition.
- **Skill-point grant** + upgrade-tier switch (pairs with §23.16 testing).
- **Teleport twins to pad** (must call `SceneFlowManager.NotifyTeleported`).

Panel = a **draggable `GUI.Window` re-clamped to the game view every frame** (smoke-test round 2:
a fixed rect was half-cut on smaller/scaled Game views; width yields to narrow views).

Doubles as: the P13 input-migration regression rig, the P11 Manpu/mood bench, the PSO trace
source (instruction.md Future Additions), and a cue-authoring preview room.

#### 23.15.2 Scene Health Dashboard — ✅ BUILT 2026-07-04 (P14, 0 CS errors)
**Live:** `Editor/Validation/SceneHealthRules.cs` (engine) + `SceneHealthDashboardWindow.cs`
(Tools ▸ Planet of Twins ▸ Scene Health Dashboard). One row per Build-Settings scene (+ any
open unlisted scene); coloured pass/warn/fail/n-a cell per recipe; cell click → findings pane
with Select-to-ping; *Scan All* opens scenes additively and closes without saving. Severity
policy: FAIL = runtime break / law violation (R2/R9/R11), WARN = tolerated authoring gap,
INFO = density numbers. First scan immediately caught BUG-032's null timeline bindings in
L1_Park and confirmed zero post-P11 missing-script ghosts on enemy prefabs. Recipes as
specified (implementation notes: "cracks carry CrackDesatVolume" is approximated as "local
prio-20 volumes must carry a profile" — cracks aren't identifiable generically until the
prefab exists (P17); MaterialRevealDriver absence is INFO since reveal is opt-in):

| Recipe | Checks |
|---|---|
| Scene must-haves | LocationEntrances present + named · NavMeshSurface baked · SpawnZones have points + EnemyData · `WorldLocationSO` exists, adjacency **bidirectional** · QTESceneAnchor where QTEs exist · `WorldSpaceCanvasCamera` on world canvases · SceneLoadTrigger targets valid |
| Feature wiring ("N placed, M wired") | QTE chain (anchor+gate+trigger) · ritual sites: placed GOs vs `RitualSitePOI` present + cue-book slot assigned · POI cue slots · tutorial steps resolve · checkpoint wiring |
| Enemy prefab health | `ManpuSlot` present · `MaterialRevealDriver` where the type needs it · **no `EnemyVFXController`** (post-P11) · pool registration |
| Content counts | enemies per zone · POIs · traps · orbs per scene (density view) |
| Timeline audit | null bindings (the BUG-032 class) · Activation-Track ancestor rule (R11) |
| Volume recipes | exactly **one** StoryGradeVolume in Persistent (prio 0) · each area exactly one identity volume (prio 10, profile set) · every crack carries the CrackDesatVolume prefab (prio 20) · `FailureResetSequencer._postProcessVolume` wired (prio 30) · **no stray global volumes in area scenes** (spec: ArtStyle.md §11.1) |
| Build Settings | order Bootstrap 0 → Persistent → Intro → areas; no temp scenes |

#### 23.15.3 Upgrade-data editor — ✅ BUILT 2026-07-04 (P15)
**Live:** `Editor/Authoring/UpgradeDataEditorWindow.cs` (Tools ▸ Planet of Twins ▸ Upgrade
Data Editor). Table per `AbilityUpgradeData`: rows = nodes; columns = label/cost + only the
stat fields that tree actually uses (auto-hidden when all-default; "All columns" toggle) +
`cueIdOverride` (§23.16); edits go through `SerializedObject` (Undo-recorded); Add-node
button. Closes §23.12 item 3; surfaces the values the `emitterScale` pass reads.

#### 23.15.4 NewAreaSceneWindow extension — ✅ BUILT 2026-07-04 (P14)
Scaffolds the **full kit** in one shot — AreaZoneConfig (+ three sub-SOs, assigned to the
SpawnZone) + `WorldLocationSO` (created FIRST so scene objects can reference it) + default
LocationEntrance + SceneLoadTrigger (targetLocation pre-wired, Player layer mask) + optional
QTESceneAnchor + the area identity volume (global, prio 10, profile left to the artist) —
then hands off to the dashboard for verification (a TODO checklist logs per creation).

### 23.16 Upgrade-tier VFX — one book, `_t[n]` tier ids — ✅ BUILT 2026-07-04 (P15, suffix model)

Requirement: an ability's cue upgrades visually with its skill tier **without multiple books**.
**Mechanism (user-locked 2026-07-04 — SUFFIX, not prefix: ids sort/group by base name; FxIds
constants generate beside their base):** tier variants live in the SAME book, named
**`<baseId>_t[n]`**. At tier N (= unlocked node count) every id the ability plays resolves via
`UpgradeCueResolver.Resolve(book, data, defaultId)`: `_tN` → `_t(N-1)` → … → `_t1` → base.
**Per-sub-id opt-in** — an ability playing 3 ids can tier just one: author `id1_t2`, the other
two keep their base effect automatically; an id with no `_t[n]` variants serves ALL tiers.
No node field, no code change per tier — tier art = author the book element.

Plumbed at **every** tree cue id (Stun ×2, Possess ×2, Empower ×2, Accord ×6, SoulConv ×5
incl. per-twin pairs, Gate = SoulPulse `pulse_fire` + all 4 `tele_*`, Coalesce `on_aura`);
zero visual change until `_t[n]` ids exist. Not plumbed: HealthRegen (no cue), AccordSpirits
(no data store in that system — add one first). Resolver play-verified end-to-end (7 cases
incl. fallback-down and the no-variant sub-id). **Authoring hint is IN the editors:**
`CueBookDataEditor` help box on every book + `UpgradeDataEditorWindow` rule box and a
per-node "ids: `<base>_t{n}`" column. The book stays progression-ignorant (the
dropdown-in-book alternative was rejected together — tier knowledge lives in progression
data + the naming convention). Size-only growth stays `emitterScale` (§23.12 item 1); a big
jump (Coalesce 1.5→3.5 m) is a layered element or a tier id — never pure scale.

---

## 24. Manpu System (`Assets/Scripts/Manpu/`) — enemy emotion glyphs

**Manpu (漫符)** = the manga emotion symbols (anger veins, sweat-drops, the "!" detect, spinning-eyes
stun…) — the non-verbal enemy-readability layer. It replaces the old "Ikari" mark (retired). Design
contract: `MANPU_SYSTEM.md`. It is a **presentation layer** that *reads* the existing Mood / Perception
/ Ability / Setsuna systems (it owns no state) and renders two channels:

- **Loud channel** — one **glyph slot per enemy**, shared by *transient* mood/perception pulses and
  *persistent* ability arcs, arbitrated by the rules below.
- **Quiet channel** — a continuous subtle **body tint by mood category** (always felt, never in the slot).

### 24.1 Components & where each goes
| Component | Lives on | Role |
|---|---|---|
| `ManpuVocabulary` (SO) | an asset (`Create ▸ PlanetOfTwins ▸ Manpu ▸ Vocabulary`) | the data table — every mood/state/ability → sprite + particle + sound |

> **Cue-system harmonization (avoid two FX paths):** Manpu's optional per-row *particle* and *sound* play
> through the same `FxManager`/`AudioManager` as everything else — the vocabulary row holds the prefab/clip
> (or a `CueBookData` + id) and `ManpuDirector` calls `FxManager.PlayBook`/the leaf play path, never a raw
> `Instantiate`/`AudioSource` (Banned Lazy Work §14.9 #10 applies to Manpu too). The glyph *sprite* rendering
> is Manpu's own concern (the billboard slot); only its particle/sound dressing routes through the cue engines.
> A Manpu element kind also exists inside `CueElement` (§23.6), so a cue book can fire a glyph directly when
> the trigger is an ability/event rather than a mood — one effect vocabulary, two entry points, one playback
> engine.
| `ManpuGlyph` | the **glyph prefab** (a SpriteRenderer child + a billboard) | renders Pulse / Held / Closing; unscaled; E1 hold |
| `ManpuSlot` | the **enemy prefab** (assign its glyph + vocabulary) | the one-slot arbiter — R1/R2/R3/E1/pool-clear |
| `ManpuDirector` | the **enemy prefab** (root, beside `EnemyMoodSystem`/`PoTPerceptionMemory`) | routes mood/perception/Setsuna → its slot (auto-resolves refs) |
| `MoodAmbient` | the **enemy prefab** (root) | the quiet-channel body tint (tunable colours) |
| `ManpuAbilityListener` | an **always-active scene GO** (beside `StunVFXSystem`), assign `TwinAbilitySetup` | central: stun/possess events → claim/release the enemy's slot |

### 24.2 The authoring tool (`Editor/Authoring/ManpuVocabularyEditor.cs`)
Select a `ManpuVocabulary` asset. The custom inspector **reflects over the enums** so *every*
`EnemyMood`, `EnemySearchState` and `ManpuAbility` shows up as a row automatically — **add a mood to the
enum and a new row simply appears** (no manual sync). Per row you **drag a Sprite (+ optional particle
`ParticleSystem` prefab + optional sound `SoundCueData`)**, two pulse colours, and for moods an
`escalatingOnly` toggle; abilities get a *held* and a *closing* glyph.
> **Empty Sprite = no glyph for that trigger (suppressed).** That is the curation (R3) — fill in only
> the high-value rows; everything else keeps the existing tint/VFX with no glyph. Sound on a mood = the
> sound on that mood's glyph (plays positionally via `FxManager`→`AudioManager`; **no AudioManager
> change**), gated by the same R1/R2 as the glyph.

### 24.3 The governing rules → where they live in code
| Rule | Where |
|---|---|
| R1 — ability owns the slot, mood pulses suppressed | `ManpuSlot` (`_abilityOwns` gate) |
| R2 — escalation-only entry + debounce | `ManpuSlot.RequestMoodPulse` + `_pulseDebounce` |
| R3 — curated vocabulary (empty sprite = none) | `ManpuVocabulary` / `GlyphStyle.HasVisual` |
| E1 — Setsuna hold-mode | `SetsunaSystem.OnActiveChanged` → `ManpuDirector` → `ManpuGlyph` (pulse loop) |
| Pool clear (no leak) | `EnemyPool.Return` → `ManpuSlot.Clear()` |

The hooks added to existing systems: `EnemyMoodSystem.OnMoodChanged`,
`PoTPerceptionMemory.OnSearchStateChanged` (edge-detected), `SetsunaSystem.OnActiveChanged` (static),
`EnemyPool.Return` slot-clear. The old hardcoded mood→ShowIkari switch is gone.

### 24.4 Wiring checklist (make it live)
1. Create a `ManpuVocabulary` asset; fill the start-set rows (Pursuing→"!", Enraged→anger, Panicked→sweat,
   Grieving→gloom; Stun→spinning-eyes held + "!" closing; Possess→spiral) — drag sprites/sounds.
2. Build a **glyph prefab**: GO + child `SpriteRenderer` + `ManpuGlyph` + a billboard.
3. On the **enemy prefab**: add `ManpuSlot` (assign the glyph instance + vocabulary), `ManpuDirector`,
   `MoodAmbient`; place the glyph child at head height.
4. In the scene: add `ManpuAbilityListener` to the GameSystem GO (beside `StunVFXSystem`); assign
   `TwinAbilitySetup`. Setsuna + pool hooks are automatic.
> **Acceptance:** the `MANPU_SYSTEM.md` §5 trace — detect "!", ClanWar produces no pulses, stun arc
> reads clean, R1 suppresses a mood change mid-stun (T=4), Setsuna holds the board.

### 24.5 Manpu in a Cue Book (the `Manpu` cue element — §23.6)
Beyond the state-driven path above, a glyph can be fired as a **cue element** so it runs *before / with /
after* the other effects in a Cue Book effect. Author a `CueElement` of kind **Manpu** (drop a sprite + 2
colours); when the effect plays, `FxManager` finds the cue **target's `ManpuSlot`** and calls
`ManpuSlot.RequestCuePulse(sprite, colorA, colorB)` — a **transient pulse**, ordered by the element's
`startMode`/`startDelay` like any element. It is **R1-respecting** (dropped if a held ability glyph owns the
slot) and **bypasses the R2 debounce** (a cue pulse is a deliberate authored accent, not state drift). The
`ManpuVocabulary` stays the single source for *state* glyphs — cue glyphs are *effect accents*, a separate
concern, so existing Manpu behaviour is unchanged. **Constraints:** the cue must target an object that has a
`ManpuSlot` (enemies) — else it LogWarns and no-ops; **held** (channel-long) cue glyphs are not supported via
this transient path — for a duration-held glyph use the **ability arc** (§24.6, the `Held` + `ClosingSequence`
path), which owns the slot for its lifetime. Event-driven *reaction* accents (e.g. a possessed ally's victim)
ride this same cue-pulse path and are specced in §24.7.
### 24.6 Ability glyph arcs — Held + timed Closing **sequence** (the hybrid mood/ability path)

The slot already arbitrates two channels (R1: ability owns → mood suppressed). What this
subsection adds is the **timed multi-beat closing** every ability needs — the case "stun ends →
sleep glyph 0.75 s → wake-up '!' glyph", or "possess ends → shocked glyph → '!'". This is an
**ability** concern (Option A, chosen by the user), so it lives in `ManpuVocabulary` keyed by
the `ManpuAbility` enum — **not** in any cue book and **not** duplicated per ability. Every
ability uses the identical mechanism; only its vocabulary rows differ.

**Data change (`ManpuVocabulary`, per `ManpuAbility` row):**
- `Held` — one glyph, rendered for the whole ability-active window (unchanged).
- `ClosingSequence` — **was a single `Closing` glyph; becomes a short ordered list** of
  `ClosingBeat { GlyphStyle glyph; float holdSeconds }`. **Cap: 1–4 beats** (authoring guidance;
  the editor lists up to 4 rows — abilities never need more). An empty list = no closing glyph
  (R3 curation preserved). Examples authored in the start-set:
  - Stun → Held: sleep/spinning-eyes; Closing: `[{sleep, 0.75}, {wake-"!", 0.5}]`
  - Possess → Held: possessed-spiral; Closing: `[{shock, 0.4}, {"!", 0.5}]`

**Ownership across the whole arc (`ManpuSlot` + `ManpuDirector`):**
- On ability **start**, `ManpuAbilityListener` calls the existing claim path → `_abilityOwns = true`,
  Held glyph shows, mood pulses suppressed (R1, unchanged).
- On ability **end**, instead of "show one Closing glyph then release," the slot runs the
  `ClosingSequence`: it **keeps `_abilityOwns = true`** and plays each beat in order
  (`glyph` for `holdSeconds`, unscaled — R10, glyphs are readability and ignore Setsuna/pause
  per E1). Mood stays suppressed for the entire sequence. Only after the **last** beat does the
  slot set `_abilityOwns = false` and release.
- **Mood handback (Q2 — current mood, chosen):** on release the slot reads the enemy's
  *current* mood/search state and lets the **next** natural pulse show it — it does not replay a
  stale mood and does not force an immediate pulse. The existing R2 escalation-only + debounce
  already prevents flicker, so "wake-up → whatever the enemy now feels (alert/enraged/searching)"
  reads as a natural transition with no one-frame gap. Make the release explicit (no glyph) so
  there's never a frame where both ability and mood believe they own the slot.

**Failure guards (write these, don't let them be improvised):**
- Enemy **dies / despawns mid-sequence** → `EnemyPool.Return` → `ManpuSlot.Clear()` aborts the
  running closing coroutine, clears `_abilityOwns`, hides the glyph (extends the existing
  pool-clear rule to the closing coroutine — a running `ClosingSequence` is exactly the kind of
  coroutine that leaks onto a pooled enemy if not stopped).
- A **new ability claims** the slot while a closing sequence is running (re-stunned during wake)
  → the new claim **cancels** the running sequence and takes ownership immediately (newest
  ability wins; the interrupted closing simply doesn't finish — it's an accent, not state).
- **Setsuna E1 hold** fires during a closing sequence → E1 hold-mode applies to whatever glyph
  is currently showing, same as it does for Held (unchanged; the sequence's unscaled timing
  means it neither freezes nor races under `timeScale = 0.15`).

This is purely additive: `Held` is unchanged, single-glyph closings become a one-entry list, and
the only new runtime state is the closing-coroutine handle on the slot (cleared by the existing
pool-clear). No cue-book involvement, no SO duplication, one path for every ability.

### 24.7 Event-driven reaction glyphs (relational Manpu — e.g. "what are you doing?!")

State-driven Manpu reads mood/perception/ability. **Event-driven** Manpu lets a discrete world
event fire a glyph on a *specific* enemy regardless of that enemy's mood — e.g. an enemy attacked
by a **possessed ally** flashes a shocked "!?" / "what are you doing?!" glyph. The capability
already exists via `ManpuSlot.RequestCuePulse` (§24.5, the transient-pulse path): it's deliberate,
bypasses the mood debounce, and is R1-respecting (dropped if an ability arc owns that victim's
slot — correct: a stunned victim shouldn't also be "shocked"). So this is a **small wiring
addition, not a new subsystem**.

**The data home:** reaction glyphs are *accents*, not state, so they do **not** go in
`ManpuVocabulary` (which is the source for *state* glyphs). They live in a **Cue Book** as a
`Manpu`-kind `CueElement` (§23.6/§24.5) — one `CueBook_Reactions` (or per-reaction books) holding
named reaction effects: `"betrayed"`, `"ally_down"`, etc. This keeps reactions authorable/tunable
as content (add a sound element beside the glyph, retime it) without touching the state vocabulary.

**The wiring setup — concretely, end to end (this is the "describe a setup" answer):**

1. **Carry the attacker on the damage event.** `DamageData` gains an optional
   `GameObject source` (the attacker; null for environmental/distance damage). The possession
   damage path already flows through `TakeDamage(DamageData)`, so the possessed enemy's attacks
   populate `source` with the possessed enemy. *(One field, optional, default null — no
   behavioural change to existing damage.)*
2. **A small reaction listener owns the rule, not the enemy.** Add `ManpuReactionListener`
   (always-active scene GO beside `ManpuAbilityListener`, same pattern). It subscribes to a thin
   `OnEnemyDamaged(victim, DamageData)` event added next to `EnemyDeathNotifier`'s combat-kill
   event — reuse that bus, don't make a new one.
3. **The rule, in one place:** on `OnEnemyDamaged(victim, data)`, if `data.source` is an enemy
   that is currently **possessed** (query the existing possession state / `IPossessable`), and
   `victim` is **not** that same enemy, the listener calls
   `victim.GetComponent<ManpuSlot>()?.PlayReaction(reactionBook, "betrayed", ctx)`. **It targets the
   VICTIM's slot, never the attacker's** — the one correctness note: the reacting glyph belongs to
   whoever was wronged.
4. **`ManpuSlot.PlayReaction(book, id, ctx)`** is a thin forward to `FxManager.PlayBook(book, id, ctx)`
   with the cue's `Manpu` element resolving to *this* slot — i.e. it's `RequestCuePulse` reached
   through the cue book, so VFX/sound author alongside the glyph and play through the one FX engine
   (§24.1 harmonization — no raw spawns). R1 drops it if an ability owns the victim; the debounce is
   bypassed (deliberate accent).

**Scope decision (per the user): build the capability now, author reactions as a short start-set.**
Wire **2–3 reactions** in the first pass — the highest-value ones: (a) **betrayed** — enemy attacked
by a possessed ally; (b) **ally_down** — enemy near an ally that just died (rides
`EnemyDeathNotifier.OnEnemyCombatKill` + a proximity check the perception layer already supports);
optionally (c) **startled** — enemy that a soul/teleport passes very close to. Everything beyond
these is later content curation (same philosophy as the R3 vocabulary curation: fill high-value
rows, leave the rest empty). Adding a reaction later = one `CueElement` + one `if` in the listener,
no architecture change.

### 24.8 Capability map + held-mood-loop design + `EnemyVFXController` retirement (P11)

**What Manpu ALREADY covers (verified against code 2026-07-03)** — check here before adding
any enemy-emotion feature; most requests are a vocabulary/cue ROW, not code:

| Scenario | Covered by |
|---|---|
| Mood change pulse (rage, fear, panic…) | `ManpuDirector` ← `EnemyMoodSystem.OnMoodChanged` → `ManpuSlot.RequestMoodPulse` (escalation filter + debounce) |
| Perception ("!", "?", curious/search) | same director, `EnemySearchState` rows |
| Ability arc (stun spiral, possess eye-change) | `ManpuAbilityListener` claim → `ShowHeld` → timed **ClosingSequence** beats on release — the "coming out of concussion" sequence is the ClosingSequence rows |
| Relational shock ("betrayed" — hit by a possessed ally; "ally_down") | `ManpuReactionListener` + `CueBook_Reactions` (§24.7); glyph goes to the **victim** |
| Setsuna | E1 hold (glyph freezes with the world) |
| Continuous mood *feel* | `MoodAmbient` quiet body tint (yields to ability tints) |
| A held mood **aura** (rage/panic/aggressive sustained VFX) | `MoodEntry.loopPrefab` → `ManpuSlot` aura channel (P11) — a vocabulary row, **not code** |
| A NEW *emotional* event | a vocabulary row or a reaction cue element (§24.7) — **not code** |
| **Dark-energy corruption** (a STATE, not a mood) | `EnemyDarkEnergy` owns a held corruption-state cue (P11) — the exception: expressed by the state system, not Manpu/moods |

**The four gaps — ✅ LANDED 2026-07-04 (P11, 0 CS errors):**

1. **Held mood loop — DONE.** `ManpuVocabulary.MoodEntry` gained `loopPrefab`; `ManpuSlot`
   runs an aura channel (`UpdateMoodLoop`/`StopMoodLoop`, held `CueHandle`): started on mood
   ENTER, stopped on EXIT + `Clear` (pool). Pool-safe (stale-handle `IsPlaying` guard), rides
   the enemy body (`CueContext.Follow`). The aura lives exactly as long as the mood.
2. **De-gate the sprite — DONE.** `RequestMoodPulse` runs `UpdateMoodLoop` FIRST, independent
   of `HasVisual`, of R1 ability ownership, and of the R2 debounce; only the transient glyph
   *pulse* stays gated. Auras play on rows with no sprite art yet.
3. **`PlayAccents` leak — DONE.** `ManpuGlyph` tracks the burst-accent `CueHandle` and stops
   it on `Hide` (a one-shot is unaffected; a looping `burstPrefab` no longer leaks).
4. **Imperative → mood mapping — DONE.** Most `PlayX` sites already sat next to (or were
   covered by) a real transition, so retirement just removed them and let Manpu drive the
   aura: **Severed grief-rage** = removed (partner death → Grieving→Enraged via
   `EnemySocialBond`); **TetherBreaker chain-broken** = added `TransitionTo(Enraged)` (the one
   site with no prior transition); **Witness bomb-panic** and **Fear** (`Enemy.FearRoutine`)
   = `TransitionTo(Panicked)` with a **stomp-safe guarded return** (`if CurrentMood ==
   Panicked → Normal`); **commander death-cascade** soldier rage = removed (the adjacent
   `TransitionTo(Enraged)` drives it).

**`EnemyVFXController` retired (deleted) — all 13 caller files migrated (0 CS errors):**
- **Dark energy — a distinct STATE, not a mood** (user 2026-07-04: "independent, consumed
  entirely by corruption"). `EnemyDarkEnergy` now owns a **held corruption-state cue**
  (`_corruptionStateBook`/`_corruptionStateCueId`, serialized, null-safe), started on the
  bond-break latch, stopped on `OnDisable`/`OnDestroy`. Its behavioural `TransitionTo(Aggressive)`
  stays. This does NOT go through the mood system or Manpu.
- **`PlayBuff` dropped** — the Common `on_AlliesBuff` cue is the sole buff visual (Witness).
- **`BTActionComboAttack`** — all ~21 per-combo `PlayX` calls removed (a combo activation is
  not a mood; mapping them would have changed combat stats mid-combo — rejected hazard).
- **Commanders** kept as greppable `// TODO(§24.8)` **stubs** (user request) so the deferred
  commander-ability VFX (ChainStrike / DivineShaft / DarkShield) isn't forgotten.
- **Penitent** (dropped roster) = `TODO(§24.8)` markers to re-add via Enraged mood at rework.
- `EnemyMoodSystem.PlayMoodReaction` block deleted (the `TransitionTo` fires `OnMoodChanged`
  → Manpu). `EnemyPool` `StopAll` removed (`ManpuSlot.Clear` + `StopAllOn` cover despawn).
  `MoodVFXTag`/`vfxTag` kept as **dead data** (dropping them = a separate GUID-safe asset pass).

**Authoring TODO (user, in Unity — all null-safe until done, no errors/no aura meanwhile):**
add `loopPrefab` to the ManpuVocabulary rows that should carry an aura (Enraged / Panicked /
Aggressive…); assign `_corruptionStateBook` + author its cue on the EnemyDarkEnergy prefabs.
`MoodAmbient` body-tint already conveys mood in the meantime.

**P11 DoD (remaining — in-editor, after auras authored):** rage aura survives pool reuse; zero
leaked loops on despawn/unload (TestLab soak); Severed's grief-rage aura ends with the rage;
the Scene Health Dashboard "no EnemyVFXController" recipe (§23.15.2) goes green.

**Packaging:** Manpu ships **with** the Fx package (§23.13, seam 2); the game supplies the
vocabulary assets + the mood-enum adapter.

---

## 25. Production Readiness Review (2026-07-03)

### 25.1 Scope & stage verdict
**Indie-AA scale; a systems-complete vertical slice entering the content phase.** ~560
scripts / 23+ systems, all core mechanics built and cross-wired (dual-twin control, bond
health, GOAP ecology AI, 9-tree progression, streaming multi-scene, cue/audio engine, Manpu,
tutorial, QTE, localization ×8). The architecture is the strong half — Rulebook discipline,
event seams, SO-driven data, pooling, one FX path. The thin half is **content**: two greybox
areas, placeholder art/audio, no encounter/boss scripting yet. Verdict: *excellent skeleton,
thin skin* — the correct next investment is content tooling + authoring (P12/P14/P15),
**not** structural rebuilds.

### 25.2 Blockers before content-scale production
1. **No discoverable tests.** `Assets/Tests/EditMode/` exists but has no asmdef —
   predefined-assembly tests are invisible to the Test Runner. Fix = `PoT.Tests.asmdef`
   (P8 backlog) + move the cue/save/streaming invariants into real tests.
2. **~~Legacy input + live debug keys~~ — DONE (P13, 2026-07-04).** Input System migration
   landed with the tutorial-gate contract preserved (checks inside `IInputProvider` getters);
   every raw-Input gameplay consumer now routes through the provider. Debug surfaces
   (`SkillPointDebug`, `DamageDealerDebug`, `GameDebuggerV2`) are all `DevConfig.Trainer`-gated
   (release-build hard-off). Human DoD remains: Bootstrap tutorial-unlock run + four entry
   paths (instruction.md §18 checklist item 5).
3. **Ledger drift.** BUGS.md/doc claims went stale against code (this review corrected
   CLAUDE.md R10/singleton/SoftReset/Phase-4 notes). The discipline: sweep ledgers with every
   changelog entry (Working Method #6/#10) — stale docs already broke the enemy system once.
4. **Log discipline.** Hot-path `Debug.Log` spam (spawn, cue, AI transitions) — gate behind
   `DevConfig`/compilation symbol (P8).
5. **R7 residue.** Skill trees are extracted (`SkillTreeRuntimeState` — Phase 4 landed,
   verified: `AbilityUpgradeData.currentNodeIndex` is now computed from
   `SkillTreeManager.GetLevel`). Residual: audit remaining SOs for runtime mutation during
   the P8 pass; leftover `currentNodeIndex:` YAML in `.asset` files is dead and ignorable.

### 25.3 Performance notes (file-anchored; fold into P8)
- `SoulPulseSystem.BurnTickLoop` allocates a fresh `List` every 0.1 s tick — reuse a buffer.
- `AccordSpiritAgent.FindNearestUnclaimed` and `RadiantSeekerOrb` target-seek via
  `FindObjectsByType` per call — replace with an R5-style registry (the portal's
  `OverlapSphere` damage is fine and stays).
- Pooling for gameplay spawns = P16 (`GameplayPool`, §25.4); PSO warmup design sits in
  instruction.md Future Additions (build at the first build-perf pass).
- Free wins already listed in §17.1 (HDR grading, SMAA, cascade count, soft-shadow tier).

### 25.4 GameplayPool — ✅ BUILT 2026-07-04 (P16, 0 CS errors, play-verified)
One Persistent system for **gameplay** spawns (cosmetics stay in `VfxPool`; enemies stay in
`EnemyPool`): `SpawnSystem/GameplayPool.cs` — `PoolCategory { Projectiles, AbilityObjects,
Summons, Hazards }`, hierarchy `GameplayPoolRoot/<Category>/…` (the GO lives in Persistent),
contract `ISpawnPoolable { OnSpawned(pool), OnDespawned() }`, prewarm via
`SpawnPrewarmProfile` SO rows `{prefab, count, category}` (asset = authoring TODO). Call
sites use the statics `Spawn/Despawn(go[, delay])` — fail-loud degrade to
Instantiate/Destroy if the GO is missing. **Delayed despawns are version-stamped** (a stale
lifetime timer never kills a reused instance — play-verified). Return runs `OnDespawned`,
then safety nets `StopAllCoroutines` + `FxManager.StopAllOn`, then **reparents home** (a
despawning host can never drag a pooled instance away — F1). `NavMeshAgent.Warp` on Get
(enabled agents only). All 9 sites migrated: `Arrow`/`BombProjectile`/`ChainProjectile`
(Projectiles), `AccordSpiritAgent`/`RadiantSeekerOrb`/`CoalesceAura`/SC shield
(AbilityObjects), `SiphonGhost` (Summons), and the **Witness minion via the new
`EnemyPool.SpawnReady`** (it is an Enemy — canonical spawn sequence as a shared method;
EnemySpawner/GameDebuggerV2 dedup = later isolated commit). Cue-on-spawn stays caller-played
through the book (pool owns lifetime, caller owns presentation). Fixed by necessity: the
SiphonEnemy ghost-kill lambda → named handler (stale subscription would have killed a reused
ghost). Found (authoring): `SmartEnemyRanged` prefab lacks `_projectilePrefab`.

### 25.5 Answered review questions
- **"Are Awake-cached references bad practice?" — No; they are the correct pattern here.**
  Cache self-components in `Awake`, resolve externals in `Start` (R4/R8), then hold the
  field. What would be wrong: per-frame `GetComponent`/`Find*`, or caching *cross-scene*
  objects without the R4/R5 lifecycle (dangling on unload). Keep the
  `[SerializeField] MonoBehaviour` → interface-cast DI exactly as practiced.
- **"Is the code production-correct?" — Mostly yes.** The review found *stale documentation*,
  a handful of perf micro-fixes (§25.3), and hygiene phases (P10–P19) — no systemic rework.

## 26. Multiplayer Feasibility (2026-07-03 analysis)

### 26.1 Couch co-op — primary target, natural fit
The entire selected-twin + mirrored-movement layer exists **only because one player drives
both twins** — with two players it is *deleted, not built*: each player = one twin with
`NormalMovementModifier`. Work list (≈ weeks, after P13):
- **Input:** Input System `PlayerInput` per device (P13 makes this an action-map bind, not a
  rewrite); `TwinSelector` becomes a device→twin binder; "switch twin" disappears (or becomes
  a solo-mode fallback — keep solo working behind the same `IInputProvider` seam).
- **Force-selection consumers to rework** (the real work): `RescueEventController`
  (rescuer = the *other player*, no force-switch), Empower anchor choice (anchor = caster),
  `EmergencyTeleportMonitor` (teleport-to-partner prompt instead of auto-switch).
- **Per-twin ability sets already exist** (dispatchers route to the selected twin —
  route to the owning player instead). Shared health/bond/distance: unchanged — it *is* the
  co-op design.
- **Camera:** group framing exists (`CameraManager`); Setsuna's global `timeScale` is fine
  locally (both players share the moment); HUD gains per-player ability strips.
- **Keep-clean rules (live now, CLAUDE.md Conventions):** all input via `IInputProvider`;
  no new statics/singletons holding player-scoped state; twin identity stays data-driven.

### 26.2 Networked co-op — months-scale; decide the stack before writing any of it
Honest cost, file-anchored:
- **AI authority:** GOAP/BT brains + per-entity Blackboards are single-machine —
  server-side simulation; clients render replicated enemy state; pooled enemies need stable
  network ids across reuse.
- **Hit detection:** melee/AOE overlap checks are client-local → server-authoritative combat
  + client prediction/rollback.
- **Time:** `Time.time` cooldowns/coroutines desync; **Setsuna is the hardest** — global
  0.15 timeScale + position/health *rewind* must become server-authoritative rewind with
  client smoothing, or Setsuna gets a networked redesign (local-bubble slow is a design
  change, not a port).
- **Shared pool/bond/distance:** server-computed, replicated as one health state.
- **Streaming:** occupancy-based scene loading must consider *both remote players* —
  `SceneFlowManager` occupancy becomes replicated state.
- **Stack:** pick NGO / Photon / backend **before** any netcode lands; retrofit is the
  expensive path. Recommendation: ship couch first; net = its own dedicated phase with a
  vertical slice (movement + one enemy + shared health) before committing the roster.

