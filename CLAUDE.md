# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Planet of Twins** is a **single-player** action game built in **Unity 6000.3.5f2 (Unity 6.3)**
with URP. One player controls **both twins simultaneously** — Kai (Vethara/dark-energy) and
Lyra (Luminari/soul-light): WASD moves both, Left Shift switches which twin is *selected* for
abilities, the unselected twin mirrors movement. The core mechanic is the bond: twins share a
health pool that drains with distance. The codebase is ~560 C# scripts across 23+ systems.

> **Read order for any session:** this file → [game.md](game.md) (canonical system-by-system
> reference, §16 = scene architecture) → [changelog.md](changelog.md) (current state + known
> issues) → [instruction.md](instruction.md) (**active multi-scene correction work order** —
> while it has unfinished phases, it outranks new feature work).

## Working Method — non-negotiable expectations

1. **Understand before editing.** Locate every consumer of a symbol (grep) before changing it.
   State which systems a change touches; if a change crosses systems (input, health, rescue,
   Accord, streaming), enumerate the interactions first.
2. **Foresee bugs, don't just fix the symptom.** Before writing, ask explicitly: What is the
   object's *scene residency and lifetime*? What happens on **scene unload**, on **Restart**
   (Bootstrap reload), during **Setsuna** (`timeScale = 0.15`), during **pause** (`0`), when a
   **pooled enemy is reused**, when this runs in **editor direct-play vs build**? The
   bug-forecast appendix in instruction.md §11 is the checklist of known failure classes.
3. **One consistent pattern beats many clever ones.** All cross-scene access follows the
   Reference Rulebook below. Never invent a new wiring pattern; never "quick-fix" with a
   lookup. If the Rulebook genuinely can't express a need, stop and raise it.
4. **Fail loudly.** Unresolved dependency ⇒ `Debug.LogError` with object context + disable
   self. Silent nulls are how this project broke.
5. **SOLID as practiced here:** interface-typed fields (`IPointBank`, `ITutorialGate`,
   `IRescueActive`…), `[SerializeField] MonoBehaviour` → interface cast for same-scene DI,
   events between gameplay and UI, SO data for tunables, one class per file, composition over
   god-objects. Concrete singleton types may appear **only** on an R4 resolve line.
6. **Changelog discipline.** Every change lands in `changelog.md` under `[Unreleased]`
   (Added/Changed/Fixed/Removed), written the same session as the change. Migrations list the
   call sites touched.
7. **Don't bundle.** Renames, dead-code deletion, and refactors ship as isolated commits
   (Unity `.meta` GUIDs must survive renames). Out-of-scope cleanup goes to game.md §20, not
   into a feature diff.
8. **Verify like a player.** After any change, run the relevant slice of instruction.md §10's
   verification protocol on at least two entry paths (Bootstrap, direct-area play).
9. **The "Banned Lazy Work" list (instruction.md §12) is enforceable review criteria** — a
   change exhibiting any item on it is rejected even if it works. Phase 8 there also defines
   the production-discipline backlog (tests, scene lint, logging, save contract, profiling).
10. **Track every defect in `BUGS.md`** (instruction.md §13): entry created *before* the fix
    is written, status swept with every changelog entry, `Verified` only after the DoD/§10
    step actually ran. Start each session by reading the `Open`/`Watch` entries for the
    systems you're about to touch.

## Scene Architecture (multi-scene — canonical)

```
Bootstrap.unity   index 0; only scene open at boot. GameBootstrapper → loads Persistent
                  additively → Intro (cutscene path) or straight to start area (dev mode).
Persistent.unity  NEVER unloaded. Owns: all manager singletons, both twins + SoulPlayer +
                  twin systems, EnemyPool root, the single EventSystem / AudioListener /
                  MainCamera (+ Cinemachine Brain), every screen-space HUD canvas,
                  QTEManager, tutorial overlay/hint + relocated failure-reset UI.
Intro.unity       skippable cutscene; background-loads gameplay scenes.
L1_Park / L2_Streets  streamed area scenes (folder per scene, co-located navmesh/occlusion):
                  geometry, lights, NavMeshSurface, SpawnZones + POIs, traps, orbs,
                  LocationEntrances, SceneLoadTriggers, QTESceneAnchor + world-space canvases,
                  per-area TutorialDirector/TutorialManager, cinematic VCams.
```

`SceneFlowManager` (Persistent) streams areas occupant-based: loaded set = occupied locations
∪ their `WorldLocationSO` adjacents; occupancy tracks **both twins and the rescue soul**;
`OnLocationWillUnload` fires before any unload (spawner despawns that zone, QTE cancels);
**every scripted teleport must call `NotifyTeleported`** — triggers never see teleports.

### The Reference Rulebook (full text: instruction.md §1 — these are law)

| # | Law |
|---|-----|
| R1 | Same-scene serialized refs allowed (incl. Persistent→Persistent). Keep the `[SerializeField] MonoBehaviour` → interface-cast DI pattern. |
| R2 | Cross-scene serialized refs **forbidden**. "Scene mismatch" in an Inspector = a bug, always. |
| R3 | Persistence = living in `Persistent.unity`. **`DontDestroyOnLoad` is banned** (duplicates managers across the Restart→Bootstrap loop). |
| R4 | Area→Persistent: optional serialized slot cast in `Awake`, then `field ??= Manager.Instance` **in `Start()`**; LogError + `enabled = false` if still null. **Never `FindAnyObjectByType` for managers** — it's reserved for scene-scoped non-singleton sweeps (e.g. `EnemyFreezeService`). |
| R5 | Persistent→Area: registries only. Area objects self-register `OnEnable`, unregister `OnDisable`/`OnDestroy`; managers null-purge before iterating. |
| R6 | Area↔Area refs forbidden — mediate via Persistent or an SO channel. |
| R7 | ScriptableObjects = config only, never runtime state. |
| R8 | `Awake` wires self; `Start` resolves others; subscribe/unsubscribe with **named handlers** (never `-=`-proof lambdas); `OnDestroy` unregisters. |
| R9 | Persistent owns the only EventSystem/AudioListener/MainCamera/screen-space HUD. Area world-space canvases use `WorldSpaceCanvasCamera`. |
| R10 | All `Time.timeScale` writes go through `TimeScaleService` (`Request(owner, value)`/`Release(owner)`, **min-value-wins**) once instruction.md 5.5 lands — seven verified writers stomp each other today. Every timer comments scaled vs `unscaledDeltaTime`. |
| R11 | **Timeline law:** track bindings are scene-local only — cross-scene targets (Brain, twins) rebound at runtime via `TimelineBindingResolver` before `Play()`. Activation Tracks never control ancestors of gameplay-logic objects (checkpoints/triggers/zones) and always set explicit Post-playback state. Completion via `director.stopped` or state-poll with Wrap Mode None; end Signals ≥0.1 s before the end. |

## Unity Workflow

No CLI build system — everything happens in the Unity Editor (Unity Hub → 6000.3.5f2, URP):

- **Entry paths (all four must work):** Play from `Bootstrap` (full flow), Bootstrap dev-mode,
  or Play directly in `L1_Park`/`L2_Streets` — `PersistentSceneAutoLoader` (editor-only)
  additively loads Persistent so managers exist. If direct-play looks "completely broken",
  check Persistent loaded *first*, before suspecting gameplay code.
- Scenes live in `Assets/Scenes/` (`Bootstrap`, `Persistent`, `Intro`, `L1_Park/`,
  `L2_Streets/`, `SampleScene` dev). `Restore.unity`/`Trees.unity` are temp merge scenes —
  never in Build Settings.
- Build Settings order: Bootstrap (0), Persistent, Intro, then areas.
- Tests: `Window > General > Test Runner`. Build: `File > Build Settings`.

## High-Level Architecture

### Dual-Twin Input & Movement
```
TwinInputReader (IInputProvider, legacy Input.GetKey — see Notes)
   ├── TwinMovementDispatcher → PlayerMovementController (×2)
   ├── TwinAttackDispatcher   → PlayerAttackController (selected twin)
   └── TwinAbilityDispatcher  → AbilityController (selected twin)
TwinSelector — selected twin gets NormalMovementModifier, the other MirroredMovementModifier.
TutorialInputGate (ITutorialGate) — optional per-category gate; consumers fail **open**.
```

### Health & Distance Bonding
`SharedHealthPool` (combined 200), per-twin `PlayerHealthComponent`, `DistanceHealthSystem`
(≤6 m full → 0 at >18 m), `TwinBondManager`, `UpgradeManager`, `DistanceZone`. All damage flows
through `DamageData { Amount, DamageType, Source, HitPoint }`; `LinkedDamage` breaks Severed
pair loops.

### AI (hybrid GOAP + BT + FSM)
Reusable engine in `AIFramework/` (BehaviourTree, StateMachine, HybridGOAP) sharing a
per-entity **Blackboard** (`FastName` keys). GOAP plans; actions delegate to BT/FSM. The
project brain layer `AIFramework/PlanetOfTwinsAI/` is **fully built** — one GOAP brain per
enemy archetype plus the ecology layer (Mood/Ikari, Social Bonds, Faction Energy, POIs,
perception memory, ClanWar). `PerceptionManager` + ServiceLocator coordinate sensors with
decay/memory. Enemy base `Enemy.cs` implements `ITimeAffected, IStunnable, IPossessable,
IGrabbable`; types are data-driven via `EnemyData` SOs.

### Abilities & Time
`AbilityData` SOs; nine `AbilityUpgradeData` trees via `SkillTreeManager` (`IPointBank`,
`ISkillUnlockState`, `IAbilityDataStore`, `ISkillTreePurchaser`). Two distinct time systems:
`TimeFactorManager` (entity-level freeze registry → `IsBrainPaused`, soul mode) vs **Setsuna**
(global `Time.timeScale = 0.15` + position/health rewind) — never conflate them (R10).

### Key Persistent Singletons
| Class | Purpose |
|-------|---------|
| `SceneFlowManager` | Area streaming (occupancy, adjacency, active-scene, pre-unload event) |
| `GameBootstrapper` | Entry modes (intro / dev) |
| `SoftResetController` | Checkpoint respawn **without** scene reload (full contract: instruction.md P7.5) |
| `CheckPointManager` | Save data (positions/points/upgrades/sword); `CheckpointLoader` is **obsolete** |
| `SkillTreeManager` | Nine upgrade trees, points |
| `EnemySpawner` + `EnemyPool` | Zone-registry spawning (R5), pool root in Persistent |
| `QTEManager` + `EnemyFreezeService` | QTE state machine + shared UI; per-QTE `QTESceneAnchor` in area scenes |
| `TutorialOverlayController` / `TutorialHintDisplay` / `FailureResetSequencer` / `FailureNotice` | Teach-anywhere UI — all four Persistent singletons; area steps resolve them via `TutorialStepContext.Resolve()`, never serialized cross-scene |
| `TwinSelector`, `TimeFactorManager`, `RescueEventController`, `AccordStateSystem`, `LanguageManager`, `CameraManager` | As named |
| `FxManager` | Unified cue sequencer + VFX pool (`FxPoolRoot`); `Play(CueData, CueContext)` → `CueHandle` (version-stamped — stale handles are inert); subscribes `OnLocationWillUnload` (F1 unload contract); see instruction.md §14 |
| `AudioManager` | 32 pooled `AudioSource` voices; voice stealing (lowest-priority then oldest); snapshot arbiter (mirrors `TimeScaleService` — highest-priority-wins); sole `AudioListener.pause` writer; `PlayUI` for unscaled/UI sounds |
| `MusicManager` | A/B `AudioSource` crossfade on **unscaled** time; subscribes `SceneFlowManager` active-location change → plays `WorldLocationSO.musicTrack` / `.ambience`; no-op when track unchanged |

Singletons use the duplicate-destroy `Awake` guard and null `Instance` in `OnDestroy`
(Restart reloads Bootstrap — stale statics and duplicates are the canary bugs). The
`MonoBehaviourSingleton<T>`/`StandaloneSingleton<T>` bases must **not** apply DDOL to
scene-resident singletons (R3).

## Conventions

- Interfaces `I<Feature>`; SOs `<Feature>Data`; `<Feature>Controller` / `<Feature>Manager`.
- Namespaces: `CommonCore`, `CharacterCore`, `BehaviourTree`, `StateMachine`, `HybridGOAP`;
  project gameplay classes have no namespace.
- All tunables in ScriptableObjects; new enemy = data class in `EnemyAI/Types/Data/` + GOAP
  brain in `PlanetOfTwinsAI/GOAP/Brains/`.
- Timers: comment scaled vs unscaled at the declaration (R10).

## Key Packages
URP 17.3 · Input System 1.17 · Cinemachine 3.1.5 · AI Navigation 2.0.11 · Localization 1.5.11
(8 languages) · VFX Graph 17.3 · Timeline 1.8.10.

## Notes & Footguns

- **`CommonCore.MonoBehaviourSingleton<T>` (verified):** `OnAwake()` unparents + applies
  `DontDestroyOnLoad` to every derived singleton, and the `Instance` getter **fabricates a
  blank GameObject when none is found** — a silently-unwired manager that then wins as the
  singleton. Both are being corrected (instruction.md 1.4). Until then: never rely on lazy
  `Instance` fabrication for gameplay managers; if a manager "exists but has no data," check
  the Hierarchy for `Singleton<T>` ghosts. Plain-C# `StandaloneSingleton<T>`/static state
  also **survives Restart in builds** (no domain reload) — stale `ServiceLocator` entries are
  the canary.
- **Raw `Input.*` outside `TwinInputReader` is banned** — it bypasses `TutorialInputGate`
  (Setsuna's F-hold and the overlay/pause ESC double-consume are the precedents). Extend
  `IInputProvider`; ESC goes through `PauseMenuController`'s priority chain.
- **Seven `Time.timeScale` writers exist** (overlay, pause, game-over, Setsuna 0.15, soul
  travel 0.85, soft reset, skill tree) — until `TimeScaleService` (R10) lands, any new
  feature touching timeScale will stomp one of them. Never add an eighth direct writer.
- **Timelines deactivate gameplay objects:** `SetActive(true)` no-ops under an inactive
  ancestor — the rescue-checkpoint bug. Keep logic objects out of Activation-Track
  hierarchies (R11); make activation paths fail loud.
- **Cross-scene Timeline binding (R11) — two mechanisms, never name strings, never YAML:** a
  track binding is a *scene-local* fileID and cannot point into another scene (R2). To drive a
  Persistent object from an area-scene Timeline: (1) *continuous* tracks (Cinemachine/Animation)
  — leave the binding empty, `TimelineBindingResolver` rebinds at runtime via `SetGenericBinding`
  resolving the target **by type/singleton** (not by track name — fragile); (2) *actions*
  (fade/HUD) — use **Signals** to a **local** `SignalReceiver` + **local relay** that forwards
  to the Persistent system at runtime (a Signal Track's own binding is still scene-local, so
  the receiver must be local — Signals don't cross scenes by themselves). Never hand-edit
  `.playable`/scene `m_SceneBindings` YAML — that corrupts the asset. **`TutorialTimelineDirector`
  is the live instance (BUG-032):** authored single-scene before the multiscene split +
  re-greybox, so 11 of 42 bindings are null — most are now-Persistent and rebound at runtime via
  the `TimelineTargetRegistry` + `TimelineBindingResolver` (camera Brain, CameraManager, FadeController,
  HUD, the two `TutorialGroupTranspose*` cameras that **moved** to Persistent, SkyboxChanger); the
  Activation 20/21 tracks are the **twin GOs** (Lyra/Kai — not "nameplates"), deleted because the
  cutscene lock is now done in code (`IntroTimelinePositioner` lock-on-play); only `MainLvl (1)/(2)`
  geometry is truly deleted/unrecoverable.
- `TutorialInputGate` is **area-resident** and push-registers into the Persistent
  `TwinInputReader` via `SetGate()` — null gate = all input allowed. Don't move it to
  Persistent and don't serialize it across scenes.
- `TwinInputReader` still uses legacy `Input.GetKey` despite the Input System dependency —
  planned migration, don't half-convert.
- `AbilityUpgradeData.currentNodeIndex` mutates the asset (R7 violation) — extraction is
  instruction.md Phase 4; until then expect dirty `.asset` files.
- `SoftResetController` currently restores only **7 of 9** skill trees (Empower + Accord
  State missing) — fix in instruction.md Phase 7.5; don't copy its hand-listed tree pattern.
- Debug skill keys L/O/P/I/K active — never ship; keep behind editor guards.
- Load-bearing typos exist (`Heath/`, `SceneLaoder/`, `FactionDefination`, `EnemySpawnner`) —
  rename only as isolated commits preserving `.meta` GUIDs (game.md §20).
- Legacy/dead (verify-then-delete list in game.md §19): `EnemyStateMachine`, `EnemyDetection`,
  `EnemyVisionCone`, `OldFactionComponent`, `TwinManager`, `AreaManager`/`AreaNode` (replaced
  by SceneFlowManager), `CheckpointLoader`.
- Two `.sln` files exist — use whichever your IDE picks up.
- The story bible (`planet_of_twins_story_bible_v4.docx`) is canonical for narrative/tone;
  `controls_and_abilities.docx` is **partially outdated** — where it conflicts with code or
  game.md, the code is the source of truth (exception: confirmed bugs).
- **FX/audio pattern (Phase 9 — instruction.md §14):** The only correct way to play a particle,
  VFX Graph effect, sound, or sequence of the above is `FxManager.Play(CueData, context)`. Never
  `Instantiate` a visual/audio prefab, never call `AudioSource.Play` directly, never write
  `AudioMixerSnapshot.TransitionTo` outside the snapshot arbiter — these are Banned Lazy Work
  items 10–12. The "3 things one after another" contract: create one `CueSequenceData` asset,
  wire one slot, call one line — no coroutines, no per-call-site Setsuna/pause handling.
